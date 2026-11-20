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
        if (Given(option) is { } fromOption)
        {
            return Parse(fromOption, "--server");
        }
        if (Given(environment.Variable(Variable)) is { } fromVariable)
        {
            return Parse(fromVariable, Variable);
        }
        if (Given(environment.Config?.SuwagcmsUrl) is { } fromConfig)
        {
            return Parse(fromConfig, $"{RepositoryConfig.AddressKey} in {RepositoryConfig.FileName}");
        }
        if (Given(SavedAddress.Read(environment.SettingsFolder)) is { } saved)
        {
            return Parse(saved, "The address saved with `suwagcms server`");
        }
        return Parse(Default, "The default address");
    }

    public static AddressChoice Parse(string text, string source) =>
        Uri.TryCreate(text.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var address) && address.Scheme is "http" or "https"
            ? new AddressChoice(address, null)
            : new AddressChoice(null, $"{source} is '{text}', not an http or https address. Give one like {Default}.");

    public static string Display(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return address.ToString().TrimEnd('/');
    }

    private static string? Given(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
