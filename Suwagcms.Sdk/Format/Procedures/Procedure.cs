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

    // Why: null rather than empty when it has none, so a version of a procedure without local facts stays byte for byte what it was.
    public IReadOnlyDictionary<string, LocalFact>? LocalFacts { get; init; }

    [JsonIgnore]
    public IReadOnlyDictionary<string, LocalFact> Locals => LocalFacts ?? new Dictionary<string, LocalFact>();
}
