using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts.Procedures;

public sealed record ProcedureDetail(
    string Id,
    string Description,
    string? FirstStep,
    IReadOnlyDictionary<string, ProcedureStep> Steps,
    StallInstruction? WhenStalled,
    int Revision,
    IReadOnlyList<Mistake> NotReady,
    IReadOnlyDictionary<string, LocalFact>? LocalFacts = null,
    string? WhenInterrupted = null
);
