namespace Suwagcms.Sdk.BotRuntime.Actions;

// Why: a failure has a reason and no results, so a half-understood answer is never used; Status, ElapsedMs and AnswerStart are what is known about the call, to see what to fix.
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
