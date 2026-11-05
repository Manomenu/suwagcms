using Suwagcms.Contracts;

namespace Suwagcms.Cli.Articles;

internal sealed record ArticleListDocument(string Bot, IReadOnlyList<ArticleView> Articles);
