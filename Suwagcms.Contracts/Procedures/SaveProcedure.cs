using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.Procedures;

namespace Suwagcms.Contracts.Procedures;

public sealed record SaveProcedure(
    string? Description,
    string? FirstStep,
    IReadOnlyDictionary<string, ProcedureStep>? Steps,
    StallInstruction? WhenStalled,
    IReadOnlyDictionary<string, LocalFact>? LocalFacts = null,
    string? WhenInterrupted = null
);
