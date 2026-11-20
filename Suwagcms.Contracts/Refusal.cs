using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts;

public sealed record Refusal(string Title, int Status, string Detail, IReadOnlyList<Mistake> Mistakes);
