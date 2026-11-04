namespace Suwagcms.Cli;

internal sealed record CliEnvironment(
    Func<string, string?> Variable,
    string SettingsFolder,
    HttpClient? Http = null,
    Action<Uri>? OpenBrowser = null
)
{
    public const string CannotSave =
        "Cannot save a setting: this machine has no settings folder (HOME is not set). Set HOME, or pass --server, --bot or the SUWAGCMS_URL, SUWAGCMS_BOT variables instead.";

    // Why: DoNotVerify, because without it .NET answers empty when the folder does not exist yet; without HOME it is empty anyway, and joining it would give a relative path inside the current directory.
    public bool CanSaveSettings => Path.IsPathRooted(SettingsFolder);

    public static CliEnvironment Real()
    {
        var applicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.DoNotVerify
        );
        return new(Environment.GetEnvironmentVariable, applicationData.Length == 0 ? "" : Path.Combine(applicationData, "suwagcms"));
    }
}
