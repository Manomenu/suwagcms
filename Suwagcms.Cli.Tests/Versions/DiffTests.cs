using System.Net;

namespace Suwagcms.Cli.Tests.Versions;

public sealed class DiffTests
{
    private static readonly string[] OnBot = ["--server", "http://cms.internal", "--bot", "rzeczoznawca"];

    private const string Comparison = """
        {"bot":"rzeczoznawca","from":12,"to":14,"added":1,"removed":1,"changed":2,"definition":[],"resources":[
          {"kind":"GlobalFact","id":"customer.city","difference":"Changed","fields":[{"field":"maxLength","old":"80","new":"120","lines":null}],"parts":[]},
          {"kind":"Article","id":"cennik","difference":"Changed","fields":[{"field":"content","old":null,"new":null,"lines":[
            {"change":"Removed","text":"Wycena domu: 900 zł"},{"change":"Added","text":"Wycena domu: 1100 zł"},{"change":"Folded","text":"8 unchanged lines"}]}],"parts":[]},
          {"kind":"Article","id":"rabaty","difference":"Added","fields":[],"parts":[]},
          {"kind":"Procedure","id":"orderValuation","difference":"Changed","fields":[],"parts":[
            {"part":"step","name":"askCity","difference":"Changed","fields":[{"field":"say","old":null,"new":"Hi","lines":null}]},
            {"part":"step","name":"askStreet","difference":"Added","fields":[]}]},
          {"kind":"Article","id":"stary-cennik","difference":"Removed","fields":[],"parts":[]}]}
        """;

    [Fact]
    [Trait("criterion", "AC-version-diff-1")]
    [Trait("criterion", "AC-version-diff-2")]
    [Trait("criterion", "AC-version-diff-3")]
    public async Task It_prints_each_resource_with_what_changed_in_it()
    {
        var server = StubServer.Answering(HttpStatusCode.OK, Comparison);

        var result = await TestCli.Run(["diff", "12", "14", .. OnBot], server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://cms.internal/api/bots/rzeczoznawca/versions/12/diff/14", Assert.Single(server.Requests).ToString());
        Assert.Equal(
            [
                "From v12 to v14, bot rzeczoznawca: 1 added, 1 removed, 2 changed",
                "  fact customer.city changed",
                "    maxLength 80 → 120",
                "  article cennik changed",
                "    content",
                "      - Wycena domu: 900 zł",
                "      + Wycena domu: 1100 zł",
                "      … 8 unchanged lines",
                "  article rabaty added",
                "  procedure orderValuation changed",
                "    step 'askCity' changed",
                "      say (none) → Hi",
                "    step 'askStreet' added",
                "  article stary-cennik removed",
            ],
            result.Output.ReplaceLineEndings("\n").TrimEnd().Split('\n')
        );
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-11")]
    public async Task Open_opens_the_matching_comparison_and_refuses_json()
    {
        Uri? opened = null;
        var open = await TestCli.Run(["diff", "12", "14", "--open", .. OnBot], openBrowser: page => opened = page);
        var both = await TestCli.Run(["diff", "12", "14", "--open", "--json", .. OnBot]);

        Assert.Equal(0, open.ExitCode);
        Assert.Equal("http://cms.internal/bots/rzeczoznawca/versions?from=12&to=14", opened?.ToString());
        Assert.Equal(2, both.ExitCode);
    }

    [Fact]
    public async Task A_number_that_is_not_a_number_is_a_wrong_use()
    {
        var result = await TestCli.Run(["diff", "one", "14", .. OnBot]);

        Assert.Equal(2, result.ExitCode);
    }
}
