using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Format.Actions;

public sealed record BotAction
{
    public required string Id { get; init; }

    public string Description { get; init; } = "";

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
