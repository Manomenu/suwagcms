namespace Suwagcms.Contracts.Actions;

public sealed record ActionTestAnswer(
    string Outcome,
    IReadOnlyDictionary<string, string> Results,
    string? Reason,
    int? Status,
    long ElapsedMs,
    string? AnswerStart,
    bool AnswerCut,
    string? Note = null
);
