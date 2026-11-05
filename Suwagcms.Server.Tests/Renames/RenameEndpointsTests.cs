using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;
using Suwagcms.Server.Editing;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Procedures;

namespace Suwagcms.Server.Tests.Renames;

public sealed class RenameEndpointsTests(TestDatabase database) : IDisposable
{
    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    private sealed record Scene(HttpClient Client, ProcedureApi Api)
    {
        public string Bot => Api.Bot;

        public async Task<HttpResponseMessage> Rename(string collection, string id, string newId, string query = "")
        {
            using var content = new StringContent(JsonSerializer.Serialize(new { newId }), Encoding.UTF8, "application/json");
            return await Client.PostAsync(
                $"/api/bots/{Bot}/{collection}/{Uri.EscapeDataString(id)}/rename{query}",
                content,
                TestContext.Current.CancellationToken
            );
        }

        public async Task<HttpResponseMessage> Put(string collection, string id, string json, string query = "")
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await Client.PutAsync($"/api/bots/{Bot}/{collection}/{id}{query}", content, TestContext.Current.CancellationToken);
        }

        public Task<HttpResponseMessage> Get(string collection, string id) =>
            Client.GetAsync($"/api/bots/{Bot}/{collection}/{Uri.EscapeDataString(id)}", TestContext.Current.CancellationToken);

        public async Task<JsonElement> Read(string collection, string id)
        {
            var response = await Get(collection, id);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        }

        public async Task AddArticle(string id, string content = "Text.", string guide = "Guide.", string baseId = "offer")
        {
            var body = JsonSerializer.Serialize(
                new
                {
                    knowledgeBase = baseId,
                    title = $"Title {id}",
                    content,
                    llmGuide = guide,
                    description = "What it answers",
                }
            );
            Assert.Equal(HttpStatusCode.OK, (await Put("articles", id, body, "?ifRevision=0")).StatusCode);
        }
    }

    private async Task<Scene> NewScene()
    {
        _ = database.Pool;
        var client = app.CreateClient();
        var scene = new Scene(client, await ProcedureApi.NewBotAsync(client));
        Assert.Equal(HttpStatusCode.OK, (await scene.Put("knowledge-bases", "offer", """{"title":"Offer"}""", "?ifRevision=0")).StatusCode);
        return scene;
    }

    private static async Task<RenameAnswer> AnswerOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<RenameAnswer>(TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("no answer");

    private static string Text(JsonElement element, string field) => element.GetProperty(field).GetString() ?? "";

    private static int Revision(JsonElement element) => element.GetProperty("revision").GetInt32();

    [Fact]
    [Trait("criterion", "AC-resource-rename-1")]
    [Trait("criterion", "AC-resource-rename-3")]
    public async Task An_article_gets_the_new_id_and_every_link_targets_it_with_only_the_target_changed()
    {
        var scene = await NewScene();
        var content =
            "See [Price list](cms://article/cennik), `[x](cms://article/cennik)`, cennik and [web](https://a.example/cennik).\r\n";
        await scene.AddArticle("cennik");
        await scene.AddArticle("wycena", content);
        await scene.AddArticle("rabaty", guide: "[a](cms://article/cennik) [b](cms://article/cennik)");

        var renamed = await scene.Rename("articles", "cennik", "priceList");

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scene.Get("articles", "cennik")).StatusCode);
        Assert.Equal("priceList", Text(await scene.Read("articles", "priceList"), "id"));
        Assert.Equal(
            content.Replace("(cms://article/cennik),", "(cms://article/priceList),", StringComparison.Ordinal),
            Text(await scene.Read("articles", "wycena"), "content")
        );
        Assert.Equal("[a](cms://article/priceList) [b](cms://article/priceList)", Text(await scene.Read("articles", "rabaty"), "llmGuide"));
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-4")]
    public async Task Changing_a_bases_id_updates_the_knowledge_base_of_each_of_its_articles()
    {
        var scene = await NewScene();
        await scene.AddArticle("cennik");
        await scene.AddArticle("wycena");

        var renamed = await AnswerOf(await scene.Rename("knowledge-bases", "offer", "pricing"));

        Assert.Equal("knowledge base", renamed.Kind);
        Assert.Equal(["cennik", "wycena"], renamed.Referrers.Select(referrer => referrer.Id));
        Assert.Equal("pricing", Text(await scene.Read("articles", "cennik"), "knowledgeBase"));
        Assert.Equal("pricing", Text(await scene.Read("articles", "wycena"), "knowledgeBase"));
        Assert.Equal(HttpStatusCode.NotFound, (await scene.Get("knowledge-bases", "offer")).StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-5")]
    [Trait("criterion", "AC-resource-rename-18")]
    public async Task A_facts_name_changes_also_with_dots_and_in_each_collect_assign_and_when()
    {
        var scene = await NewScene();
        var procedure = """
            {"description":"D","firstStep":"ask","steps":{
              "ask":{"collect":["customer.city"],"next":[{"when":{"customer.city":{"known":true}},"to":"mark"},{"to":"mark"}]},
              "mark":{"assign":{"customer.city":"Warsaw"}}}}
            """;
        Assert.Equal(HttpStatusCode.OK, (await scene.Api.Add("orderValuation", procedure)).StatusCode);

        var bad = await scene.Rename("facts", "customer.city", "Client Town");
        Assert.Equal(["fact.name.invalid"], await FactApi.CodesOf(bad));

        var renamed = await AnswerOf(await scene.Rename("facts", "customer.city", "client.town"));

        Assert.Equal(
            ["steps.ask.collect[0]", "steps.ask.next[0].when.customer.city", "steps.mark.assign.customer.city"],
            renamed.Referrers.Select(referrer => referrer.Field)
        );
        var stored = await scene.Read("procedures", "orderValuation");
        var raw = stored.GetProperty("steps").GetRawText();
        Assert.Contains("client.town", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("customer.city", raw, StringComparison.Ordinal);
        Assert.Equal(["orderValuation"], (await FactApi.FactOf(await scene.Api.Facts.Get("client.town"))).UsedBy);
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-6")]
    public async Task A_taken_id_a_bad_form_the_same_id_and_a_missing_resource_are_each_refused_and_nothing_changes()
    {
        var scene = await NewScene();
        await scene.AddArticle("cennik");
        await scene.AddArticle("priceList");
        await scene.AddArticle("wycena", "[a](cms://article/cennik)");

        var refusals = new[]
        {
            (await scene.Rename("articles", "cennik", "priceList"), "resource.duplicateId"),
            (await scene.Rename("articles", "cennik", "Bad Id"), "article.id.invalid"),
            (await scene.Rename("articles", "cennik", "cennik"), "resource.sameId"),
        };
        foreach (var (refusal, code) in refusals)
        {
            Assert.Equal([code], await FactApi.CodesOf(refusal));
            Assert.False(string.IsNullOrWhiteSpace(Text(Assert.Single(await FactApi.MistakesOf(refusal)), "fix")));
        }
        var missing = await scene.Rename("articles", "ghost", "other");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var mistake = Assert.Single(await FactApi.MistakesOf(missing));
        Assert.Contains("'ghost'", Text(mistake, "message"), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(Text(mistake, "fix")));
        Assert.Equal(1, Revision(await scene.Read("articles", "cennik")));
        Assert.Equal(1, Revision(await scene.Read("articles", "priceList")));
        Assert.Equal("[a](cms://article/cennik)", Text(await scene.Read("articles", "wycena"), "content"));
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-7")]
    public async Task A_failure_while_writing_leaves_every_resource_as_it_was()
    {
        var scene = await NewScene();
        await scene.AddArticle("cennik");
        await scene.AddArticle("wycena", "[a](cms://article/cennik)");
        var trigger = "rename_fails_" + scene.Bot;
        await using var connection = await database.Pool.OpenAsync(TestContext.Current.CancellationToken);
        // Why: the renamed row is written after its referrers, so failing on it proves the referrer already written is rolled back.
        await connection.ExecuteAsync(
            $"""
            CREATE FUNCTION {trigger}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'no'; END $$;
            CREATE TRIGGER {trigger} BEFORE UPDATE ON resource FOR EACH ROW
            WHEN (NEW.bot_id = '{scene.Bot}' AND NEW.public_id = 'priceList') EXECUTE FUNCTION {trigger}();
            """
        );
        try
        {
            var editor = new DefinitionEditor(database.Pool, TimeProvider.System);
            var target = new ResourceRef(ResourceKind.Article, "cennik");

            await Assert.ThrowsAsync<PostgresException>(() =>
                editor.ApplyAsync(scene.Bot, [new Rename(target, "priceList", null)], false, TestContext.Current.CancellationToken)
            );
        }
        finally
        {
            await connection.ExecuteAsync($"DROP TRIGGER {trigger} ON resource; DROP FUNCTION {trigger}();");
        }

        Assert.Equal(HttpStatusCode.NotFound, (await scene.Get("articles", "priceList")).StatusCode);
        Assert.Equal(1, Revision(await scene.Read("articles", "cennik")));
        var referrer = await scene.Read("articles", "wycena");
        Assert.Equal("[a](cms://article/cennik)", Text(referrer, "content"));
        Assert.Equal(1, Revision(referrer));
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-8")]
    public async Task A_dry_run_gives_the_same_refusal_or_lists_each_referrer_and_field_and_nothing_changes()
    {
        var scene = await NewScene();
        await scene.AddArticle("cennik");
        await scene.AddArticle("wycena", "[a](cms://article/cennik)");
        await scene.AddArticle("rabaty", guide: "[a](cms://article/cennik) [b](cms://article/cennik)");

        var dry = await AnswerOf(await scene.Rename("articles", "cennik", "priceList", "?dryRun=true"));

        Assert.Equal(
            ["rabaty llmGuide 2", "wycena content 1"],
            dry.Referrers.Select(referrer => $"{referrer.Id} {referrer.Field} {referrer.Links}")
        );
        Assert.Equal(["article.id.invalid"], await FactApi.CodesOf(await scene.Rename("articles", "cennik", "Bad Id", "?dryRun=true")));
        Assert.Equal(1, Revision(await scene.Read("articles", "cennik")));
        Assert.Equal("[a](cms://article/cennik)", Text(await scene.Read("articles", "wycena"), "content"));
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-10")]
    public async Task Each_rewritten_referrer_gets_a_new_revision_and_another_resource_keeps_its_own()
    {
        var scene = await NewScene();
        await scene.AddArticle("cennik");
        await scene.AddArticle("wycena", "[a](cms://article/cennik)");
        await scene.AddArticle("alone", "Plain text naming cennik.");

        var renamed = await AnswerOf(await scene.Rename("articles", "cennik", "priceList"));

        Assert.Equal(2, renamed.Revision);
        Assert.Equal(2, Revision(await scene.Read("articles", "priceList")));
        Assert.Equal(2, Assert.Single(renamed.Referrers).Revision);
        Assert.Equal(2, Revision(await scene.Read("articles", "wycena")));
        Assert.Equal(1, Revision(await scene.Read("articles", "alone")));
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-11")]
    public async Task A_save_of_a_referrer_based_on_its_revision_before_the_rename_is_refused()
    {
        var scene = await NewScene();
        await scene.AddArticle("cennik");
        await scene.AddArticle("wycena", "[a](cms://article/cennik)");
        Assert.Equal(HttpStatusCode.OK, (await scene.Rename("articles", "cennik", "priceList")).StatusCode);

        var stale = await scene.Put(
            "articles",
            "wycena",
            """{"knowledgeBase":"offer","title":"T","content":"[a](cms://article/cennik)","llmGuide":"G","description":"D"}""",
            "?ifRevision=1"
        );

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(["resource.stale"], await FactApi.CodesOf(stale));
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-12")]
    public async Task If_revision_with_an_old_revision_is_refused_with_the_current_one()
    {
        var scene = await NewScene();
        await scene.AddArticle("cennik");
        Assert.Equal(HttpStatusCode.OK, (await scene.Rename("articles", "cennik", "first")).StatusCode);

        var stale = await scene.Rename("articles", "first", "second", "?ifRevision=1");

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var mistake = Assert.Single(await FactApi.MistakesOf(stale));
        Assert.Contains("it is now revision 2", Text(mistake, "message"), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await scene.Rename("articles", "first", "second", "?ifRevision=2")).StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-13")]
    public async Task A_rename_and_a_new_link_to_the_old_id_at_the_same_moment_never_leave_a_link_without_a_target()
    {
        var scene = await NewScene();
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var target = $"target{attempt}";
            var referrer = $"referrer{attempt}";
            await scene.AddArticle(target);
            var link = JsonSerializer.Serialize(
                new
                {
                    knowledgeBase = "offer",
                    title = referrer,
                    content = $"[a](cms://article/{target})",
                    llmGuide = "G",
                    description = "D",
                }
            );

            var answers = await Task.WhenAll(scene.Rename("articles", target, $"moved{attempt}"), scene.Put("articles", referrer, link));

            Assert.Equal(HttpStatusCode.OK, answers[0].StatusCode);
            if (answers[1].StatusCode == HttpStatusCode.OK)
            {
                Assert.Equal($"[a](cms://article/moved{attempt})", Text(await scene.Read("articles", referrer), "content"));
            }
            else
            {
                Assert.Equal(["reference.missing"], await FactApi.CodesOf(answers[1]));
            }
        }
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-14")]
    public async Task A_resource_with_the_same_id_in_another_bot_and_the_links_to_it_stay_unchanged()
    {
        var scene = await NewScene();
        var other = await NewScene();
        await scene.AddArticle("cennik");
        await other.AddArticle("cennik");
        await other.AddArticle("wycena", "[a](cms://article/cennik)");

        Assert.Equal(HttpStatusCode.OK, (await scene.Rename("articles", "cennik", "priceList")).StatusCode);

        Assert.Equal(1, Revision(await other.Read("articles", "cennik")));
        Assert.Equal("[a](cms://article/cennik)", Text(await other.Read("articles", "wycena"), "content"));
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-15")]
    public async Task After_the_change_a_link_to_the_old_id_is_refused_and_a_new_resource_may_take_the_id()
    {
        var scene = await NewScene();
        await scene.AddArticle("cennik");
        Assert.Equal(HttpStatusCode.OK, (await scene.Rename("articles", "cennik", "priceList")).StatusCode);

        var link = """{"knowledgeBase":"offer","title":"T","content":"[a](cms://article/cennik)","llmGuide":"G","description":"D"}""";
        Assert.Equal(["reference.missing"], await FactApi.CodesOf(await scene.Put("articles", "wycena", link, "?ifRevision=0")));

        await scene.AddArticle("cennik");
        Assert.Equal(HttpStatusCode.OK, (await scene.Put("articles", "wycena", link, "?ifRevision=0")).StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-19")]
    [Trait("criterion", "AC-resource-rename-20")]
    public async Task A_procedures_id_changes_conversation_cannot_and_the_prefix_rule_holds_from_either_side()
    {
        var scene = await NewScene();
        Assert.Equal(HttpStatusCode.OK, (await scene.Api.Add("conversation", ProcedureApi.Conversation)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await scene.Api.Add("orderValuation", """{"description":"D"}""")).StatusCode);

        Assert.Equal(["procedure.conversation.required"], await FactApi.CodesOf(await scene.Rename("procedures", "conversation", "base")));
        Assert.Equal(["fact.name.procedurePrefix"], await FactApi.CodesOf(await scene.Rename("procedures", "orderValuation", "customer")));
        Assert.Equal(
            ["fact.name.procedurePrefix"],
            await FactApi.CodesOf(await scene.Rename("facts", "order.kind", "orderValuation.kind"))
        );

        var renamed = await AnswerOf(await scene.Rename("procedures", "orderValuation", "valuation"));

        Assert.Equal(("procedure", "orderValuation", "valuation"), (renamed.Kind, renamed.OldId, renamed.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await scene.Get("procedures", "orderValuation")).StatusCode);
        Assert.Equal("D", Text(await scene.Read("procedures", "valuation"), "description"));
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-20")]
    public async Task A_facts_new_name_that_a_local_fact_already_has_is_refused()
    {
        var scene = await NewScene();
        var procedure = """{"description":"D","localFacts":{"visitDate":{"type":"text","description":"When","maxLength":9}}}""";
        Assert.Equal(HttpStatusCode.OK, (await scene.Api.Add("orderValuation", procedure)).StatusCode);

        Assert.Equal(["fact.local.nameClash"], await FactApi.CodesOf(await scene.Rename("facts", "customer.city", "visitDate")));
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-21")]
    public async Task A_procedures_node_positions_stay_after_its_id_changes()
    {
        var scene = await NewScene();
        Assert.Equal(HttpStatusCode.OK, (await scene.Api.Add("orderValuation", """{"description":"D"}""")).StatusCode);
        var positions = """{"positions":{"ask":{"x":11,"y":22}}}""";
        Assert.Equal(HttpStatusCode.OK, (await scene.Api.PutLayout("orderValuation", positions)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await scene.Rename("procedures", "orderValuation", "valuation")).StatusCode);

        var layout = await (await scene.Api.GetLayout("valuation")).Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken
        );
        Assert.Equal(11, layout.GetProperty("positions").GetProperty("ask").GetProperty("x").GetDouble());
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-7")]
    public async Task An_article_does_not_list_its_knowledge_base_among_what_it_uses()
    {
        var scene = await NewScene();
        await scene.AddArticle("cennik");

        var usage = await scene.Client.GetFromJsonAsync<UsageView>(
            $"/api/bots/{scene.Bot}/articles/cennik/usage",
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(usage);
        Assert.Empty(usage.Uses);
    }
}
