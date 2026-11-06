namespace Suwagcms.Sdk.Format.Procedures;

public sealed record Transition
{
    public IReadOnlyDictionary<string, Condition>? When { get; init; }

    public string To { get; init; } = "";
}
