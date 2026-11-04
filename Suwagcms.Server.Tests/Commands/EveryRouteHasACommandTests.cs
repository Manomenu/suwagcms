extern alias Cli;

using System.CommandLine;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using CliApp = Cli::Suwagcms.Cli.CliApp;
using CliEnvironment = Cli::Suwagcms.Cli.CliEnvironment;

namespace Suwagcms.Server.Tests.Commands;

// Why: "anything the web can do, the CLI can do" is enforced here, in the server's tests because the gate project is protected.
public sealed partial class EveryRouteHasACommandTests(TestDatabase database) : IDisposable
{
    private readonly TestApp app = new();

    private sealed record Coverage(string? Command, string? Reason = null)
    {
        public static Coverage Cli(string words) => new(words);

        public static Coverage NoCommand(string reason) => new(null, reason);
    }

    // Why: words as typed after `suwagcms`; a new route is added here in the change that adds it, with its command.
    private static readonly Dictionary<string, Coverage> Routes = new() { ["GET /api/health"] = Coverage.Cli("status") };

    public void Dispose() => app.Dispose();

    [GeneratedRegex(@"\{(\w+):[^}]+\}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RouteConstraint();

    private List<string> Mapped()
    {
        _ = database.Pool;
        return
        [
            .. app
                .Services.GetRequiredService<EndpointDataSource>()
                .Endpoints.OfType<RouteEndpoint>()
                .SelectMany(endpoint =>
                    (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).Select(method =>
                        $"{method} {RouteConstraint().Replace(endpoint.RoutePattern.RawText ?? "", "{$1}")}"
                    )
                )
                .Order(StringComparer.Ordinal),
        ];
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
            .Where(route => string.IsNullOrWhiteSpace(route.Value.Command) == string.IsNullOrWhiteSpace(route.Value.Reason))
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
        var commands = Routes
            .Values.Where(coverage => coverage.Command is not null)
            .Select(coverage => coverage.Command)
            .OfType<string>()
            .Distinct();

        foreach (var words in commands)
        {
            Command? found = root;
            foreach (var word in words.Split(' '))
            {
                found = found.Subcommands.SingleOrDefault(command => command.Name == word);
                Assert.True(found is not null, $"`suwagcms {words}` is named in the table of routes and commands but is not a command.");
            }
        }
    }
}
