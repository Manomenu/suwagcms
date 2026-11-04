namespace Suwagcms.Cli;

internal sealed record CliEnvironment(
    Func<string, string?> Variable,
    string SettingsFolder,
    HttpClient? Http = null,
    Action<Uri>? OpenBrowser = null
)
{
    public static CliEnvironment Real() =>
        new(
            Environment.GetEnvironmentVariable,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "suwagcms")
        );
}
