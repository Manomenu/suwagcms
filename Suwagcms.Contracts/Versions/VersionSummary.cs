namespace Suwagcms.Contracts.Versions;

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
