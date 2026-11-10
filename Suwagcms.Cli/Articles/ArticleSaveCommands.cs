using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Cli.Output;
using Suwagcms.Contracts;
using Suwagcms.Contracts.Articles;

namespace Suwagcms.Cli.Articles;

internal static class ArticleSaveCommands
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private sealed record ArticleFields(
        Option<string?> KnowledgeBase,
        Option<string?> Title,
        TextInput Content,
        TextInput Guide,
        Option<string?> Description
    );

    // Why: the options are left optional so that a missing one reaches the server, which reports every mistake at once.
    private static ArticleFields FieldOptions() =>
        new(
            new Option<string?>("--kb")
            {
                Description =
                    "The id of the knowledge base the article is in; changing it moves the article, and the article takes the language of the new base.",
            },
            new Option<string?>("--title") { Description = "The article's title." },
            TextInput.Create(
                "content",
                "--content",
                "--content-file",
                "content: what is true, in Markdown; the customer reads it, through the model"
            ),
            TextInput.Create(
                "llmGuide",
                "--guide",
                "--guide-file",
                "guide: how the model should talk about it, in Markdown; only the model reads it"
            ),
            new Option<string?>("--description")
            {
                Description = "What the article answers, as plain text; only the bot's search reads it.",
            }
        );

    private static void AddFields(Command command, ArticleFields fields)
    {
        command.Options.Add(fields.KnowledgeBase);
        command.Options.Add(fields.Title);
        fields.Content.AddTo(command);
        fields.Guide.AddTo(command);
        command.Options.Add(fields.Description);
    }

    private static void RefuseBadTexts(Command command, ArticleFields fields) =>
        command.Validators.Add(result =>
        {
            foreach (
                var problem in new[]
                {
                    fields.Content.Problem(result),
                    fields.Guide.Problem(result),
                    TextInput.StdinProblem(result, fields.Content, fields.Guide),
                }
            )
            {
                if (problem is not null)
                {
                    result.AddError(problem);
                }
            }
        });

    private static Argument<string> IdArgument() =>
        new("id")
        {
            Description = "The article's id: camelCase, like priceList. It does not change, and is unique across the bot's bases.",
        };

    public static Command CreateAdd(GlobalOptions options, CliEnvironment environment)
    {
        var id = IdArgument();
        var fields = FieldOptions();
        var dryRun = EditOptions.DryRun();
        var command = new Command(
            "add",
            "Add an article to a knowledge base of the bot. Content and guide may come from files, or from stdin with -."
        )
        {
            id,
        };
        AddFields(command, fields);
        command.Options.Add(dryRun);
        EditOptions.OpenWhatItSaves(command, options, dryRun);
        RefuseBadTexts(command, fields);
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                var (content, contentProblem) = await fields.Content.ReadAsync(parsed, environment, cancellationToken);
                var (guide, guideProblem) = await fields.Guide.ReadAsync(parsed, environment, cancellationToken);
                var problem = contentProblem ?? guideProblem;
                if (problem is not null)
                {
                    return await target.Run.Report.FailedAsync(ExitCodes.Misused, new FailureDocument(problem), problem);
                }
                var body = new SaveArticle(
                    parsed.GetValue(fields.KnowledgeBase),
                    parsed.GetValue(fields.Title),
                    content,
                    guide,
                    parsed.GetValue(fields.Description)
                );
                var dry = parsed.GetValue(dryRun);
                var answer = await ServerCall.PutAsync(
                    environment,
                    target.Address,
                    target.ItemPath(ArticlesCommand.Collection, parsed.GetRequiredValue(id)) + EditOptions.Query(dry, 0),
                    body,
                    cancellationToken
                );
                return await target.OpenSavedAsync(
                    await ReportSavedAsync(target, answer, dry, "add", cancellationToken),
                    parsed,
                    options,
                    "Article",
                    parsed.GetRequiredValue(id)
                );
            }
        );
        return command;
    }

    public static Command CreateEdit(GlobalOptions options, CliEnvironment environment)
    {
        var id = IdArgument();
        var fields = FieldOptions();
        var ifRevision = EditOptions.IfRevision();
        var dryRun = EditOptions.DryRun();
        var command = new Command(
            "edit",
            "Change an article: only the options you give change, the other fields stay. Its language is its knowledge base's."
        )
        {
            id,
        };
        AddFields(command, fields);
        command.Options.Add(ifRevision);
        command.Options.Add(dryRun);
        EditOptions.OpenWhatItSaves(command, options, dryRun);
        RefuseBadTexts(command, fields);
        command.Validators.Add(result =>
        {
            if (
                result.GetValue(fields.KnowledgeBase) is null
                && result.GetValue(fields.Title) is null
                && result.GetValue(fields.Description) is null
                && !fields.Content.Given(result)
                && !fields.Guide.Given(result)
            )
            {
                result.AddError(
                    "Nothing to change: give at least one of --kb, --title, --content, --content-file, --guide, --guide-file, --description."
                );
            }
        });
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                var (content, contentProblem) = await fields.Content.ReadAsync(parsed, environment, cancellationToken);
                var (guide, guideProblem) = await fields.Guide.ReadAsync(parsed, environment, cancellationToken);
                var problem = contentProblem ?? guideProblem;
                if (problem is not null)
                {
                    return await target.Run.Report.FailedAsync(ExitCodes.Misused, new FailureDocument(problem), problem);
                }
                var path = target.ItemPath(ArticlesCommand.Collection, parsed.GetRequiredValue(id));
                var read = await ServerCall.GetAsync(environment, target.Address, path, cancellationToken);
                if (read.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(read, cancellationToken);
                }
                var current = read.Body?.Deserialize<ArticleView>(Web) ?? throw new InvalidOperationException("the server sent no article");
                var body = EditOverlay.Over(
                    current,
                    new SaveArticle(
                        parsed.GetValue(fields.KnowledgeBase),
                        parsed.GetValue(fields.Title),
                        content,
                        guide,
                        parsed.GetValue(fields.Description)
                    )
                );
                var dry = parsed.GetValue(dryRun);
                // Why: without --if-revision the edit still guards the fields it just read, so a change made in between is reported, never overwritten.
                var answer = await ServerCall.PutAsync(
                    environment,
                    target.Address,
                    path + EditOptions.Query(dry, parsed.GetValue(ifRevision) ?? current.Revision),
                    body,
                    cancellationToken
                );
                return await target.OpenSavedAsync(
                    await ReportSavedAsync(target, answer, dry, "edit", cancellationToken),
                    parsed,
                    options,
                    "Article",
                    parsed.GetRequiredValue(id)
                );
            }
        );
        return command;
    }

    private static async Task<int> ReportSavedAsync(
        BotTarget target,
        ServerAnswer answer,
        bool dry,
        string word,
        CancellationToken cancellationToken
    )
    {
        if (answer.ExitCode != ExitCodes.Done)
        {
            return await target.FailedAsync(answer, cancellationToken);
        }
        var article = answer.Body?.Deserialize<ArticleView>(Web) ?? throw new InvalidOperationException("the server sent no article");
        var past = word == "add" ? "Added" : "Saved";
        var text = dry
            ? $"Would {word} '{article.Id}' ({article.Title}, {article.Language}, in {article.KnowledgeBase}). Nothing was changed."
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{past} '{article.Id}' ({article.Title}, {article.Language}, in {article.KnowledgeBase}), revision {article.Revision}."
            );
        return await target.Run.Report.DoneAsync(new ArticleSavedDocument(target.Bot, article, dry), text);
    }
}
