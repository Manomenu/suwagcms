using Suwagcms.Sdk.Format.Actions;

namespace Suwagcms.Sdk.BotRuntime.Actions;

public sealed record ActionTestRequest
{
    public required BotAction Action { get; init; }

    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();
}
