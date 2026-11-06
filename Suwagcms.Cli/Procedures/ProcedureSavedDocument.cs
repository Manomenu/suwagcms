using Suwagcms.Contracts.Procedures;

namespace Suwagcms.Cli.Procedures;

internal sealed record ProcedureSavedDocument(string Bot, ProcedureDetail Procedure, bool DryRun);
