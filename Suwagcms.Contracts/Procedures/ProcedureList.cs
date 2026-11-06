using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts.Procedures;

/// <summary>A bot's procedures, and the mistakes that stop a version but belong to no procedure, such as having no conversation.</summary>
public sealed record ProcedureList(IReadOnlyList<ProcedureSummary> Procedures, IReadOnlyList<Mistake> BotNotReady);
