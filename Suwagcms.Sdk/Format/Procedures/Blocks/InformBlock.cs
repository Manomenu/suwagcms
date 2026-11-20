namespace Suwagcms.Sdk.Format.Procedures.Blocks;

public sealed record InformBlock(InformArguments Arguments) : Block
{
    public override string Name => "inform";

    public override IEnumerable<BlockFact> Facts => [];

    public override IEnumerable<BlockTarget> Targets => KnowledgeEntry.TargetsAt("inform.knowledge", Arguments.Knowledge);

    public override BlockGap? Gap => null;
}
