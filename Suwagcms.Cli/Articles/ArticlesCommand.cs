using System.CommandLine;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Cli.Output;
using Suwagcms.Contracts;

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
        var text = new Option<string?>("--text")
        {
            Description = "Only the articles with this text in the title, id or description, ignoring case. The content is not read.",
        };
        var command = new Command("list", "List the articles of the bot, filtered (or --open the Articles screen in the web app).")
        {
            knowledgeBase,
            text,
        };
        EditOptions.RefuseOpenWithJson(command, options);
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await BotTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                if (parsed.GetValue(options.Open))
                {
                    return await Open(target, parsed);
                }
                var answer = await ServerCall.GetAsync(
                    environment,
                    target.Address,
                    target.CollectionPath(Collection) + Query(parsed.GetValue(knowledgeBase), parsed.GetValue(text)),
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

    private static string Query(string? knowledgeBase, string? text)
    {
        var parts = new List<string>();
        if (knowledgeBase is not null)
        {
            parts.Add($"knowledgeBase={Uri.EscapeDataString(knowledgeBase)}");
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
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await BotTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                if (parsed.GetValue(options.Open))
                {
                    return await Open(target, parsed);
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

    private static Command CreateDelete(GlobalOptions options, CliEnvironment environment)
    {
        var id = new Argument<string>("id") { Description = "The article's id, like priceList." };
        var ifRevision = EditOptions.IfRevision();
        var dryRun = EditOptions.DryRun();
        var command = new Command("delete", "Delete an article. Nothing checks yet whether another article or a procedure refers to it.")
        {
            id,
            ifRevision,
            dryRun,
        };
        EditOptions.RefuseOpen(command, options, "articles delete");
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await BotTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                var articleId = parsed.GetRequiredValue(id);
                var dry = parsed.GetValue(dryRun);
                var answer = await ServerCall.DeleteAsync(
                    environment,
                    target.Address,
                    target.ItemPath(Collection, articleId) + EditOptions.Query(dry, parsed.GetValue(ifRevision)),
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                return await target.Run.Report.DoneAsync(
                    new ArticleDeletedDocument(target.Bot, articleId, dry),
                    dry ? $"Would delete '{articleId}'. Nothing was changed." : $"Deleted '{articleId}'."
                );
            }
        );
        return command;
    }

    private static Task<int> Open(BotTarget target, ParseResult parsed) =>
        WebPage.ShowAsync(
            target.PageOf(Collection),
            parsed.InvocationConfiguration.Output,
            parsed.InvocationConfiguration.Error,
            target.Machine.OpenBrowser
        );
}
