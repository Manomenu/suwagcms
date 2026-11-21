using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Suwagcms.Contracts.Facts;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Tests.History;

namespace Suwagcms.Server.Tests.Versions;

public sealed class VersionRollbackTests(TestDatabase database, ClassServer server) : IClassFixture<ClassServer>
{
    private TestApp App => server.App;

    private async Task<(PushScene Scene, TestBot Bot)> FilledAsync()
    {
        _ = database.DataSource;
        var client = App.CreateClient();
        var id = PushScene.FreshId();
        var bot = await TestBot.StartAsync(id);
        var scene = await PushScene.RegisterAsync(client, id, bot.Address);
        await scene.FillAsync();
        return (scene, bot);
    }

    private static async Task TwoHeldAsync(PushScene scene)
    {
        await scene.PushAsync((await scene.CreateAsync()).Number);
        await scene.ChangeAsync();
        await scene.PushAsync((await scene.CreateAsync()).Number);
    }

    private static async Task<string[]> HeldAsync(TestBot bot)
    {
        var state = await bot.Store.ReadStateAsync(TestContext.Current.CancellationToken);
        return [.. state.Held.Select(number => number.ToString(System.Globalization.CultureInfo.InvariantCulture))];
    }

    private static async Task<int?> CurrentAsync(TestBot bot) =>
        (await bot.Store.ReadStateAsync(TestContext.Current.CancellationToken)).Current;

    private static List<string> CountPuts(TestBot bot)
    {
        var paths = new List<string>();
        bot.OnPush = context =>
        {
            lock (paths)
            {
                paths.Add(context.Request.Path.Value ?? "");
            }
            return Task.CompletedTask;
        };
        return paths;
    }

    private static Func<HttpContext, Task> Answering(int status, string body) =>
        async context =>
        {
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(body, TestContext.Current.CancellationToken);
        };

    [Fact]
    [Trait("criterion", "AC-version-rollback-1")]
    public async Task A_version_the_bot_does_not_hold_is_sent_and_becomes_current_and_the_status_shows_it_with_the_latest()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await scene.PushAsync((await scene.CreateAsync()).Number);
        await bot.Definitions.ReportInUseAsync([], TestContext.Current.CancellationToken);
        await scene.ChangeAsync();
        await scene.PushAsync((await scene.CreateAsync()).Number);
        Assert.Equal(["2"], await HeldAsync(bot));
        var puts = CountPuts(bot);

        var pushed = await scene.CliAsync("push", "--version", "1");
        var status = await scene.CliAsync("versions", "status");

        Assert.Equal((0, $"Bot {scene.Bot} has v1. The latest version is v2."), (pushed.ExitCode, pushed.Output.Trim()));
        Assert.Equal(["/suwagcms/versions/1"], puts);
        Assert.Equal(1, await CurrentAsync(bot));
        Assert.Contains($"Bot {scene.Bot} has v1. Latest v2.", status.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-2")]
    public async Task A_version_the_bot_holds_and_does_not_run_becomes_current_without_the_document_being_sent()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await TwoHeldAsync(scene);
        var puts = CountPuts(bot);

        var pushed = await scene.CliAsync("push", "--version", "1");

        Assert.Equal((0, $"Bot {scene.Bot} has v1. The latest version is v2."), (pushed.ExitCode, pushed.Output.Trim()));
        Assert.Equal(["/suwagcms/current"], puts);
        Assert.Equal(1, await CurrentAsync(bot));
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-3")]
    public async Task A_bot_that_answers_with_success_but_names_another_version_is_a_failed_push_saying_which_it_reports()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await TwoHeldAsync(scene);
        bot.OnCurrent = Answering(200, $$"""{"bot":"{{scene.Bot}}","current":2,"held":[1,2],"inUse":[],"storeError":null}""");

        var result = await scene.PushAsync(1);
        var cli = await scene.CliAsync("push", "--version", "1");

        Assert.Equal((PushOutcome.Failed, 2), (result.Outcome, result.BotHas));
        Assert.Contains("reports v2 as current, not v1", result.Reason, StringComparison.Ordinal);
        Assert.Equal(1, cli.ExitCode);
        Assert.Contains("Push of v1 failed: the bot answered but reports v2 as current", cli.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-3")]
    public async Task A_sent_document_whose_answer_names_another_version_is_a_failed_push_too()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await scene.PushAsync((await scene.CreateAsync()).Number);
        await scene.ChangeAsync();
        var second = await scene.CreateAsync();
        bot.OnPush = Answering(200, $$"""{"bot":"{{scene.Bot}}","current":1,"held":[1],"inUse":[],"storeError":null}""");

        var result = await scene.PushAsync(second.Number);

        Assert.Equal(PushOutcome.Failed, result.Outcome);
        Assert.Contains("reports v1 as current, not v2", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-4")]
    public async Task After_a_rollback_pushing_the_latest_version_makes_it_current_again()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await TwoHeldAsync(scene);
        await scene.PushAsync(1);
        var puts = CountPuts(bot);

        var forward = await scene.CliAsync("push");

        Assert.Equal((0, $"Bot {scene.Bot} has v2."), (forward.ExitCode, forward.Output.Trim()));
        Assert.Equal(["/suwagcms/current"], puts);
        Assert.Equal(2, await CurrentAsync(bot));
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-5")]
    public async Task A_rollback_leaves_the_working_state_and_the_versions_as_they_were_also_with_a_resource_deleted_since()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await scene.ChangeAsync();
        await scene.PushAsync((await scene.CreateAsync()).Number);
        Assert.Equal(0, (await scene.CliAsync("facts", "delete", "extra.n1")).ExitCode);
        await scene.PushAsync((await scene.CreateAsync()).Number);
        var before = await scene.SnapshotAsync();

        Assert.Equal(PushOutcome.Succeeded, (await scene.PushAsync(1)).Outcome);

        Assert.Equal(before, await scene.SnapshotAsync());
        var running = await bot.Definitions.GetAsync(1, TestContext.Current.CancellationToken);
        Assert.Contains(running.Facts, fact => fact.Name == "extra.n1");
        await scene.ChangeAsync();
        Assert.Equal(3, (await scene.CreateAsync()).Number);
        Assert.Equal(1, await CurrentAsync(bot));
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-6")]
    public async Task After_a_rollback_a_new_conversation_gets_the_chosen_version_and_an_earlier_one_its_own()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await TwoHeldAsync(scene);
        var startedOn = bot.Definitions.CurrentNumber.GetValueOrDefault();
        var definition = await bot.Definitions.GetAsync(startedOn, TestContext.Current.CancellationToken);

        await scene.PushAsync(1);

        Assert.Equal((2, 1), (startedOn, bot.Definitions.CurrentNumber));
        Assert.Equal(definition, await bot.Definitions.GetAsync(startedOn, TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-9")]
    public async Task When_the_bot_no_longer_holds_the_version_the_cms_sends_the_document_and_it_becomes_current()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await TwoHeldAsync(scene);
        var puts = CountPuts(bot);
        bot.OnCurrent = _ => bot.Store.RemoveVersionAsync(1, TestContext.Current.CancellationToken);

        var result = await scene.PushAsync(1);

        Assert.Equal((PushOutcome.Succeeded, 1), (result.Outcome, result.BotHas));
        Assert.Equal(["/suwagcms/current", "/suwagcms/versions/1"], puts);
        Assert.Equal(1, await CurrentAsync(bot));
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-7")]
    public async Task The_digest_the_cms_names_is_that_of_the_held_document()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await TwoHeldAsync(scene);
        var sent = "";
        bot.OnCurrent = async context =>
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            sent = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        };
        var version =
            await scene.Client.GetFromJsonAsync<VersionDetail>($"/api/bots/{scene.Bot}/versions/1", TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("no version in the answer");

        Assert.Equal(PushOutcome.Succeeded, (await scene.PushAsync(1)).Outcome);

        Assert.Contains($"\"digest\":\"{version.Version.Sha256}\"", sent, StringComparison.Ordinal);
        Assert.Contains("\"number\":1", sent, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-11")]
    public async Task A_bot_without_the_route_fails_saying_to_upgrade_keeps_what_it_had_and_still_takes_a_version_it_does_not_hold()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await TwoHeldAsync(scene);
        bot.OnCurrent = Answering(404, "");

        var result = await scene.PushAsync(1);
        var cli = await scene.CliAsync("push", "--version", "1");

        Assert.Equal(PushOutcome.Failed, result.Outcome);
        Assert.Contains("Upgrade the bot's Suwagcms.Sdk", result.Reason, StringComparison.Ordinal);
        Assert.Equal(1, cli.ExitCode);
        Assert.Equal(2, await CurrentAsync(bot));

        await scene.ChangeAsync();
        var third = await scene.CreateAsync();
        Assert.Equal(PushOutcome.Succeeded, (await scene.PushAsync(third.Number)).Outcome);
        Assert.Equal(3, await CurrentAsync(bot));
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-12")]
    public async Task Pushing_the_current_version_sends_nothing_and_a_number_the_cms_lacks_exits_1_naming_kept_numbers()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await TwoHeldAsync(scene);
        var puts = CountPuts(bot);

        var same = await scene.CliAsync("push", "--version", "2");
        var viaRoute = await scene.PushAsync(2);
        var missing = await scene.CliAsync("push", "--version", "9");

        Assert.Equal((0, $"Bot {scene.Bot} already has v2. Nothing was sent."), (same.ExitCode, same.Output.Trim()));
        Assert.Equal((PushOutcome.Succeeded, 2), (viaRoute.Outcome, viaRoute.BotHas));
        Assert.Empty(puts);
        Assert.Equal(1, missing.ExitCode);
        Assert.Contains("the nearest it keeps: v1, v2.", missing.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-14")]
    public async Task A_dry_run_says_how_the_version_would_go_and_leaves_the_bot_and_the_attempts_as_they_were()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await TwoHeldAsync(scene);
        await scene.ChangeAsync();
        var third = await scene.CreateAsync();
        var puts = CountPuts(bot);
        var attempts = (await scene.Attempts()).Count;

        var makeCurrent = await scene.CliAsync("push", "--version", "1", "--dry-run");
        var nothing = await scene.CliAsync("push", "--version", "2", "--dry-run");
        var send = await scene.CliAsync("push", "--dry-run");

        Assert.Equal(0, makeCurrent.ExitCode);
        Assert.Contains("v1 would be made current without sending it", makeCurrent.Output, StringComparison.Ordinal);
        Assert.Equal(0, nothing.ExitCode);
        Assert.Contains("already has v2. Nothing would be sent.", nothing.Output, StringComparison.Ordinal);
        Assert.Equal(0, send.ExitCode);
        Assert.Contains($"v{third.Number} would be sent", send.Output, StringComparison.Ordinal);
        Assert.Empty(puts);
        Assert.Equal(2, await CurrentAsync(bot));
        Assert.Equal(attempts, (await scene.Attempts()).Count);
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-14")]
    public async Task A_dry_run_answers_with_the_same_refusal_a_push_would_get()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await TwoHeldAsync(scene);
        await bot.StopAsync();

        var run = await scene.CliAsync("push", "--version", "1", "--dry-run");

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("Push of v1 failed: cannot reach", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-12")]
    public async Task Every_attempt_at_a_rollback_is_recorded_and_listed_with_its_version()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await TwoHeldAsync(scene);
        await scene.PushAsync(1);
        await bot.StopAsync();
        await scene.PushAsync(2);

        var attempts = await scene.Attempts();

        Assert.Equal(
            [(2, PushOutcome.Failed), (1, PushOutcome.Succeeded), (2, PushOutcome.Succeeded), (1, PushOutcome.Succeeded)],
            attempts.Select(attempt => (attempt.Version, attempt.Outcome))
        );
    }

    [Fact]
    [Trait("criterion", "AC-version-push-17")]
    public async Task A_version_using_a_block_the_bot_no_longer_runs_is_refused_naming_it_and_the_bot_keeps_what_it_had()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await TwoHeldAsync(scene);
        var puts = CountPuts(bot);
        bot.OnDeclaration = Answering(
            200,
            $$"""{"bot":"{{scene.Bot}}","formatVersion":5,"blocks":["listen","say","assign"],"embeddingProfiles":["default"]}"""
        );

        var result = await scene.PushAsync(1);

        Assert.Equal(PushOutcome.Failed, result.Outcome);
        var mistake = Assert.Single(result.Mistakes);
        Assert.Equal(MistakeCode.DeclarationBlock, mistake.Code);
        Assert.Contains("collect", mistake.Message, StringComparison.Ordinal);
        Assert.Empty(puts);
        Assert.Equal(2, await CurrentAsync(bot));
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-18")]
    public async Task A_dry_run_lists_the_same_changes_as_the_comparison_of_what_the_bot_has_with_the_chosen_version()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await TwoHeldAsync(scene);

        var run = await scene.CliAsync("push", "--version", "1", "--dry-run");
        var diff = await scene.CliAsync("diff", "2", "1");
        var plan = await scene.Client.GetFromJsonAsync<PushPlan>(
            $"/api/bots/{scene.Bot}/versions/1/push-plan",
            TestContext.Current.CancellationToken
        );

        Assert.Contains(diff.Output.Trim(), run.Output, StringComparison.Ordinal);
        Assert.Equal((PushWay.MakeCurrent, 2, 1), (plan?.Way, plan?.BotHas, plan?.Changes?.Removed));
    }

    [Fact]
    [Trait("criterion", "AC-change-history-14")]
    public async Task A_rollback_is_recorded_with_the_version_it_left_and_the_version_it_chose()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await TwoHeldAsync(scene);

        await scene.PushAsync(1);

        var history = new HistoryScene(scene.Client, scene.Bot);
        var entries = (await history.Of("versions", "1")).Entries.Where(line => line.Action == "pushed").ToList();
        var moved = Assert.Single(entries[0].Changes, change => change.Field == "botVersion");
        Assert.Equal((2, 1), (moved.Old?.GetInt32(), moved.New?.GetInt32()));
    }

    [Fact]
    [Trait("criterion", "AC-change-history-14")]
    public async Task A_push_that_failed_records_no_move_of_the_bot()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await TwoHeldAsync(scene);
        bot.OnCurrent = Answering(404, "");

        await scene.PushAsync(1);

        var history = new HistoryScene(scene.Client, scene.Bot);
        var entries = (await history.Of("versions", "1")).Entries.Where(line => line.Action == "pushed").ToList();
        Assert.DoesNotContain(entries[0].Changes, change => change.Field == "botVersion");
    }
}
