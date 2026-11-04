namespace Suwagcms.Sdk.Format;

public sealed record ListenBlock : Block
{
    public override string Name => "listen";

    public override IEnumerable<BlockFact> Facts => [];

    public override BlockGap? Gap => null;
}
