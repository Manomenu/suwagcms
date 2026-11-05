using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Suwagcms.Contracts;
using Suwagcms.Server.Tests.Articles;
using Suwagcms.Server.Tests.Changes;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Procedures;
using Suwagcms.Server.Tests.Versions;

namespace Suwagcms.Server.Tests.History;

public sealed class HistoryTests(TestDatabase database) : IDisposable
{
    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    private async Task<(ArticleApi Articles, HistoryScene History)> NewBot()
    {
        _ = database.Pool;
        var client = app.CreateClient();
        var articles = await ArticleApi.NewBotAsync(client);
        return (articles, new HistoryScene(client, articles.Bot));
    }

    [Fact]
    [Trait("criterion", "AC-change-history-1")]
    public async Task Adding_editing_and_deleting_an_article_each_adds_one_entry_with_its_time_action_revision_and_changed_fields()
    {
        var (articles, history) = await NewBot();
        var before = TimeProvider.System.GetUtcNow().AddMinutes(-1);
        await articles.Add("cennik", ArticleApi.Body(content: "Wycena domu: 900 zł"));
        await articles.Put("cennik", ArticleApi.Body(content: "Wycena domu: 1100 zł"));
        await articles.Delete("cennik");

        var entries = (await history.Of("articles", "cennik")).Entries;

        Assert.Equal(["deleted", "edited", "added"], entries.Select(entry => entry.Action));
        Assert.All(entries, entry => Assert.InRange(entry.At, before, TimeProvider.System.GetUtcNow().AddMinutes(1)));
        Assert.All(entries, entry => Assert.Equal(("article", "cennik"), (entry.Kind, entry.ResourceId)));
        var (deleted, edited, added) = (entries[0], entries[1], entries[2]);
        Assert.Equal(2, edited.Revision);
        var content = Assert.Single(edited.Changes);
        Assert.Equal("content", content.Field);
        Assert.Equal("Wycena domu: 900 zł", HistoryScene.TextOf(content, old: true));
        Assert.Equal("Wycena domu: 1100 zł", HistoryScene.TextOf(content, old: false));
        Assert.Equal(1, added.Revision);
        Assert.Equal(
            ["content", "description", "knowledgeBase", "llmGuide", "title"],
            added.Changes.Select(change => change.Field).Order(StringComparer.Ordinal)
        );
        Assert.Null(deleted.Revision);
    }

    [Fact]
    [Trait("criterion", "AC-change-history-1")]
    public async Task A_knowledge_base_records_the_same_and_the_command_prints_it_with_old_and_new_values()
    {
        var (articles, history) = await NewBot();
        await articles.KnowledgeBases.Add("oferta", "Oferta");
        await articles.KnowledgeBases.Put("oferta", """{"title":"Nowa oferta"}""", "?ifRevision=1");

        var entries = (await history.Of("knowledge-bases", "oferta")).Entries;
        var run = await history.CliAsync("history", "kb", "oferta");

        Assert.Equal(["edited", "added"], entries.Select(entry => entry.Action));
        Assert.Equal(0, run.ExitCode);
        var lines = run.Output.ReplaceLineEndings("\n").Split('\n');
        Assert.Contains(
            lines,
            line => line.Contains("edited", StringComparison.Ordinal) && line.Contains("revision 2", StringComparison.Ordinal)
        );
        Assert.Contains("    - Oferta", lines);
        Assert.Contains("    + Nowa oferta", lines);
        Assert.Contains(
            lines,
            line => line.Contains("added", StringComparison.Ordinal) && line.Contains("revision 1", StringComparison.Ordinal)
        );
    }

    [Fact]
    [Trait("criterion", "AC-change-history-2")]
    public async Task A_refused_save_a_dry_run_and_a_save_with_no_real_change_add_no_entry()
    {
        var (articles, history) = await NewBot();
        await articles.Add("cennik", ArticleApi.Body());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await articles.Put("cennik", ArticleApi.Body(title: ""))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await articles.Put("cennik", ArticleApi.Body(title: "Other"), "?dryRun=true")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await articles.Delete("cennik", "?dryRun=true")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await articles.Put("cennik", ArticleApi.Body())).StatusCode);

        Assert.Equal("added", Assert.Single((await history.Of("articles", "cennik")).Entries).Action);
    }

    [Fact]
    [Trait("criterion", "AC-change-history-3")]
    public async Task When_an_entry_cannot_be_written_the_save_fails_too_and_the_resource_stays_as_it_was()
    {
        var (articles, history) = await NewBot();
        await articles.Add("cennik", ArticleApi.Body(title: "Before"));
        var trigger = "refuse_" + history.Bot;
        await using var connection = await database.Pool.OpenAsync(TestContext.Current.CancellationToken);
        await connection.ExecuteAsync(
            $$"""
            CREATE FUNCTION {{trigger}}() RETURNS trigger LANGUAGE plpgsql AS $body$ BEGIN RAISE EXCEPTION 'no entries today'; END $body$;
            CREATE TRIGGER {{trigger}} BEFORE INSERT ON change_entry FOR EACH ROW WHEN (NEW.bot_id = '{{history.Bot}}') EXECUTE FUNCTION {{trigger}}();
            """
        );
        try
        {
            var response = await articles.Put("cennik", ArticleApi.Body(title: "After"));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        finally
        {
            await connection.ExecuteAsync($"DROP TRIGGER {trigger} ON change_entry; DROP FUNCTION {trigger}();");
        }
        var stored = await ArticleApi.ViewOf(await articles.Get("cennik"));
        Assert.Equal(("Before", 1), (stored.Title, stored.Revision));
    }

    [Fact]
    [Trait("criterion", "AC-change-history-4")]
    public async Task A_deleted_resource_keeps_its_history_with_what_it_held_last_and_a_new_one_lists_both()
    {
        var (articles, history) = await NewBot();
        await articles.Add("cennik", ArticleApi.Body(content: "Pierwszy"));
        await articles.Delete("cennik");

        var afterDelete = (await history.Of("articles", "cennik")).Entries;
        Assert.Equal(["deleted", "added"], afterDelete.Select(entry => entry.Action));
        var held = afterDelete[0].Changes.Single(change => change.Field == "content");
        Assert.Equal("Pierwszy", HistoryScene.TextOf(held, old: true));
        Assert.Null(held.New);

        await articles.Add("cennik", ArticleApi.Body(content: "Drugi"));

        Assert.Equal(["added", "deleted", "added"], (await history.Of("articles", "cennik")).Entries.Select(entry => entry.Action));
    }

    [Fact]
    [Trait("criterion", "AC-change-history-5")]
    public void No_route_changes_or_deletes_an_entry_and_no_server_code_can()
    {
        _ = database.Pool;
        var routes = app
            .Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => (endpoint.RoutePattern.RawText ?? "").Contains("history", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(routes);
        foreach (var route in routes)
        {
            Assert.True(
                (route.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).SequenceEqual(["GET"]),
                $"{route.RoutePattern.RawText} does more than read: the history cannot be edited, so remove the route."
            );
        }

        var writers = Directory
            .GetFiles(Path.Combine(ServerSources(), "Suwagcms.Server"), "*.*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".sql", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file =>
                File.ReadAllText(file).Contains("UPDATE change_entry", StringComparison.OrdinalIgnoreCase)
                || File.ReadAllText(file).Contains("DELETE FROM change_entry", StringComparison.OrdinalIgnoreCase)
                || File.ReadAllText(file).Contains("TRUNCATE change_entry", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();
        Assert.True(
            writers.Count == 0,
            $"These files change the history, which cannot be edited: {string.Join(", ", writers)}. Remove the statement."
        );
    }

    private static string ServerSources()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Suwagcms.slnx")))
        {
            folder = folder.Parent;
        }
        return folder?.FullName ?? throw new InvalidOperationException("the repository root was not found above the test's folder");
    }

    [Fact]
    [Trait("criterion", "AC-change-history-6")]
    public async Task The_bots_history_lists_every_change_newest_first_one_page_at_a_time()
    {
        var (articles, history) = await NewBot();
        await articles.Add("alfa", ArticleApi.Body());
        await articles.Add("beta", ArticleApi.Body());
        await articles.Add("gamma", ArticleApi.Body());

        var all = await history.OfBot();
        var first = await history.OfBot("?limit=2");
        var second = await history.OfBot($"?limit=2&before={first.Next}");
        Assert.Equal(["gamma", "beta", "alfa"], all.Entries.Take(3).Select(entry => entry.ResourceId));
        Assert.Equal(["gamma", "beta"], first.Entries.Select(entry => entry.ResourceId));
        Assert.NotNull(first.Next);
        Assert.Equal("alfa", second.Entries[0].ResourceId);
        Assert.Null((await history.OfBot($"?before={all.Entries[^1].Id}")).Next);

        var run = await history.CliAsync("history", "--limit", "2");
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("gamma", run.Output, StringComparison.Ordinal);
        Assert.Contains("beta", run.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("alfa", run.Output, StringComparison.Ordinal);
        Assert.Contains($"--before {first.Next}", run.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-change-history-7")]
    public async Task An_entry_of_one_bot_is_not_listed_for_another_also_when_two_resources_share_an_id()
    {
        var (first, firstHistory) = await NewBot();
        var (second, secondHistory) = await NewBot();
        await first.Add("cennik", ArticleApi.Body(content: "Pierwszy"));
        await second.Add("cennik", ArticleApi.Body(content: "Drugi"));
        await first.Delete("cennik");

        Assert.Equal(["deleted", "added"], (await firstHistory.Of("articles", "cennik")).Entries.Select(entry => entry.Action));
        var entry = Assert.Single((await secondHistory.Of("articles", "cennik")).Entries);
        Assert.Equal("Drugi", HistoryScene.TextOf(entry.Changes.Single(change => change.Field == "content"), old: false));
        Assert.DoesNotContain((await secondHistory.OfBot()).Entries, line => line.Action == "deleted");
    }

    [Fact]
    [Trait("criterion", "AC-change-history-8")]
    public async Task A_resource_without_entries_says_no_change_has_been_recorded_since_history_started()
    {
        var (_, history) = await NewBot();

        var page = await history.Of("articles", "rabaty");
        var run = await history.CliAsync("history", "article", "rabaty");

        Assert.Empty(page.Entries);
        Assert.InRange(page.Since, TimeProvider.System.GetUtcNow().AddDays(-1), TimeProvider.System.GetUtcNow().AddMinutes(1));
        Assert.Equal(0, run.ExitCode);
        Assert.Contains($"No change has been recorded since {page.Since.UtcDateTime:yyyy-MM-dd}", run.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-change-history-9")]
    public async Task Json_returns_the_same_entries_as_the_api()
    {
        var (articles, history) = await NewBot();
        await articles.Add("cennik", ArticleApi.Body());
        var page = await history.Of("articles", "cennik");

        var run = await history.CliAsync("history", "article", "cennik", "--json");

        Assert.Equal(0, run.ExitCode);
        using var document = JsonDocument.Parse(run.Output);
        var entry = Assert.Single(document.RootElement.GetProperty("entries").EnumerateArray());
        Assert.Equal(page.Entries[0].Id, entry.GetProperty("id").GetInt64());
        Assert.Equal("added", entry.GetProperty("action").GetString());
        Assert.Equal(page.Entries[0].Changes.Count, entry.GetProperty("changes").GetArrayLength());
    }

    [Fact]
    [Trait("criterion", "AC-change-history-1")]
    public async Task History_covers_facts_and_procedures()
    {
        var (articles, history) = await NewBot();
        var facts = new FactApi(app.CreateClient(), articles.Bot);
        var procedures = new ProcedureApi(app.CreateClient(), facts);
        await facts.Add("customer.city", FactApi.Text("One"));
        await facts.Put("customer.city", FactApi.Text("Two"));
        await procedures.Add("conversation", ProcedureApi.Conversation);

        var fact = (await history.Of("facts", "customer.city")).Entries;
        var procedure = Assert.Single((await history.Of("procedures", "conversation")).Entries);

        Assert.Equal(["edited", "added"], fact.Select(entry => entry.Action));
        Assert.Equal("description", Assert.Single(fact[0].Changes).Field);
        Assert.Equal("fact", fact[0].Kind);
        Assert.Equal("procedure", procedure.Kind);
        Assert.Contains(procedure.Changes, change => change.Field == "steps");
    }

    [Fact]
    [Trait("criterion", "AC-change-history-11")]
    public async Task A_push_is_recorded_with_the_version_and_the_result()
    {
        _ = database.Pool;
        var client = app.CreateClient();
        var scene = await PushScene.RegisterAsync(client, PushScene.FreshId(), "http://127.0.0.1:1");
        await scene.FillAsync();
        var history = new HistoryScene(client, scene.Bot);
        Assert.True(
            (await client.PostAsync($"/api/bots/{scene.Bot}/versions", null, TestContext.Current.CancellationToken)).IsSuccessStatusCode
        );

        var push = await client.PostAsync($"/api/bots/{scene.Bot}/versions/1/push", null, TestContext.Current.CancellationToken);

        Assert.True(push.IsSuccessStatusCode, await push.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var entry = Assert.Single((await history.Of("versions", "1")).Entries, line => line.Action == "pushed");
        Assert.Equal(("version", "1"), (entry.Kind, entry.ResourceId));
        var result = Assert.Single(entry.Changes);
        Assert.Equal("result", result.Field);
        Assert.StartsWith("Failed: ", HistoryScene.TextOf(result, old: false), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-change-history-12")]
    public async Task Creating_a_version_is_recorded_and_a_dry_run_or_a_refusal_is_not()
    {
        _ = database.Pool;
        var client = app.CreateClient();
        var scene = await PushScene.RegisterAsync(client, PushScene.FreshId(), "http://bot.internal");
        var history = new HistoryScene(client, scene.Bot);

        Assert.False(
            (await client.PostAsync($"/api/bots/{scene.Bot}/versions", null, TestContext.Current.CancellationToken)).IsSuccessStatusCode
        );
        await scene.FillAsync();
        Assert.True(
            (
                await client.PostAsync($"/api/bots/{scene.Bot}/versions?dryRun=true", null, TestContext.Current.CancellationToken)
            ).IsSuccessStatusCode
        );
        Assert.Empty((await history.Of("versions", "1")).Entries);
        Assert.True(
            (await client.PostAsync($"/api/bots/{scene.Bot}/versions", null, TestContext.Current.CancellationToken)).IsSuccessStatusCode
        );

        var entry = Assert.Single((await history.Of("versions", "1")).Entries);
        Assert.Equal("created", entry.Action);
        Assert.Equal("created", (await history.OfBot()).Entries[0].Action);
    }

    [Fact]
    [Trait("criterion", "AC-change-history-13")]
    public async Task Each_resource_a_file_changes_gets_its_own_entry_and_the_entries_of_one_file_share_a_batch()
    {
        var (articles, history) = await NewBot();
        var scene = new ChangeScene(app.CreateClient(), articles.Bot);
        await articles.Add("single", ArticleApi.Body());
        Assert.Null((await history.Of("articles", "single")).Entries[0].Batch);

        await scene.Post(
            ChangeScene.File(
                ChangeScene.AddFact("order.first"),
                ChangeScene.AddFact("order.second"),
                """{"delete":"article","id":"single"}"""
            )
        );
        await scene.Post(ChangeScene.File(ChangeScene.AddFact("order.third"), ChangeScene.AddFact("order.fourth")));

        var first = (await history.Of("facts", "order.first")).Entries[0];
        var second = (await history.Of("facts", "order.second")).Entries[0];
        var deleted = (await history.Of("articles", "single")).Entries[0];
        var other = (await history.Of("facts", "order.third")).Entries[0];
        Assert.NotNull(first.Batch);
        Assert.Equal(first.Batch, second.Batch);
        Assert.Equal(first.Batch, deleted.Batch);
        Assert.NotEqual(first.Batch, other.Batch);
    }

    private async Task<HttpResponseMessage> Rename(string bot, string collection, string id, string newId) =>
        await app.CreateClient()
            .PostAsJsonAsync($"/api/bots/{bot}/{collection}/{id}/rename", new { newId }, TestContext.Current.CancellationToken);

    [Fact]
    [Trait("criterion", "AC-change-history-17")]
    public async Task A_rename_is_one_entry_for_the_resource_and_one_for_each_rewritten_referrer_in_one_batch()
    {
        var (articles, history) = await NewBot();
        await articles.Add("cennik", ArticleApi.Body());
        await articles.Add("wycena", ArticleApi.Body(content: "See [prices](cms://article/cennik)."));

        (await Rename(articles.Bot, "articles", "cennik", "priceList")).EnsureSuccessStatusCode();

        var entries = (await history.Of("articles", "priceList")).Entries;
        Assert.Equal(["renamed", "added"], entries.Select(entry => entry.Action));
        var renamed = entries[0];
        Assert.Equal(("article", "priceList", 2), (renamed.Kind, renamed.ResourceId, renamed.Revision));
        var id = Assert.Single(renamed.Changes);
        Assert.Equal(("id", "cennik", "priceList"), (id.Field, HistoryScene.TextOf(id, old: true), HistoryScene.TextOf(id, old: false)));
        var referrer = (await history.Of("articles", "wycena")).Entries[0];
        Assert.Equal(("edited", "content"), (referrer.Action, Assert.Single(referrer.Changes).Field));
        Assert.NotNull(renamed.Batch);
        Assert.Equal(renamed.Batch, referrer.Batch);
        Assert.DoesNotContain((await history.OfBot()).Entries, entry => entry.Action == "deleted");
    }

    [Fact]
    [Trait("criterion", "AC-change-history-17")]
    public async Task A_resource_is_found_under_either_id_also_when_another_takes_the_old_one()
    {
        var (articles, history) = await NewBot();
        await articles.Add("cennik", ArticleApi.Body());
        (await Rename(articles.Bot, "articles", "cennik", "priceList")).EnsureSuccessStatusCode();
        await articles.Add("cennik", ArticleApi.Body(content: "Another one."));

        var underNew = (await history.Of("articles", "priceList")).Entries;
        var underOld = (await history.Of("articles", "cennik")).Entries;

        Assert.Equal(["renamed", "added"], underNew.Select(entry => entry.Action));
        Assert.Equal(["added", "renamed", "added"], underOld.Select(entry => entry.Action));
    }

    [Fact]
    [Trait("criterion", "AC-change-history-17")]
    public async Task A_rename_of_a_fact_names_its_old_and_new_name()
    {
        var (articles, history) = await NewBot();
        var facts = new FactApi(app.CreateClient(), articles.Bot);
        await facts.Add("customer.city", FactApi.Text());

        (await Rename(articles.Bot, "facts", "customer.city", "client.town")).EnsureSuccessStatusCode();

        var change = Assert.Single((await history.Of("facts", "client.town")).Entries[0].Changes);
        Assert.Equal(
            ("name", "customer.city", "client.town"),
            (change.Field, HistoryScene.TextOf(change, old: true), HistoryScene.TextOf(change, old: false))
        );
    }
}
