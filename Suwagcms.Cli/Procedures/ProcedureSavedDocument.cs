using Suwagcms.Contracts;

namespace Suwagcms.Cli.Procedures;

internal sealed record ProcedureSavedDocument(string Bot, ProcedureDetail Procedure, bool DryRun);
