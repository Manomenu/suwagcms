using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts.Validation;

/// <summary>What the validator says about a definition: every mistake, each with its fix; none means it passes.</summary>
public sealed record ValidationAnswer(IReadOnlyList<Mistake> Mistakes);
