using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Cli.Output;
using Suwagcms.Contracts.Actions;

namespace Suwagcms.Cli.Actions;

internal static class ActionTestCommand
{
    private const string Success = "success";
    private const string Failure = "failure";

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(70);

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Command Create(GlobalOptions options, CliEnvironment environment)
    {
        var id = new Argument<string>("id") { Description = "The action's id." };
        var parameters = new Option<string[]>("--param")
        {
            Description = "A parameter's value as name=value; repeat it for each parameter.",
            AllowMultipleArgumentsPerToken = false,
        };
        var yes = new Option<bool>("--yes")
        {
            Description = "Really call the service: every method but GET, and every code action, asks for this first.",
        };
        var command = new Command(
            "test",
            "Try a saved action through the bot, which really calls the service, and show the results or the failure. Writes nothing (or --open its page, with the test panel, and call nothing)."
        )
        {
            id,
            parameters,
            yes,
        };
        EditOptions.RefuseOpenWithJson(command, options);
        command.Validators.Add(result =>
        {
            foreach (var given in result.GetValue(parameters) ?? [])
            {
                if (!given.Contains('=', StringComparison.Ordinal))
                {
                    result.AddError($"--param '{given}' has no '=': write it as name=value.");
                }
            }
        });
        command.SetBotAction(options, environment, async (target, parsed, cancellationToken) =>
        {
            if (parsed.GetValue(options.Open))
            {
                return await target.OpenAsync(target.PageAt("Action", parsed.GetRequiredValue(id)), parsed);
            }
            var path = target.ItemPath(ActionsCommand.Collection, parsed.GetRequiredValue(id));
            var read = await ServerCall.GetAsync(environment, target.Address, path, cancellationToken);
            if (read.ExitCode != ExitCodes.Done)
            {
                return await target.FailedAsync(read, cancellationToken);
            }
            var action = read.Body?.Deserialize<ActionView>(Web) ?? throw new InvalidOperationException("the server sent no action");
            if (Asks(read.Body.Value) is { } question && !parsed.GetValue(yes))
            {
                return await target.Run.Report.FailedAsync(ExitCodes.Refused, new FailureDocument(question), question);
            }
            var values = (parsed.GetValue(parameters) ?? [])
                .Select(given => given.Split('=', 2))
                .ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);
            var answer = await ServerCall.PostAsync(
                environment,
                target.Address,
                path + "/test",
                new RunAction(values),
                cancellationToken,
                Wait
            );
            if (answer.ExitCode != ExitCodes.Done)
            {
                return await target.FailedAsync(answer, cancellationToken);
            }
            var run = answer.Body?.Deserialize<ActionTestAnswer>(Web) ?? throw new InvalidOperationException("the server sent no answer");
            return run.Outcome == Success
                ? await target.Run.Report.DoneAsync(run, Text(run, target.Bot))
                : await target.Run.Report.FailedWithAsync(ExitCodes.Refused, run, Text(run, target.Bot));
        });
        return command;
    }

    private static string? Asks(JsonElement action)
    {
        var implementation = action.GetProperty("implementation");
        if (implementation.ValueKind == JsonValueKind.String)
        {
            return $"This really runs the bot's code for '{action.GetProperty("id").GetString()}'. Pass --yes to run it. Nothing was sent.";
        }
        var http = implementation.GetProperty("http");
        var method = http.GetProperty("method").GetString();
        return method == "GET"
            ? null
            : $"This really calls {method} {http.GetProperty("url").GetString()}. Pass --yes to run it. Nothing was sent.";
    }

    private static string Text(ActionTestAnswer run, string bot)
    {
        var known = new List<string>();
        if (run.Status is { } status)
        {
            known.Add(string.Create(CultureInfo.InvariantCulture, $"Answer {status}."));
        }
        if (run.Outcome is Success or Failure)
        {
            known.Add(string.Create(CultureInfo.InvariantCulture, $"{run.ElapsedMs} ms."));
        }
        var lines = new List<string>();
        if (run.Outcome == Success)
        {
            lines.Add($"Success. {string.Join(" ", known)}");
            lines.AddRange(run.Results.Select(result => $"  {result.Key} = {result.Value}"));
            if (run.Note is { } note)
            {
                lines.Add(note);
            }
            return string.Join(Environment.NewLine, lines);
        }
        lines.Add(run.Outcome == Failure ? $"Failed: {run.Reason}" : $"Not judged: {run.Reason}");
        if (run.Reason?.Contains("the bot's environment", StringComparison.Ordinal) == true)
        {
            lines.Add($"Set it in the environment of bot {bot} and run again.");
        }
        if (known.Count > 0)
        {
            lines.Add(string.Join(" ", known));
        }
        if (run.Note is { } off)
        {
            lines.Add(off);
        }
        if (run.AnswerStart is { Length: > 0 } start)
        {
            lines.Add($"The answer{(run.AnswerCut ? ", cut at its first 2000 characters" : "")}:");
            lines.Add(start);
        }
        return string.Join(Environment.NewLine, lines);
    }
}
