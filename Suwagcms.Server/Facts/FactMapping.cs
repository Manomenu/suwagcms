using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Facts;

internal static class FactMapping
{
    public static GlobalFact ToRecord(string name, SaveFact body) =>
        new()
        {
            Name = name,
            Type = body.Type ?? "",
            Description = body.Description ?? "",
            WritableBy = body.WritableBy ?? FactWriters.Set,
            Values = body.Values,
            Min = body.Min,
            Max = body.Max,
            MaxLength = body.MaxLength,
        };

    public static Fact Show(StoredResource stored, IReadOnlyList<StoredResource> all) =>
        Show((GlobalFact)stored.Record, stored.Revision, all);

    public static Fact Show(GlobalFact fact, int revision, IEnumerable<StoredResource> all) =>
        new(
            fact.Name,
            fact.Type,
            fact.Description,
            fact.WritableBy,
            fact.Values,
            fact.Min,
            fact.Max,
            fact.MaxLength,
            revision,
            UsedBy(fact.Name, all)
        );

    // Why: the procedures that name the fact come from the reference walker, the one place that knows where a definition names a fact.
    private static List<string> UsedBy(string fact, IEnumerable<StoredResource> all) =>
        [
            .. ReferenceWalker
                .From(ResourceKinds.Assemble(all))
                .Where(reference => reference.To.Id == fact)
                .Select(reference => reference.From.Id)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
}
