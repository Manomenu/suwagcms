namespace Suwagcms.Sdk.Format;

public sealed record InformArguments
{
    public string Instruction { get; init; } = "";

    public IReadOnlyList<string>? Articles { get; init; }

    public IReadOnlyList<string>? Facts { get; init; }
}
