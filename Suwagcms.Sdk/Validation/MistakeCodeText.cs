using System.Text.Json;

namespace Suwagcms.Sdk.Validation;

public static class MistakeCodeText
{
    public static string Text(this MistakeCode code) =>
        JsonSerializer.SerializeToElement(code).GetString()
        ?? throw new ArgumentOutOfRangeException(nameof(code), code, "A mistake code always serializes as its text.");
}
