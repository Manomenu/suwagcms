using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Suwagcms.Contracts.Bots;
using Suwagcms.Contracts.Facts;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.BotRuntime.Versions;
using Suwagcms.Server.Tests.Facts;

namespace Suwagcms.Server.Tests.Versions;

public sealed partial class VersionPushTests(TestDatabase database, SharedApp shared) : IClassFixture<SharedApp>, IDisposable
{
    private TestApp App => shared.App;
    private readonly List<IDisposable> disposables = [];

    public void Dispose()
    {
        foreach (var item in disposables)
        {
            item.Dispose();
        }
    }

    [GeneratedRegex("\"digest\":\"[0-9a-f]+\"", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Digest();

    private HttpClient Client()
    {
        _ = database.Pool;
        return App.CreateClient();
    }

    private HttpClient ClientWithPushTimeout(int seconds)
    {
        _ = database.Pool;
        var shorter = App.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Push:PushTimeoutSeconds"] = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        }
                    )
            )
        );
        disposables.Add(shorter);
        return shorter.CreateClient();
    }

    private static async Task<(PushScene Scene, TestBot Bot)> FilledAsync(HttpClient client)
    {
        var id = PushScene.FreshId();
        var bot = await TestBot.StartAsync(id);
        var scene = await PushScene.RegisterAsync(client, id, bot.Address);
        await scene.FillAsync();
        return (scene, bot);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-1")]
    public async Task Pushing_the_latest_version_makes_the_bot_hold_it_and_the_status_shows_the_number_the_bot_reports()
    {
        var (scene, bot) = await FilledAsync(Client());
        await using var _ = bot;
        var version = await scene.CreateAsync();
        var before = await scene.StatusAsync();
        Assert.Equal((BotReach.Answered, null, 1), (before.Reach, before.Has, before.Latest));

        var result = await scene.PushAsync(version.Number);

        Assert.Equal(PushOutcome.Succeeded, result.Outcome);
        Assert.Equal(1, result.BotHas);
        Assert.Equal(1, (await bot.Store.ReadStateAsync(TestContext.Current.CancellationToken)).Current);
        var status = await scene.StatusAsync();
        Assert.Equal((BotReach.Answered, 1, 1), (status.Reach, status.Has, status.Latest));
    }

    [Fact]
    [Trait("criterion", "AC-version-push-1")]
    public async Task The_push_command_makes_the_bot_hold_the_latest_version_and_versions_status_shows_it()
    {
        var (scene, bot) = await FilledAsync(Client());
        await using var _ = bot;
        await scene.CreateAsync();

        var pushed = await scene.CliAsync("push");
        var status = await scene.CliAsync("versions", "status");

        Assert.Equal((0, $"Bot {scene.Bot} has v1."), (pushed.ExitCode, pushed.Output.Trim()));
        Assert.Equal(1, (await bot.Store.ReadStateAsync(TestContext.Current.CancellationToken)).Current);
        Assert.Contains($"Bot {scene.Bot} has v1, up to date.", status.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-2")]
    public async Task The_status_shows_the_version_the_bot_reports_when_it_was_given_another_one_directly()
    {
        var (scene, bot) = await FilledAsync(Client());
        await using var _ = bot;
        await scene.PushAsync((await scene.CreateAsync()).Number);
        await scene.ChangeAsync();
        var second = await scene.CreateAsync();
        var document =
            await scene.Client.GetFromJsonAsync<VersionDetail>($"/api/bots/{scene.Bot}/versions/2", TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("no version in the answer");

        Assert.Equal(HttpStatusCode.OK, await TestBot.PutDirectAsync(bot.Address, second.Number, document.Document));

        var status = await scene.StatusAsync();
        Assert.Equal(2, status.Has);
        Assert.Single(await scene.Attempts());
    }

    [Fact]
    [Trait("criterion", "AC-version-push-4")]
    public async Task An_unreachable_bot_shows_as_such_with_the_last_reported_number_and_a_push_fails_saying_why()
    {
        var (scene, bot) = await FilledAsync(Client());
        await using var _ = bot;
        await scene.PushAsync((await scene.CreateAsync()).Number);
        await scene.ChangeAsync();
        var second = await scene.CreateAsync();
        await bot.StopAsync();

        var status = await scene.StatusAsync();
        var result = await scene.PushAsync(second.Number);
        var cli = await scene.CliAsync("push");

        Assert.Equal((BotReach.Unreachable, null, 1), (status.Reach, status.Has, status.LastKnown));
        Assert.NotNull(status.LastKnownAt);
        Assert.Contains("cannot reach", status.Reason, StringComparison.Ordinal);
        Assert.Equal(PushOutcome.Failed, result.Outcome);
        Assert.Contains($"cannot reach {bot.Address}. The bot last reported v1, at ", result.Reason, StringComparison.Ordinal);
        Assert.Equal(1, cli.ExitCode);
        Assert.Contains("Push of v2 failed: cannot reach", cli.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-5")]
    public async Task A_bot_that_does_not_answer_in_time_fails_the_push_with_that_reason_and_keeps_what_it_had()
    {
        var client = ClientWithPushTimeout(1);
        var (scene, bot) = await FilledAsync(client);
        await using var _ = bot;
        await scene.PushAsync((await scene.CreateAsync()).Number);
        await scene.ChangeAsync();
        var second = await scene.CreateAsync();
        bot.OnPush = async context =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                // Why: the CMS gave up, which is what the test waits for.
            }
        };

        var result = await scene.PushAsync(second.Number);

        Assert.Equal(PushOutcome.Failed, result.Outcome);
        Assert.Contains("did not answer within 1 seconds", result.Reason, StringComparison.Ordinal);
        Assert.Equal(1, (await scene.StatusAsync()).Has);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-6")]
    public async Task A_version_the_bot_refuses_is_a_failed_push_with_the_bots_mistakes_and_the_shown_version_stays()
    {
        var (scene, bot) = await FilledAsync(Client());
        await using var _ = bot;
        await scene.PushAsync((await scene.CreateAsync()).Number);
        await scene.ChangeAsync();
        var second = await scene.CreateAsync();
        bot.OnPush = async context =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var tampered = Digest().Replace(await reader.ReadToEndAsync(), $"\"digest\":\"{new string('0', 64)}\"");
            context.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(tampered));
        };

        var result = await scene.PushAsync(second.Number);
        var cli = await scene.CliAsync("push");

        Assert.Equal(PushOutcome.Failed, result.Outcome);
        Assert.Contains("the bot refused v2", result.Reason, StringComparison.Ordinal);
        Assert.Contains(result.Mistakes, mistake => mistake.Code.Contains("digest", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, (await scene.StatusAsync()).Has);
        Assert.Equal(1, cli.ExitCode);
        Assert.Contains("Fix:", cli.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-7")]
    public async Task The_cms_shows_the_new_version_when_it_asks_either_of_two_instances_that_share_a_store()
    {
        var client = Client();
        var store = new InMemoryVersionStore();
        var id = PushScene.FreshId();
        await using var first = await TestBot.StartAsync(id, store);
        // Why: the second instance answers as another bot id, so the CMS can hold an address for each instance of the one store.
        var otherId = PushScene.FreshId();
        await using var second = await TestBot.StartAsync(otherId, store);
        var scene = await PushScene.RegisterAsync(client, id, first.Address);
        var other = await PushScene.RegisterAsync(client, otherId, second.Address);
        await scene.FillAsync();
        await scene.PushAsync((await scene.CreateAsync()).Number);

        Assert.Equal(1, (await scene.StatusAsync()).Has);
        Assert.Equal(1, (await other.StatusAsync()).Has);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-8")]
    public async Task A_bot_answering_with_another_bots_id_is_shown_as_a_wrong_address_naming_it_and_a_push_fails()
    {
        var client = Client();
        await using var elsewhere = await TestBot.StartAsync(PushScene.FreshId());
        var scene = await PushScene.RegisterAsync(client, PushScene.FreshId(), elsewhere.Address);
        await scene.FillAsync();
        await scene.CreateAsync();

        var status = await scene.StatusAsync();
        var result = await scene.PushAsync(1);

        Assert.Equal((BotReach.WrongAddress, elsewhere.Id, null), (status.Reach, status.OtherBot, status.Has));
        Assert.Equal(PushOutcome.Failed, result.Outcome);
        Assert.Null((await elsewhere.Store.ReadStateAsync(TestContext.Current.CancellationToken)).Current);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-9")]
    public async Task A_push_changes_neither_the_working_state_nor_any_version_whether_it_succeeds_or_fails()
    {
        var (scene, bot) = await FilledAsync(Client());
        await using var _ = bot;
        await scene.CreateAsync();
        var before = await scene.SnapshotAsync();

        await scene.PushAsync(1);
        var afterSuccess = await scene.SnapshotAsync();
        await bot.StopAsync();
        await scene.PushAsync(1);
        var afterFailure = await scene.SnapshotAsync();

        Assert.Equal(before, afterSuccess);
        Assert.Equal(before, afterFailure);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-11")]
    public async Task A_save_during_a_push_that_waits_on_a_slow_bot_completes_without_waiting_for_it()
    {
        var (scene, bot) = await FilledAsync(Client());
        await using var _ = bot;
        var version = await scene.CreateAsync();
        var arrived = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        bot.OnPush = async _ =>
        {
            arrived.TrySetResult();
            await release.Task;
        };

        var pushing = scene.PushAsync(version.Number);
        await arrived.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        var saved = await scene.Facts.Add("late.fact", FactApi.Text());
        var created = await scene.Client.PostAsync($"/api/bots/{scene.Bot}/versions", null, TestContext.Current.CancellationToken);

        Assert.True(saved.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.False(pushing.IsCompleted);
        release.SetResult();
        Assert.Equal(PushOutcome.Succeeded, (await pushing).Outcome);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-12")]
    public async Task Every_attempt_is_recorded_with_its_time_version_result_and_reason_newest_first()
    {
        var (scene, bot) = await FilledAsync(Client());
        await using var _ = bot;
        await scene.PushAsync((await scene.CreateAsync()).Number);
        await scene.ChangeAsync();
        var second = await scene.CreateAsync();
        await bot.StopAsync();
        await scene.PushAsync(second.Number);

        var attempts = (await scene.StatusAsync()).Attempts;
        var listed = await scene.CliAsync("versions", "status");

        Assert.Equal([(2, PushOutcome.Failed), (1, PushOutcome.Succeeded)], attempts.Select(attempt => (attempt.Version, attempt.Outcome)));
        Assert.Contains("cannot reach", attempts[0].Error, StringComparison.Ordinal);
        Assert.Null(attempts[1].Error);
        Assert.All(
            attempts,
            attempt =>
                Assert.InRange(attempt.At, TimeProvider.System.GetUtcNow().AddMinutes(-5), TimeProvider.System.GetUtcNow().AddMinutes(1))
        );
        Assert.Contains("  v2  failed  ", listed.Output, StringComparison.Ordinal);
        Assert.Contains("  v1  succeeded", listed.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-13")]
    public async Task The_versions_a_bot_still_holds_for_conversations_in_use_are_listed()
    {
        var (scene, bot) = await FilledAsync(Client());
        await using var _ = bot;
        await scene.PushAsync((await scene.CreateAsync()).Number);
        await bot.Definitions.ReportInUseAsync([1], TestContext.Current.CancellationToken);
        await scene.ChangeAsync();
        await scene.PushAsync((await scene.CreateAsync()).Number);

        var status = await scene.StatusAsync();
        var listed = await scene.CliAsync("versions", "status");

        Assert.Equal([1, 2], status.Held);
        Assert.Equal([1], status.InUse);
        Assert.Equal(2, status.Has);
        Assert.Contains("Still in use by conversations: v1", listed.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-16")]
    public async Task From_a_real_server_and_a_test_bot_a_pushed_version_is_what_the_bot_reports_and_a_stopped_bot_fails_visibly_and_returns_with_the_old_version()
    {
        var (scene, bot) = await FilledAsync(Client());
        await using var _ = bot;

        var deletedUsed = await scene.CliAsync("articles", "delete", "cheaper");
        var closedLoop = await scene.CliAsync("articles", "edit", "cheaper", "--content", "See [prices](cms://article/priceList).");

        Assert.Equal(1, deletedUsed.ExitCode);
        Assert.Contains("priceList", deletedUsed.Error, StringComparison.Ordinal);
        Assert.Equal(1, closedLoop.ExitCode);
        Assert.Contains("cheaper → priceList → cheaper", closedLoop.Error, StringComparison.Ordinal);

        var created = await scene.CliAsync("versions", "create");
        var pushed = await scene.CliAsync("push");
        var afterPush = await scene.CliAsync("versions", "status", "--json");

        Assert.Equal(0, created.ExitCode);
        Assert.Equal((0, $"Bot {scene.Bot} has v1."), (pushed.ExitCode, pushed.Output.Trim()));
        Assert.Contains($"\"current\":1", await TestBot.StateAsync(bot.Address), StringComparison.Ordinal);
        Assert.Contains("\"has\": 1", afterPush.Output, StringComparison.Ordinal);

        await scene.ChangeAsync();
        Assert.Equal(0, (await scene.CliAsync("versions", "create")).ExitCode);
        await bot.StopAsync();
        var failed = await scene.CliAsync("push");
        var whileDown = await scene.CliAsync("versions", "status");

        Assert.Equal(1, failed.ExitCode);
        Assert.Contains("Push of v2 failed: cannot reach", failed.Error, StringComparison.Ordinal);
        Assert.Contains("The bot last reported v1", failed.Error, StringComparison.Ordinal);
        Assert.Contains("cannot be reached", whileDown.Output, StringComparison.Ordinal);
        Assert.Contains("Last known: v1", whileDown.Output, StringComparison.Ordinal);

        await bot.StartAsync();
        var back = await scene.CliAsync("versions", "status");

        Assert.Contains("\"current\":1", await TestBot.StateAsync(bot.Address), StringComparison.Ordinal);
        Assert.Contains($"Bot {scene.Bot} has v1. Latest v2.", back.Output, StringComparison.Ordinal);
        var again = await scene.CliAsync("push");
        Assert.Equal((0, $"Bot {scene.Bot} has v2."), (again.ExitCode, again.Output.Trim()));
    }
}
