using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Suwagcms.Sdk.Tests.Repository;

namespace Suwagcms.Sdk.Tests.BotRuntime;

public sealed class PackedBotTests
{
    private const string Program = """
        using Suwagcms.Sdk.BotRuntime;
        using Suwagcms.Sdk.Format;

        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSuwagcmsBot(
            "packed-bot",
            declares: new BotCapabilities
            {
                FormatVersion = BotDefinition.CurrentFormatVersion,
                Blocks = ["listen", "say"],
                CodeActions = [new CodeAction { Id = "lookupPolicy", Description = "Finds a policy" }],
            }
        );
        var app = builder.Build();
        app.MapSuwagcmsBot();
        app.Run();
        """;

    private const string Project = """
        <Project Sdk="Microsoft.NET.Sdk.Web">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Suwagcms.Sdk" Version="0.0.0-local" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    [Trait("criterion", "AC-bot-runtime-sdk-1")]
    [Trait("criterion", "AC-bot-declaration-1")]
    public async Task A_bot_built_on_the_packed_package_takes_a_version_without_a_restart_reports_it_and_answers_what_it_declares()
    {
        // Why: packing and publishing cost about 10 s, over the quick gate's budget; CI always runs this, a machine when the SDK changes.
        var passed = RepositoryFiles.Path($".artifacts/packed-bot-{InputsHash()}");
        if (Environment.GetEnvironmentVariable("CI") is null && File.Exists(passed))
        {
            return;
        }

        using var scratch = new TempDirectory();
        var packages = Path.Combine(scratch.Path, "packages");
        var bot = Path.Combine(scratch.Path, "bot");
        var published = Path.Combine(bot, "out");
        Directory.CreateDirectory(packages);
        Directory.CreateDirectory(bot);
        // Why: a nuget.config of its own so that only the package packed here is the source, and no file above the scratch folder applies.
        await File.WriteAllTextAsync(
            Path.Combine(bot, "nuget.config"),
            $"""<configuration><packageSources><clear /><add key="local" value="{packages}" /></packageSources></configuration>""",
            TestContext.Current.CancellationToken
        );
        await File.WriteAllTextAsync(Path.Combine(bot, "Bot.csproj"), Project, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(bot, "Program.cs"), Program, TestContext.Current.CancellationToken);

        await RunAsync(
            RepositoryFiles.Root,
            null,
            "pack",
            RepositoryFiles.Path("Suwagcms.Sdk/Suwagcms.Sdk.csproj"),
            "-o",
            packages,
            "--nologo",
            "-v",
            "q"
        );
        // Why: every local pack has the same version, so the machine's package cache would hand back an older pack of the SDK.
        await RunAsync(bot, Path.Combine(scratch.Path, "cache"), "publish", "-o", published, "--nologo", "-v", "q", "-nodeReuse:false");

        using var process =
            Process.Start(
                new ProcessStartInfo("dotnet", [Path.Combine(published, "Bot.dll")])
                {
                    WorkingDirectory = bot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Environment = { ["ASPNETCORE_URLS"] = "http://127.0.0.1:0" },
                }
            ) ?? throw new InvalidOperationException("dotnet did not start.");
        try
        {
            var address = await ListeningAddressAsync(process);
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            Assert.Null((await StateAsync(client))["current"]);
            var declared =
                JsonNode.Parse(
                    await client.GetStringAsync(new Uri("/suwagcms/declaration", UriKind.Relative), TestContext.Current.CancellationToken)
                ) ?? throw new InvalidOperationException("empty declaration");
            Assert.Equal("packed-bot", declared.Member("bot").Text());
            Assert.Equal(1, declared.Member("formatVersion").GetValue<int>());
            Assert.Equal("""["listen","say"]""", declared.Member("blocks").ToJsonString());
            Assert.Equal("lookupPolicy", declared.Member("codeActions").AsArray()[0]?.Member("id").Text());

            var document = Documents.Make(7, "packed", bot: "packed-bot");
            using var content = new StringContent(document.ToJsonString(), Encoding.UTF8, "application/json");
            using var put = await client.PutAsync(
                new Uri("/suwagcms/versions/7", UriKind.Relative),
                content,
                TestContext.Current.CancellationToken
            );

            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var state = await StateAsync(client);
            Assert.Equal(7, state.Member("current").GetValue<int>());
            Assert.Equal("packed-bot", state.Member("bot").Text());
            Directory.CreateDirectory(Path.GetDirectoryName(passed) ?? RepositoryFiles.Root);
            await File.WriteAllTextAsync(passed, "", TestContext.Current.CancellationToken);
        }
        finally
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        }
    }

    private static string InputsHash()
    {
        var sdk = RepositoryFiles.Path("Suwagcms.Sdk");
        var files = Directory
            .EnumerateFiles(sdk, "*", SearchOption.AllDirectories)
            .Where(file => !file.Contains("/bin/", StringComparison.Ordinal) && !file.Contains("/obj/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(Program + Project));
        foreach (var file in files)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(sdk, file)));
            hash.AppendData(File.ReadAllBytes(file));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static async Task<JsonNode> StateAsync(HttpClient client) =>
        JsonNode.Parse(await client.GetStringAsync(new Uri("/suwagcms/state", UriKind.Relative), TestContext.Current.CancellationToken))
        ?? throw new InvalidOperationException("empty state");

    private static async Task<string> ListeningAddressAsync(Process process)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        const string Marker = "Now listening on: ";
        while (await process.StandardOutput.ReadLineAsync(timeout.Token) is { } line)
        {
            var at = line.IndexOf(Marker, StringComparison.Ordinal);
            if (at >= 0)
            {
                return line[(at + Marker.Length)..].Trim();
            }
        }

        throw new InvalidOperationException(
            "The packed bot ended without listening: " + await process.StandardError.ReadToEndAsync(timeout.Token)
        );
    }

    private static async Task RunAsync(string directory, string? packageCache, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (packageCache is not null)
        {
            start.Environment["NUGET_PACKAGES"] = packageCache;
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("dotnet did not start.");
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.True(process.ExitCode == 0, $"dotnet {string.Join(' ', arguments)} failed: {await output}{await error}");
    }
}
