using System.CommandLine;
using Suwagcms.Cli.Articles;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Changes;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Facts;
using Suwagcms.Cli.History;
using Suwagcms.Cli.KnowledgeBases;
using Suwagcms.Cli.Output;
using Suwagcms.Cli.Procedures;
using Suwagcms.Cli.Renames;
using Suwagcms.Cli.Status;
using Suwagcms.Cli.Usage;
using Suwagcms.Cli.Validation;
using Suwagcms.Cli.Versions;

namespace Suwagcms.Cli;

// Why: the streams come in through the configuration, and the machine through the environment, so that the tests can run any command in process.
internal static class CliApp
{
    public static async Task<int> Run(string[] args, InvocationConfiguration configuration, CliEnvironment? environment = null)
    {
        var root = Create(environment ?? CliEnvironment.Real());
        var parsed = root.Parse(args);
        return parsed.Errors.Count > 0 ? await MisusedAsync(root, parsed, configuration) : await parsed.InvokeAsync(configuration);
    }

    public static RootCommand Create(CliEnvironment environment)
    {
        var options = new GlobalOptions();
        var root = new RootCommand("The command line of Suwagcms.")
        {
            StatusCommand.Create(options, environment),
            ServerCommand.Create(options, environment),
            BotsCommand.Create(options, environment),
            UseCommand.Create(options, environment),
            FactsCommand.Create(options, environment),
            ArticlesCommand.Create(options, environment),
            KnowledgeBasesCommand.Create(options, environment),
            ProceduresCommand.Create(options, environment),
            VersionsCommand.Create(options, environment),
            UsageCommand.Create(options, environment),
            RenameCommand.Create(options, environment),
            PushCommand.Create(options, environment),
            ApplyCommand.Create(options, environment),
            HistoryCommand.Create(options, environment),
            DiffCommand.Create(options, environment),
            ExportCommand.Create(options, environment),
            ValidateCommand.Create(options, environment),
        };
        options.AddTo(root);
        return root;
    }

    // Why: System.CommandLine ends a parse error with exit code 1, which here means "the server refused"; a wrong use is 2 and shows the usage.
    private static async Task<int> MisusedAsync(RootCommand root, ParseResult parsed, InvocationConfiguration configuration)
    {
        var path = new List<string>();
        for (Symbol? symbol = parsed.CommandResult.Command; symbol is not null && symbol != root; symbol = symbol.Parents.FirstOrDefault())
        {
            path.Insert(0, symbol.Name);
        }
        using var usage = new StringWriter();
        await root.Parse([.. path, "--help"]).InvokeAsync(new InvocationConfiguration { Output = usage, Error = usage });

        var problems = string.Join(Environment.NewLine, parsed.Errors.Select(error => error.Message));
        var report = new Report(configuration.Output, configuration.Error, parsed.GetValue<bool>("--json"));
        return await report.FailedAsync(
            ExitCodes.Misused,
            new FailureDocument(problems, Usage: usage.ToString().Trim()),
            $"{problems}{Environment.NewLine}{Environment.NewLine}{usage.ToString().Trim()}"
        );
    }
}
