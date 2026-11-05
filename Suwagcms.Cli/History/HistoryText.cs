using System.Globalization;
using System.Text;
using System.Text.Json;
using Suwagcms.Contracts;

namespace Suwagcms.Cli.History;

internal static class HistoryText
{
    public static string Page(HistoryPage page, bool ofBot)
    {
        if (page.Entries.Count == 0)
        {
            return $"No change has been recorded since {Day(page.Since)}, when history started.";
        }
        var text = new StringBuilder();
        foreach (var entry in page.Entries)
        {
            Entry(text, entry, ofBot);
        }
        if (page.Next is { } next)
        {
            text.Append(string.Create(CultureInfo.InvariantCulture, $"More: --before {next}{Environment.NewLine}"));
        }
        return text.ToString().TrimEnd();
    }

    private static void Entry(StringBuilder text, HistoryEntry entry, bool ofBot)
    {
        var parts = new List<string>
        {
            entry.At.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            entry.Action.PadRight(8),
        };
        if (ofBot)
        {
            parts.Add($"{entry.Kind} {entry.ResourceId}");
        }
        if (entry.Revision is { } revision)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"revision {revision}"));
        }
        if (entry.Batch is { } batch)
        {
            parts.Add($"batch {batch.ToString()[..8]}");
        }
        text.Append(string.Join("  ", parts).TrimEnd()).Append(Environment.NewLine);
        // Why: an add holds every field, which is the resource as `show` prints it; the line that it was added says enough.
        if (entry.Action == "added")
        {
            return;
        }
        foreach (var change in entry.Changes)
        {
            text.Append("  ").Append(change.Field).Append(Environment.NewLine);
            var (removed, added) = Differing(Lines(change.Old), Lines(change.New));
            foreach (var line in removed)
            {
                text.Append("    - ").Append(line).Append(Environment.NewLine);
            }
            foreach (var line in added)
            {
                text.Append("    + ").Append(line).Append(Environment.NewLine);
            }
        }
    }

    private static string[] Lines(JsonElement? value) =>
        value switch
        {
            null or { ValueKind: JsonValueKind.Null } => [],
            { ValueKind: JsonValueKind.String } text => (text.GetString() ?? "").ReplaceLineEndings("\n").Split('\n'),
            { } other => [other.GetRawText()],
        };

    // Why: a long article changes in a line or two, so the lines both sides share at either end are left out.
    private static (IEnumerable<string> Removed, IEnumerable<string> Added) Differing(string[] old, string[] fresh)
    {
        var start = 0;
        while (start < old.Length && start < fresh.Length && old[start] == fresh[start])
        {
            start++;
        }
        var end = 0;
        while (end < old.Length - start && end < fresh.Length - start && old[old.Length - 1 - end] == fresh[fresh.Length - 1 - end])
        {
            end++;
        }
        return (old.Skip(start).Take(old.Length - start - end), fresh.Skip(start).Take(fresh.Length - start - end));
    }

    private static string Day(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
