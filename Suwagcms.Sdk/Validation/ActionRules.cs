using System.Text.RegularExpressions;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Validation;

// Why: what the CMS controls is refused at Save; a code action's contract is the bot's, and a Save mistake from a declaration would stop every save of the bot, so these rules leave it alone.
internal static partial class ActionRules
{
    private const string Sample = "CRM_TOKEN";

    private static readonly string[] Methods = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    private static readonly string[] SensitiveHeaders = ["authorization", "proxy-authorization", "cookie"];

    private static readonly string[] SensitiveWords = ["key", "token", "secret", "password", "auth"];

    public static IReadOnlyList<Rule> All { get; } =
    [
        For(MistakeCodes.ActionIdInvalid, Id),
        For(MistakeCodes.ActionDescriptionMissing, Description),
        For(MistakeCodes.ActionImplementationMissing, Implementation),
        ForHttp(MistakeCodes.ActionMethodInvalid, Method),
        ForHttp(MistakeCodes.ActionUrlInvalid, Url),
        ForHttp(MistakeCodes.ActionUrlCredentials, Credentials),
        ForHttp(MistakeCodes.ActionUrlParameter, UrlParameters),
        ForHttp(MistakeCodes.ActionTimeoutInvalid, Timeout),
        ForHttp(MistakeCodes.ActionHeaderInvalid, HeaderShape),
        ForHttp(MistakeCodes.ActionSecretName, SecretNames),
        ForHttp(MistakeCodes.ActionSecretInUrl, SecretsInUrl),
        ForHttp(MistakeCodes.ActionSecretLiteral, Literals),
    ];

    // Why: a character a header cannot hold would let a value start another header of the request.
    [GeneratedRegex("^[!#$%&'*+.^_`|~0-9A-Za-z-]+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HeaderName();

    private static Rule For(string code, Func<BotAction, IEnumerable<Mistake>> check) =>
        new(code, ValidationStage.Save, definition => definition.Actions.Where(action => !action.IsCode).SelectMany(check));

    private static Rule ForHttp(string code, Func<BotAction, HttpCall, IEnumerable<Mistake>> check) =>
        For(code, action => action.Implementation?.Http is { } http ? check(action, http) : []);

    private static IEnumerable<Mistake> Id(BotAction action)
    {
        if (!ResourceIds.IsCamelCase(action.Id))
        {
            yield return Make(
                MistakeCodes.ActionIdInvalid,
                action,
                "id",
                $"'{action.Id}' is not an action id: it is one camelCase word.",
                $"Rename the action to '{ResourceIds.Suggest(action.Id)}'."
            );
        }
    }

    private static IEnumerable<Mistake> Description(BotAction action)
    {
        if (string.IsNullOrWhiteSpace(action.Description))
        {
            yield return Make(
                MistakeCodes.ActionDescriptionMissing,
                action,
                "description",
                $"Action '{action.Id}' has no description.",
                "Add one: an author reads it to choose the action."
            );
        }
    }

    private static IEnumerable<Mistake> Implementation(BotAction action)
    {
        if (action.Implementation?.Http is null)
        {
            yield return Make(
                MistakeCodes.ActionImplementationMissing,
                action,
                "implementation",
                $"Action '{action.Id}' has no implementation.",
                "Add 'implementation: { http: { method: POST, url: https://... } }'."
            );
        }
    }

    private static IEnumerable<Mistake> Method(BotAction action, HttpCall http)
    {
        if (!Methods.Contains(http.Method, StringComparer.Ordinal))
        {
            yield return Make(
                MistakeCodes.ActionMethodInvalid,
                action,
                Http("method"),
                string.IsNullOrWhiteSpace(http.Method)
                    ? $"Action '{action.Id}' has no method."
                    : $"Action '{action.Id}' has the method '{http.Method}'.",
                $"Use one of: {string.Join(", ", Methods)}."
            );
        }
    }

    // Why: {name} is filled in at run time, so a URL is judged with something ordinary in its place.
    private static Uri? Parsed(HttpCall http) =>
        Uri.TryCreate(ActionTemplates.Placeholder().Replace(http.Address, "x"), UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && uri.Host.Length > 0
            ? uri
            : null;

    private static IEnumerable<Mistake> Url(BotAction action, HttpCall http)
    {
        if (Parsed(http) is null)
        {
            yield return Make(
                MistakeCodes.ActionUrlInvalid,
                action,
                Http("url"),
                $"The URL of action '{action.Id}' is not an absolute http or https address.",
                "Write a whole address, like https://crm.internal/serviceability."
            );
        }
    }

    private static IEnumerable<Mistake> Credentials(BotAction action, HttpCall http)
    {
        if (Parsed(http) is { UserInfo.Length: > 0 })
        {
            yield return Make(
                MistakeCodes.ActionUrlCredentials,
                action,
                Http("url"),
                $"The URL of action '{action.Id}' holds a user or a password, and URLs end up in logs.",
                $"Remove them and send them in a header: Authorization: Bearer {{secret:{Sample}}}."
            );
        }
    }

    private static IEnumerable<Mistake> UrlParameters(BotAction action, HttpCall http) =>
        ActionTemplates
            .PlaceholdersIn(http.Address)
            .Where(name => !name.StartsWith(ActionTemplates.SecretPrefix, StringComparison.Ordinal))
            .Where(name => !action.ParameterFields.ContainsKey(name))
            .Distinct(StringComparer.Ordinal)
            .Select(name =>
                Make(
                    MistakeCodes.ActionUrlParameter,
                    action,
                    Http("url"),
                    $"The URL of action '{action.Id}' names {{{name}}}, which is not one of its parameters.",
                    action.ParameterFields.Count == 0
                        ? $"Add a parameter '{name}', or remove {{{name}}} from the URL."
                        : $"Add a parameter '{name}', or use one it has: {string.Join(", ", action.ParameterFields.Keys.Order(StringComparer.Ordinal))}."
                )
            );

    private static IEnumerable<Mistake> Timeout(BotAction action, HttpCall http)
    {
        if (http.TimeoutSeconds is < 1 or > 60)
        {
            yield return Make(
                MistakeCodes.ActionTimeoutInvalid,
                action,
                Http("timeoutSeconds"),
                $"The timeout of action '{action.Id}' is outside 1 to 60 seconds.",
                $"Use a whole number from 1 to 60, or leave it out for {HttpCall.DefaultTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} seconds."
            );
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> Headers(HttpCall http) => http.Headers ?? new Dictionary<string, string>();

    private static IEnumerable<Mistake> HeaderShape(BotAction action, HttpCall http) =>
        Headers(http)
            .Where(header => !HeaderName().IsMatch(header.Key) || header.Value.AsSpan().IndexOfAny('\r', '\n') >= 0)
            .Select(header =>
                Make(
                    MistakeCodes.ActionHeaderInvalid,
                    action,
                    Http($"headers.{header.Key}"),
                    $"The header '{header.Key}' of action '{action.Id}' cannot be sent: its name holds a character no header name can, or its value breaks a line.",
                    "Use a plain header name, and a value on one line."
                )
            );

    private static IEnumerable<Mistake> SecretNames(BotAction action, HttpCall http) =>
        Headers(http)
            .SelectMany(header =>
                ActionTemplates
                    .SecretsIn(header.Value)
                    .Where(name => !ActionTemplates.SecretName().IsMatch(name))
                    .Select(name =>
                        Make(
                            MistakeCodes.ActionSecretName,
                            action,
                            Http($"headers.{header.Key}"),
                            $"'{name}' in header '{header.Key}' is not a secret's name: it is capital letters, digits and underscores, starting with a letter.",
                            $"Write the reference like {{secret:{Sample}}}, and set that name in the bot's environment."
                        )
                    )
            );

    private static IEnumerable<Mistake> SecretsInUrl(BotAction action, HttpCall http)
    {
        if (ActionTemplates.SecretsIn(http.Address).Any())
        {
            yield return Make(
                MistakeCodes.ActionSecretInUrl,
                action,
                Http("url"),
                $"The URL of action '{action.Id}' holds a secret reference, and URLs end up in logs.",
                $"Send it in a header, like Authorization: Bearer {{secret:{Sample}}}, or put the call behind a code action."
            );
        }
    }

    private static IEnumerable<Mistake> Literals(BotAction action, HttpCall http) =>
        Headers(http)
            .Where(header => IsSensitive(header.Key) && !ActionTemplates.SecretsIn(header.Value).Any())
            .Select(header =>
                Make(
                    MistakeCodes.ActionSecretLiteral,
                    action,
                    Http($"headers.{header.Key}"),
                    $"Header '{header.Key}' holds a literal value, and the CMS never stores secrets.",
                    header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                        ? $"Write 'Bearer {{secret:{Sample}}}' and set {Sample} in the bot's environment."
                        : $"Write '{{secret:{Sample}}}' and set {Sample} in the bot's environment."
                )
            )
            .Concat(
                LiteralQueryNames(http)
                    .Select(name =>
                        Make(
                            MistakeCodes.ActionSecretLiteral,
                            action,
                            Http("url"),
                            $"The URL's query parameter '{name}' holds a literal value, and the CMS never stores secrets.",
                            $"Send it in a header, like Authorization: Bearer {{secret:{Sample}}}, or put the call behind a code action."
                        )
                    )
            );

    private static IEnumerable<string> LiteralQueryNames(HttpCall http)
    {
        var query = http.Address.Split('#')[0].Split('?', 2);
        return query.Length < 2
            ? []
            : query[1]
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split('=', 2))
                .Where(pair => pair.Length == 2 && pair[1].Length > 0 && !pair[1].Contains('{', StringComparison.Ordinal))
                .Select(pair => Uri.UnescapeDataString(pair[0]))
                .Where(IsSensitive)
                .Distinct(StringComparer.Ordinal);
    }

    private static bool IsSensitive(string name)
    {
        var lower = name.ToLowerInvariant();
        return SensitiveHeaders.Contains(lower, StringComparer.Ordinal)
            || SensitiveWords.Any(word => lower.Contains(word, StringComparison.Ordinal));
    }

    private static string Http(string field) => $"implementation.http.{field}";

    private static Mistake Make(string code, BotAction action, string path, string message, string fix) =>
        new(code, ValidationStage.Save, new ResourceRef(ResourceKind.Action, action.Id), path, message, fix, []);
}
