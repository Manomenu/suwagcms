namespace Suwagcms.Sdk.Format;

// Why: the mapping lives in the step and never in the action, so one action serves many procedures and every mapping can be checked against the types on both sides.
public sealed record ActionBlock(
    string ActionId,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyDictionary<string, string> Results
) : Block
{
    public override string Name => "action";

    public override IEnumerable<BlockFact> Facts =>
        Parameters
            .Select(entry => new BlockFact(entry.Value, $"with.{entry.Key}", FactUse.Parameter))
            .Concat(Results.Select(entry => new BlockFact(entry.Value, $"results.{entry.Key}", FactUse.Result)));

    public override BlockGap? Gap =>
        string.IsNullOrWhiteSpace(ActionId)
            ? new BlockGap("action names no action.", "Write the id of the action to run: action: checkServiceability.")
            : null;
}
