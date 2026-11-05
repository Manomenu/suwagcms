using System.Globalization;
using System.Text.Json;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.BotRuntime;

// Why: a test run uses the runner a conversation uses, so it proves what a conversation will do; it writes nothing, so it changes neither the bot's version nor its store.
internal sealed class ActionTestRoute(
    HttpActionRunner runner,
    Func<string, IReadOnlyDictionary<string, string>, CancellationToken, Task<ActionOutcome>>? runCode,
    TimeProvider time
)
{
    public async Task<ActionOutcome> RunAsync(string body, CancellationToken cancellationToken)
    {
        var request =
            JsonSerializer.Deserialize<ActionTestRequest>(body, BotRuntimeJson.Options) ?? throw new JsonException("The body is empty.");
        return request.Action.IsCode
            ? await RunCodeAsync(request.Action, request.Values, cancellationToken)
            : await runner.RunAsync(request.Action, request.Values, cancellationToken);
    }

    private async Task<ActionOutcome> RunCodeAsync(
        BotAction action,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken
    )
    {
        if (runCode is null)
        {
            return Failed("This bot has no way to run a code action from the SDK. Pass runCodeAction to AddSuwagcmsBot, and deploy.", 0);
        }
        var unfit = ActionValues.Check(action, values);
        if (unfit.Count > 0)
        {
            return Failed($"{string.Join(" ", unfit.Select(item => $"{item.Message} {item.Fix}"))} Nothing was run.", 0);
        }
        var started = time.GetTimestamp();
        long Elapsed()
        {
            return (long)time.GetElapsedTime(started).TotalMilliseconds;
        }
        try
        {
            var outcome = await runCode(action.Id, values, cancellationToken);
            var problems = action
                .ResultFields.Select(field => (field.Key, Fact: field.Value.Named(field.Key)))
                .Where(field =>
                    outcome.Succeeded && (!outcome.Results.TryGetValue(field.Key, out var value) || !FactValues.Accepts(field.Fact, value))
                )
                .Select(field => $"Result '{field.Key}' is missing or does not fit. {FactValues.Valid(field.Fact)}")
                .ToList();
            return problems.Count > 0 ? Failed(string.Join(" ", problems), Elapsed()) : outcome with { ElapsedMs = Elapsed() };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failed(string.Create(CultureInfo.InvariantCulture, $"The action's code threw {exception.GetType().Name}."), Elapsed());
        }
    }

    private static ActionOutcome Failed(string reason, long elapsed) =>
        new()
        {
            Outcome = ActionOutcome.Failure,
            Reason = reason,
            ElapsedMs = elapsed,
        };
}
