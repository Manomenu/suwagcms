using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Suwagcms.Contracts.History;
using Suwagcms.Contracts.Versions;
using Suwagcms.Server.Tests.Articles;
using Suwagcms.Server.Tests.Changes;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Versions;

namespace Suwagcms.Server.Tests.History;

public sealed class VersionWindowTests(TestDatabase database, ClassServer server) : IClassFixture<ClassServer>
{
    private TestApp App => server.App;

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private async Task<(VersionApi Versions, FactApi Facts, ArticleApi Articles, HistoryScene History)> NewBot()
    {
        _ = database.DataSource;
        var client = App.CreateClient();
        var versions = await (await VersionApi.NewBotAsync(client)).Ready();
        return (versions, new FactApi(client, versions.Bot), new ArticleApi(client, versions.Bot), new HistoryScene(client, versions.Bot));
    }

    private async Task<HistoryPage> Window(string bot, int number, string query = "")
    {
        var response = await App.CreateClient()
            .GetAsync($"/api/bots/{bot}/versions/{number}/window{query}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<HistoryPage>(Web, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("no window in the answer");
    }

    private async Task Execute(string sql, object parameters)
    {
        await using var connection = await database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: TestContext.Current.CancellationToken));
    }

    private static string Names(HistoryPage page) => string.Join(' ', page.Entries.Select(entry => $"{entry.Action}:{entry.ResourceId}"));

    [Fact]
    [Trait("criterion", "AC-change-history-27")]
    public async Task Every_entry_falls_in_one_window_by_the_order_it_was_recorded_and_entries_after_the_latest_in_none()
    {
        var (versions, facts, _, history) = await NewBot();
        var other = await (await VersionApi.NewBotAsync(App.CreateClient())).Ready();
        var before = (await history.OfBot("?limit=50")).Entries.Count;
        await versions.Created();
        await facts.Add("a.one", FactApi.Text());
        await facts.Add("a.two", FactApi.Text());
        await versions.Created();
        await facts.Add("b.one", FactApi.Text());
        await facts.Add("b.two", FactApi.Text());
        await facts.Add("b.three", FactApi.Text());
        await versions.Created();
        await facts.Add("c.after", FactApi.Text());
        await other.Created();
        await Execute("UPDATE change_entry SET at = now() WHERE bot_id = @bot", new { bot = versions.Bot });

        var (first, second, third) = (await Window(versions.Bot, 1), await Window(versions.Bot, 2), await Window(versions.Bot, 3));

        Assert.Equal(before + 1, first.Entries.Count);
        Assert.Equal(("created", "1"), (first.Entries[0].Action, first.Entries[0].ResourceId));
        Assert.Equal("created:2 added:a.two added:a.one", Names(second));
        Assert.Equal("created:3 added:b.three added:b.two added:b.one", Names(third));
        var all = first.Entries.Concat(second.Entries).Concat(third.Entries).Select(entry => entry.Id).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
        var everyEntry = (await history.OfBot("?limit=50")).Entries;
        Assert.Equal(everyEntry.Where(entry => entry.ResourceId != "c.after").Select(entry => entry.Id).Order(), all.Order());
        Assert.DoesNotContain(all, id => id == everyEntry[0].Id);
    }

    [Fact]
    [Trait("criterion", "AC-change-history-28")]
    public async Task A_push_of_an_older_version_leaves_every_window_and_falls_in_the_next_one()
    {
        _ = database.DataSource;
        var client = App.CreateClient();
        var scene = await PushScene.RegisterAsync(client, PushScene.FreshId(), "http://127.0.0.1:1");
        await scene.FillAsync();
        await scene.CreateAsync();
        await scene.ChangeAsync();
        await scene.CreateAsync();
        await scene.ChangeAsync();
        await scene.CreateAsync();
        var windows = new[] { Names(await Window(scene.Bot, 1)), Names(await Window(scene.Bot, 2)), Names(await Window(scene.Bot, 3)) };

        await scene.PushAsync(1);

        string[] after = [Names(await Window(scene.Bot, 1)), Names(await Window(scene.Bot, 2)), Names(await Window(scene.Bot, 3))];
        Assert.Equal(windows, after);
        await scene.ChangeAsync();
        await scene.CreateAsync();
        Assert.Contains(("pushed", "1"), (await Window(scene.Bot, 4)).Entries.Select(entry => (entry.Action, entry.ResourceId)));
    }

    [Fact]
    [Trait("criterion", "AC-change-history-29")]
    public async Task A_batch_stays_together_a_rename_is_one_entry_and_a_deleted_resource_holds_what_it_held_last()
    {
        var (versions, _, articles, _) = await NewBot();
        var client = App.CreateClient();
        await versions.Created();
        var changes = new ChangeScene(client, versions.Bot);
        (
            await changes.Post(
                ChangeScene.File(
                    ChangeScene.AddBase("oferta"),
                    ChangeScene.AddArticle("cennik", "oferta"),
                    ChangeScene.AddArticle("stary", "oferta")
                )
            )
        ).EnsureSuccessStatusCode();
        (
            await client.PostAsJsonAsync(
                $"/api/bots/{versions.Bot}/articles/cennik/rename",
                new { newId = "prices" },
                TestContext.Current.CancellationToken
            )
        ).EnsureSuccessStatusCode();
        (await articles.Delete("stary", "?ifRevision=1")).EnsureSuccessStatusCode();
        await versions.Created();

        var entries = (await Window(versions.Bot, 2)).Entries;

        var batch = entries.Where(entry => entry.Action == "added").ToList();
        Assert.Equal(3, batch.Count);
        Assert.Single(batch.Select(entry => entry.Batch).Distinct());
        Assert.NotNull(batch[0].Batch);
        Assert.Equal(
            ("renamed", "prices"),
            entries.Where(entry => entry.Action == "renamed").Select(entry => (entry.Action, entry.ResourceId)).Single()
        );
        var deleted = Assert.Single(entries, entry => entry.Action == "deleted");
        Assert.Equal("What is true.", HistoryScene.TextOf(Assert.Single(deleted.Changes, change => change.Field == "content"), old: true));
    }

    [Fact]
    [Trait("criterion", "AC-change-history-30")]
    public async Task A_window_that_opens_before_history_says_so_and_any_other_does_not()
    {
        var (versions, facts, _, _) = await NewBot();
        await versions.Created();
        await facts.Add("a.one", FactApi.Text());
        await versions.Created();
        await facts.Add("a.two", FactApi.Text());
        await versions.Created();
        Assert.False((await Window(versions.Bot, 1)).Incomplete, "a bot registered after history started has its beginning recorded");
        Assert.False((await Window(versions.Bot, 2)).Incomplete);

        await Execute("DELETE FROM change_entry WHERE bot_id = @bot AND kind = 'version' AND public_id = '1'", new { bot = versions.Bot });
        await Execute("UPDATE bot SET created_at = '2000-01-01' WHERE id = @bot", new { bot = versions.Bot });

        var older = await Window(versions.Bot, 1);
        Assert.True(older.Incomplete);
        Assert.Empty(older.Entries);
        var afterOlder = await Window(versions.Bot, 2);
        Assert.True(afterOlder.Incomplete);
        Assert.Contains(afterOlder.Entries, entry => entry.ResourceId == "a.one");
        Assert.False((await Window(versions.Bot, 3)).Incomplete);

        var (fresh, _, _, _) = await NewBot();
        await fresh.Created();
        await Execute("UPDATE bot SET created_at = '2000-01-01' WHERE id = @bot", new { bot = fresh.Bot });
        Assert.True(
            (await Window(fresh.Bot, 1)).Incomplete,
            "the first version on a bot older than history: its beginning was not recorded"
        );
    }

    [Fact]
    [Trait("criterion", "AC-change-history-31")]
    public async Task A_window_of_12_entries_at_a_page_size_of_5_gives_3_pages_with_none_missing_or_repeated()
    {
        var (versions, facts, _, _) = await NewBot();
        await versions.Created();
        for (var index = 0; index < 11; index++)
        {
            await facts.Add($"a.n{index}", FactApi.Text());
        }
        await versions.Created();

        var pages = new[]
        {
            await Window(versions.Bot, 2, "?limit=5&page=1"),
            await Window(versions.Bot, 2, "?limit=5&page=2"),
            await Window(versions.Bot, 2, "?limit=5&page=3"),
        };

        Assert.All(pages, page => Assert.Equal(12, page.Total));
        Assert.Equal([5, 5, 2], pages.Select(page => page.Entries.Count));
        var ids = pages.SelectMany(page => page.Entries).Select(entry => entry.Id).ToList();
        Assert.Equal(ids.OrderDescending(), ids);
        Assert.Equal(12, ids.Distinct().Count());
    }

    [Fact]
    [Trait("criterion", "AC-change-history-32")]
    public async Task A_price_changed_and_restored_is_two_entries_in_the_window_and_no_difference_in_the_comparison()
    {
        var (versions, _, articles, _) = await NewBot();
        await articles.KnowledgeBases.Add("offer", "Offer");
        await articles.Add("cennik", ArticleApi.Body(content: "Wycena domu: 900 zł"));
        await versions.Created();
        (await articles.Put("cennik", ArticleApi.Body(content: "Wycena domu: 1100 zł"))).EnsureSuccessStatusCode();
        (await articles.Put("cennik", ArticleApi.Body(content: "Wycena domu: 900 zł"))).EnsureSuccessStatusCode();
        await new FactApi(App.CreateClient(), versions.Bot).Add("a.other", FactApi.Text());
        await versions.Created();

        var edits = (await Window(versions.Bot, 2)).Entries.Where(entry => entry.ResourceId == "cennik").ToList();
        var comparison = await App.CreateClient()
            .GetFromJsonAsync<VersionComparison>($"/api/bots/{versions.Bot}/versions/1/diff/2", TestContext.Current.CancellationToken);

        Assert.Equal(["edited", "edited"], edits.Select(entry => entry.Action));
        Assert.DoesNotContain(comparison?.Resources ?? [], resource => resource.Id == "cennik");
    }

    [Fact]
    [Trait("criterion", "AC-change-history-21")]
    public async Task Given_the_same_old_and_new_text_an_entry_and_a_comparison_write_the_same_lines()
    {
        var (versions, facts, articles, _) = await NewBot();
        await articles.KnowledgeBases.Add("offer", "Offer");
        var lines = Enumerable.Range(0, 60).Select(index => $"line {index}").ToList();
        await articles.Add("cennik", ArticleApi.Body(content: string.Join('\n', lines)));
        await facts.Add("customer.size", FactApi.Text(length: 80));
        await versions.Created();
        lines[5] = "first edit";
        lines[45] = "second edit";
        (await articles.Put("cennik", ArticleApi.Body(content: string.Join('\n', lines)))).EnsureSuccessStatusCode();
        (await facts.Put("customer.size", FactApi.Text(length: 120))).EnsureSuccessStatusCode();
        await versions.Created();

        var entries = (await Window(versions.Bot, 2, "?limit=50&page=1")).Entries;
        var comparison = await App.CreateClient()
            .GetFromJsonAsync<VersionComparison>($"/api/bots/{versions.Bot}/versions/1/diff/2", TestContext.Current.CancellationToken);

        var written = Assert.Single(entries.Single(entry => entry.ResourceId == "cennik").Written?.Fields ?? []);
        var compared = Assert.Single(comparison?.Resources.Single(resource => resource.Id == "cennik").Fields ?? []);
        Assert.Equal(compared.Lines, written.Lines);
        Assert.Equal(2, written.Lines?.Count(line => line.Change == LineChange.Removed));
        Assert.Contains(written.Lines ?? [], line => line.Change == LineChange.Kept);
        Assert.Contains(written.Lines ?? [], line => line.Change == LineChange.Folded);

        var shortValue = Assert.Single(entries.Single(entry => entry.ResourceId == "customer.size").Written?.Fields ?? []);
        var comparedValue = Assert.Single(comparison?.Resources.Single(resource => resource.Id == "customer.size").Fields ?? []);
        Assert.Equal(comparedValue, shortValue);
        Assert.Equal(("maxLength", "80", "120"), (shortValue.Field, shortValue.Old, shortValue.New));
    }

    [Fact]
    [Trait("criterion", "AC-change-history-22")]
    public async Task An_entry_that_changes_a_procedures_steps_is_written_as_the_parts_a_comparison_writes()
    {
        var (versions, _, _, history) = await NewBot();
        const string Changed = """
            {"description":"The base of every conversation","firstStep":"wait",
             "steps":{"wait":{"listen":{},"next":"answer"},"answer":{"say":"Hi there","next":"thanks"},"thanks":{"say":"Thanks","next":"wait"}}}
            """;
        await versions.Created();
        (await versions.Procedures.Put("conversation", Changed)).EnsureSuccessStatusCode();
        await versions.Created();

        var entry = (await history.Of("procedures", "conversation", "?limit=5&page=1")).Entries[0];
        var comparison = await App.CreateClient()
            .GetFromJsonAsync<VersionComparison>($"/api/bots/{versions.Bot}/versions/1/diff/2", TestContext.Current.CancellationToken);

        Assert.Empty(entry.Written?.Fields ?? []);
        var answer = Assert.Single(entry.Written?.Parts ?? [], part => part.Name == "answer");
        Assert.Equal(("step", Difference.Changed), (answer.Part, answer.Difference));
        Assert.Equal(("next", "wait", "thanks"), (answer.Fields[0].Field, answer.Fields[0].Old, answer.Fields[0].New));
        var compared = comparison?.Resources.Single(resource => resource.Id == "conversation").Parts ?? [];
        Assert.Equal(
            compared.Select(part => (part.Part, part.Name, part.Difference)),
            (entry.Written?.Parts ?? []).Select(part => (part.Part, part.Name, part.Difference))
        );
        Assert.Equal(compared.Single(part => part.Name == "answer").Fields, answer.Fields);
        Assert.Contains(Assert.Single(entry.Written?.Parts ?? [], part => part.Name == "thanks").Fields, field => field.Field == "say");
    }

    [Fact]
    [Trait("criterion", "AC-change-history-23")]
    public async Task What_the_cli_reads_and_prints_stays_as_it_was_and_a_versions_own_history_is_not_its_window()
    {
        var (versions, facts, _, history) = await NewBot();
        await versions.Created();
        await facts.Add("a.one", FactApi.Text());
        await versions.Created();

        var run = await history.CliAsync("history", "--json");

        Assert.Equal(0, run.ExitCode);
        using var printed = JsonDocument.Parse(run.Output);
        var page = printed.RootElement;
        Assert.Equal(["entries", "since"], page.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal));
        string[] recorded = ["action", "at", "batch", "changes", "id", "kind", "resourceId", "revision"];
        foreach (var entry in page.GetProperty("entries").EnumerateArray())
        {
            Assert.All(entry.EnumerateObject(), member => Assert.Contains(member.Name, recorded));
            Assert.True(entry.TryGetProperty("changes", out _));
        }
        var text = await history.CliAsync("history", "fact", "a.one");
        Assert.Contains("added", text.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("→", text.Output, StringComparison.Ordinal);
        Assert.All((await history.OfBot()).Entries, entry => Assert.Null(entry.Written));

        var own = (await history.Of("versions", "2")).Entries;
        Assert.Equal("created:2", Names(new HistoryPage(own, null, default)));
        Assert.Null((await history.Of("versions", "2")).Incomplete);
    }

    [Fact]
    [Trait("criterion", "AC-change-history-33")]
    public async Task Before_goes_on_from_the_entry_the_last_page_named_through_the_whole_window()
    {
        var (versions, facts, _, history) = await NewBot();
        await versions.Created();
        await facts.Add("a.one", FactApi.Text());
        await facts.Add("a.two", FactApi.Text());
        await versions.Created();

        var seen = new List<string>();
        string[] more = [];
        for (var page = 0; page < 3; page++)
        {
            var run = await history.CliAsync(["versions", "window", "2", "--json", "--limit", "1", .. more]);
            Assert.Equal(0, run.ExitCode);
            using var printed = JsonDocument.Parse(run.Output);
            seen.Add(printed.RootElement.GetProperty("entries")[0].GetProperty("resourceId").GetString() ?? "");
            more = printed.RootElement.TryGetProperty("next", out var next)
                ? ["--before", next.GetInt64().ToString(CultureInfo.InvariantCulture)]
                : [];
        }

        Assert.Equal(["2", "a.two", "a.one"], seen);
        Assert.Empty(more);
    }

    [Fact]
    [Trait("criterion", "AC-change-history-33")]
    public async Task The_command_reads_the_window_and_a_number_the_bot_does_not_have_is_refused_naming_its_latest()
    {
        var (versions, facts, _, history) = await NewBot();
        await versions.Created();
        await facts.Add("a.one", FactApi.Text());
        await versions.Created();

        var run = await history.CliAsync("versions", "window", "2");
        var json = await history.CliAsync("versions", "window", "2", "--json", "--limit", "1");
        var missing = await history.CliAsync("versions", "window", "99");
        var response = await App.CreateClient()
            .GetAsync($"/api/bots/{versions.Bot}/versions/99/window", TestContext.Current.CancellationToken);
        var codes = await VersionApi.CodesOf(response);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("fact a.one", run.Output, StringComparison.Ordinal);
        Assert.Contains("version 2", run.Output, StringComparison.Ordinal);
        using var printed = JsonDocument.Parse(json.Output);
        Assert.False(printed.RootElement.GetProperty("incomplete").GetBoolean());
        Assert.Equal(1, printed.RootElement.GetProperty("entries").GetArrayLength());
        Assert.Equal(1, missing.ExitCode);
        Assert.Contains("its latest is 2", missing.Error, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(["version.missing"], codes);
    }
}
