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
        new("kb", "List, show, add, rename and delete the knowledge bases of a bot: the groups its articles are in.")
        {
            CreateList(options, environment),
            CreateShow(options, environment),
            CreateAdd(options, environment),
            CreateRename(options, environment),
            CreateDelete(options, environment),
        };

    private static Argument<string> IdArgument() =>
        new("id") { Description = "The knowledge base's id: camelCase, like offer. It does not change." };

    private static Option<string?> TitleOption(string description) => new("--title") { Description = description };

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
        var dryRun = EditOptions.DryRun();
        var command = new Command("add", "Add a knowledge base to the bot.") { id, title, dryRun };
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
                    new SaveKnowledgeBase(parsed.GetValue(title)),
                    cancellationToken
                );
                return await ReportSavedAsync(target, answer, dry, "add", cancellationToken);
            }
        );
        return command;
    }

    private static Command CreateRename(GlobalOptions options, CliEnvironment environment)
    {
        var id = IdArgument();
        var title = new Option<string>("--title") { Description = "The new title.", Required = true };
        var ifRevision = EditOptions.IfRevision();
        var dryRun = EditOptions.DryRun();
        var command = new Command("rename", "Change the title of a knowledge base. Its id stays, so whatever points at it still does.")
        {
            id,
            title,
            ifRevision,
            dryRun,
        };
        EditOptions.RefuseOpen(command, options, "kb rename");
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
                // Why: without --if-revision the rename still guards the revision it just read, so a rename made in between is reported, never overwritten.
                var answer = await ServerCall.PutAsync(
                    environment,
                    target.Address,
                    path + EditOptions.Query(dry, parsed.GetValue(ifRevision) ?? current.Revision),
                    new SaveKnowledgeBase(parsed.GetRequiredValue(title)),
                    cancellationToken
                );
                return await ReportSavedAsync(target, answer, dry, "rename", cancellationToken);
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
        var past = word == "add" ? "Added" : "Renamed";
        var text = dry
            ? $"Would {word} '{saved.Id}' ({saved.Title}). Nothing was changed."
            : string.Create(CultureInfo.InvariantCulture, $"{past} '{saved.Id}' ({saved.Title}), revision {saved.Revision}.");
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
