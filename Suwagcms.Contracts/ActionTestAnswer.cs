namespace Suwagcms.Contracts;

/// <summary>
/// What a test run came to. <c>Outcome</c> is <c>success</c> or <c>failure</c> when the bot ran the action, <c>unreachable</c> when the bot could not be reached or gave no usable answer, and <c>notOffered</c> when the bot has no such route; in the last two the action has not been judged.
/// <c>Status</c>, <c>ElapsedMs</c> and <c>AnswerStart</c> are what is known of the call; <c>AnswerCut</c> says the answer was longer than what is shown.
/// </summary>
public sealed record ActionTestAnswer(
    string Outcome,
    IReadOnlyDictionary<string, string> Results,
    string? Reason,
    int? Status,
    long ElapsedMs,
    string? AnswerStart,
    bool AnswerCut
);
