using System.CommandLine;
using Suwagcms.Cli.Output;

namespace Suwagcms.Cli.Bots;

internal static class UseCommand
{
    public static Command Create(GlobalOptions options, CliEnvironment environment)
    {
        var id = new Argument<string?>("id")
        {
            Description = "The bot to store for later commands. Without it, say which bot is in use.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var command = new Command("use", "Store the bot later commands work on, or say which bot is in use.") { id };
        command.Validators.Add(result =>
        {
            if (result.GetValue(options.Open))
            {
                result.AddError("`suwagcms use` has no web page to open: drop --open.");
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
                var (bots, answer) = await BotListing.FetchAsync(environment, address, cancellationToken);
                if (bots is null)
                {
                    return await run.Report.ReportAsync(answer, address);
                }
                var given = parsed.GetValue(id);
                var chosen = string.IsNullOrWhiteSpace(given)
                    ? ChosenBot.Choose(parsed.GetValue(options.Bot), environment)
                    : new ChosenBot(given.Trim(), "");
                if (chosen is null)
                {
                    var text =
                        $"No bot is chosen. Choose one with --bot <id>, the {ChosenBot.Variable} variable or `suwagcms use <id>`. Bots: {BotListing.Names(bots)}.";
                    return await run.Report.FailedAsync(ExitCodes.Misused, new FailureDocument(text), text);
                }
                if (bots.All(bot => bot.Id != chosen.Id))
                {
                    var text = $"No bot '{chosen.Id}'. Bots: {BotListing.Names(bots)}.";
                    return await run.Report.FailedAsync(ExitCodes.Refused, new FailureDocument(text), text);
                }
                if (string.IsNullOrWhiteSpace(given))
                {
                    return await run.Report.DoneAsync(new UseDocument(chosen.Id, chosen.Source), $"{chosen.Id} (from {chosen.Source})");
                }
                if (!environment.CanSaveSettings)
                {
                    return await run.Report.FailedAsync(
                        ExitCodes.Misused,
                        new FailureDocument(CliEnvironment.CannotSave),
                        CliEnvironment.CannotSave
                    );
                }
                SavedBot.Write(environment.SettingsFolder, chosen.Id);
                return await run.Report.DoneAsync(
                    new UseDocument(chosen.Id, "`suwagcms use`"),
                    $"Using {chosen.Id}: later commands work on it unless --bot or {ChosenBot.Variable} says otherwise."
                );
            }
        );
        return command;
    }
}
