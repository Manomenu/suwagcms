using System.Globalization;
using Suwagcms.Contracts.Renames;

namespace Suwagcms.Cli.Renames;

internal static class RenameText
{
    public static string Of(RenameAnswer answer, bool dryRun)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var count = answer.Referrers.Select(referrer => (referrer.Kind, referrer.Id)).Distinct().Count();
        var referrers = string.Create(CultureInfo.InvariantCulture, $"{count} referrer{(count == 1 ? "" : "s")}");
        var name = $"{answer.Kind} '{answer.OldId}' to '{answer.Id}'";
        if (!dryRun)
        {
            return $"Renamed {name}. Rewrote {referrers}.";
        }
        return string.Join(
            Environment.NewLine,
            [
                count == 0 ? $"Would rename {name}, rewriting nothing." : $"Would rename {name} and rewrite {referrers}:",
                .. answer.Referrers.Select(Line),
                "Nothing was changed.",
            ]
        );
    }

    private static string Line(RenameReferrer referrer) =>
        referrer.Field is "content" or "llmGuide"
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"  {referrer.Kind} {referrer.Id} ({referrer.Field}, {referrer.Links} link{(referrer.Links == 1 ? "" : "s")})"
            )
            : $"  {referrer.Kind} {referrer.Id} ({referrer.Field})";
}
