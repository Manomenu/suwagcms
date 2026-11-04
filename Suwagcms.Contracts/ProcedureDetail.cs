using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts;

/// <summary>A procedure as the API shows it: the format's fields, the revision to send back to guard an edit, and what still stops it from becoming a version.</summary>
public sealed record ProcedureDetail(
    string Id,
    string Description,
    string? FirstStep,
    IReadOnlyDictionary<string, ProcedureStep> Steps,
    StallInstruction? WhenStalled,
    int Revision,
    IReadOnlyList<Mistake> NotReady
);
