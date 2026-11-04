using Suwagcms.Contracts;

namespace Suwagcms.Cli.KnowledgeBases;

internal sealed record KnowledgeBaseSavedDocument(string Bot, KnowledgeBaseView KnowledgeBase, bool DryRun);
