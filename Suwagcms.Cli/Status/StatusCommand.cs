using System.CommandLine;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Output;

namespace Suwagcms.Cli.Status;

internal static class StatusCommand
{
    public static Command Create(GlobalOptions options, CliEnvironment environment)
    {
        var command = new Command("status", "Say which server address is used and whether the server answers (or --open the web app).");
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
                var report = new Report(
                    parsed.InvocationConfiguration.Output,
                    parsed.InvocationConfiguration.Error,
                    parsed.GetValue(options.Json)
                );
                var choice = ServerAddress.Choose(parsed.GetValue(options.Server), environment);
                if (choice.Address is not { } address)
                {
                    var problem = choice.Problem ?? "The server address is unknown.";
                    return await report.FailedAsync(ExitCodes.Misused, new FailureDocument(problem), problem);
                }
                var display = ServerAddress.Display(address);
                if (parsed.GetValue(options.Open))
                {
                    return await WebPage.ShowAsync(
                        address,
                        parsed.InvocationConfiguration.Output,
                        parsed.InvocationConfiguration.Error,
                        environment.OpenBrowser
                    );
                }
                var answer = await ServerCall.GetAsync(environment, address, "api/health", cancellationToken);
                return answer.ExitCode switch
                {
                    ExitCodes.Done => await report.DoneAsync(new StatusDocument(display, true), $"Server {display} reachable"),
                    _ => await report.FailedAsync(
                        answer.ExitCode,
                        new FailureDocument(answer.Message, display, answer.Mistakes.Count == 0 ? null : answer.Mistakes),
                        answer.Message
                    ),
                };
            }
        );
        return command;
    }
}
