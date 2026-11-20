namespace Suwagcms.Sdk.Format.Procedures.Blocks;

public abstract record Block
{
    public static IReadOnlyList<string> Names { get; } = ["listen", "collect", "say", "assign", "start", "inform", "action"];

    private protected Block() { }

    public abstract string Name { get; }

    public abstract IEnumerable<BlockFact> Facts { get; }

    public abstract IEnumerable<BlockTarget> Targets { get; }

    public abstract BlockGap? Gap { get; }
}
