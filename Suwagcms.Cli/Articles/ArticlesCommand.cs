using System.CommandLine;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Contracts.Articles;

namespace Suwagcms.Cli.Articles;

internal static class ArticlesCommand
{
    public const string Collection = "articles";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Command Create(GlobalOptions options, CliEnvironment environment) =>
        new("articles", "List, show, add, edit and delete the articles of a bot.")
        {
            CreateList(options, environment),
            CreateShow(options, environment),
            ArticleSaveCommands.CreateAdd(options, environment),
            ArticleSaveCommands.CreateEdit(options, environment),
            CreateDelete(options, environment),
        };

    private static Command CreateList(GlobalOptions options, CliEnvironment environment)
    {
        var knowledgeBase = new Option<string?>("--kb") { Description = "Only the articles of this knowledge base." };
        var language = new Option<string?>("--language")
        {
            Description = "Only the articles in this language, which is their knowledge base's: pl or en.",
        };
        var text = new Option<string?>("--text")
        {
            Description = "Only the articles with this text in the title, id or description, ignoring case. The content is not read.",
        };
        var command = new Command(
            "list",
            "List the articles of the bot, filtered (or --open the Articles screen in the web app, filtered the same)."
        )
        {
            knowledgeBase,
            language,
            text,
        };
        EditOptions.RefuseOpenWithJson(command, options);
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                // Why: the screen reads its filters from the same words the route takes.
                var filter = Query(parsed.GetValue(knowledgeBase), parsed.GetValue(language), parsed.GetValue(text));
                if (parsed.GetValue(options.Open))
                {
                    return await target.OpenAsync(new Uri(target.PageOf(Collection), filter), parsed);
                }
                var answer = await ServerCall.GetAsync(
                    environment,
                    target.Address,
                    target.CollectionPath(Collection) + filter,
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var articles = answer.Body?.Deserialize<List<ArticleView>>(Web) ?? [];
                return await target.Run.Report.DoneAsync(new ArticleListDocument(target.Bot, articles), ArticleText.Table(articles));
            }
        );
        return command;
    }

    private static string Query(string? knowledgeBase, string? language, string? text)
    {
        var parts = new List<string>();
        if (knowledgeBase is not null)
        {
            parts.Add($"knowledgeBase={Uri.EscapeDataString(knowledgeBase)}");
        }
        if (language is not null)
        {
            parts.Add($"language={Uri.EscapeDataString(language)}");
        }
        if (text is not null)
        {
            parts.Add($"text={Uri.EscapeDataString(text)}");
        }
        return parts.Count == 0 ? "" : "?" + string.Join('&', parts);
    }

    private static Command CreateShow(GlobalOptions options, CliEnvironment environment)
    {
        var id = new Argument<string>("id") { Description = "The article's id, like priceList." };
        var command = new Command("show", "Show one article in full with its revision (or --open the Articles screen in the web app).")
        {
            id,
        };
        EditOptions.RefuseOpenWithJson(command, options);
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                if (parsed.GetValue(options.Open))
                {
                    return await target.OpenAsync(target.PageAt("Article", parsed.GetRequiredValue(id)), parsed);
                }
                var answer = await ServerCall.GetAsync(
                    environment,
                    target.Address,
                    target.ItemPath(Collection, parsed.GetRequiredValue(id)),
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var article =
                    answer.Body?.Deserialize<ArticleView>(Web) ?? throw new InvalidOperationException("the server sent no article");
                return await target.Run.Report.DoneAsync(article, ArticleText.Detail(article));
            }
        );
        return command;
    }

    private static Command CreateDelete(GlobalOptions options, CliEnvironment environment) =>
        ResourceCommand.Delete(
            options,
            environment,
            "articles delete",
            Collection,
            new Argument<string>("id") { Description = "The article's id, like priceList." },
            "Delete an article. Refused while another article links to it, with every referrer listed; `usage article <id>` shows them first.",
            (bot, name, dry) => new ArticleDeletedDocument(bot, name, dry)
        );
}
