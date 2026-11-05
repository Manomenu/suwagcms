using System.Globalization;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Cli.Versions;

internal static class DiffText
{
    public static string Of(VersionComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        var lines = new List<string> { Heading(comparison) };
        lines.AddRange(comparison.Definition.SelectMany(field => Field(field, "  ")));
        foreach (var resource in comparison.Resources)
        {
            lines.Add($"  {Noun(resource.Kind)} {resource.Id} {Word(resource.Difference)}");
            lines.AddRange(resource.Fields.SelectMany(field => Field(field, "    ")));
            foreach (var part in resource.Parts)
            {
                lines.Add($"    {part.Part} '{part.Name}' {Word(part.Difference)}");
                lines.AddRange(part.Fields.SelectMany(field => Field(field, "      ")));
            }
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string Heading(VersionComparison comparison)
    {
        var start = string.Create(CultureInfo.InvariantCulture, $"From v{comparison.From} to v{comparison.To}, bot {comparison.Bot}: ");
        return comparison.Resources.Count == 0 && comparison.Definition.Count == 0
            ? $"{start}no differences"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{start}{comparison.Added} added, {comparison.Removed} removed, {comparison.Changed} changed"
            );
    }

    private static IEnumerable<string> Field(FieldDifference field, string indent)
    {
        if (field.Lines is null)
        {
            yield return $"{indent}{field.Field} {field.Old ?? "(none)"} → {field.New ?? "(none)"}";
            yield break;
        }
        yield return $"{indent}{field.Field}";
        foreach (var line in field.Lines)
        {
            yield return line.Change switch
            {
                LineChange.Removed => $"{indent}  - {line.Text}",
                LineChange.Added => $"{indent}  + {line.Text}",
                LineChange.Folded => $"{indent}  … {line.Text}",
                LineChange.Kept => $"{indent}    {line.Text}",
                _ => throw new ArgumentOutOfRangeException(nameof(field), "unknown change of a line"),
            };
        }
    }

    private static string Word(Difference difference) => difference.ToString().ToLowerInvariant();

    private static string Noun(ResourceKind kind) =>
        kind switch
        {
            ResourceKind.GlobalFact => "fact",
            ResourceKind.KnowledgeBase => "knowledge base",
            ResourceKind.Article or ResourceKind.Action or ResourceKind.Procedure or ResourceKind.Definition => kind.ToString()
                .ToLowerInvariant(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
