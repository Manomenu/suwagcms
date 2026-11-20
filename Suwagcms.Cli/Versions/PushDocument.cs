using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Cli.Versions;

internal sealed record PushDocument(string Bot, string Result, int Version, int? BotHas, string? Reason, IReadOnlyList<Mistake> Mistakes)
{
    public const string Pushed = "pushed";
    public const string Unchanged = "unchanged";
    public const string Failed = "failed";

    public static PushDocument Of(string bot, PushResult result) =>
        new(bot, result.Outcome == PushOutcome.Succeeded ? Pushed : Failed, result.Version, result.BotHas, result.Reason, result.Mistakes);
}
