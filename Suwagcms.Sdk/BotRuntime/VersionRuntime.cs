using System.Collections.Concurrent;
using System.Text.Json;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.BotRuntime;

internal sealed class VersionRuntime(string botId, IVersionStore store) : IBotDefinitions, IDisposable
{
    private readonly SemaphoreSlim writes = new(1, 1);
    private readonly ConcurrentDictionary<int, BotDefinition> loaded = new();
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
        return loaded.GetOrAdd(number, Parse(json));
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

    public async Task<View> RefreshAsync(CancellationToken cancellationToken)
    {
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
    }

    public async Task<(BotState? Accepted, BotRefusal? Refused)> PushAsync(int number, string json, CancellationToken cancellationToken)
    {
        var mistakes = VersionIntake.Check(json, botId, number);
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

            loaded[number] = Parse(json);
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

            return (Answer(current), null);
        }
        finally
        {
            writes.Release();
        }
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

    public void Dispose() => writes.Dispose();

    private View Publish(View next)
    {
        Volatile.Write(ref view, next);
        return next;
    }

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

    private static BotDefinition Parse(string json) =>
        JsonSerializer.Deserialize<BotDefinition>(json, DefinitionFormat.Options)
        ?? throw new InvalidDataException("A stored version is not a definition.");

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
