namespace Suwagcms.Sdk.Format.Procedures.Blocks;

public sealed record ListenBlock : Block
{
    public override string Name => "listen";

    public override IEnumerable<BlockFact> Facts => [];

    public override IEnumerable<BlockTarget> Targets => [];

    public override BlockGap? Gap => null;
}
