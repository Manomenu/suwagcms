using System.Text.Json;
using Suwagcms.Contracts.Facts;

namespace Suwagcms.Contracts;

public static class EditOverlay
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static TSave Over<TSave>(object stored, TSave given)
        where TSave : class
    {
        ArgumentNullException.ThrowIfNull(stored);
        var merged = JsonSerializer.SerializeToNode(stored, stored.GetType(), Web)?.AsObject() ?? [];
        foreach (var (field, value) in JsonSerializer.SerializeToNode(given, Web)?.AsObject() ?? [])
        {
            if (value is not null)
            {
                merged[field] = value.DeepClone();
            }
        }
        return merged.Deserialize<TSave>(Web) ?? throw new InvalidOperationException($"{typeof(TSave).Name} read back as nothing.");
    }

    public static SaveFact OverFact(object stored, SaveFact given, bool clearsPattern)
    {
        ArgumentNullException.ThrowIfNull(given);
        var kept = Over(stored, new SaveFact(null, null, null, null, null, null, null, null));
        if (given.Type is not null && given.Type != kept.Type)
        {
            kept = kept with { Values = null, Min = null, Max = null, MaxLength = null, Pattern = null };
        }
        return Over(clearsPattern ? kept with { Pattern = null } : kept, given);
    }
}
