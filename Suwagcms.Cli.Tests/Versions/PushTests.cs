using System.Net;
using System.Text;
using System.Text.Json;
using Suwagcms.Cli.Tests.Plugin;

namespace Suwagcms.Cli.Tests.Versions;

public sealed class PushTests
{
    private static readonly string[] OnBot = ["--server", "http://cms.internal", "--bot", "rzeczoznawca"];

    private static string Status(string reach = "Answered", int? has = 12, int? latest = 14, string? reason = null) =>
        $$"""
            {"bot":"rzeczoznawca","address":"http://rzeczoznawca.internal:8080","reach":"{{reach}}","has":{{Number(
                has
            )}},"held":[],"inUse":[],
             "lastKnown":12,"lastKnownAt":"2026-11-04T21:40:00+00:00","reason":{{(
                reason is null ? "null" : $"\"{reason}\""
            )}},"otherBot":null,
             "latest":{{Number(latest)}},"hasUnknownVersion":false,"attempts":[]}
            """;

    private static string Number(int? value) =>
        value is { } number ? number.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null";

    private const string Pushed = """{"outcome":"Succeeded","version":14,"botHas":14,"reason":null,"mistakes":[]}""";

    private const string Refused = """
        {"outcome":"Failed","version":14,"botHas":null,"reason":"the bot refused v14: 1 mistake(s) in the document.","mistakes":[
         {"code":"version.digest","stage":"Version","resource":{"kind":"Definition","id":""},"path":"digest","message":"The digest does not match.","fix":"Create the version again.","related":[]}]}
        """;

    private sealed class Recorder(string status, string? push)
    {
        public List<(string Method, string Url)> Calls { get; } = [];

        public StubServer Serving() =>
            new(request =>
            {
                var path = request.RequestUri?.PathAndQuery ?? "";
                Calls.Add((request.Method.Method, path));
                var body = path.EndsWith("/push", StringComparison.Ordinal) ? push ?? "{}" : status;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            });
    }

    private static string[] Args(params string[] words) => [.. words, .. OnBot];

    [Fact]
    [Trait("criterion", "AC-version-push-1")]
    public async Task Push_sends_the_latest_version_and_prints_the_version_the_bot_reports()
    {
        var recorder = new Recorder(Status(), Pushed);

        var result = await TestCli.Run(Args("push"), recorder.Serving());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Bot rzeczoznawca has v14.", result.Output.Trim());
        Assert.Equal(
            [("GET", "/api/bots/rzeczoznawca/versions/status"), ("POST", "/api/bots/rzeczoznawca/versions/14/push")],
            recorder.Calls
        );
    }

    [Fact]
    [Trait("criterion", "AC-version-push-1")]
    public async Task Versions_status_prints_what_the_bot_has_and_the_latest_version_with_the_command_to_push()
    {
        var result = await TestCli.Run(Args("versions", "status"), new Recorder(Status(), null).Serving());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Bot rzeczoznawca has v12. Latest v14.", result.Output, StringComparison.Ordinal);
        Assert.Contains("suwagcms push", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-4")]
    public async Task A_push_that_fails_prints_the_reason_to_stderr_and_exits_1()
    {
        const string failed =
            """{"outcome":"Failed","version":14,"botHas":null,"reason":"cannot reach http://rzeczoznawca.internal:8080. The bot last reported v12, at 2026-11-04 21:40 UTC. Check that the bot is running and that the CMS can reach it, then push again.","mistakes":[]}""";

        var result = await TestCli.Run(Args("push"), new Recorder(Status("Unreachable", null), failed).Serving());

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("", result.Output);
        Assert.StartsWith("Push of v14 failed: cannot reach http://rzeczoznawca.internal:8080.", result.Error, StringComparison.Ordinal);
        Assert.Contains("The bot last reported v12, at 2026-11-04 21:40 UTC.", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-4")]
    public async Task Versions_status_of_an_unreachable_bot_says_so_with_the_last_reported_version_and_its_time()
    {
        var result = await TestCli.Run(
            Args("versions", "status"),
            new Recorder(Status("Unreachable", null, reason: "cannot reach http://rzeczoznawca.internal:8080"), null).Serving()
        );

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("cannot be reached: cannot reach http://rzeczoznawca.internal:8080.", result.Output, StringComparison.Ordinal);
        Assert.Contains("Last known: v12, at 2026-11-04 21:40 UTC.", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-6")]
    public async Task A_version_the_bot_refuses_exits_1_with_every_mistake_and_its_fix()
    {
        var result = await TestCli.Run(Args("push"), new Recorder(Status(), Refused).Serving());

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Push of v14 failed: the bot refused v14", result.Error, StringComparison.Ordinal);
        Assert.Contains("version.digest The digest does not match.", result.Error, StringComparison.Ordinal);
        Assert.Contains("Fix: Create the version again.", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-8")]
    public async Task Versions_status_of_a_wrong_address_names_the_bot_that_answers()
    {
        var wrong = Status("WrongAddress", null).Replace("\"otherBot\":null", "\"otherBot\":\"sklep\"", StringComparison.Ordinal);

        var result = await TestCli.Run(Args("versions", "status"), new Recorder(wrong, null).Serving());

        Assert.Contains("answers as bot 'sklep'", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-10")]
    public async Task Push_when_the_bot_has_the_latest_version_says_so_sends_nothing_and_exits_0()
    {
        var recorder = new Recorder(Status(has: 14), Pushed);

        var result = await TestCli.Run(Args("push"), recorder.Serving());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Bot rzeczoznawca already has v14. Nothing was sent.", result.Output.Trim());
        Assert.Equal([("GET", "/api/bots/rzeczoznawca/versions/status")], recorder.Calls);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-10")]
    public async Task Push_with_no_version_exits_1_and_says_to_create_one_sending_nothing()
    {
        var recorder = new Recorder(Status(has: null, latest: null), Pushed);

        var result = await TestCli.Run(Args("push"), recorder.Serving());

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("suwagcms versions create", result.Error, StringComparison.Ordinal);
        Assert.Single(recorder.Calls);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-12")]
    public async Task Versions_status_lists_the_recent_pushes_with_time_version_result_and_reason()
    {
        var attempts = """
            "attempts":[{"id":"6f1d0a52-0000-4000-8000-000000000001","version":14,"outcome":"Failed","error":"cannot reach http://rzeczoznawca.internal:8080.","at":"2026-11-04T21:42:00+00:00"},
            {"id":"6f1d0a52-0000-4000-8000-000000000002","version":13,"outcome":"Succeeded","error":null,"at":"2026-11-02T09:20:00+00:00"}]
            """;

        var result = await TestCli.Run(
            Args("versions", "status"),
            new Recorder(Status().Replace("\"attempts\":[]", attempts, StringComparison.Ordinal), null).Serving()
        );

        Assert.Contains(
            "  2026-11-04 21:42  v14  failed  cannot reach http://rzeczoznawca.internal:8080.",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.Contains("  2026-11-02 09:20  v13  succeeded", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-13")]
    public async Task Versions_status_lists_the_versions_conversations_still_use_apart_from_the_current_one()
    {
        var result = await TestCli.Run(
            Args("versions", "status"),
            new Recorder(Status(has: 14).Replace("\"inUse\":[]", "\"inUse\":[11,14]", StringComparison.Ordinal), null).Serving()
        );

        Assert.Contains("Still in use by conversations: v11", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("v11, v14", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-14")]
    public async Task Push_asks_for_no_confirmation_and_json_gives_the_result_the_reason_and_the_version_the_bot_reports()
    {
        var pushed = await TestCli.Run([.. Args("push"), "--json"], new Recorder(Status(), Pushed).Serving());
        var failed = await TestCli.Run([.. Args("push"), "--json"], new Recorder(Status(), Refused).Serving());

        using var done = JsonDocument.Parse(pushed.Output);
        using var refused = JsonDocument.Parse(failed.Output);
        Assert.Equal(0, pushed.ExitCode);
        Assert.Equal(
            ("pushed", 14, 14),
            (
                done.RootElement.GetProperty("result").GetString(),
                done.RootElement.GetProperty("version").GetInt32(),
                done.RootElement.GetProperty("botHas").GetInt32()
            )
        );
        Assert.Equal(1, failed.ExitCode);
        Assert.Equal("failed", refused.RootElement.GetProperty("result").GetString());
        Assert.Contains("the bot refused v14", refused.RootElement.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Equal(1, refused.RootElement.GetProperty("mistakes").GetArrayLength());
    }

    [Fact]
    [Trait("criterion", "AC-version-push-14")]
    public async Task Push_takes_no_confirmation_option_and_no_version_option()
    {
        var result = await TestCli.Run(Args("push", "--version", "3"), new Recorder(Status(), Pushed).Serving());

        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-15")]
    public void The_main_skill_has_a_line_for_push_and_one_for_versions_status()
    {
        var skill = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills", "suwagcms", "SKILL.md"));

        var listed = CommandList.Listed(skill);

        Assert.Contains("push", listed);
        Assert.Contains("versions status", listed);
    }
}
