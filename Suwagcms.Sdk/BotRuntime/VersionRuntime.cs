using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.BotRuntime;

internal sealed partial class VersionRuntime(
    string botId,
    IVersionStore store,
    ILogger<VersionRuntime> logger,
    BotDeclaration? declaration = null
) : IBotDefinitions, IDisposable
{
    private readonly SemaphoreSlim writes = new(1, 1);
    private readonly SemaphoreSlim refreshes = new(1, 1);
    private readonly ConcurrentDictionary<int, BotDefinition> loaded = new();
    private readonly Lock publishing = new();
    private readonly List<Func<int, CancellationToken, Task>> listeners = [];
    private readonly List<Func<int, CancellationToken, Task>> availabilityListeners = [];
    private readonly CancellationTokenSource stopping = new();
    private View view = new(new VersionStoreState(null, [], null), null);

    public int? CurrentNumber => Volatile.Read(ref view).State.Current;

    public async Task<BotDefinition> GetAsync(int number, CancellationToken cancellationToken = default)
    {
        if (loaded.TryGetValue(number, out var known))
        {
            return known;
        }

        var json =
            await store.ReadVersionAsync(number, cancellationToken) ?? throw new KeyNotFoundException($"Version {number} is not held.");
        return loaded.GetOrAdd(number, BotDefinition.Read(json));
    }

    public async Task<IReadOnlyList<FeedArticle>> GetArticlesAsync(int number, CancellationToken cancellationToken = default)
    {
        var definition = await GetAsync(number, cancellationToken);
        var languages = definition.KnowledgeBases.ToDictionary(knowledgeBase => knowledgeBase.Id, knowledgeBase => knowledgeBase.Language);
        return
        [
            .. definition
                .Articles.OrderBy(article => article.Id, StringComparer.Ordinal)
                // Why: a version is held only after validation, which refuses an article whose base it does not have.
                .Select(article => new FeedArticle(
                    article,
                    languages[article.KnowledgeBase],
                    IsOff(number, ResourceKind.Article, article.Id)
                )),
        ];
    }

    public void OnCurrentChanged(Func<int, CancellationToken, Task> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        lock (publishing)
        {
            listeners.Add(listener);
            if (view.State.Current is { } number)
            {
                Tell(listener, number, ListenerFailed);
            }
        }
    }

    public void OnAvailabilityChanged(Func<int, CancellationToken, Task> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        lock (publishing)
        {
            availabilityListeners.Add(listener);
            if (view.State.Availability is { } availability)
            {
                Tell(listener, availability.Number, AvailabilityListenerFailed);
            }
        }
    }

    public bool IsOff(int version, ResourceKind kind, string id) =>
        Volatile
            .Read(ref view)
            .State.Availability?.Off.Any(item =>
                item.Kind == kind && item.Id == id && (item.UpToVersion is not { } upTo || version <= upTo)
            ) == true;

    public async Task ReportInUseAsync(IReadOnlyCollection<int> numbers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(numbers);
        await writes.WaitAsync(cancellationToken);
        try
        {
            await store.WriteInUseAsync([.. numbers.Distinct().Order()], cancellationToken);
            var current = await RefreshAsync(cancellationToken);
            if (current.Error is null)
            {
                await SweepAsync(current.State, cancellationToken);
            }
        }
        finally
        {
            writes.Release();
        }
    }

    public async Task<BotState> ReadStateAsync(CancellationToken cancellationToken) => Answer(await RefreshAsync(cancellationToken));

    // Why: one at a time, so a read that began before a write cannot publish after the read that saw it and tell the bot's code of a change that never happened.
    public async Task<View> RefreshAsync(CancellationToken cancellationToken)
    {
        await refreshes.WaitAsync(cancellationToken);
        try
        {
            var state = await store.ReadStateAsync(cancellationToken);
            foreach (var number in loaded.Keys.Where(number => !state.Held.Contains(number)))
            {
                loaded.TryRemove(number, out _);
            }

            return Publish(new View(state, null));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Publish(Volatile.Read(ref view) with { Error = exception.Message });
        }
        finally
        {
            refreshes.Release();
        }
    }

    public async Task<(BotState? Accepted, BotRefusal? Refused)> PushAsync(int number, string json, CancellationToken cancellationToken)
    {
        var mistakes = VersionIntake.Check(json, botId, number, declaration);
        if (mistakes.Count > 0)
        {
            return (null, Refusal(422, "The version was refused.", $"{mistakes.Count} mistake(s) in the document.", mistakes));
        }

        await writes.WaitAsync(cancellationToken);
        try
        {
            try
            {
                var existing = await store.ReadVersionAsync(number, cancellationToken);
                if (existing is not null)
                {
                    return VersionDigest.Of(existing) == VersionDigest.Of(json)
                        ? (await ReadStateAsync(cancellationToken), null)
                        : (null, Conflict(number));
                }

                await store.WriteVersionAsync(number, json, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return (null, Refusal(503, "The store failed.", $"The version was not stored: {exception.Message}", []));
            }

            return (await TakeEffectAsync(number, json, cancellationToken), null);
        }
        finally
        {
            writes.Release();
        }
    }

    public async Task<(BotState? Accepted, BotRefusal? Refused)> MakeCurrentAsync(string body, CancellationToken cancellationToken)
    {
        if (ChoiceIn(body) is not { } choice)
        {
            return (null, BadChoice());
        }

        await writes.WaitAsync(cancellationToken);
        try
        {
            string json;
            try
            {
                if (await store.ReadVersionAsync(choice.Number, cancellationToken) is not { } held)
                {
                    return (null, NotHeld(choice.Number));
                }

                if (VersionDigest.Of(held) != choice.Digest)
                {
                    return (null, Conflict(choice.Number));
                }

                // Why: the SDK may have been upgraded since the version arrived, and a bot never runs what the validator of today has not checked.
                var mistakes = VersionIntake.Check(held, botId, choice.Number, declaration);
                if (mistakes.Count > 0)
                {
                    return (null, Refusal(422, "The version was refused.", $"{mistakes.Count} mistake(s) in the held document.", mistakes));
                }

                if ((await store.ReadStateAsync(cancellationToken)).Current == choice.Number)
                {
                    return (await ReadStateAsync(cancellationToken), null);
                }

                await store.WriteCurrentAsync(choice.Number, cancellationToken);
                json = held;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return (null, Refusal(503, "The store failed.", $"The version was not made current: {exception.Message}", []));
            }

            return (await TakeEffectAsync(choice.Number, json, cancellationToken), null);
        }
        finally
        {
            writes.Release();
        }
    }

    private async Task<BotState> TakeEffectAsync(int number, string json, CancellationToken cancellationToken)
    {
        loaded[number] = BotDefinition.Read(json);
        var current = await RefreshAsync(cancellationToken);
        if (current.Error is null)
        {
            await SweepAsync(current.State, cancellationToken);
        }
        else
        {
            // Why: the write succeeded, so the answer must already say so even though the store cannot be read back.
            var held = current.State.Held.Contains(number) ? current.State.Held : [.. current.State.Held, number];
            current = Publish(current with { State = current.State with { Current = number, Held = [.. held.Order()] } });
        }

        return Answer(current);
    }

    public async Task<(BotState? Accepted, BotRefusal? Refused)> TakeAvailabilityAsync(string json, CancellationToken cancellationToken)
    {
        var (message, mistakes) = AvailabilityIntake.Read(json);
        if (message is null)
        {
            return (null, Refusal(422, "The availability was refused.", $"{mistakes.Count} mistake(s) in the message.", mistakes));
        }

        await writes.WaitAsync(cancellationToken);
        try
        {
            try
            {
                await store.WriteAvailabilityAsync(message, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return (null, Refusal(503, "The store failed.", $"The availability was not stored: {exception.Message}", []));
            }

            var current = await RefreshAsync(cancellationToken);
            if (current.Error is not null && message.Number >= (current.State.Availability?.Number ?? 0))
            {
                // Why: the write succeeded, so this instance and its answer already go by it even though the store cannot be read back.
                current = Publish(current with { State = current.State with { Availability = message } });
            }

            return (Answer(current), null);
        }
        finally
        {
            writes.Release();
        }
    }

    // Why: keeping too much is safe, removing too much is not: nothing goes before the bot has reported once.
    private async Task SweepAsync(VersionStoreState state, CancellationToken cancellationToken)
    {
        if (state.InUse is null)
        {
            return;
        }

        foreach (var number in state.Held.Where(number => number != state.Current && !state.InUse.Contains(number)))
        {
            await store.RemoveVersionAsync(number, cancellationToken);
            loaded.TryRemove(number, out _);
        }

        await RefreshAsync(cancellationToken);
    }

    public void Dispose()
    {
        // Why: the container disposes it once per service it is registered as, and a disposed source cannot be cancelled.
        if (!stopping.IsCancellationRequested)
        {
            stopping.Cancel();
        }

        stopping.Dispose();
        refreshes.Dispose();
        writes.Dispose();
    }

    private View Publish(View next)
    {
        lock (publishing)
        {
            var before = view.State;
            Volatile.Write(ref view, next);
            if (next.State.Current is { } number && number != before.Current)
            {
                foreach (var listener in listeners)
                {
                    Tell(listener, number, ListenerFailed);
                }
            }
            if (next.State.Availability is { } availability && availability.Number != before.Availability?.Number)
            {
                foreach (var listener in availabilityListeners)
                {
                    Tell(listener, availability.Number, AvailabilityListenerFailed);
                }
            }
        }

        return next;
    }

    // Why: on its own task, so a slow or failing listener never holds up a route's answer or the next change; listeners may overlap and go by the number.
    private void Tell(Func<int, CancellationToken, Task> listener, int number, Action<ILogger, int, Exception> failed)
    {
        var token = stopping.Token;
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await listener(number, token);
                }
                catch (Exception exception) when (!token.IsCancellationRequested)
                {
                    failed(logger, number, exception);
                }
            },
            CancellationToken.None
        );
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The bot's code failed while handling the news that version {Number} is current")]
    private static partial void ListenerFailed(ILogger logger, int number, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The bot's code failed while handling the news of availability message {Number}")]
    private static partial void AvailabilityListenerFailed(ILogger logger, int number, Exception exception);

    private BotState Answer(View from) =>
        new()
        {
            Bot = botId,
            Current = from.State.Current,
            Held = from.State.Held,
            InUse = from.State.InUse ?? [],
            StoreError = from.Error,
            Availability = from.State.Availability ?? BotAvailability.Nothing,
        };

    private static CurrentChoice? ChoiceIn(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<CurrentChoice>(body, BotRuntimeJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static BotRefusal BadChoice() =>
        Refusal(
            422,
            "The version was refused.",
            "The body does not name a version.",
            [
                new Mistake(
                    MistakeCodes.FormatInvalid,
                    ValidationStage.Version,
                    new ResourceRef(ResourceKind.Definition, string.Empty),
                    "",
                    "The body is not an object with a number and a digest.",
                    "Send { \"number\": 12, \"digest\": \"…\" }.",
                    []
                ),
            ]
        );

    private static BotRefusal NotHeld(int number) =>
        Refusal(
            409,
            "The version was refused.",
            $"Version {number} is not held.",
            [
                new Mistake(
                    MistakeCodes.VersionNotHeld,
                    ValidationStage.Version,
                    new ResourceRef(ResourceKind.Definition, string.Empty),
                    "number",
                    $"Version {number} is not held by this bot.",
                    "Send the document with PUT /suwagcms/versions/{number}.",
                    []
                ),
            ]
        );

    private static BotRefusal Conflict(int number) =>
        Refusal(
            409,
            "The version was refused.",
            $"Version {number} exists with another document, and a version never changes.",
            [
                new Mistake(
                    MistakeCodes.VersionConflict,
                    ValidationStage.Version,
                    new ResourceRef(ResourceKind.Definition, string.Empty),
                    "number",
                    $"Version {number} is already held with another document.",
                    "Create a new version with the next number.",
                    []
                ),
            ]
        );

    private static BotRefusal Refusal(int status, string title, string detail, IReadOnlyList<Mistake> mistakes) =>
        new()
        {
            Title = title,
            Status = status,
            Detail = detail,
            Mistakes = mistakes,
        };

    internal sealed record View(VersionStoreState State, string? Error);
}
