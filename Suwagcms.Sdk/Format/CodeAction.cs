namespace Suwagcms.Sdk.Format;

public sealed record CodeAction
{
    public string Id { get; init; } = "";

    public string Description { get; init; } = "";

    public IReadOnlyList<CodeActionField> Parameters { get; init; } = [];

    public IReadOnlyList<CodeActionField> Results { get; init; } = [];
}
