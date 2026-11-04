namespace Suwagcms.Sdk.Format;

public sealed record Transition
{
    public IReadOnlyDictionary<string, Condition>? When { get; init; }

    public string To { get; init; } = "";
}
