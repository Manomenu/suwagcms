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
