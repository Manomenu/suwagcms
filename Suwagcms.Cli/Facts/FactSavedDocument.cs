using Suwagcms.Contracts;

namespace Suwagcms.Cli.Facts;

internal sealed record FactSavedDocument(string Bot, Fact Fact, bool DryRun);
