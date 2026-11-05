using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Cli.Output;
using Suwagcms.Contracts;

namespace Suwagcms.Cli.KnowledgeBases;

internal static class KnowledgeBasesCommand
{
    private const string Collection = "knowledge-bases";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Command Create(GlobalOptions options, CliEnvironment environment) =>
        new(
            "kb",
            "List, show, add, edit and delete the knowledge bases of a bot: the groups its articles are in, each with the language of its articles."
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

    private static Command CreateList(GlobalOptions options, CliEnvironment environment)
    {
        var command = new Command("list", "List the knowledge bases of the bot (or --open the Knowledge bases screen in the web app).");
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
                var answer = await ServerCall.GetAsync(environment, target.Address, target.CollectionPath(Collection), cancellationToken);
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var bases = answer.Body?.Deserialize<List<KnowledgeBaseView>>(Web) ?? [];
                return await target.Run.Report.DoneAsync(new KnowledgeBaseListDocument(target.Bot, bases), KnowledgeBaseText.Table(bases));
            }
        );
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
                var shown =
                    answer.Body?.Deserialize<KnowledgeBaseView>(Web)
                    ?? throw new InvalidOperationException("the server sent no knowledge base");
                return await target.Run.Report.DoneAsync(shown, KnowledgeBaseText.Detail(shown));
            }
        );
        return command;
    }

    private static Command CreateAdd(GlobalOptions options, CliEnvironment environment)
    {
        var id = IdArgument();
        var title = TitleOption("The title people see; unique within the bot, ignoring case and surrounding spaces.");
        var language = LanguageOption("pl or en; the language of every article in the base. en when this is left out.");
        var dryRun = EditOptions.DryRun();
        var command = new Command("add", "Add a knowledge base to the bot.") { id, title, language, dryRun };
        EditOptions.RefuseOpen(command, options, "kb add");
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await BotTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                var dry = parsed.GetValue(dryRun);
                var answer = await ServerCall.PutAsync(
                    environment,
                    target.Address,
                    target.ItemPath(Collection, parsed.GetRequiredValue(id)) + EditOptions.Query(dry, 0),
                    new SaveKnowledgeBase(parsed.GetValue(title), parsed.GetValue(language)),
                    cancellationToken
                );
                return await ReportSavedAsync(target, answer, dry, "add", cancellationToken);
            }
        );
        return command;
    }

    private static Command CreateEdit(GlobalOptions options, CliEnvironment environment)
    {
        var id = IdArgument();
        var title = TitleOption("The new title.");
        var language = LanguageOption("pl or en. Every article in the base takes it.");
        var ifRevision = EditOptions.IfRevision();
        var dryRun = EditOptions.DryRun();
        var command = new Command(
            "edit",
            "Change the title or the language of a knowledge base: only the options you give change. Its id stays, so whatever points at it still does."
        )
        {
            id,
            title,
            language,
            ifRevision,
            dryRun,
        };
        EditOptions.RefuseOpen(command, options, "kb edit");
        command.Validators.Add(result =>
        {
            if (result.GetValue(title) is null && result.GetValue(language) is null)
            {
                result.AddError("Nothing to change: give at least one of --title, --language.");
            }
        });
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await BotTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                var path = target.ItemPath(Collection, parsed.GetRequiredValue(id));
                var read = await ServerCall.GetAsync(environment, target.Address, path, cancellationToken);
                if (read.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(read, cancellationToken);
                }
                var current =
                    read.Body?.Deserialize<KnowledgeBaseView>(Web)
                    ?? throw new InvalidOperationException("the server sent no knowledge base");
                var dry = parsed.GetValue(dryRun);
                // Why: without --if-revision the edit still guards the revision it just read, so an edit made in between is reported, never overwritten.
                var answer = await ServerCall.PutAsync(
                    environment,
                    target.Address,
                    path + EditOptions.Query(dry, parsed.GetValue(ifRevision) ?? current.Revision),
                    new SaveKnowledgeBase(parsed.GetValue(title) ?? current.Title, parsed.GetValue(language) ?? current.Language),
                    cancellationToken
                );
                return await ReportSavedAsync(target, answer, dry, "edit", cancellationToken);
            }
        );
        return command;
    }

    private static Command CreateDelete(GlobalOptions options, CliEnvironment environment)
    {
        var id = IdArgument();
        var ifRevision = EditOptions.IfRevision();
        var dryRun = EditOptions.DryRun();
        var command = new Command("delete", "Delete a knowledge base.") { id, ifRevision, dryRun };
        EditOptions.RefuseOpen(command, options, "kb delete");
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await BotTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                var baseId = parsed.GetRequiredValue(id);
                var dry = parsed.GetValue(dryRun);
                var answer = await ServerCall.DeleteAsync(
                    environment,
                    target.Address,
                    target.ItemPath(Collection, baseId) + EditOptions.Query(dry, parsed.GetValue(ifRevision)),
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                return await target.Run.Report.DoneAsync(
                    new KnowledgeBaseDeletedDocument(target.Bot, baseId, dry),
                    dry ? $"Would delete '{baseId}'. Nothing was changed." : $"Deleted '{baseId}'."
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
        var saved =
            answer.Body?.Deserialize<KnowledgeBaseView>(Web) ?? throw new InvalidOperationException("the server sent no knowledge base");
        var past = word == "add" ? "Added" : "Edited";
        var text = dry
            ? $"Would {word} '{saved.Id}' ({saved.Title}, {saved.Language}). Nothing was changed."
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{past} '{saved.Id}' ({saved.Title}, {saved.Language}), revision {saved.Revision}."
            );
        return await target.Run.Report.DoneAsync(new KnowledgeBaseSavedDocument(target.Bot, saved, dry), text);
    }

    private static Task<int> Open(BotTarget target, ParseResult parsed) =>
        WebPage.ShowAsync(
            target.PageOf(Collection),
            parsed.InvocationConfiguration.Output,
            parsed.InvocationConfiguration.Error,
            target.Machine.OpenBrowser
        );
}
