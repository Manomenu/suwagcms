using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.Procedures;

namespace Suwagcms.Contracts.Procedures;

/// <summary>What saving a procedure takes: every field of it but the id, which the address gives. A field left out is checked by the server, which reports every mistake at once.</summary>
public sealed record SaveProcedure(
    string? Description,
    string? FirstStep,
    IReadOnlyDictionary<string, ProcedureStep>? Steps,
    StallInstruction? WhenStalled,
    IReadOnlyDictionary<string, LocalFact>? LocalFacts = null,
    string? WhenInterrupted = null
);
