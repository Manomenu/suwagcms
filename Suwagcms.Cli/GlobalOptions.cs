using System.CommandLine;
using Suwagcms.Cli.Connection;

namespace Suwagcms.Cli;

internal sealed class GlobalOptions
{
    public Option<bool> Json { get; } =
        new("--json") { Description = "Print exactly one JSON document on stdout, also when the command fails.", Recursive = true };

    public Option<bool> Open { get; } =
        new("--open")
        {
            Description = "Open the matching web page instead of printing; a command with no page refuses it.",
            Recursive = true,
        };

    public Option<string?> Server { get; } = new("--server") { Description = ServerAddress.OptionDescription, Recursive = true };

    public Option<string?> Bot { get; } = new("--bot") { Description = Bots.ChosenBot.OptionDescription, Recursive = true };

    public void AddTo(RootCommand root)
    {
        root.Options.Add(Json);
        root.Options.Add(Open);
        root.Options.Add(Server);
        root.Options.Add(Bot);
    }
}
