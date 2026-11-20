using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Sdk.Format.Actions;
using Suwagcms.Sdk.Format.Facts;

namespace Suwagcms.Sdk.BotRuntime.Actions;

public sealed class HttpActionRunner(Func<string, string?>? findSecret = null, TimeProvider? time = null) : IDisposable
{
    public const int AnswerStartLength = 2000;

    private const string Mask = "***";

    private readonly HttpClient http = new(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
    private readonly Func<string, string?> find = findSecret ?? ActionSecrets.FromEnvironment;
    private readonly TimeProvider clock = time ?? TimeProvider.System;

    public void Dispose() => http.Dispose();

    public async Task<ActionOutcome> RunAsync(
        BotAction action,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(values);
        if (action.Implementation?.Http is not { } call)
        {
            return Failed($"Action '{action.Id}' is not an HTTP action, so the bot's code runs it. Nothing was sent.");
        }
        var unfit = ActionValues.Check(action, values);
        if (unfit.Count > 0)
        {
            return Failed($"{string.Join(" ", unfit.Select(item => $"{item.Message} {item.Fix}"))} Nothing was sent.");
        }
        var names = (call.Headers ?? new Dictionary<string, string>()).Values.SelectMany(ActionTemplates.SecretsIn).Distinct().ToList();
        var secrets = names.ToDictionary(name => name, find, StringComparer.Ordinal);
        var absent = names.Where(name => string.IsNullOrEmpty(secrets[name])).ToList();
        if (absent.Count > 0)
        {
            return Failed(
                $"{string.Join(" ", absent.Select(name => $"Secret {name} was not found in the bot's environment."))} Nothing was sent."
            );
        }
        var known = secrets.Values.OfType<string>().ToList();
        return Masked(await SendAsync(action, call, values, secrets, cancellationToken), known);
    }

    private async Task<ActionOutcome> SendAsync(
        BotAction action,
        HttpCall call,
        IReadOnlyDictionary<string, string> values,
        Dictionary<string, string?> secrets,
        CancellationToken cancellationToken
    )
    {
        var seconds = call.TimeoutSeconds ?? HttpCall.DefaultTimeoutSeconds;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(seconds), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var started = clock.GetTimestamp();
        long Elapsed()
        {
            return (long)clock.GetElapsedTime(started).TotalMilliseconds;
        }
        var host = new Uri(ActionTemplates.Placeholder().Replace(call.Address, "x")).Host;
        try
        {
            using var request = Request(action, call, values, secrets);
            using var response = await http.SendAsync(request, linked.Token);
            var status = (int)response.StatusCode;
            var body = await response.Content.ReadAsStringAsync(linked.Token);
            return Answered(action, status, body, Elapsed());
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return Failed(
                string.Create(CultureInfo.InvariantCulture, $"{host} gave no answer within the time limit of {seconds} seconds."),
                Elapsed()
            );
        }
        catch (HttpRequestException)
        {
            return Failed($"No connection to {host}.", Elapsed());
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException)
        {
            return Failed($"The request to {host} could not be sent: {exception.GetType().Name}. Check the action's URL and headers.");
        }
    }

    private static HttpRequestMessage Request(
        BotAction action,
        HttpCall call,
        IReadOnlyDictionary<string, string> values,
        Dictionary<string, string?> secrets
    )
    {
        var inUrl = ActionTemplates.PlaceholdersIn(call.Address).ToHashSet(StringComparer.Ordinal);
        var others = action.ParameterFields.Where(field => !inUrl.Contains(field.Key)).ToList();
        var url = ActionTemplates
            .Placeholder()
            .Replace(call.Address, match => Uri.EscapeDataString(values.GetValueOrDefault(match.Groups[1].Value, "")));
        var inQuery = call.Method is "GET" or "DELETE";
        if (inQuery && others.Count > 0)
        {
            var query = string.Join(
                '&',
                others.Select(field => $"{Uri.EscapeDataString(field.Key)}={Uri.EscapeDataString(values[field.Key])}")
            );
            url += (url.Contains('?', StringComparison.Ordinal) ? "&" : "?") + query;
        }
        var request = new HttpRequestMessage(new HttpMethod(call.Method), new Uri(url));
        if (!inQuery)
        {
            var body = new JsonObject();
            foreach (var (name, field) in others)
            {
                body[name] = field.Type == FactTypes.WholeNumber ? JsonValue.Create(FactValues.Parse(values[name])) : values[name];
            }
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }
        foreach (var (name, template) in call.Headers ?? new Dictionary<string, string>())
        {
            var value = ActionTemplates
                .Placeholder()
                .Replace(
                    template,
                    match =>
                        match.Groups[1].Value.StartsWith(ActionTemplates.SecretPrefix, StringComparison.Ordinal)
                            ? secrets.GetValueOrDefault(match.Groups[1].Value[ActionTemplates.SecretPrefix.Length..]) ?? ""
                            : match.Value
                );
            if (!request.Headers.TryAddWithoutValidation(name, value))
            {
                request.Content?.Headers.Remove(name);
                request.Content?.Headers.TryAddWithoutValidation(name, value);
            }
        }
        return request;
    }

    private static ActionOutcome Answered(BotAction action, int status, string body, long elapsed)
    {
        var outcome = new ActionOutcome
        {
            Outcome = ActionOutcome.Success,
            Status = status,
            ElapsedMs = elapsed,
            AnswerStart = body,
        };
        if (status is < 200 or >= 300)
        {
            return outcome with
            {
                Outcome = ActionOutcome.Failure,
                Reason = string.Create(CultureInfo.InvariantCulture, $"The service answered {status}, outside 2xx."),
            };
        }
        if (action.ResultFields.Count == 0)
        {
            return outcome;
        }
        var (results, problems) = Read(action, body);
        return problems.Count == 0
            ? outcome with
            {
                Results = results,
            }
            : outcome with
            {
                Outcome = ActionOutcome.Failure,
                Reason = string.Create(CultureInfo.InvariantCulture, $"Answer {status}: {string.Join(" ", problems)}"),
            };
    }

    private static (Dictionary<string, string> Results, List<string> Problems) Read(BotAction action, string body)
    {
        JsonObject? answer;
        try
        {
            answer = JsonNode.Parse(body) as JsonObject;
        }
        catch (JsonException)
        {
            answer = null;
        }
        if (answer is null)
        {
            return ([], ["it is not a JSON object, and the action declares results."]);
        }
        var results = new Dictionary<string, string>(StringComparer.Ordinal);
        var problems = new List<string>();
        foreach (var (name, field) in action.ResultFields)
        {
            var fact = field.Named(name);
            if (!answer.TryGetPropertyValue(name, out var member) || member is null)
            {
                problems.Add($"result '{name}' is missing from the answer.");
            }
            else if (Text(member, fact) is { } text && FactValues.Accepts(fact, text))
            {
                results[name] = text;
            }
            else
            {
                problems.Add($"result '{name}' does not fit. {FactValues.Valid(fact)}");
            }
        }
        return (results, problems);
    }

    private static string? Text(JsonNode member, GlobalFact fact)
    {
        var kind = member.GetValueKind();
        return fact.Type == FactTypes.WholeNumber
            ? kind == JsonValueKind.Number
                ? member.ToJsonString()
                : null
            : kind == JsonValueKind.String
                ? member.GetValue<string>()
                : null;
    }

    private static ActionOutcome Failed(string reason, long elapsed = 0) =>
        new()
        {
            Outcome = ActionOutcome.Failure,
            Reason = reason,
            ElapsedMs = elapsed,
        };

    private static ActionOutcome Masked(ActionOutcome outcome, List<string> secrets)
    {
        var answer = Hidden(outcome.AnswerStart, secrets);
        var cut = answer is { Length: > AnswerStartLength };
        return outcome with
        {
            Reason = Hidden(outcome.Reason, secrets),
            AnswerStart = cut ? answer?[..AnswerStartLength] : answer,
            AnswerCut = cut,
            Results = outcome.Results.ToDictionary(result => result.Key, result => Hidden(result.Value, secrets) ?? ""),
        };
    }

    private static string? Hidden(string? text, List<string> secrets) =>
        text is null ? null : secrets.Aggregate(text, (masked, secret) => masked.Replace(secret, Mask, StringComparison.Ordinal));
}
