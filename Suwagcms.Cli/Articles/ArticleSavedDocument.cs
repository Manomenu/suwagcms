using Suwagcms.Contracts.Articles;

namespace Suwagcms.Cli.Articles;

internal sealed record ArticleSavedDocument(string Bot, ArticleView Article, bool DryRun);
