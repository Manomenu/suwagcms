using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Cli.Output;
using Suwagcms.Contracts;

namespace Suwagcms.Cli.Versions;

internal static class DiffCommand
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Command Create(GlobalOptions options, CliEnvironment environment)
    {
        var from = new Argument<int>("from") { Description = "The version to compare from, like 12." };
        var to = new Argument<int>("to") { Description = "The version to compare to, like 14." };
        var command = new Command(
            "diff",
            "Say what going from one version to another changes: resources added, removed and changed, with old and new values. Finding differences exits 0 (or --open the comparison in the web app)."
        )
        {
            from,
            to,
        };
        EditOptions.RefuseOpenWithJson(command, options);
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                var first = parsed.GetRequiredValue(from).ToString(CultureInfo.InvariantCulture);
                var second = parsed.GetRequiredValue(to).ToString(CultureInfo.InvariantCulture);
                if (parsed.GetValue(options.Open))
                {
                    return await WebPage.ShowAsync(
                        new Uri(target.PageOf(VersionsCommand.Collection), $"?from={first}&to={second}"),
                        parsed.InvocationConfiguration.Output,
                        parsed.InvocationConfiguration.Error,
                        target.Machine.OpenBrowser
                    );
                }
                var answer = await ServerCall.GetAsync(
                    environment,
                    target.Address,
                    $"{target.ItemPath(VersionsCommand.Collection, first)}/diff/{second}",
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var comparison =
                    answer.Body?.Deserialize<VersionComparison>(Web)
                    ?? throw new InvalidOperationException("the server sent no comparison");
                return await target.Run.Report.DoneAsync(comparison, DiffText.Of(comparison));
            }
        );
        return command;
    }
}
