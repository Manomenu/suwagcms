using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Suwagcms.Contracts.Availability;
using Suwagcms.Contracts.Facts;
using Suwagcms.Contracts.Usage;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.BotRuntime.Availability;
using Suwagcms.Sdk.BotRuntime.Versions;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Server.Tests.Articles;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.History;
using Suwagcms.Server.Tests.Versions;

namespace Suwagcms.Server.Tests.Availability;

public sealed class AvailabilityTests(TestDatabase database, ClassServer server) : IClassFixture<ClassServer>, IDisposable
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private TestApp App => server.App;
    private readonly List<IDisposable> disposables = [];

    public void Dispose()
    {
        foreach (var item in disposables)
        {
            item.Dispose();
        }
    }

    private HttpClient Client()
    {
        _ = database.DataSource;
        return App.CreateClient();
    }

    private HttpClient ClientWithBotTimeout(int seconds)
    {
        _ = database.DataSource;
        var shorter = App.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Push:StateTimeoutSeconds"] = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        }
                    )
            )
        );
        disposables.Add(shorter);
        return shorter.CreateClient();
    }

    private Task<AvailabilityScene> SceneAsync() => AvailabilityScene.StartAsync(Client());

    private static async Task<List<string>> MistakeCodesOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return [.. body.GetProperty("mistakes").EnumerateArray().Select(mistake => mistake.GetProperty("code").GetString() ?? "")];
    }

    private static async Task<string> FixOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("mistakes")[0].GetProperty("fix").GetString() ?? "";
    }

    [Fact]
    [Trait("criterion", "AC-availability-1")]
    public async Task With_the_cli_an_article_turns_off_and_on_and_the_list_says_so_and_one_never_touched_is_on()
    {
        await using var scene = await SceneAsync();
        var listed = await scene.Push.CliAsync("availability", "list");
        Assert.Equal(0, listed.ExitCode);
        Assert.Contains($"article {AvailabilityScene.Used}: on", listed.Output, StringComparison.Ordinal);
        Assert.Contains("procedure orderValuation: on", listed.Output, StringComparison.Ordinal);

        Assert.Equal(0, (await scene.Push.CliAsync("availability", "off", "article", AvailabilityScene.Unused)).ExitCode);

        var off = await scene.Push.CliAsync("availability", "list");
        Assert.Contains($"article {AvailabilityScene.Unused}: off", off.Output, StringComparison.Ordinal);
        Assert.Contains($"article {AvailabilityScene.Used}: on", off.Output, StringComparison.Ordinal);
        Assert.Contains($"Bot {scene.Id} confirmed.", off.Output, StringComparison.Ordinal);

        Assert.Equal(0, (await scene.Push.CliAsync("availability", "on", "article", AvailabilityScene.Unused)).ExitCode);
        Assert.Contains(
            $"article {AvailabilityScene.Unused}: on",
            (await scene.Push.CliAsync("availability", "list")).Output,
            StringComparison.Ordinal
        );
    }

    [Fact]
    [Trait("criterion", "AC-availability-1")]
    public async Task A_bot_that_is_down_with_nothing_ever_turned_off_is_not_reported_as_missing_anything()
    {
        await using var scene = await SceneAsync();
        await scene.Bot.StopAsync();

        var view = await scene.View();

        Assert.Equal(AvailabilityDeliveryState.NotAsked, view.Delivery.State);
        Assert.All(view.Resources, item => Assert.False(item.Off));
    }

    [Fact]
    [Trait("criterion", "AC-availability-2")]
    public async Task The_list_before_turning_off_is_the_same_as_the_used_by_panel()
    {
        await using var scene = await SceneAsync();

        var answer = await scene.Turn("off", query: "?dryRun=true");

        var panel =
            await scene.Push.Client.GetFromJsonAsync<UsageView>(
                $"/api/bots/{scene.Id}/articles/{AvailabilityScene.Used}/usage",
                TestContext.Current.CancellationToken
            ) ?? throw new InvalidOperationException("no usage");
        Assert.NotEmpty(panel.UsedBy);
        Assert.Equal(panel.UsedBy, answer.UsedBy);
    }

    [Fact]
    [Trait("criterion", "AC-availability-3")]
    public async Task Without_yes_a_resource_in_use_stays_on_and_exit_is_1_with_yes_it_goes_off_and_unused_needs_no_yes()
    {
        await using var scene = await SceneAsync();

        var refused = await scene.Push.CliAsync("availability", "off", "article", AvailabilityScene.Used);

        Assert.Equal(1, refused.ExitCode);
        Assert.Contains("Used by:", refused.Error, StringComparison.Ordinal);
        Assert.Contains($"article {AvailabilityScene.Referrer}, content", refused.Error, StringComparison.Ordinal);
        Assert.Contains("--yes", refused.Error, StringComparison.Ordinal);
        Assert.False(await scene.IsOffInCms());
        Assert.Equal(0, (await scene.OnBot()).Number);

        var done = await scene.Push.CliAsync("availability", "off", "article", AvailabilityScene.Used, "--yes");

        Assert.Equal(0, done.ExitCode);
        Assert.Contains($"article {AvailabilityScene.Referrer}, content", done.Output, StringComparison.Ordinal);
        Assert.Contains($"Bot {scene.Id} confirmed.", done.Output, StringComparison.Ordinal);
        Assert.True(await scene.IsOffInCms());

        var unused = await scene.Push.CliAsync("availability", "off", "article", AvailabilityScene.Unused);
        Assert.Equal(0, unused.ExitCode);
        Assert.DoesNotContain("Used by", unused.Output, StringComparison.Ordinal);
        Assert.True(await scene.IsOffInCms(AvailabilityScene.Unused));
    }

    [Fact]
    [Trait("criterion", "AC-availability-4")]
    public async Task A_resource_in_use_turns_off_and_no_reference_to_it_is_refused_or_broken()
    {
        await using var scene = await SceneAsync();
        var dryOn = await scene.Push.Client.PostAsync(
            $"/api/bots/{scene.Id}/versions?dryRun=true",
            null,
            TestContext.Current.CancellationToken
        );
        var answerOn = await dryOn.Content.ReadFromJsonAsync<VersionCreated>(Web, TestContext.Current.CancellationToken);

        Assert.True((await scene.Turn("off")).Off);

        var dryOff = await scene.Push.Client.PostAsync(
            $"/api/bots/{scene.Id}/versions?dryRun=true",
            null,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.OK, dryOn.StatusCode);
        Assert.Equal(dryOn.StatusCode, dryOff.StatusCode);
        var answerOff = await dryOff.Content.ReadFromJsonAsync<VersionCreated>(Web, TestContext.Current.CancellationToken);
        Assert.NotNull(answerOn);
        Assert.NotNull(answerOff);
        Assert.False(answerOn.Written);
        Assert.Equal(answerOn.Version with { CreatedAt = default }, answerOff.Version with { CreatedAt = default });
        Assert.Equal(answerOn.Written, answerOff.Written);
        var referrer = await scene.Articles.Get(AvailabilityScene.Referrer);
        var revision = (await ArticleApi.ViewOf(referrer)).Revision;
        var saved = await scene.Articles.Put(
            AvailabilityScene.Referrer,
            ArticleApi.Body(title: "Price list again", content: $"Still [linked](cms://article/{AvailabilityScene.Used})."),
            $"?ifRevision={revision}"
        );
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var version = await scene.Push.CreateAsync();
        Assert.Equal(1, version.Number);
        Assert.Equal(PushOutcome.Succeeded, (await scene.Push.PushAsync(version.Number)).Outcome);
    }

    [Fact]
    [Trait("criterion", "AC-availability-5")]
    public async Task A_dry_run_prints_the_list_and_what_would_change_and_changes_nothing_in_the_cms_or_on_the_bot()
    {
        await using var scene = await SceneAsync();
        var requestsBefore = scene.Bot.Requests;

        var dry = await scene.Push.CliAsync("availability", "off", "article", AvailabilityScene.Used, "--dry-run");

        Assert.Equal(0, dry.ExitCode);
        Assert.Contains($"article {AvailabilityScene.Referrer}, content", dry.Output, StringComparison.Ordinal);
        Assert.Contains($"Would turn article {AvailabilityScene.Used} off.", dry.Output, StringComparison.Ordinal);
        Assert.Contains("Nothing was changed.", dry.Output, StringComparison.Ordinal);
        Assert.Equal(requestsBefore, scene.Bot.Requests);
        var view = await scene.View(ask: false);
        Assert.Equal(0, view.Number);
        Assert.False(await scene.IsOffInCms());
        Assert.Equal(0, (await scene.OnBot()).Number);
    }

    [Fact]
    [Trait("criterion", "AC-availability-6")]
    public async Task Turning_on_asks_nothing_lists_nothing_and_takes_effect_the_same_way()
    {
        await using var scene = await SceneAsync();
        await scene.Turn("off");

        var on = await scene.Push.CliAsync("availability", "on", "article", AvailabilityScene.Used);

        Assert.Equal(0, on.ExitCode);
        Assert.DoesNotContain("Used by", on.Output, StringComparison.Ordinal);
        Assert.Contains($"Bot {scene.Id} confirmed.", on.Output, StringComparison.Ordinal);
        Assert.False(await scene.IsOffInCms());
        var onBot = await scene.OnBot();
        Assert.Equal(2, onBot.Number);
        Assert.Empty(onBot.Off);
    }

    [Fact]
    [Trait("criterion", "AC-availability-7")]
    [Trait("criterion", "AC-version-create-17")]
    public async Task Turning_off_makes_no_revision_an_open_edit_still_saves_and_a_version_finds_nothing_changed()
    {
        await using var scene = await SceneAsync();
        await scene.Push.CreateAsync();
        var read = await ArticleApi.ViewOf(await scene.Articles.Get(AvailabilityScene.Used));
        var snapshot = await scene.Push.SnapshotAsync();

        await scene.Turn("off");

        Assert.Equal(snapshot, await scene.Push.SnapshotAsync());
        var noChange = await scene.Push.Client.PostAsync($"/api/bots/{scene.Id}/versions", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noChange.StatusCode);
        Assert.Contains("version.unchanged", await FactApi.CodesOf(noChange));

        var saved = await scene.Articles.Put(
            AvailabilityScene.Used,
            ArticleApi.Body(title: "Cheaper", content: "Edited while off."),
            $"?ifRevision={read.Revision}"
        );
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.True(await scene.IsOffInCms());
    }

    [Theory]
    [InlineData("fact", "customer.city")]
    [InlineData("kb", "offer")]
    [InlineData("knowledgeBase", "offer")]
    [InlineData("nonsense", "x")]
    [Trait("criterion", "AC-availability-8")]
    public async Task A_kind_without_availability_is_refused_naming_what_can_be_turned_off(string kind, string id)
    {
        await using var scene = await SceneAsync();

        var response = await scene.TurnRaw(kind, id, "off");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(
            "a procedure or an action",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal
        );
        var cli = await scene.Push.CliAsync("availability", "off", kind, id);
        Assert.Equal(1, cli.ExitCode);
        Assert.Contains("availability.kind", cli.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-availability-8")]
    public async Task A_resource_that_does_not_exist_is_refused_naming_what_can_be_turned_off_and_an_unknown_bot_too()
    {
        await using var scene = await SceneAsync();

        var missing = await scene.TurnRaw("article", "noSuchArticle", "off");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("a procedure or an action", await FixOf(missing), StringComparison.Ordinal);
        var unknownBot = await scene.Push.Client.PostAsync(
            "/api/bots/noSuchBot/availability/article/x/off",
            null,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.NotFound, unknownBot.StatusCode);
        Assert.Equal(["bot.notFound"], await MistakeCodesOf(unknownBot));
    }

    [Fact]
    [Trait("criterion", "AC-availability-9")]
    public async Task A_change_reaches_a_running_bot_with_no_version_and_no_push_and_the_cms_says_confirmed_from_the_answer()
    {
        await using var scene = await SceneAsync();

        var answer = await scene.Turn("off");

        Assert.Equal(AvailabilityDeliveryState.Confirmed, answer.Delivery.State);
        Assert.Equal(1, answer.Delivery.BotNumber);
        var onBot = await scene.OnBot();
        Assert.Equal(1, onBot.Number);
        Assert.Equal([new OffResource(ResourceKind.Article, AvailabilityScene.Used)], onBot.Off);
        Assert.True(scene.Bot.Definitions.IsOff(1, ResourceKind.Article, AvailabilityScene.Used));
        Assert.Null((await scene.Bot.Store.ReadStateAsync(TestContext.Current.CancellationToken)).Current);
        Assert.Empty(await scene.Push.Attempts());
        Assert.Equal(AvailabilityDeliveryState.Confirmed, (await scene.View()).Delivery.State);
    }

    [Fact]
    [Trait("criterion", "AC-availability-10")]
    public async Task A_change_takes_effect_also_for_a_conversation_on_an_older_version_without_any_push()
    {
        await using var scene = await SceneAsync();
        await scene.Push.PushAsync((await scene.Push.CreateAsync()).Number);
        await scene.Push.ChangeAsync();
        await scene.Push.PushAsync((await scene.Push.CreateAsync()).Number);

        await scene.Turn("off");

        Assert.True(scene.Bot.Definitions.IsOff(1, ResourceKind.Article, AvailabilityScene.Used));
        Assert.True(scene.Bot.Definitions.IsOff(2, ResourceKind.Article, AvailabilityScene.Used));
        Assert.Equal(2, (await scene.Push.Attempts()).Count);
    }

    [Fact]
    [Trait("criterion", "AC-availability-14")]
    [Trait("criterion", "AC-availability-16")]
    public async Task After_a_missed_change_the_next_change_or_send_leaves_the_bot_with_the_whole_setting_and_the_cli_says_not_delivered()
    {
        await using var scene = await SceneAsync();
        await scene.Bot.StopAsync();

        var missed = await scene.Push.CliAsync("availability", "off", "article", AvailabilityScene.Unused, "--json");

        Assert.Equal(1, missed.ExitCode);
        using (var document = JsonDocument.Parse(missed.Output))
        {
            var delivery = document.RootElement.GetProperty("delivery");
            Assert.Equal("NotDelivered", delivery.GetProperty("state").GetString());
            Assert.Contains(
                $"cannot reach {scene.Bot.Address}",
                delivery.GetProperty("reason").GetString() ?? "",
                StringComparison.Ordinal
            );
        }
        Assert.True(await scene.IsOffInCms(AvailabilityScene.Unused));
        var text = await scene.Push.CliAsync("availability", "on", "article", AvailabilityScene.Unused);
        Assert.Equal(1, text.ExitCode);
        Assert.Contains("not delivered", text.Error, StringComparison.Ordinal);
        Assert.Contains("suwagcms availability send", text.Error, StringComparison.Ordinal);
        Assert.Equal(1, (await scene.Push.CliAsync("availability", "off", "article", AvailabilityScene.Unused)).ExitCode);
        Assert.Equal(1, (await scene.Push.CliAsync("availability", "send")).ExitCode);

        await scene.Bot.StartAsync();

        var sent = await scene.Push.CliAsync("availability", "send");
        Assert.Equal(0, sent.ExitCode);
        Assert.Contains($"Bot {scene.Id} confirmed.", sent.Output, StringComparison.Ordinal);
        Assert.Equal([new OffResource(ResourceKind.Article, AvailabilityScene.Unused)], (await scene.OnBot()).Off);

        await scene.Bot.StopAsync();
        await scene.Turn("off");
        await scene.Bot.StartAsync();
        await scene.Turn("off", id: "priceList");

        Assert.Equal(
            [AvailabilityScene.Used, "priceList", AvailabilityScene.Unused],
            (await scene.OnBot()).Off.Select(item => item.Id).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    [Trait("criterion", "AC-availability-32")]
    public async Task A_bot_that_missed_a_change_receives_the_whole_setting_when_it_next_answers_a_read_of_its_status()
    {
        await using var scene = await SceneAsync();
        await scene.Turn("off");
        await scene.Bot.StopAsync();
        Assert.Equal(AvailabilityDeliveryState.NotDelivered, (await scene.Turn("off", id: AvailabilityScene.Unused)).Delivery.State);
        await scene.Bot.StartAsync();
        Assert.Equal(1, (await scene.OnBot()).Number);

        var asked = await scene.Push.Client.GetAsync($"/api/bots/{scene.Id}/versions/status", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, asked.StatusCode);
        var onBot = await scene.OnBot();
        Assert.Equal(2, onBot.Number);
        Assert.Equal([AvailabilityScene.Used, AvailabilityScene.Unused], onBot.Off.Select(item => item.Id).Order(StringComparer.Ordinal));
        Assert.Equal(AvailabilityDeliveryState.Confirmed, (await scene.View()).Delivery.State);
    }

    [Fact]
    [Trait("criterion", "AC-availability-16")]
    public async Task With_a_bot_too_slow_the_change_stays_saved_and_the_reason_says_so()
    {
        await using var scene = await AvailabilityScene.StartAsync(ClientWithBotTimeout(1));
        using var release = new CancellationTokenSource();
        scene.Bot.OnPush = async context =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, release.Token);
            }
            catch (OperationCanceledException)
            {
                context.Abort();
            }
        };

        var answer = await scene.Turn("off");

        await release.CancelAsync();
        Assert.Equal(AvailabilityDeliveryState.NotDelivered, answer.Delivery.State);
        Assert.Contains("did not answer within 1 seconds", answer.Delivery.Reason, StringComparison.Ordinal);
        Assert.True(await scene.IsOffInCms());
    }

    [Fact]
    [Trait("criterion", "AC-availability-17")]
    public async Task A_bot_reporting_a_lower_number_shows_as_not_delivered_and_sending_repairs_it()
    {
        await using var scene = await SceneAsync();
        await scene.Turn("off");
        scene.Bot.OnPush = context =>
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Abort();
            return Task.CompletedTask;
        };
        await scene.Turn("on");
        scene.Bot.OnPush = null;

        var view = await scene.View();

        Assert.Equal(AvailabilityDeliveryState.NotDelivered, view.Delivery.State);
        Assert.Equal(1, view.Delivery.BotNumber);
        Assert.Contains("Send again", view.Delivery.Reason, StringComparison.Ordinal);
        Assert.Equal(AvailabilityDeliveryState.Confirmed, (await scene.Send()).State);
        Assert.Equal(AvailabilityDeliveryState.Confirmed, (await scene.View()).Delivery.State);
    }

    [Fact]
    [Trait("criterion", "AC-availability-17")]
    public async Task A_bot_with_a_replaced_store_shows_as_not_delivered()
    {
        var store = new InMemoryVersionStore();
        await using var scene = await AvailabilityScene.StartAsync(Client(), store);
        await scene.Turn("off");
        await scene.Bot.StopAsync();
        await using var replaced = await TestBot.StartAsync(scene.Id, new InMemoryVersionStore(), scene.Bot.Port);

        var view = await scene.View();

        Assert.Equal(AvailabilityDeliveryState.NotDelivered, view.Delivery.State);
        Assert.Equal(0, view.Delivery.BotNumber);
    }

    [Fact]
    [Trait("criterion", "AC-availability-17")]
    public async Task A_bot_on_an_older_sdk_shows_as_not_delivered_saying_to_update_its_sdk()
    {
        var id = PushScene.FreshId();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        await using var older = builder.Build();
        older.MapGet(
            "/suwagcms/state",
            () =>
                Results.Json(
                    new
                    {
                        bot = id,
                        current = (int?)null,
                        held = Array.Empty<int>(),
                        inUse = Array.Empty<int>(),
                        storeError = (string?)null,
                    }
                )
        );
        await older.StartAsync(TestContext.Current.CancellationToken);
        var client = Client();
        await PushScene.RegisterAsync(client, id, older.Urls.Single());
        var articles = new ArticleApi(client, id);
        Assert.True((await articles.KnowledgeBases.Add("offer", "Offer")).IsSuccessStatusCode);
        Assert.True((await articles.Add("cennik", ArticleApi.Body())).IsSuccessStatusCode);

        var turned = await client.PostAsync($"/api/bots/{id}/availability/article/cennik/off", null, TestContext.Current.CancellationToken);

        var answer = await turned.Content.ReadFromJsonAsync<AvailabilityAnswer>(Web, TestContext.Current.CancellationToken);
        Assert.Equal(AvailabilityDeliveryState.NotDelivered, answer?.Delivery.State);
        Assert.Contains("update Suwagcms.Sdk", answer?.Delivery.Reason, StringComparison.Ordinal);
        var view = await client.GetFromJsonAsync<AvailabilityView>(
            $"/api/bots/{id}/availability",
            Web,
            TestContext.Current.CancellationToken
        );
        Assert.Equal((AvailabilityDeliveryState.NotDelivered, 1, 0), (view?.Delivery.State, view?.Number, view?.Delivery.BotNumber));
        await older.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("criterion", "AC-availability-18")]
    public async Task A_save_of_the_same_bot_during_a_change_waiting_on_a_slow_bot_completes_without_waiting()
    {
        await using var scene = await SceneAsync();
        using var entered = new SemaphoreSlim(0);
        using var release = new SemaphoreSlim(0);
        scene.Bot.OnPush = async _ =>
        {
            entered.Release();
            await release.WaitAsync(TestContext.Current.CancellationToken);
        };

        var turning = scene.Turn("off");
        await entered.WaitAsync(TestContext.Current.CancellationToken);

        var saved = await scene.Articles.Add("whileWaiting", ArticleApi.Body(title: "While waiting", content: "Saved meanwhile."));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.False(turning.IsCompleted);

        release.Release();
        Assert.Equal(AvailabilityDeliveryState.Confirmed, (await turning).Delivery.State);
    }

    [Fact]
    [Trait("criterion", "AC-availability-30")]
    public async Task A_delete_and_a_change_of_one_resource_at_the_same_moment_run_one_after_the_other()
    {
        await using var scene = await SceneAsync();

        for (var round = 0; round < 5; round++)
        {
            var id = $"race{round}";
            Assert.True((await scene.Articles.Add(id, ArticleApi.Body(title: id, content: "Text."))).IsSuccessStatusCode);

            var turning = scene.TurnRaw("article", id, "off");
            var deleting = scene.Articles.Delete(id);
            var (turned, deleted) = (await turning, await deleting);

            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            Assert.True(turned.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound, turned.StatusCode.ToString());
            Assert.Null((await scene.View(ask: false)).Resources.SingleOrDefault(item => item.Id == id));
        }
    }

    [Fact]
    [Trait("criterion", "AC-availability-19")]
    public async Task A_deleted_resources_setting_goes_with_it_and_the_same_id_created_again_is_on_and_the_bot_learns_at_the_next_send()
    {
        await using var scene = await SceneAsync();
        await scene.Turn("off", id: AvailabilityScene.Unused);

        Assert.Equal(HttpStatusCode.NoContent, (await scene.Articles.Delete(AvailabilityScene.Unused)).StatusCode);

        var view = await scene.View();
        Assert.Equal(AvailabilityDeliveryState.NotDelivered, view.Delivery.State);
        Assert.Equal(2, view.Number);
        Assert.Equal(AvailabilityDeliveryState.Confirmed, (await scene.Send()).State);
        Assert.Empty((await scene.OnBot()).Off);
        Assert.True(
            (await scene.Articles.Add(AvailabilityScene.Unused, ArticleApi.Body(title: "Back", content: "Back."))).IsSuccessStatusCode
        );
        Assert.False(await scene.IsOffInCms(AvailabilityScene.Unused));
    }

    [Fact]
    [Trait("criterion", "AC-availability-22")]
    public async Task A_procedure_turns_off_the_sdk_says_so_while_its_definition_stays_available()
    {
        await using var scene = await SceneAsync();
        await scene.Push.PushAsync((await scene.Push.CreateAsync()).Number);

        var answer = await scene.Turn("off", id: "orderValuation", kind: "procedure");

        Assert.Equal(AvailabilityDeliveryState.Confirmed, answer.Delivery.State);
        Assert.Contains((await scene.View()).Resources, item => item is { Kind: "procedure", Id: "orderValuation", Off: true });
        Assert.True(scene.Bot.Definitions.IsOff(1, ResourceKind.Procedure, "orderValuation"));
        Assert.False(scene.Bot.Definitions.IsOff(1, ResourceKind.Article, "orderValuation"));
        var definition = await scene.Bot.Definitions.GetAsync(1, TestContext.Current.CancellationToken);
        Assert.Contains(definition.Procedures, procedure => procedure.Id == "orderValuation");
    }

    [Fact]
    [Trait("criterion", "AC-availability-23")]
    public async Task Turning_off_conversation_is_refused_with_the_reason()
    {
        await using var scene = await SceneAsync();

        var response = await scene.TurnRaw("procedure", Procedure.ConversationId, "off");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("availability.conversation", text, StringComparison.Ordinal);
        Assert.Contains("every conversation is based on it", text, StringComparison.Ordinal);
        Assert.Equal(0, (await scene.View(ask: false)).Number);
    }

    [Fact]
    [Trait("criterion", "AC-availability-24")]
    public async Task A_push_sends_the_whole_set_before_the_version_and_repairs_a_missed_change()
    {
        await using var scene = await SceneAsync();
        var version = await scene.Push.CreateAsync();
        await scene.Bot.StopAsync();
        await scene.Turn("off");
        await scene.Bot.StartAsync();
        Assert.Empty((await scene.OnBot()).Off);
        var order = new List<string>();
        scene.Bot.OnPush = context =>
        {
            lock (order)
            {
                order.Add(context.Request.Path.Value ?? "");
            }
            return Task.CompletedTask;
        };

        var pushed = await scene.Push.PushAsync(version.Number);

        Assert.Equal(PushOutcome.Succeeded, pushed.Outcome);
        Assert.Equal(["/suwagcms/availability", $"/suwagcms/versions/{version.Number}"], order);
        Assert.Equal([new OffResource(ResourceKind.Article, AvailabilityScene.Used)], (await scene.OnBot()).Off);
    }

    [Fact]
    [Trait("criterion", "AC-availability-24")]
    public async Task A_push_that_cannot_deliver_the_set_sends_no_version_and_says_why()
    {
        await using var scene = await SceneAsync();
        await scene.Turn("off");
        var version = await scene.Push.CreateAsync();
        scene.Bot.OnPush = context =>
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Abort();
            return Task.CompletedTask;
        };

        var pushed = await scene.Push.PushAsync(version.Number);

        Assert.Equal(PushOutcome.Failed, pushed.Outcome);
        Assert.Contains("what is turned off did not reach the bot", pushed.Reason, StringComparison.Ordinal);
        Assert.Null((await scene.Bot.Store.ReadStateAsync(TestContext.Current.CancellationToken)).Current);
    }

    [Fact]
    [Trait("criterion", "AC-availability-24")]
    public async Task A_push_to_a_bot_never_told_anything_sends_only_the_version()
    {
        await using var scene = await SceneAsync();
        var version = await scene.Push.CreateAsync();
        var order = new List<string>();
        scene.Bot.OnPush = context =>
        {
            lock (order)
            {
                order.Add(context.Request.Path.Value ?? "");
            }
            return Task.CompletedTask;
        };

        Assert.Equal(PushOutcome.Succeeded, (await scene.Push.PushAsync(version.Number)).Outcome);
        Assert.Equal([$"/suwagcms/versions/{version.Number}"], order);
    }

    private async Task<(AvailabilityScene Scene, int Older)> TwoHeldAsync()
    {
        var scene = await SceneAsync();
        var older = await scene.Push.CreateAsync();
        await scene.Push.PushAsync(older.Number);
        await scene.Push.ChangeAsync();
        await scene.Push.PushAsync((await scene.Push.CreateAsync()).Number);
        return (scene, older.Number);
    }

    private static List<string> Order(TestBot bot)
    {
        var order = new List<string>();
        bot.OnPush = context =>
        {
            lock (order)
            {
                order.Add(context.Request.Path.Value ?? "");
            }
            return Task.CompletedTask;
        };
        return order;
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-20")]
    public async Task A_rollback_to_a_held_version_delivers_the_whole_set_first_and_repairs_a_missed_change()
    {
        var (scene, older) = await TwoHeldAsync();
        await using var _ = scene;
        await scene.Bot.StopAsync();
        await scene.Turn("off");
        await scene.Bot.StartAsync();
        Assert.Empty((await scene.OnBot()).Off);
        var order = Order(scene.Bot);

        var pushed = await scene.Push.PushAsync(older);

        Assert.Equal(PushOutcome.Succeeded, pushed.Outcome);
        Assert.Equal(older, pushed.BotHas);
        Assert.Equal(["/suwagcms/availability", "/suwagcms/current"], order);
        Assert.Equal([new OffResource(ResourceKind.Article, AvailabilityScene.Used)], (await scene.OnBot()).Off);
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-20")]
    public async Task A_rollback_that_cannot_deliver_the_set_leaves_the_bot_on_its_version_and_says_why()
    {
        var (scene, older) = await TwoHeldAsync();
        await using var _ = scene;
        await scene.Turn("off");
        var newer = (await scene.Bot.Store.ReadStateAsync(TestContext.Current.CancellationToken)).Current;
        scene.Bot.OnPush = context =>
        {
            context.Abort();
            return Task.CompletedTask;
        };

        var pushed = await scene.Push.PushAsync(older);

        Assert.Equal(PushOutcome.Failed, pushed.Outcome);
        Assert.Contains("what is turned off did not reach the bot", pushed.Reason, StringComparison.Ordinal);
        Assert.Equal(newer, (await scene.Bot.Store.ReadStateAsync(TestContext.Current.CancellationToken)).Current);
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-20")]
    public async Task A_push_of_the_version_already_current_sends_nothing_not_even_the_set()
    {
        var (scene, _) = await TwoHeldAsync();
        await using var __ = scene;
        await scene.Turn("off");
        var current = (await scene.Bot.Store.ReadStateAsync(TestContext.Current.CancellationToken)).Current ?? 0;
        var order = Order(scene.Bot);

        var pushed = await scene.Push.PushAsync(current);

        Assert.Equal(PushOutcome.Succeeded, pushed.Outcome);
        Assert.Empty(order);
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-20")]
    public async Task A_setting_of_a_resource_the_chosen_version_does_not_hold_has_no_effect()
    {
        var (scene, older) = await TwoHeldAsync();
        await using var _ = scene;
        const string later = "laterArticle";
        Assert.True((await scene.Articles.Add(later, ArticleApi.Body(title: "Later", content: "Added later."))).IsSuccessStatusCode);
        await scene.Turn("off", later);

        var pushed = await scene.Push.PushAsync(older);

        Assert.Equal(PushOutcome.Succeeded, pushed.Outcome);
        Assert.Equal(older, pushed.BotHas);
        var articles = await scene.Bot.Definitions.GetArticlesAsync(older, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(articles, article => article.Id == later);
        Assert.DoesNotContain(articles, article => article.IsOff);
    }

    private static async Task<HttpResponseMessage> RenameAsync(AvailabilityScene scene, string id, string newId)
    {
        using var content = new StringContent(JsonSerializer.Serialize(new { newId }, Web), System.Text.Encoding.UTF8, "application/json");
        return await scene.Push.Client.PostAsync(
            $"/api/bots/{scene.Id}/articles/{id}/rename",
            content,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    [Trait("criterion", "AC-availability-25")]
    public async Task A_resource_off_stays_off_after_its_id_changes_also_on_a_version_holding_the_old_id()
    {
        await using var scene = await SceneAsync();
        await scene.Push.PushAsync((await scene.Push.CreateAsync()).Number);
        await scene.Turn("off");

        Assert.Equal(HttpStatusCode.OK, (await RenameAsync(scene, AvailabilityScene.Used, "renamedOne")).StatusCode);

        Assert.True(await scene.IsOffInCms("renamedOne"));
        Assert.Equal(AvailabilityDeliveryState.NotDelivered, (await scene.View()).Delivery.State);
        Assert.Equal(AvailabilityDeliveryState.Confirmed, (await scene.Send()).State);
        await scene.Push.PushAsync((await scene.Push.CreateAsync()).Number);
        Assert.True(scene.Bot.Definitions.IsOff(1, ResourceKind.Article, AvailabilityScene.Used));
        Assert.True(scene.Bot.Definitions.IsOff(1, ResourceKind.Article, "renamedOne"));
        Assert.True(scene.Bot.Definitions.IsOff(2, ResourceKind.Article, "renamedOne"));
        Assert.False(scene.Bot.Definitions.IsOff(2, ResourceKind.Article, AvailabilityScene.Used));
    }

    [Fact]
    [Trait("criterion", "AC-availability-25")]
    public async Task Turned_off_after_the_rename_it_reaches_a_version_holding_the_old_id_and_not_a_new_resource_with_that_id()
    {
        await using var scene = await SceneAsync();
        await scene.Push.PushAsync((await scene.Push.CreateAsync()).Number);
        Assert.Equal(HttpStatusCode.OK, (await RenameAsync(scene, AvailabilityScene.Used, "renamedOne")).StatusCode);
        var view = (await scene.View(ask: false)).Number;
        Assert.Equal(0, view);

        await scene.Turn("off", id: "renamedOne");

        Assert.True(scene.Bot.Definitions.IsOff(1, ResourceKind.Article, AvailabilityScene.Used));
        Assert.True(
            (
                await scene.Articles.Add(AvailabilityScene.Used, ArticleApi.Body(title: "New one", content: "A new resource."))
            ).IsSuccessStatusCode
        );
        await scene.Push.PushAsync((await scene.Push.CreateAsync()).Number);
        Assert.False(await scene.IsOffInCms(AvailabilityScene.Used));
        Assert.False(scene.Bot.Definitions.IsOff(2, ResourceKind.Article, AvailabilityScene.Used));
        Assert.True(scene.Bot.Definitions.IsOff(2, ResourceKind.Article, "renamedOne"));
    }

    [Fact]
    [Trait("criterion", "AC-availability-25")]
    public async Task A_rename_on_a_bot_with_no_version_remembers_nothing_since_no_version_can_hold_the_old_id()
    {
        await using var scene = await SceneAsync();
        Assert.Equal(HttpStatusCode.OK, (await RenameAsync(scene, AvailabilityScene.Used, "renamedOne")).StatusCode);

        await scene.Turn("off", id: "renamedOne");

        Assert.Equal([new OffResource(ResourceKind.Article, "renamedOne")], (await scene.OnBot()).Off);
        await using var connection = await database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        var rows = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT count(*) FROM resource_former_id f JOIN resource r ON r.id = f.resource_id WHERE r.bot_id = @bot",
                new { bot = scene.Id },
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(0, rows);
    }

    [Fact]
    [Trait("criterion", "AC-change-history-15")]
    public async Task Turning_on_or_off_is_recorded_as_a_change_outside_versions_with_no_revision_and_a_repeat_records_nothing()
    {
        await using var scene = await SceneAsync();
        var history = new HistoryScene(scene.Push.Client, scene.Id);

        await scene.Turn("off");
        await scene.Turn("off");
        await scene.Turn("on");

        var entries = (await history.Of("articles", AvailabilityScene.Used))
            .Entries.Where(entry => entry.Action.StartsWith("turned", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(["turned on", "turned off"], entries.Select(entry => entry.Action));
        Assert.All(entries, entry => Assert.Null(entry.Revision));
        var change = Assert.Single(entries[1].Changes);
        Assert.Equal("availability", change.Field);
        Assert.Equal("on", HistoryScene.TextOf(change, old: true));
        Assert.Equal("off", HistoryScene.TextOf(change, old: false));
    }

    [Fact]
    [Trait("criterion", "AC-version-create-17")]
    public async Task A_version_created_while_a_resource_is_off_holds_nothing_about_it_nor_does_its_export()
    {
        await using var scene = await SceneAsync();
        await scene.Turn("off");
        var version = await scene.Push.CreateAsync();

        foreach (var path in new[] { "", "/export" })
        {
            var text = await scene.Push.Client.GetStringAsync(
                $"/api/bots/{scene.Id}/versions/{version.Number}{path}",
                TestContext.Current.CancellationToken
            );
            Assert.DoesNotContain("availability", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(AvailabilityScene.Used, text, StringComparison.Ordinal);
        }
    }
}
