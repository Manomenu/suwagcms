namespace Suwagcms.Sdk.BotRuntime.Versions;

public sealed record CurrentChoice
{
    public required int Number { get; init; }

    public required string Digest { get; init; }
}
