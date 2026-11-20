namespace Suwagcms.Sdk.BotRuntime.Actions;

public sealed record ActionOutcome
{
    public const string Success = "success";
    public const string Failure = "failure";

    public required string Outcome { get; init; }

    public IReadOnlyDictionary<string, string> Results { get; init; } = new Dictionary<string, string>();

    public string? Reason { get; init; }

    public int? Status { get; init; }

    public long ElapsedMs { get; init; }

    public string? AnswerStart { get; init; }

    public bool AnswerCut { get; init; }

    public bool Succeeded => Outcome == Success;
}
