using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts.Procedures;

/// <summary>A procedure as the API shows it: the format's fields, the revision to send back to guard an edit, and what still stops it from becoming a version.</summary>
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
