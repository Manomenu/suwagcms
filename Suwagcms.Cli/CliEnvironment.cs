using System.Diagnostics.CodeAnalysis;
using Suwagcms.Cli.Connection;

namespace Suwagcms.Cli;

internal sealed record CliEnvironment(
    Func<string, string?> Variable,
    string SettingsFolder,
    HttpClient? Http = null,
    Action<Uri>? OpenBrowser = null,
    Func<Stream>? Stdin = null,
    string WorkingDirectory = "",
    RepositoryConfig? Config = null
)
{
    public const string CannotSave =
        "Cannot save a setting: this machine has no settings folder (HOME is not set). Set HOME, or pass --server, --bot or the SUWAGCMS_URL, SUWAGCMS_BOT variables instead.";

    public bool CanSaveSettings => Path.IsPathRooted(SettingsFolder);

    public static CliEnvironment Real()
    {
        var applicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.DoNotVerify
        );
        return new(
            Environment.GetEnvironmentVariable,
            applicationData.Length == 0 ? "" : Path.Combine(applicationData, "suwagcms"),
            Stdin: StandardInput,
            WorkingDirectory: Environment.CurrentDirectory
        );
    }

    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "The only place that reads the process's standard input."
    )]
    private static Stream StandardInput() => Console.OpenStandardInput();
}
