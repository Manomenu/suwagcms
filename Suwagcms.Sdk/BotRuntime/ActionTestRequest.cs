using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.BotRuntime;

// Why: the request carries the saved action, so an action can be tried before any version holds it and a run does not depend on what the bot has loaded.
public sealed record ActionTestRequest
{
    public required BotAction Action { get; init; }

    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();
}
