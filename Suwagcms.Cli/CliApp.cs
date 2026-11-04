using System.CommandLine;

namespace Suwagcms.Cli;

// Why: the streams come in through the configuration so that the tests can read what the command prints.
internal static class CliApp
{
    public static Task<int> Run(string[] args, InvocationConfiguration configuration) =>
        new RootCommand("The command line of Suwagcms.").Parse(args).InvokeAsync(configuration);
}
