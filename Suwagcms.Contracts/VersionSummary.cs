namespace Suwagcms.Contracts;

/// <summary>A version of a bot in the list: its number, when it was created, how many resources of each kind it holds, and the sha256 of its document.</summary>
public sealed record VersionSummary(
    int Number,
    DateTimeOffset CreatedAt,
    int Facts,
    int KnowledgeBases,
    int Articles,
    int Procedures,
    int Actions,
    string Sha256
);
