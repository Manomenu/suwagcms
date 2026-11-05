using System.CommandLine;

namespace Suwagcms.Cli.Tests;

internal static class TestCli
{
    public static string FreshSettingsFolder() => Path.Combine(Path.GetTempPath(), "suwagcms-cli-tests", Guid.NewGuid().ToString("N"));

    public static async Task<CliResult> Run(
        string[] args,
        StubServer? server = null,
        Dictionary<string, string>? variables = null,
        string? settingsFolder = null,
        Action<Uri>? openBrowser = null,
        byte[]? stdin = null
    )
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var handler = server is null ? null : new StubHandler(server);
        using var http = handler is null ? null : new HttpClient(handler);
        var environment = new CliEnvironment(
            name => variables is not null && variables.TryGetValue(name, out var value) ? value : null,
            settingsFolder ?? FreshSettingsFolder(),
            http,
            openBrowser,
            stdin is null ? null : () => new MemoryStream(stdin)
        );

        var exitCode = await CliApp.Run(args, new InvocationConfiguration { Output = output, Error = error }, environment);

        return new CliResult(exitCode, output.ToString(), error.ToString());
    }
}
