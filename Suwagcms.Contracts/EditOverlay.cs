using System.Text.Json;
using Suwagcms.Contracts.Facts;

namespace Suwagcms.Contracts;

/// <summary>What an edit saves: the fields it gives over the fields stored, whichever the kind, so a field added to a kind is kept by every edit with no line to forget. A field given as null is not given.</summary>
public static class EditOverlay
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>The body to save: <paramref name="given"/> over <paramref name="stored"/>, which is the stored record or the view of it the server sent.</summary>
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

    /// <summary>A fact's edit: a type that changes starts from no constraints, since they belong to a type, and <paramref name="clearsPattern"/> takes the pattern off; a pattern given replaces the stored one whole.</summary>
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
