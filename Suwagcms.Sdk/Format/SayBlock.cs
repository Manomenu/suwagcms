namespace Suwagcms.Sdk.Format;

public sealed record SayBlock(string Text) : Block
{
    public override string Name => "say";

    public override IEnumerable<BlockFact> Facts => [];

    public override IEnumerable<BlockTarget> Targets => [];

    public override BlockGap? Gap =>
        string.IsNullOrWhiteSpace(Text) ? new BlockGap("say has no text.", "Write the text the bot says after 'say:'.") : null;
}
