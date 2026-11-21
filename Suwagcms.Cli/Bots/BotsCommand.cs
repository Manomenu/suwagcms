using System.CommandLine;
using System.Text.Json;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Output;
using Suwagcms.Contracts.Bots;

namespace Suwagcms.Cli.Bots;

internal static class BotsCommand
{
    public static Command Create(GlobalOptions options, CliEnvironment environment) =>
        new("bots", "List the registered bots, register a new one, and ask a bot what it supports.")
        {
            CreateList(options, environment),
            CreateAdd(options, environment),
            DeclarationCommand.Create(options, environment),
        };

    private static Command CreateList(GlobalOptions options, CliEnvironment environment)
    {
        var command = new Command(
            "list",
            "List the registered bots, marking the one you work on with * (or --open the list in the web app)."
        );
        command.Validators.Add(result =>
        {
            if (result.GetValue(options.Json) && result.GetValue(options.Open))
            {
                result.AddError("--json and --open cannot be used together: one prints the data, the other opens the page.");
            }
        });
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var run = BotCommandRun.Start(parsed, options, environment);
                if (run.Address is not { } address)
                {
                    return await run.BadAddressAsync();
                }
                if (parsed.GetValue(options.Open))
                {
                    await WebPage.ShowAsync(
                        new Uri(address, "bots"),
                        parsed.InvocationConfiguration.Output,
                        parsed.InvocationConfiguration.Error,
                        environment.OpenBrowser
                    );
                    return ExitCodes.Done;
                }
                var (bots, answer) = await BotListing.FetchAsync(environment, address, cancellationToken);
                if (bots is null)
                {
                    return await run.Report.ReportAsync(answer, address);
                }
                var active = ChosenBot.Choose(parsed.GetValue(options.Bot), environment)?.Id;
                return await run.Report.DoneAsync(new BotListDocument(bots, active), Table(bots, active));
            }
        );
        return command;
    }

    private static string Table(IReadOnlyList<Bot> bots, string? active)
    {
        if (bots.Count == 0)
        {
            return "No bots yet. Register one: suwagcms bots add <id> --name <name> --address <url>";
        }
        var idWidth = bots.Max(bot => bot.Id.Length);
        var nameWidth = bots.Max(bot => bot.Name.Length);
        return string.Join(
            Environment.NewLine,
            bots.Select(bot =>
                $"{(bot.Id == active ? "*" : " ")} {bot.Id.PadRight(idWidth)}  {bot.Name.PadRight(nameWidth)}  {bot.Address}"
            )
        );
    }

    private static Command CreateAdd(GlobalOptions options, CliEnvironment environment)
    {
        var id = new Argument<string?>("id")
        {
            Description = "The bot's id: lowercase letters, digits and hyphens, starting with a letter. It does not change.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var name = new Option<string?>("--name") { Description = "The name people see." };
        var address = new Option<string?>("--address")
        {
            Description = "Where the CMS reaches the bot, an absolute http or https address.",
        };
        var command = new Command("add", "Register a bot. The CMS never contacts it, so it can be registered before it runs.")
        {
            id,
            name,
            address,
        };
        command.Validators.Add(result =>
        {
            if (result.GetValue(options.Open))
            {
                result.AddError("`suwagcms bots add` has no web page to open: drop --open.");
            }
        });
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var run = BotCommandRun.Start(parsed, options, environment);
                if (run.Address is not { } server)
                {
                    return await run.BadAddressAsync();
                }
                var request = new RegisterBot(parsed.GetValue(id) ?? "", parsed.GetValue(name) ?? "", parsed.GetValue(address) ?? "");
                var answer = await ServerCall.PostAsync(environment, server, "api/bots", request, cancellationToken);
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await run.Report.ReportAsync(answer, server);
                }
                var bot = answer.Body?.Deserialize<Bot>(BotListing.Web) ?? throw new InvalidOperationException("the server sent no bot");
                return await run.Report.DoneAsync(bot, $"Registered {bot.Id} ({bot.Name}) at {bot.Address}");
            }
        );
        return command;
    }
}
