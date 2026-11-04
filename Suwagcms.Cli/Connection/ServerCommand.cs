using System.CommandLine;
using Suwagcms.Cli.Output;

namespace Suwagcms.Cli.Connection;

internal static class ServerCommand
{
    public static Command Create(GlobalOptions options, CliEnvironment environment)
    {
        var url = new Argument<string>("url") { Description = "The server's address, e.g. " + ServerAddress.Default };
        url.Validators.Add(result =>
        {
            if (ServerAddress.Parse(result.GetValueOrDefault<string>(), "The address").Problem is { } problem)
            {
                result.AddError(problem);
            }
        });
        var command = new Command("server", "Save the server address that later commands use.") { url };
        command.Validators.Add(result =>
        {
            if (result.GetValue(options.Open))
            {
                result.AddError("`suwagcms server` has no web page to open: drop --open.");
            }
        });
        command.SetAction(async parsed =>
        {
            var report = new Report(
                parsed.InvocationConfiguration.Output,
                parsed.InvocationConfiguration.Error,
                parsed.GetValue(options.Json)
            );
            var display = ServerAddress.Display(
                ServerAddress.Parse(parsed.GetRequiredValue(url), "The address").Address
                    ?? throw new InvalidOperationException("the validator let a bad address through")
            );
            SavedAddress.Write(environment.SettingsFolder, display);
            return await report.DoneAsync(
                new ServerDocument(display),
                $"Saved {display}: later commands use it unless --server or {ServerAddress.Variable} says otherwise."
            );
        });
        return command;
    }
}
