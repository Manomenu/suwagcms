namespace Suwagcms.Sdk.Format;

public sealed record InformBlock(InformArguments Arguments) : Block
{
    public override string Name => "inform";

    public override IEnumerable<BlockFact> Facts =>
        (Arguments.Facts ?? []).Select((name, index) => new BlockFact(name, $"inform.facts[{index}]", FactUse.Inform));

    public override IEnumerable<BlockTarget> Targets =>
        (Arguments.Articles ?? []).Select(
            (id, index) => new BlockTarget(new ResourceRef(ResourceKind.Article, id), $"inform.articles[{index}]")
        );

    // Why: an inform can miss two things at once, its content and its instruction, so its gaps are version rules with codes of their own.
    public override BlockGap? Gap => null;
}
