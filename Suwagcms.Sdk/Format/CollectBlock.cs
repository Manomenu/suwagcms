namespace Suwagcms.Sdk.Format;

public sealed record CollectBlock(IReadOnlyList<string> FactNames) : Block
{
    public override string Name => "collect";

    public override IEnumerable<BlockFact> Facts =>
        FactNames.Select((name, index) => new BlockFact(name, $"collect[{index}]", FactUse.Collect));

    public override BlockGap? Gap =>
        FactNames.Count == 0 ? new BlockGap("collect lists no facts.", "List at least one fact: collect: [customer.city].") : null;
}
