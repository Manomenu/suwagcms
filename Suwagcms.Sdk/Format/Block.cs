namespace Suwagcms.Sdk.Format;

// Why: the blocks are a closed set (only this assembly can add one), and what each means for references and for a finished procedure is an abstract member, so a new block cannot compile unhandled.
public abstract record Block
{
    public static IReadOnlyList<string> Names { get; } = ["listen", "collect", "say", "assign", "start", "inform", "action"];

    private protected Block() { }

    public abstract string Name { get; }

    public abstract IEnumerable<BlockFact> Facts { get; }

    public abstract IEnumerable<BlockTarget> Targets { get; }

    public abstract BlockGap? Gap { get; }
}
