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

    public static Fact Show(StoredResource stored) => Show((GlobalFact)stored.Record, stored.Revision);

    public static Fact Show(GlobalFact fact, int revision) =>
        new(fact.Name, fact.Type, fact.Description, fact.WritableBy, fact.Values, fact.Min, fact.Max, fact.MaxLength, revision);
}
