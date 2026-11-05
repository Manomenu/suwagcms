namespace Suwagcms.Sdk.Format;

public sealed record CodeAction
{
    public string Id { get; init; } = "";

    public string Description { get; init; } = "";

    public IReadOnlyList<CodeActionField> Parameters { get; init; } = [];

    public IReadOnlyList<CodeActionField> Results { get; init; } = [];

    // Why: a declaration lists its fields and a definition names them by key, and the CMS keeps its copy in the definition's form, so a version stands on its own.
    public BotAction ToAction() =>
        new()
        {
            Id = Id,
            Description = Description,
            Parameters = Named(Parameters),
            Results = Named(Results),
            Implementation = new ActionImplementation { IsCode = true },
        };

    private static Dictionary<string, ActionField>? Named(IReadOnlyList<CodeActionField> fields) =>
        fields.Count == 0
            ? null
            : fields
                .DistinctBy(field => field.Name, StringComparer.Ordinal)
                .ToDictionary(
                    field => field.Name,
                    field => new ActionField
                    {
                        Type = field.Type,
                        Description = field.Description,
                        Values = field.Values,
                        Min = field.Min,
                        Max = field.Max,
                        MaxLength = field.MaxLength,
                    },
                    StringComparer.Ordinal
                );
}
