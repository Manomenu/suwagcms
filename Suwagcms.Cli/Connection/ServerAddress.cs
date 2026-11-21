using Suwbackkit.Cli;

namespace Suwagcms.Cli.Connection;

internal static class ServerAddress
{
    public const string Variable = "SUWAGCMS_URL";
    public const string Default = "http://localhost:3330";

    public const string OptionDescription =
        $"The server's address (the web app; the API is under /api). Default: ${Variable}, else {RepositoryConfig.AddressKey} in {RepositoryConfig.DefaultFolder}/{RepositoryConfig.FileName} under the current folder, else the address saved with `suwagcms server <url>`, else {Default}.";

    public static AddressChoice Choose(string? option, CliEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return Suwbackkit.Cli.ServerAddress.Choose(
            [
                new("--server", () => option),
                new(Variable, () => environment.Variable(Variable)),
                new($"{RepositoryConfig.AddressKey} in {RepositoryConfig.FileName}", () => environment.Config?.SuwagcmsUrl),
                new("The address saved with `suwagcms server`", () => SavedAddress.Read(environment.SettingsFolder)),
            ],
            Default
        );
    }

    public static AddressChoice Parse(string text, string source) => Suwbackkit.Cli.ServerAddress.Parse(text, source, Default);

    public static string Display(Uri address) => Suwbackkit.Cli.ServerAddress.Display(address);
}
