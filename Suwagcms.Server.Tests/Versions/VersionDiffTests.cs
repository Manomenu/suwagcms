using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Suwagcms.Contracts.Facts;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Server.Tests.Articles;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.KnowledgeBases;
using Suwagcms.Server.Tests.Procedures;

namespace Suwagcms.Server.Tests.Versions;

public sealed class VersionDiffTests(TestDatabase database, ClassServer server) : IClassFixture<ClassServer>
{
    private TestApp App => server.App;

    private async Task<(PushScene Scene, TestBot Bot)> FilledAsync()
    {
        _ = database.Pool;
        var client = App.CreateClient();
        var id = PushScene.FreshId();
        var bot = await TestBot.StartAsync(id);
        var scene = await PushScene.RegisterAsync(client, id, bot.Address);
        await scene.FillAsync();
        return (scene, bot);
    }

    private static Task<HttpResponseMessage> GetAsync(PushScene scene, int from, int to) =>
        scene.Client.GetAsync($"/api/bots/{scene.Bot}/versions/{from}/diff/{to}", TestContext.Current.CancellationToken);

    private static async Task<VersionComparison> CompareAsync(PushScene scene, int from, int to)
    {
        var response = await GetAsync(scene, from, to);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<VersionComparison>(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("no comparison in the answer");
    }

    [Fact]
    [Trait("criterion", "AC-change-history-23")]
    public async Task What_diff_prints_for_a_step_added_and_one_removed_is_pinned_word_for_word()
    {
        _ = database.Pool;
        var versions = await (await VersionApi.NewBotAsync(App.CreateClient())).Ready();
        var scene = new PushScene(versions.Client, versions.Bot);
        await versions.Created();
        const string Changed = """
            {"description":"The base of every conversation","firstStep":"wait",
             "steps":{"wait":{"listen":{},"next":"reply"},"reply":{"say":"Hello","next":"wait"}}}
            """;
        (await versions.Procedures.Put("conversation", Changed)).EnsureSuccessStatusCode();
        await versions.Created();

        const string Json = """
            {
              "bot": "{bot}",
              "from": 1,
              "to": 2,
              "added": 0,
              "removed": 0,
              "changed": 1,
              "definition": [],
              "resources": [
                {
                  "kind": "Procedure",
                  "id": "conversation",
                  "difference": "Changed",
                  "fields": [],
                  "parts": [
                    {
                      "part": "step",
                      "name": "answer",
                      "difference": "Removed",
                      "fields": []
                    },
                    {
                      "part": "step",
                      "name": "reply",
                      "difference": "Added",
                      "fields": []
                    },
                    {
                      "part": "step",
                      "name": "wait",
                      "difference": "Changed",
                      "fields": [
                        {
                          "field": "next",
                          "old": "answer",
                          "new": "reply"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;
        const string Text = """
            From v1 to v2, bot {bot}: 0 added, 0 removed, 1 changed
              procedure conversation changed
                step 'answer' removed
                step 'reply' added
                step 'wait' changed
                  next answer → reply
            """;

        var json = await scene.CliAsync("diff", "1", "2", "--json");
        var text = await scene.CliAsync("diff", "1", "2");

        Assert.Equal(Json.Replace("{bot}", versions.Bot, StringComparison.Ordinal), json.Output.ReplaceLineEndings("\n").TrimEnd());
        Assert.Equal(Text.Replace("{bot}", versions.Bot, StringComparison.Ordinal), text.Output.ReplaceLineEndings("\n").TrimEnd());
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-15")]
    public async Task A_resource_whose_id_changed_is_listed_as_removed_under_the_old_id_and_added_under_the_new_one()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        var first = await scene.CreateAsync();
        var renamed = await scene.Client.PostAsJsonAsync(
            $"/api/bots/{scene.Bot}/articles/priceList/rename",
            new { newId = "prices" },
            TestContext.Current.CancellationToken
        );
        Assert.True(renamed.IsSuccessStatusCode);
        var second = await scene.CreateAsync();

        var comparison = await CompareAsync(scene, first.Number, second.Number);

        Assert.Contains(comparison.Resources, resource => (resource.Id, resource.Difference) == ("priceList", Difference.Removed));
        Assert.Contains(comparison.Resources, resource => (resource.Id, resource.Difference) == ("prices", Difference.Added));
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-1")]
    public async Task The_route_and_the_cli_list_the_same_resources_for_real_versions_of_a_bot()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        var first = await scene.CreateAsync();
        Assert.True((await scene.Facts.Put("customer.city", FactApi.Text(length: 120))).IsSuccessStatusCode);
        var articles = new ArticleApi(scene.Client, scene.Bot);
        Assert.True((await articles.Add("discounts", ArticleApi.Body(title: "Discounts"))).IsSuccessStatusCode);
        Assert.True((await articles.Delete("priceList")).IsSuccessStatusCode);
        var second = await scene.CreateAsync();

        var comparison = await CompareAsync(scene, first.Number, second.Number);

        Assert.Equal(
            [
                (ResourceKind.GlobalFact, "customer.city", Difference.Changed),
                (ResourceKind.Article, "discounts", Difference.Added),
                (ResourceKind.Article, "priceList", Difference.Removed),
            ],
            comparison.Resources.Select(resource => (resource.Kind, resource.Id, resource.Difference))
        );
        var text = await scene.CliAsync("diff", "1", "2");
        Assert.Equal(0, text.ExitCode);
        Assert.Contains($"From v1 to v2, bot {scene.Bot}: 1 added, 1 removed, 1 changed", text.Output, StringComparison.Ordinal);
        Assert.Contains("fact customer.city changed", text.Output, StringComparison.Ordinal);
        Assert.Contains("maxLength 80 → 120", text.Output, StringComparison.Ordinal);
        Assert.Contains("article discounts added", text.Output, StringComparison.Ordinal);
        Assert.Contains("article priceList removed", text.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-3")]
    public async Task A_procedure_changed_for_real_lists_its_step_with_what_changed_in_it()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await scene.CreateAsync();
        var procedures = new ProcedureApi(scene.Client, scene.Facts);
        Assert.True(
            (
                await procedures.Put("conversation", ProcedureApi.Conversation.Replace("Hello", "Good morning", StringComparison.Ordinal))
            ).IsSuccessStatusCode
        );
        await scene.CreateAsync();

        var procedure = Assert.Single((await CompareAsync(scene, 1, 2)).Resources);

        var step = Assert.Single(procedure.Parts);
        Assert.Equal(("step", "answer", Difference.Changed), (step.Part, step.Name, step.Difference));
        var said = Assert.Single(step.Fields);
        Assert.Equal(("say", "Hello", "Good morning"), (said.Field, said.Old, said.New));
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-4")]
    public async Task A_change_of_a_knowledge_bases_language_is_a_changed_field_with_both_values()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await scene.CreateAsync();
        var bases = new KnowledgeBaseApi(scene.Client, scene.Bot);
        Assert.True((await bases.Put("offer", KnowledgeBaseApi.Body("Offer", "pl"))).IsSuccessStatusCode);
        await scene.CreateAsync();

        var changed = Assert.Single((await CompareAsync(scene, 1, 2)).Resources);

        Assert.Equal((ResourceKind.KnowledgeBase, "offer"), (changed.Kind, changed.Id));
        var language = Assert.Single(changed.Fields);
        Assert.Equal(("language", "en", "pl"), (language.Field, language.Old, language.New));
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-5")]
    [Trait("criterion", "AC-article-links-13")]
    public async Task An_article_whose_link_label_changed_because_the_target_was_retitled_is_listed_with_that_line_and_its_own_revision_stays()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await scene.CreateAsync();
        var articles = new ArticleApi(scene.Client, scene.Bot);
        var revision = (await ArticleApi.ViewOf(await articles.Get("priceList"))).Revision;
        Assert.True((await articles.Put("cheaper", ArticleApi.Body(title: "Bargains", content: "Cheaper options."))).IsSuccessStatusCode);
        var second = await scene.CreateAsync();

        var referrer = (await CompareAsync(scene, 1, second.Number)).Resources.Single(resource => resource.Id == "priceList");

        Assert.Equal(Difference.Changed, referrer.Difference);
        Assert.Contains(
            new TextLine(LineChange.Added, "See [Bargains](cms://article/cheaper)."),
            Assert.Single(referrer.Fields).Lines ?? []
        );
        var stored = await ArticleApi.ViewOf(await articles.Get("priceList"));
        Assert.Equal(revision, stored.Revision);
        Assert.Equal("See [the cheaper ones](cms://article/cheaper).", stored.Content);
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-6")]
    public async Task A_version_compared_with_itself_or_with_an_undone_change_has_no_differences_and_exits_0_and_differences_also_exit_0()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await scene.CreateAsync();
        Assert.True((await scene.Facts.Put("customer.city", FactApi.Text(length: 120))).IsSuccessStatusCode);
        await scene.CreateAsync();
        Assert.True((await scene.Facts.Put("customer.city", FactApi.Text())).IsSuccessStatusCode);
        await scene.CreateAsync();

        Assert.Empty((await CompareAsync(scene, 2, 2)).Resources);
        Assert.Empty((await CompareAsync(scene, 1, 3)).Resources);
        var same = await scene.CliAsync("diff", "1", "3");
        Assert.Equal(0, same.ExitCode);
        Assert.Contains("no differences", same.Output, StringComparison.Ordinal);
        Assert.Equal(0, (await scene.CliAsync("diff", "1", "2")).ExitCode);
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-7")]
    public async Task Compared_in_the_order_given_the_later_version_first_says_what_going_back_would_change()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await scene.CreateAsync();
        Assert.True((await scene.Facts.Put("customer.city", FactApi.Text(length: 120))).IsSuccessStatusCode);
        await scene.ChangeAsync();
        await scene.CreateAsync();

        var back = await CompareAsync(scene, 2, 1);

        Assert.Equal((0, 1, 1), (back.Added, back.Removed, back.Changed));
        var field = back.Resources.Single(resource => resource.Id == "customer.city").Fields[0];
        Assert.Equal(("120", "80"), (field.Old, field.New));
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-8")]
    public async Task A_number_the_bot_does_not_have_is_refused_naming_the_number_and_the_latest_with_exit_code_1()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await scene.CreateAsync();

        var response = await GetAsync(scene, 1, 99);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var message = Assert.Single(await FactApi.MistakesOf(response)).GetProperty("message").GetString();
        Assert.Equal($"Bot '{scene.Bot}' has no version 99; its latest is 1.", message);
        var cli = await scene.CliAsync("diff", "1", "99");
        Assert.Equal(1, cli.ExitCode);
        Assert.Contains("has no version 99; its latest is 1.", cli.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-9")]
    public async Task Comparing_changes_no_version_and_no_part_of_the_working_state_and_sends_nothing_to_the_bot()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await scene.CreateAsync();
        await scene.ChangeAsync();
        await scene.CreateAsync();
        var before = await scene.SnapshotAsync();
        var requests = bot.Requests;

        await CompareAsync(scene, 1, 2);

        Assert.Equal(before, await scene.SnapshotAsync());
        Assert.Equal(requests, bot.Requests);
    }

    [Fact]
    [Trait("criterion", "AC-version-diff-11")]
    public async Task The_same_two_versions_always_give_the_same_json_in_a_fixed_order()
    {
        var (scene, bot) = await FilledAsync();
        await using var _ = bot;
        await scene.CreateAsync();
        Assert.True((await scene.Facts.Put("customer.city", FactApi.Text(length: 120))).IsSuccessStatusCode);
        await scene.ChangeAsync();
        var articles = new ArticleApi(scene.Client, scene.Bot);
        Assert.True((await articles.Add("zebra", ArticleApi.Body(title: "Zebra"))).IsSuccessStatusCode);
        Assert.True((await articles.Add("bravo", ArticleApi.Body(title: "Bravo"))).IsSuccessStatusCode);
        Assert.True((await articles.KnowledgeBases.Add("zulu", "Zulu")).IsSuccessStatusCode);
        await scene.CreateAsync();

        var once = await scene.CliAsync("diff", "1", "2", "--json");
        var twice = await scene.CliAsync("diff", "1", "2", "--json");

        Assert.Equal(once.Output, twice.Output);
        using var document = JsonDocument.Parse(once.Output);
        Assert.Equal(1, document.RootElement.GetProperty("from").GetInt32());
        Assert.Equal(
            ["GlobalFact:customer.city", "GlobalFact:extra.n1", "KnowledgeBase:zulu", "Article:bravo", "Article:zebra"],
            document
                .RootElement.GetProperty("resources")
                .EnumerateArray()
                .Select(item => $"{item.GetProperty("kind").GetString()}:{item.GetProperty("id").GetString()}")
        );
    }
}
