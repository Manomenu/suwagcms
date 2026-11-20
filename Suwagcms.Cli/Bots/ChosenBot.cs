using Suwagcms.Cli.Connection;

namespace Suwagcms.Cli.Bots;

internal sealed record ChosenBot(string Id, string Source)
{
    public const string Variable = "SUWAGCMS_BOT";

    public const string OptionDescription =
        $"The bot a command works on. Default: ${Variable}, else {RepositoryConfig.BotKey} in {RepositoryConfig.DefaultFolder}/{RepositoryConfig.FileName} under the current folder, else the bot stored with `suwagcms use <id>`.";

    public static ChosenBot? Choose(string? option, CliEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (Given(option) is { } fromOption)
        {
            return new ChosenBot(fromOption, "--bot");
        }
        if (Given(environment.Variable(Variable)) is { } fromVariable)
        {
            return new ChosenBot(fromVariable, Variable);
        }
        if (Given(environment.Config?.BotId) is { } fromConfig)
        {
            return new ChosenBot(fromConfig, $"{RepositoryConfig.BotKey} in {RepositoryConfig.FileName}");
        }
        return Given(SavedBot.Read(environment.SettingsFolder)) is { } saved ? new ChosenBot(saved, "`suwagcms use`") : null;
    }

    private static string? Given(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
