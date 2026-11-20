using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts.Procedures;

public sealed record ProcedureList(IReadOnlyList<ProcedureSummary> Procedures, IReadOnlyList<Mistake> BotNotReady);
