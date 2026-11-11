using Suwagcms.Sdk.Format.Facts;

namespace Suwagcms.Sdk.Format.Procedures.Blocks;

public sealed record SayBlock(string Text) : Block
{
    public override string Name => "say";

    public override IEnumerable<BlockFact> Facts => SayPlaceholders.Names(Text).Select(name => new BlockFact(name, "say", FactUse.Say));

    public override IEnumerable<BlockTarget> Targets => [];

    public override BlockGap? Gap =>
        string.IsNullOrWhiteSpace(Text) ? new BlockGap("say has no text.", "Write the text the bot says after 'say:'.") : null;
}
