using System.Globalization;
using Suwagcms.Contracts;

namespace Suwagcms.Cli.Facts;

internal static class FactText
{
    public static string Constraint(Fact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return fact.Type switch
        {
            "enum" => $"enum: {string.Join(", ", fact.Values ?? [])}",
            "int" => string.Create(CultureInfo.InvariantCulture, $"int, {fact.Min} to {fact.Max}"),
            "text" => string.Create(CultureInfo.InvariantCulture, $"text, up to {fact.MaxLength}")
                + (fact.Pattern is null ? "" : $", pattern {fact.Pattern.Regex}"),
            _ => fact.Type,
        };
    }

    public static string Table(IReadOnlyList<Fact> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Count == 0)
        {
            return "No facts yet. Add one: suwagcms facts add <name> --type text --max-length 80 --description <text>";
        }
        var nameWidth = facts.Max(fact => fact.Name.Length);
        var constraintWidth = facts.Max(fact => Constraint(fact).Length);
        var writerWidth = facts.Max(fact => fact.WritableBy.Length);
        return string.Join(
            Environment.NewLine,
            facts.Select(fact =>
                $"{fact.Name.PadRight(nameWidth)}  {Constraint(fact).PadRight(constraintWidth)}  {fact.WritableBy.PadRight(writerWidth)}  {fact.Description}"
            )
        );
    }

    private static string[] PatternLines(Fact fact) =>
        fact.Pattern is not { } pattern
            ? []
            :
            [
                $"  pattern:     {pattern.Regex}",
                $"    means:     {pattern.Means}",
                .. pattern.Example is null ? Array.Empty<string>() : [$"    example:   {pattern.Example}"],
                $"    tell:      {pattern.TellCustomer}",
            ];

    public static string Detail(Fact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return string.Join(
            Environment.NewLine,
            (string[])
                [
                    $"{fact.Name}",
                    $"  type:        {Constraint(fact)}",
                    $"  writableBy:  {fact.WritableBy}",
                    $"  description: {fact.Description}",
                    .. PatternLines(fact),
                    fact.UsedBy is { Count: > 0 } ? $"  used by:     {string.Join(", ", fact.UsedBy)}" : "  used by:     no procedure",
                    string.Create(CultureInfo.InvariantCulture, $"  revision:    {fact.Revision}"),
                ]
        );
    }
}
