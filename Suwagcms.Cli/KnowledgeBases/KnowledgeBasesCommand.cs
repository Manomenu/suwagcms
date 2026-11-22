using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Contracts;
using Suwagcms.Contracts.KnowledgeBases;
using Suwagcms.Sdk.Format.KnowledgeBases;

namespace Suwagcms.Cli.KnowledgeBases;

internal static class KnowledgeBasesCommand
{
    private const string Collection = "knowledge-bases";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Command Create(GlobalOptions options, CliEnvironment environment) =>
        new(
            "kb",
            "List, show, add, edit and delete the knowledge bases of a bot: the groups its articles are in, each with the language of its articles, the embedding profile the bot searches them with and how many results one search gives."
        )
        {
            CreateList(options, environment),
            CreateShow(options, environment),
            CreateAdd(options, environment),
            CreateEdit(options, environment),
            CreateDelete(options, environment),
        };

    private static Argument<string> IdArgument() =>
        new("id") { Description = "The knowledge base's id: camelCase, like offer. It does not change." };

    private static Option<string?> TitleOption(string description) => new("--title") { Description = description };

    private static Option<string?> LanguageOption(string description) => new("--language") { Description = description };

    private static Option<string?> EmbeddingOption(string description) => new("--embedding") { Description = description };

    private static Option<int?> ResultsOption(string description) => new("--results") { Description = description };

    private static Command CreateList(GlobalOptions options, CliEnvironment environment)
    {
        var command = new Command("list", "List the knowledge bases of the bot (or --open the Knowledge bases screen in the web app).");
        EditOptions.RefuseOpenWithJson(command, options);
        command.SetBotAction(options, environment, async (target, parsed, cancellationToken) =>
        {
            if (parsed.GetValue(options.Open))
            {
                return await target.OpenAsync(target.PageOf(Collection), parsed);
            }
            var answer = await ServerCall.GetAsync(environment, target.Address, target.CollectionPath(Collection), cancellationToken);
            if (answer.ExitCode != ExitCodes.Done)
            {
                return await target.FailedAsync(answer, cancellationToken);
            }
            var bases = answer.Body?.Deserialize<List<KnowledgeBaseView>>(Web) ?? [];
            return await target.Run.Report.DoneAsync(new KnowledgeBaseListDocument(target.Bot, bases), KnowledgeBaseText.Table(bases));
        });
        return command;
    }

    private static Command CreateShow(GlobalOptions options, CliEnvironment environment)
    {
        var id = IdArgument();
        var command = new Command(
            "show",
            "Show one knowledge base with its revision (or --open the Knowledge bases screen in the web app)."
        )
        {
            id,
        };
        EditOptions.RefuseOpenWithJson(command, options);
        command.SetBotAction(options, environment, async (target, parsed, cancellationToken) =>
        {
            if (parsed.GetValue(options.Open))
            {
                return await target.OpenAsync(target.PageAt("KnowledgeBase", parsed.GetRequiredValue(id)), parsed);
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
            var shown = answer.Body?.Deserialize<KnowledgeBaseView>(Web)
                ?? throw new InvalidOperationException("the server sent no knowledge base");
            return await target.Run.Report.DoneAsync(shown, KnowledgeBaseText.Detail(shown));
        });
        return command;
    }

    private static Command CreateAdd(GlobalOptions options, CliEnvironment environment)
    {
        var id = IdArgument();
        var title = TitleOption("The title people see; unique within the bot, ignoring case and surrounding spaces.");
        var language = LanguageOption("pl or en; the language of every article in the base. en when this is left out.");
        var embedding = EmbeddingOption(
            $"The name of the embedding profile the bot embeds the base's articles with; the bot's environment holds its model. {KnowledgeBase.DefaultEmbedding} when this is left out."
        );
        var results = ResultsOption(
            string.Create(
                CultureInfo.InvariantCulture,
                $"How many results one search gives from the base, {KnowledgeBase.MinResults} to {KnowledgeBase.MaxResults}. {KnowledgeBase.DefaultResults} when this is left out."
            )
        );
        var dryRun = EditOptions.DryRun();
        var command = new Command("add", "Add a knowledge base to the bot.") { id, title, language, embedding, results, dryRun };
        EditOptions.OpenWhatItSaves(command, options, dryRun);
        command.SetBotAction(options, environment, async (target, parsed, cancellationToken) =>
        {
            var dry = parsed.GetValue(dryRun);
            var answer = await ServerCall.PutAsync(
                environment,
                target.Address,
                target.ItemPath(Collection, parsed.GetRequiredValue(id)) + EditOptions.Query(dry, 0),
                new SaveKnowledgeBase(
                    parsed.GetValue(title),
                    parsed.GetValue(language),
                    parsed.GetValue(embedding) ?? KnowledgeBase.DefaultEmbedding,
                    parsed.GetValue(results) ?? KnowledgeBase.DefaultResults
                ),
                cancellationToken
            );
            return await target.OpenSavedAsync(
                await ReportSavedAsync(target, answer, dry, "add", cancellationToken),
                parsed,
                options,
                "KnowledgeBase",
                parsed.GetRequiredValue(id)
            );
        });
        return command;
    }

    private static Command CreateEdit(GlobalOptions options, CliEnvironment environment)
    {
        var id = IdArgument();
        var title = TitleOption("The new title.");
        var language = LanguageOption("pl or en. Every article in the base takes it.");
        var embedding = EmbeddingOption("The name of the embedding profile the bot embeds the base's articles with.");
        var results = ResultsOption(
            string.Create(
                CultureInfo.InvariantCulture,
                $"How many results one search gives from the base, {KnowledgeBase.MinResults} to {KnowledgeBase.MaxResults}."
            )
        );
        var ifRevision = EditOptions.IfRevision();
        var dryRun = EditOptions.DryRun();
        var command = new Command(
            "edit",
            "Change the title, the language, the embedding profile or the results per search of a knowledge base: only the options you give change. Its id stays, so whatever points at it still does."
        )
        {
            id,
            title,
            language,
            embedding,
            results,
            ifRevision,
            dryRun,
        };
        EditOptions.OpenWhatItSaves(command, options, dryRun);
        command.Validators.Add(result =>
        {
            if (
                result.GetValue(title) is null
                && result.GetValue(language) is null
                && result.GetValue(embedding) is null
                && result.GetValue(results) is null
            )
            {
                result.AddError("Nothing to change: give at least one of --title, --language, --embedding, --results.");
            }
        });
        command.SetBotAction(options, environment, async (target, parsed, cancellationToken) =>
        {
            var path = target.ItemPath(Collection, parsed.GetRequiredValue(id));
            var read = await ServerCall.GetAsync(environment, target.Address, path, cancellationToken);
            if (read.ExitCode != ExitCodes.Done)
            {
                return await target.FailedAsync(read, cancellationToken);
            }
            var current = read.Body?.Deserialize<KnowledgeBaseView>(Web)
                ?? throw new InvalidOperationException("the server sent no knowledge base");
            var dry = parsed.GetValue(dryRun);
            var answer = await ServerCall.PutAsync(
                environment,
                target.Address,
                path + EditOptions.Query(dry, parsed.GetValue(ifRevision) ?? current.Revision),
                EditOverlay.Over(
                    current,
                    new SaveKnowledgeBase(
                        parsed.GetValue(title),
                        parsed.GetValue(language),
                        parsed.GetValue(embedding),
                        parsed.GetValue(results)
                    )
                ),
                cancellationToken
            );
            return await target.OpenSavedAsync(
                await ReportSavedAsync(target, answer, dry, "edit", cancellationToken),
                parsed,
                options,
                "KnowledgeBase",
                parsed.GetRequiredValue(id)
            );
        });
        return command;
    }

    private static Command CreateDelete(GlobalOptions options, CliEnvironment environment) =>
        ResourceCommand.Delete(options, environment, "kb delete", Collection, IdArgument(), "Delete a knowledge base.", (bot, name, dry) =>
            new KnowledgeBaseDeletedDocument(bot, name, dry)
        );

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
        var saved = answer.Body?.Deserialize<KnowledgeBaseView>(Web)
            ?? throw new InvalidOperationException("the server sent no knowledge base");
        var past = word == "add" ? "Added" : "Saved";
        var text = dry
            ? $"Would {word} '{saved.Id}' ({KnowledgeBaseText.Summary(saved)}). Nothing was changed."
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{past} '{saved.Id}' ({KnowledgeBaseText.Summary(saved)}), revision {saved.Revision}."
            );
        return await target.Run.Report.DoneAsync(new KnowledgeBaseSavedDocument(target.Bot, saved, dry), text);
    }
}
