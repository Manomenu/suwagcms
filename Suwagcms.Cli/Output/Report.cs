using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Suwagcms.Cli.Output;

internal sealed class Report(TextWriter output, TextWriter error, bool json)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<int> DoneAsync<T>(T document, string text)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(document, Options));
        }
        else if (text.Length > 0)
        {
            await output.WriteLineAsync(text);
        }
        return ExitCodes.Done;
    }

    // Why: a failed push still has a result of its own to give, so --json shows that document instead of a bare error.
    public async Task<int> FailedWithAsync<T>(int exitCode, T document, string text)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(document, Options));
        }
        else
        {
            await error.WriteLineAsync(text);
        }
        return exitCode;
    }

    public async Task<int> FailedAsync(int exitCode, FailureDocument document, string text)
    {
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(document, Options));
        }
        else
        {
            await error.WriteLineAsync(text);
        }
        return exitCode;
    }
}
