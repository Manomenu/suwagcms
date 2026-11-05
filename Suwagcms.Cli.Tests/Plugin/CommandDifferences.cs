namespace Suwagcms.Cli.Tests.Plugin;

internal sealed record CommandDifferences(IReadOnlyList<string> NotListed, IReadOnlyList<string> NotInCli);
