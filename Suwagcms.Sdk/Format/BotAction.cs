using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Format;

// Why: an action never names a fact: its parameters and results are its own, and a step of a procedure maps them, so one action serves many procedures.
public sealed record BotAction
{
    public required string Id { get; init; }

    public string Description { get; init; } = "";

    // Why: null rather than empty when it has none, so what the author left out stays left out when read back.
    public IReadOnlyDictionary<string, ActionField>? Parameters { get; init; }

    public IReadOnlyDictionary<string, ActionField>? Results { get; init; }

    public ActionImplementation? Implementation { get; init; }

    [JsonIgnore]
    public IReadOnlyDictionary<string, ActionField> ParameterFields => Parameters ?? new Dictionary<string, ActionField>();

    [JsonIgnore]
    public IReadOnlyDictionary<string, ActionField> ResultFields => Results ?? new Dictionary<string, ActionField>();

    [JsonIgnore]
    public bool IsCode => Implementation is { IsCode: true };
}
