namespace Suwagcms.Sdk.Format.Procedures.Blocks;

public sealed record InformBlock(InformArguments Arguments) : Block
{
    public override string Name => "inform";

    public override IEnumerable<BlockFact> Facts => [];

    public override IEnumerable<BlockTarget> Targets => KnowledgeEntry.TargetsAt("inform.knowledge", Arguments.Knowledge);

    // Why: an inform can miss two things at once, its points and its instruction, so its gaps are version rules with codes of their own.
    public override BlockGap? Gap => null;
}
