using System.Text.Json;

namespace Suwagcms.Cli.Connection;

internal sealed record ServerAnswer(int ExitCode, string Message, IReadOnlyList<JsonElement> Mistakes, JsonElement? Body = null)
{
    public static ServerAnswer Done { get; } = new(ExitCodes.Done, "", []);

    public static ServerAnswer Succeeded(JsonElement? body) => new(ExitCodes.Done, "", [], body);

    public static ServerAnswer Failed(string message) => new(ExitCodes.Unreachable, message, []);

    public static ServerAnswer Refused(IReadOnlyList<JsonElement> mistakes) =>
        new(ExitCodes.Refused, string.Join(Environment.NewLine, mistakes.Select(Describe)), mistakes);

    // Why: the server states a mistake as code, path, message and fix; whatever of them it sends is shown, the fix last.
    private static string Describe(JsonElement mistake)
    {
        var code = Text(mistake, "code");
        var path = Text(mistake, "path");
        var message = Text(mistake, "message");
        var fix = Text(mistake, "fix");
        var head = string.Join(" ", new[] { code, path, message }.Where(part => part is not null));
        return fix is null ? head : $"{head}{Environment.NewLine}  Fix: {fix}";
    }

    private static string? Text(JsonElement mistake, string name) =>
        mistake.ValueKind == JsonValueKind.Object && mistake.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
