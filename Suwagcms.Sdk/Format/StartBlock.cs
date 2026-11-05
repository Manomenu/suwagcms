namespace Suwagcms.Sdk.Format;

public sealed record StartBlock(string Target) : Block
{
    public override string Name => "start";

    public override IEnumerable<BlockFact> Facts => [];

    // Why: a blank target is work still to do, and conversation is refused by a rule of its own, so neither is an edge that a loop or a missing-target rule should also report.
    public override IEnumerable<BlockTarget> Targets =>
        string.IsNullOrWhiteSpace(Target) || Target == Procedure.ConversationId
            ? []
            : [new BlockTarget(new ResourceRef(ResourceKind.Procedure, Target), "start")];

    public override BlockGap? Gap =>
        string.IsNullOrWhiteSpace(Target)
            ? new BlockGap("start names no procedure.", "Write the id of the procedure to start: start: documentsList.")
            : null;
}
