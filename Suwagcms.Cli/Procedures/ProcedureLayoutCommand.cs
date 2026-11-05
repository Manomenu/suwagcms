using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Contracts;

namespace Suwagcms.Cli.Procedures;

// Why: the web creator keeps node positions apart from the procedure, so the CLI reads and sets them apart too, and never touches a revision.
internal static class ProcedureLayoutCommand
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Command Create(GlobalOptions options, CliEnvironment environment)
    {
        var id = new Argument<string>("id") { Description = "The procedure's id, like orderValuation." };
        var set = new Option<string[]>("--set")
        {
            Description = "Put a step at a position: <step>=<x>,<y>, like ask=120,40. Repeat it for more steps; the others keep theirs.",
            Arity = ArgumentArity.ZeroOrMore,
        };
        var clear = new Option<bool>("--clear") { Description = "Forget every position, so the creator arranges the whole graph itself." };
        var command = new Command(
            "layout",
            "Show where the web creator draws each step of a procedure, or set it with --set or --clear. A step with no position is arranged automatically; positions are no part of the procedure and change no revision."
        )
        {
            id,
            set,
            clear,
        };
        EditOptions.RefuseOpen(command, options, "procedures layout");
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await ProcedureTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                var name = parsed.GetRequiredValue(id);
                var changes = parsed.GetValue(set) ?? [];
                if (parsed.GetValue(clear) && changes.Length > 0)
                {
                    return await target.MisusedAsync("--clear and --set cannot be used together: clear first, then set.");
                }
                var (given, problem) = Parse(changes);
                if (given is null)
                {
                    return await target.MisusedAsync(problem);
                }
                var path = target.LayoutPath(name);
                var answer = await ServerCall.GetAsync(environment, target.Address, path, cancellationToken);
                if (answer.ExitCode == ExitCodes.Done && (parsed.GetValue(clear) || given.Count > 0))
                {
                    var merged = parsed.GetValue(clear) ? [] : Read(answer).Positions.ToDictionary();
                    foreach (var (step, position) in given)
                    {
                        merged[step] = position;
                    }
                    answer = await ServerCall.PutAsync(environment, target.Address, path, new ProcedureLayout(merged), cancellationToken);
                }
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var layout = Read(answer);
                return await target.Run.Report.DoneAsync(new ProcedureLayoutDocument(target.Bot, name, layout), Table(layout));
            }
        );
        return command;
    }

    private static ProcedureLayout Read(ServerAnswer answer) =>
        answer.Body?.Deserialize<ProcedureLayout>(Web) ?? throw new InvalidOperationException("the server sent no layout");

    private static string Table(ProcedureLayout layout)
    {
        if (layout.Positions.Count == 0)
        {
            return "No positions stored: the creator arranges the steps itself. Set one with --set <step>=<x>,<y>.";
        }
        var width = layout.Positions.Keys.Max(step => step.Length);
        return string.Join(
            Environment.NewLine,
            layout
                .Positions.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => string.Create(CultureInfo.InvariantCulture, $"{item.Key.PadRight(width)}  {item.Value.X}  {item.Value.Y}"))
        );
    }

    private static (Dictionary<string, NodePosition>? Positions, string Problem) Parse(string[] changes)
    {
        var result = new Dictionary<string, NodePosition>();
        foreach (var change in changes)
        {
            var sides = change.Split('=', 2);
            var numbers = sides.Length == 2 ? sides[1].Split(',') : [];
            if (
                sides[0].Length == 0
                || numbers.Length != 2
                || !double.TryParse(numbers[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !double.TryParse(numbers[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                || !double.IsFinite(x)
                || !double.IsFinite(y)
            )
            {
                return (null, $"'{change}' is not a position. Write --set <step>=<x>,<y>, like --set ask=120,40.");
            }
            result[sides[0]] = new NodePosition(x, y);
        }
        return (result, "");
    }
}
