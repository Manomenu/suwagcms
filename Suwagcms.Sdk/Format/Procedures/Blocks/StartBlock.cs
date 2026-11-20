using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Sdk.Format.Procedures.Blocks;

public sealed record StartBlock(string Target) : Block
{
    public override string Name => "start";

    public override IEnumerable<BlockFact> Facts => [];

    public override IEnumerable<BlockTarget> Targets =>
        string.IsNullOrWhiteSpace(Target) || Target == Procedure.ConversationId
            ? []
            : [new BlockTarget(new ResourceRef(ResourceKind.Procedure, Target), "start")];

    public override BlockGap? Gap =>
        string.IsNullOrWhiteSpace(Target)
            ? new BlockGap("start names no procedure.", "Write the id of the procedure to start: start: documentsList.")
            : null;
}
