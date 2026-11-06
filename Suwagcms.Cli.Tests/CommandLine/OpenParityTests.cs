using System.CommandLine;
using System.Text.RegularExpressions;
using Suwagcms.Cli.Tests.Plugin;

namespace Suwagcms.Cli.Tests.CommandLine;

public sealed partial class OpenParityTests
{
    private const string Deletes = "what it deletes is gone, so there is no page left to open";
    private const string NoOneResource =
        "it changes no one resource: `bots declaration --open` and `versions show --open` open what it made";
    private const string Spans = "what it reaches spans several screens, and no one screen shows it all";
    private const string NotOfTheCms = "it concerns this machine or a local file, not a page of the CMS";

    // Why: --open is refused on these only; a new command either opens its page or joins this list with its reason.
    private static readonly Dictionary<string, string> NoPage = new(StringComparer.Ordinal)
    {
        ["server"] = NotOfTheCms,
        ["use"] = NotOfTheCms,
        ["validate"] = NotOfTheCms,
        ["apply"] = Spans,
        ["availability list"] = Spans,
        ["availability send"] = Spans,
        ["bots add"] = NoOneResource,
        ["facts delete"] = Deletes,
        ["kb delete"] = Deletes,
        ["articles delete"] = Deletes,
        ["actions delete"] = Deletes,
        ["procedures delete"] = Deletes,
        ["versions create"] = NoOneResource,
        ["push"] = NoOneResource,
    };

    // Why: the screens as the web app routes them, each with the command that lands on it, or the reason none does.
    private static readonly Dictionary<string, string> Screens = new(StringComparer.Ordinal)
    {
        ["/bots/:id/articles/*?"] = "articles list",
        ["/bots/:id/facts/*?"] = "facts list",
        ["/bots/:id/knowledge-bases/*?"] = "kb list",
        ["/bots/:id/procedures"] = "procedures list",
        ["/bots/:id/procedures/new"] = "no: it only starts a file, which `procedures add --file` takes whole",
        ["/bots/:id/actions/*?"] = "actions list",
        ["/bots/:id/persona"] = "persona show",
        ["/bots/:id/versions"] = "versions list",
        ["/bots/:id/versions/:number"] = "versions show",
        ["/bots/:id/history"] = "history",
        ["/bots/:id/procedures/:procedure/edit"] = "procedures show",
        ["/bots/:id/procedures/:procedure"] = "procedures show",
        ["/bots"] = "bots list",
        ["/"] = "bots list",
        ["/:id"] = "bots declaration",
    };

    [GeneratedRegex("""<Route path="([^"]+)"[ >]""", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RoutePath();

    private static bool RefusesOpen(string words)
    {
        var parsed = CliApp.Create(CliEnvironment.Real()).Parse($"{words} --open");
        return parsed.Errors.Any(error => error.Message.Contains("no web page to open", StringComparison.Ordinal));
    }

    private static List<string> RoutedScreens()
    {
        var source = Path.Combine(RepositoryRoot.Path, "Suwagcms.Web", "src");
        return
        [
            .. new[] { "App.tsx", Path.Combine("bots", "BotsRoutes.tsx") }.SelectMany(file =>
                RoutePath().Matches(File.ReadAllText(Path.Combine(source, file))).Select(match => match.Groups[1].Value)
            ),
        ];
    }

    private static List<string> Undecided(IEnumerable<string> commands, Dictionary<string, string> noPage, Func<string, bool> refuses) =>
        [.. commands.Where(words => refuses(words) != noPage.ContainsKey(words))];

    [Fact]
    [Trait("criterion", "AC-cli-foundation-18")]
    public void Every_command_opens_a_page_or_stands_in_the_short_list_with_its_reason()
    {
        var undecided = Undecided(CommandList.RunnableInCli(), NoPage, RefusesOpen);

        Assert.True(
            undecided.Count == 0,
            $"--open and the list disagree for: {string.Join(", ", undecided)}. A command opens its page, or refuses --open and stands in NoPage in "
                + "Suwagcms.Cli.Tests/CommandLine/OpenParityTests.cs with the reason."
        );
        Assert.Empty(NoPage.Keys.Except(CommandList.RunnableInCli()));
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-18")]
    public void A_command_that_refuses_open_with_no_reason_or_opens_with_one_is_reported()
    {
        var reasons = new Dictionary<string, string> { ["b"] = "why" };

        Assert.Equal(["a", "b"], Undecided(["a", "b", "c"], reasons, words => words == "a"));
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-18")]
    public void Every_screen_of_the_web_app_names_a_command_that_opens_it_or_a_reason()
    {
        var routed = RoutedScreens();
        var unknown = routed.Except(Screens.Keys).ToList();

        Assert.NotEmpty(routed);
        Assert.True(
            unknown.Count == 0,
            $"These screens have no command that opens them: {string.Join(", ", unknown)}. Give a command --open for each and list it in Screens in "
                + "Suwagcms.Cli.Tests/CommandLine/OpenParityTests.cs, or list the reason there."
        );
        Assert.Empty(Screens.Keys.Except(routed));
        foreach (var words in Screens.Values.Where(value => !value.StartsWith("no: ", StringComparison.Ordinal)))
        {
            Assert.Contains(words, CommandList.RunnableInCli());
            Assert.False(RefusesOpen(words), $"`suwagcms {words}` is named as opening a screen but refuses --open.");
        }
    }
}
