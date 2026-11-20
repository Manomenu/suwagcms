using System.Globalization;
using Suwagcms.Cli.Versions;
using Suwagcms.Contracts.Changes;

namespace Suwagcms.Cli.Changes;

internal static class CompareText
{
    public const string Nothing = "Nothing to apply: the file lists no changes.";

    private const string Unchanged = "unchanged";

    public static bool Differs(ChangesApplied applied) => applied.Changes.Any(change => change.Action != Unchanged);

    public static string Counts(ChangesApplied applied) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{applied.Added} added, {applied.Replaced} replaced, {applied.Unchanged} unchanged, {applied.Edited} edited, {applied.Deleted} deleted"
        );

    public static string Of(ChangesApplied applied)
    {
        ArgumentNullException.ThrowIfNull(applied);
        if (applied.Changes.Count == 0)
        {
            return Nothing;
        }
        var lines = new List<string>
        {
            Differs(applied)
                ? $"Would apply: {Counts(applied)}."
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"The same: applying the file would change nothing, {applied.Unchanged} unchanged."
                ),
        };
        foreach (var change in applied.Changes.Where(change => change.Action != Unchanged))
        {
            lines.Add(
                string.Create(CultureInfo.InvariantCulture, $"  change {change.Change}, {Noun(change.Kind)} '{change.Id}' {change.Action}")
            );
            lines.AddRange(DiffText.Differences(change.Fields, change.Parts, "    "));
        }
        if (applied.NotMentioned.Count > 0)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"Not in the file, left as they are: {applied.NotMentioned.Count}"));
            lines.AddRange(applied.NotMentioned.Select(resource => $"  {Noun(resource.Kind)} '{resource.Id}'"));
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string Noun(string kind) => kind == "knowledgeBase" ? "knowledge base" : kind;
}
