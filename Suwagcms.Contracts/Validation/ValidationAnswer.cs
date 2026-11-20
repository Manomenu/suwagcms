using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts.Validation;

public sealed record ValidationAnswer(IReadOnlyList<Mistake> Mistakes);
