namespace Suwagcms.Sdk.BotRuntime;

public sealed record CurrentChoice
{
    public required int Number { get; init; }

    public required string Digest { get; init; }
}
