using System.CommandLine;
using System.Text.Json;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Contracts.Bots;

namespace Suwagcms.Cli.Bots;

internal static class DeclarationCommand
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Command Create(GlobalOptions options, CliEnvironment environment)
    {
        var stored = new Option<bool>("--stored") { Description = "Show the last answer without asking the bot." };
        var command = new Command(
            "declaration",
            "Ask the bot what it supports (format version, blocks, actions in its code), keep a good answer with its time and show it (or --open the bot's profile in the web app)."
        )
        {
            stored,
        };
        EditOptions.RefuseOpenWithJson(command, options);
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                if (parsed.GetValue(options.Open))
                {
                    await WebPage.ShowAsync(
                        new Uri(target.Address, $"bots/{Uri.EscapeDataString(target.Bot)}"),
                        parsed.InvocationConfiguration.Output,
                        parsed.InvocationConfiguration.Error,
                        environment.OpenBrowser
                    );
                    return ExitCodes.Done;
                }
                var answer = await ServerCall.GetAsync(
                    environment,
                    target.Address,
                    target.CollectionPath("declaration") + (parsed.GetValue(stored) ? "?ask=false" : ""),
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var status =
                    answer.Body?.Deserialize<DeclarationStatus>(Web)
                    ?? throw new InvalidOperationException("the server sent no declaration");
                return status.Ask is DeclarationAsk.Answered or DeclarationAsk.NotAsked
                    ? await target.Run.Report.DoneAsync(status, DeclarationText.Of(status))
                    : await target.Run.Report.FailedWithAsync(ExitCodes.Refused, status, DeclarationText.Of(status));
            }
        );
        return command;
    }
}
