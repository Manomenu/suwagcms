namespace Suwagcms.Sdk.Format;

public sealed record Procedure
{
    public const string ConversationId = "conversation";

    public required string Id { get; init; }

    public string Description { get; init; } = "";

    public string? FirstStep { get; init; }

    public IReadOnlyDictionary<string, ProcedureStep> Steps { get; init; } = new Dictionary<string, ProcedureStep>();

    public StallInstruction? WhenStalled { get; init; }
}
