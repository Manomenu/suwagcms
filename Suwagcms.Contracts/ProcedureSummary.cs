namespace Suwagcms.Contracts;

/// <summary>One line of a bot's procedures: how many mistakes still stop this procedure from becoming a version.</summary>
public sealed record ProcedureSummary(string Id, string Description, int Revision, int NotReady);
