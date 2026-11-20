using System.Text.Json.Serialization;
using Suwagcms.Sdk.Format.Facts;

namespace Suwagcms.Sdk.Format.Procedures;

public sealed record Procedure
{
    public const string ConversationId = "conversation";

    public required string Id { get; init; }

    public string Description { get; init; } = "";

    public string? FirstStep { get; init; }

    public IReadOnlyDictionary<string, ProcedureStep> Steps { get; init; } = new Dictionary<string, ProcedureStep>();

    public StallInstruction? WhenStalled { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WhenInterrupted { get; init; }

    public IReadOnlyDictionary<string, LocalFact>? LocalFacts { get; init; }

    [JsonIgnore]
    public bool IsDiscardedWhenInterrupted => WhenInterrupted == ProcedureInterruptions.Discard;

    [JsonIgnore]
    public IReadOnlyDictionary<string, LocalFact> Locals => LocalFacts ?? new Dictionary<string, LocalFact>();
}
