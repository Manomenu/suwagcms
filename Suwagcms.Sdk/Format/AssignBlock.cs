namespace Suwagcms.Sdk.Format;

public sealed record AssignBlock(IReadOnlyDictionary<string, string> Values) : Block
{
    public override string Name => "assign";

    public override IEnumerable<BlockFact> Facts => Values.Keys.Select(name => new BlockFact(name, $"assign.{name}", FactUse.Assign));

    public override BlockGap? Gap =>
        Values.Count == 0
            ? new BlockGap("assign gives no fact a value.", "Write the facts and their values: assign: { customer.city: Warsaw }.")
            : null;
}
