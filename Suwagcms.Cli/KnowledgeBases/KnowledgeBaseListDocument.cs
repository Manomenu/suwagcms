using Suwagcms.Contracts.KnowledgeBases;

namespace Suwagcms.Cli.KnowledgeBases;

internal sealed record KnowledgeBaseListDocument(string Bot, IReadOnlyList<KnowledgeBaseView> KnowledgeBases);
