using System.CommandLine;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Cli.Output;
using Suwagcms.Contracts;

namespace Suwagcms.Cli.Facts;

internal static class FactsCommand
{
    public const string Collection = "facts";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Command Create(GlobalOptions options, CliEnvironment environment) =>
        new("facts", "List, show, add, edit and delete the global facts of a bot.")
        {
            CreateList(options, environment),
            CreateShow(options, environment),
            FactSaveCommands.CreateAdd(options, environment),
            FactSaveCommands.CreateEdit(options, environment),
            CreateDelete(options, environment),
        };

    private static Command CreateList(GlobalOptions options, CliEnvironment environment)
    {
        var command = new Command("list", "List the global facts of the bot (or --open the Facts screen in the web app).");
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
                var facts = answer.Body?.Deserialize<List<Fact>>(Web) ?? [];
                return await target.Run.Report.DoneAsync(new FactListDocument(target.Bot, facts), FactText.Table(facts));
            }
        );
        return command;
    }

    private static Command CreateShow(GlobalOptions options, CliEnvironment environment)
    {
        var name = new Argument<string>("name") { Description = "The fact's name, like customer.city." };
        var command = new Command("show", "Show one global fact with its revision (or --open the Facts screen in the web app).") { name };
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
                    target.ItemPath(Collection, parsed.GetRequiredValue(name)),
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var fact = answer.Body?.Deserialize<Fact>(Web) ?? throw new InvalidOperationException("the server sent no fact");
                return await target.Run.Report.DoneAsync(fact, FactText.Detail(fact));
            }
        );
        return command;
    }

    private static Command CreateDelete(GlobalOptions options, CliEnvironment environment)
    {
        var name = new Argument<string>("name") { Description = "The fact's name, like customer.city." };
        var ifRevision = EditOptions.IfRevision();
        var dryRun = EditOptions.DryRun();
        var command = new Command("delete", "Delete a global fact. Nothing checks yet whether a procedure uses it.")
        {
            name,
            ifRevision,
            dryRun,
        };
        EditOptions.RefuseOpen(command, options, "facts delete");
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await BotTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                var factName = parsed.GetRequiredValue(name);
                var dry = parsed.GetValue(dryRun);
                var answer = await ServerCall.DeleteAsync(
                    environment,
                    target.Address,
                    target.ItemPath(Collection, factName) + EditOptions.Query(dry, parsed.GetValue(ifRevision)),
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                return await target.Run.Report.DoneAsync(
                    new FactDeletedDocument(target.Bot, factName, dry),
                    dry ? $"Would delete '{factName}'. Nothing was changed." : $"Deleted '{factName}'."
                );
            }
        );
        return command;
    }

    private static Task<int> Open(BotTarget target, ParseResult parsed) =>
        WebPage.ShowAsync(
            target.PageOf(FactsCommand.Collection),
            parsed.InvocationConfiguration.Output,
            parsed.InvocationConfiguration.Error,
            target.Machine.OpenBrowser
        );
}
