using System.Text.Json;

namespace Suwagcms.Cli.Connection;

internal sealed record ServerAnswer(int ExitCode, string Message, IReadOnlyList<JsonElement> Mistakes, JsonElement? Body = null)
{
    public static ServerAnswer Done { get; } = new(ExitCodes.Done, "", []);

    public static ServerAnswer Succeeded(JsonElement? body) => new(ExitCodes.Done, "", [], body);

    public static ServerAnswer Failed(string message) => new(ExitCodes.Unreachable, message, []);

    public static ServerAnswer Refused(IReadOnlyList<JsonElement> mistakes) => new(ExitCodes.Refused, Describe(mistakes, null), mistakes);

    // Why: a link to each related resource is added when the caller knows the web app's address, which the answer does not.
    public static string Describe(IReadOnlyList<JsonElement> mistakes, Func<string, string, string?>? link) =>
        string.Join(Environment.NewLine, mistakes.Select(mistake => Describe(mistake, link)));

    // Why: the server states a mistake as code, path, message and fix; whatever of them it sends is shown, the fix last.
    private static string Describe(JsonElement mistake, Func<string, string, string?>? link)
    {
        var code = Text(mistake, "code");
        var path = Text(mistake, "path");
        var message = Text(mistake, "message");
        var fix = Text(mistake, "fix");
        var head = string.Join(" ", new[] { code, path, message }.Where(part => part is not null));
        var related = string.Concat(Related(mistake, code, link).Select(line => $"{Environment.NewLine}{line}"));
        return fix is null ? head + related : $"{head}{related}{Environment.NewLine}  Fix: {fix}";
    }

    private static IEnumerable<string> Related(JsonElement mistake, string? code, Func<string, string, string?>? link)
    {
        if (
            mistake.ValueKind != JsonValueKind.Object
            || !mistake.TryGetProperty("related", out var related)
            || related.ValueKind != JsonValueKind.Array
            || related.GetArrayLength() == 0
        )
        {
            yield break;
        }
        yield return code
            is "reference.missing"
                or "fact.enum.valueInUse"
                or "fact.type.inUse"
                or "fact.range.inUse"
                or "fact.writableBy.inUse"
            ? "  Used by:"
            : "  Related:";
        foreach (var item in related.EnumerateArray())
        {
            if (
                !item.TryGetProperty("resource", out var resource)
                || Text(resource, "kind") is not { } kind
                || Text(resource, "id") is not { } id
            )
            {
                continue;
            }
            var at = Text(item, "location");
            var page = link?.Invoke(kind, id);
            yield return $"    {Noun(kind)} {id}{Where(at)}{(page is null ? "" : $"  {page}")}";
        }
    }

    // Why: inside a procedure a place is a step and a block, which is how an author finds it; any other place is shown as the server names it.
    private static string Where(string? location)
    {
        if (location is null)
        {
            return "";
        }
        var parts = location.Split('.');
        if (parts is not ["steps", _, _, ..])
        {
            return $" ({location})";
        }
        var block = parts[2].Split('[')[0];
        return $", step {parts[1]} ({(block == "next" ? "when" : block)})";
    }

    public static string Noun(string kind) =>
        kind switch
        {
            "GlobalFact" => "fact",
            "KnowledgeBase" => "knowledge base",
            _ => kind.ToLowerInvariant(),
        };

    private static string? Text(JsonElement mistake, string name) =>
        mistake.ValueKind == JsonValueKind.Object && mistake.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
