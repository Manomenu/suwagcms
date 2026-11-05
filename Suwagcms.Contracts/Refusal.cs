using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts;

/// <summary>The Problem Details of a refused change: every mistake at once, each with its fix.</summary>
public sealed record Refusal(string Title, int Status, string Detail, IReadOnlyList<Mistake> Mistakes);
