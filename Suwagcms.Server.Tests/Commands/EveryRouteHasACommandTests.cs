extern alias Cli;

using System.CommandLine;
using System.Text.Json;
using Suwbackkit.Cli;
using Suwbackkit.Testing;
using CliApp = Cli::Suwagcms.Cli.CliApp;
using CliEnvironment = Cli::Suwagcms.Cli.CliEnvironment;

namespace Suwagcms.Server.Tests.Commands;

public sealed class EveryRouteHasACommandTests(TestDatabase database, ClassServer server) : IClassFixture<ClassServer>
{
    private TestApp App => server.App;

    private sealed record Coverage(IReadOnlyList<string> Commands, string? Reason = null)
    {
        public static Coverage Cli(params string[] words) => new(words);

        public static Coverage NoCommand(string reason) => new([], reason);
    }

    private static readonly Dictionary<string, Coverage> Routes = new()
    {
        ["GET /api/health"] = Coverage.Cli("status"),
        ["GET /api/settings"] = Coverage.NoCommand(
            "how many rows a screen holds is for the web app: the CLI takes --limit, 50 when left out, so a script's output never moves with the setting"
        ),
        ["GET /api/bots"] = Coverage.Cli("bots list"),
        ["POST /api/bots"] = Coverage.Cli("bots add"),
        ["GET /api/bots/{bot}/declaration"] = Coverage.Cli("bots declaration"),
        ["POST /api/validate"] = Coverage.Cli("validate"),
        ["GET /api/bots/{bot}/facts"] = Coverage.Cli("facts list"),
        ["GET /api/bots/{bot}/facts/{name}"] = Coverage.Cli("facts show"),
        ["PUT /api/bots/{bot}/facts/{name}"] = Coverage.Cli("facts add", "facts edit"),
        ["DELETE /api/bots/{bot}/facts/{name}"] = Coverage.Cli("facts delete"),
        ["GET /api/bots/{bot}/knowledge-bases"] = Coverage.Cli("kb list"),
        ["GET /api/bots/{bot}/knowledge-bases/{id}"] = Coverage.Cli("kb show"),
        ["PUT /api/bots/{bot}/knowledge-bases/{id}"] = Coverage.Cli("kb add", "kb edit"),
        ["DELETE /api/bots/{bot}/knowledge-bases/{id}"] = Coverage.Cli("kb delete"),
        ["GET /api/bots/{bot}/articles"] = Coverage.Cli("articles list"),
        ["GET /api/bots/{bot}/articles/{id}"] = Coverage.Cli("articles show"),
        ["PUT /api/bots/{bot}/articles/{id}"] = Coverage.Cli("articles add", "articles edit"),
        ["DELETE /api/bots/{bot}/articles/{id}"] = Coverage.Cli("articles delete"),
        ["GET /api/bots/{bot}/procedures"] = Coverage.Cli("procedures list"),
        ["GET /api/bots/{bot}/procedures/{id}"] = Coverage.Cli("procedures show"),
        ["PUT /api/bots/{bot}/procedures/{id}"] = Coverage.Cli("procedures add", "procedures edit"),
        ["DELETE /api/bots/{bot}/procedures/{id}"] = Coverage.Cli("procedures delete"),
        ["GET /api/bots/{bot}/actions"] = Coverage.Cli("actions list"),
        ["GET /api/bots/{bot}/actions/{id}"] = Coverage.Cli("actions show"),
        ["PUT /api/bots/{bot}/actions/{id}"] = Coverage.Cli("actions add", "actions edit"),
        ["DELETE /api/bots/{bot}/actions/{id}"] = Coverage.Cli("actions delete"),
        ["POST /api/bots/{bot}/actions/{id}/test"] = Coverage.Cli("actions test"),
        ["GET /api/bots/{bot}/persona"] = Coverage.Cli("persona show"),
        ["PUT /api/bots/{bot}/persona"] = Coverage.Cli("persona edit"),
        ["GET /api/bots/{bot}/{kind}/{id}/usage"] = Coverage.Cli("usage"),
        ["POST /api/bots/{bot}/{kind}/{id}/rename"] = Coverage.Cli("rename"),
        ["GET /api/bots/{bot}/versions"] = Coverage.Cli("versions list"),
        ["GET /api/bots/{bot}/versions/{number}"] = Coverage.Cli("versions show"),
        ["POST /api/bots/{bot}/versions"] = Coverage.Cli("versions create"),
        ["GET /api/bots/{bot}/versions/{number}/window"] = Coverage.Cli("versions window"),
        ["GET /api/bots/{bot}/versions/{from}/diff/{to}"] = Coverage.Cli("diff"),
        ["GET /api/bots/{bot}/versions/{number}/export"] = Coverage.Cli("export"),
        ["GET /api/bots/{bot}/versions/status"] = Coverage.Cli("versions status"),
        ["POST /api/bots/{bot}/versions/{number}/push"] = Coverage.Cli("push"),
        ["GET /api/bots/{bot}/versions/{number}/push-plan"] = Coverage.Cli("push"),
        ["POST /api/bots/{bot}/changes"] = Coverage.Cli("apply"),
        ["GET /api/bots/{bot}/history"] = Coverage.Cli("history"),
        ["GET /api/bots/{bot}/{kind}/{id}/history"] = Coverage.Cli("history"),
        ["GET /api/bots/{bot}/availability"] = Coverage.Cli("availability list"),
        ["POST /api/bots/{bot}/availability/send"] = Coverage.Cli("availability send"),
        ["POST /api/bots/{bot}/availability/{kind}/{id}/off"] = Coverage.Cli("availability off"),
        ["POST /api/bots/{bot}/availability/{kind}/{id}/on"] = Coverage.Cli("availability on"),
    };

    private static readonly Dictionary<string, string> OptionOfParameter = new(StringComparer.Ordinal)
    {
        ["ask"] = "--stored",
        ["before"] = "--before",
        ["dryRun"] = "--dry-run",
        ["ifRevision"] = "--if-revision",
        ["kind"] = "--kind",
        ["knowledgeBase"] = "--kb",
        ["language"] = "--language",
        ["limit"] = "--limit",
        ["page"] = "--page",
        ["replace"] = "--replace",
        ["text"] = "--text",
    };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Suwagcms.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }

    private static List<(string Route, string Parameter)> QueryParameters()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "contract", "openapi.json")));
        return
        [
            .. document
                .RootElement.GetProperty("paths")
                .EnumerateObject()
                .SelectMany(path =>
                    path.Value.EnumerateObject()
                        .Where(operation => operation.Value.TryGetProperty("parameters", out _))
                        .SelectMany(operation =>
                            operation
                                .Value.GetProperty("parameters")
                                .EnumerateArray()
                                .Where(parameter => parameter.GetProperty("in").GetString() == "query")
                                .Select(parameter =>
                                    ($"{operation.Name.ToUpperInvariant()} {path.Name}", parameter.GetProperty("name").GetString() ?? "")
                                )
                        )
                ),
        ];
    }

    private static List<string> ParametersWithNoOption(IEnumerable<(string Route, string Parameter)> parameters, RootCommand root) =>
        [
            .. parameters
                .Where(item =>
                    !OptionOfParameter.TryGetValue(item.Parameter, out var option)
                    || !Routes.TryGetValue(item.Route, out var coverage)
                    || !coverage.Commands.Any(words => CommandTree.Find(root, words)?.Options.Any(found => found.Name == option) == true)
                )
                .Select(item => $"{item.Route} ?{item.Parameter}"),
        ];

    private IReadOnlyList<string> Mapped()
    {
        _ = database.DataSource;
        return MappedRoutes.Of(App.Services);
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-10")]
    public void Every_route_the_server_maps_names_its_command_or_says_why_it_has_none()
    {
        var missing = Mapped().Where(route => !Routes.ContainsKey(route)).ToList();

        Assert.True(
            missing.Count == 0,
            $"These routes have no `suwagcms` command: {string.Join("; ", missing)}. Anything the web can do, the CLI can do: "
                + "add the command (with its tests and its line in claude-plugin/skills/suwagcms/SKILL.md) and list the route in "
                + "Suwagcms.Server.Tests/Commands/EveryRouteHasACommandTests.cs, or list it with NoCommand and the reason."
        );
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-10")]
    public void The_table_lists_no_route_the_server_does_not_map()
    {
        var unknown = Routes.Keys.Except(Mapped()).ToList();

        Assert.True(
            unknown.Count == 0,
            $"The server maps no route {string.Join("; ", unknown)}: delete it from the table of routes and commands."
        );
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-10")]
    public void Every_route_of_the_table_names_a_command_or_a_reason_never_both_or_neither()
    {
        var undecided = Routes
            .Where(route => route.Value.Commands.Any() != string.IsNullOrWhiteSpace(route.Value.Reason))
            .Select(route => route.Key)
            .ToList();

        Assert.True(
            undecided.Count == 0,
            $"Give each of these routes either its command or the reason it has none: {string.Join("; ", undecided)}."
        );
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-10")]
    public void Every_command_the_table_names_exists()
    {
        var root = CliApp.Create(CliEnvironment.Real());

        foreach (var words in Routes.Values.SelectMany(coverage => coverage.Commands).Distinct())
        {
            Assert.True(
                CommandTree.Find(root, words) is not null,
                $"`suwagcms {words}` is named in the table of routes and commands but is not a command."
            );
        }
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-17")]
    public void Every_query_parameter_of_a_route_has_an_option_on_its_command()
    {
        var parameters = QueryParameters();
        var missing = ParametersWithNoOption(parameters, CliApp.Create(CliEnvironment.Real()));

        Assert.NotEmpty(parameters);
        Assert.True(
            missing.Count == 0,
            $"The CLI cannot ask for: {string.Join("; ", missing)}. Add the option to the route's command, with its tests and its "
                + "place in claude-plugin/skills/suwagcms/SKILL.md, and name it in OptionOfParameter in Suwagcms.Server.Tests/Commands/EveryRouteHasACommandTests.cs."
        );
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-17")]
    public void A_parameter_with_no_option_is_reported_by_route_and_name()
    {
        var root = CliApp.Create(CliEnvironment.Real());

        Assert.Equal(
            ["GET /api/bots/{bot}/history ?shoe", "GET /api/bots/{bot}/facts ?page"],
            ParametersWithNoOption(
                [("GET /api/bots/{bot}/history", "shoe"), ("GET /api/bots/{bot}/facts", "page"), ("GET /api/bots/{bot}/history", "page")],
                root
            )
        );
    }
}
