using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.BotRuntime;

public sealed record BotRefusal
{
    public required string Title { get; init; }

    public required int Status { get; init; }

    public required string Detail { get; init; }

    public required IReadOnlyList<Mistake> Mistakes { get; init; }
}
