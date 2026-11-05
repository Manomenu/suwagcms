using System.Net;
using System.Text.Json;
using Suwagcms.Server.Tests.Articles;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Procedures;

namespace Suwagcms.Server.Tests.Usage;

public sealed class DeleteGuardTests(TestDatabase database) : IDisposable
{
    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    private async Task<ArticleApi> NewBot()
    {
        _ = database.Pool;
        return await ArticleApi.NewBotAsync(app.CreateClient());
    }

    private static string Linking(string target) => ArticleApi.Body(content: $"See [it](cms://article/{target}).");

    private static List<string> Referrers(JsonElement mistake) =>
        [
            .. mistake
                .GetProperty("related")
                .EnumerateArray()
                .Select(item => $"{item.GetProperty("resource").GetProperty("id").GetString()}:{item.GetProperty("location").GetString()}")
                .Order(StringComparer.Ordinal),
        ];

    [Fact]
    [Trait("criterion", "AC-delete-guard-1")]
    public async Task Deleting_an_article_another_article_links_to_is_refused_with_reference_missing_and_the_article_stays()
    {
        var api = await NewBot();
        await api.Add("cennik", ArticleApi.Body());
        await api.Add("wycena", Linking("cennik"));

        var response = await api.Delete("cennik");
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("reference.missing", mistake.GetProperty("code").GetString());
        Assert.Equal("cennik", mistake.GetProperty("resource").GetProperty("id").GetString());
        Assert.Contains("cannot be deleted", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(["cennik", "wycena"], (await api.List()).Select(article => article.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-2")]
    public async Task The_refusal_lists_every_referrer_with_the_field_not_only_the_first()
    {
        var api = await NewBot();
        await api.Add("cennik", ArticleApi.Body());
        await api.Add(
            "wycena",
            ArticleApi.Body(content: "[a](cms://article/cennik) [b](cms://article/cennik)", llmGuide: "[c](cms://article/cennik)")
        );
        await api.Add("rabaty", ArticleApi.Body(llmGuide: "[d](cms://article/cennik)"));
        await api.Add("bez", ArticleApi.Body(content: "Only the word cennik."));

        var mistake = Assert.Single(await FactApi.MistakesOf(await api.Delete("cennik")));

        Assert.Equal(["rabaty:llmGuide", "wycena:content", "wycena:llmGuide"], Referrers(mistake));
        Assert.Contains("Remove the reference", mistake.GetProperty("fix").GetString(), StringComparison.Ordinal);
        Assert.Equal(4, (await api.List()).Count);
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-4")]
    public async Task After_the_last_link_is_removed_the_article_can_be_deleted()
    {
        var api = await NewBot();
        await api.Add("cennik", ArticleApi.Body());
        await api.Add("wycena", Linking("cennik"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await api.Delete("cennik")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await api.Put("wycena", ArticleApi.Body(content: "No link now."))).StatusCode);
        var response = await api.Delete("cennik");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(["wycena"], (await api.List()).Select(article => article.Id));
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-5")]
    public async Task After_a_referrer_is_deleted_it_no_longer_blocks_its_target_nor_shows_in_its_used_by()
    {
        var api = await NewBot();
        await api.Add("cennik", ArticleApi.Body());
        await api.Add("wycena", Linking("cennik"));

        Assert.Equal(HttpStatusCode.NoContent, (await api.Delete("wycena")).StatusCode);
        var usage = await UsageApi.Of(app.CreateClient(), api.Bot, "articles", "cennik");
        var response = await api.Delete("cennik");

        Assert.Empty(usage.UsedBy);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-6")]
    public async Task A_delete_and_a_new_link_to_the_same_article_at_the_same_moment_never_both_succeed()
    {
        var api = await NewBot();
        for (var round = 0; round < 8; round++)
        {
            var target = $"target{round}";
            var linker = $"linker{round}";
            await api.Add(target, ArticleApi.Body());

            var delete = api.Delete(target);
            var link = api.Add(linker, Linking(target));
            var results = await Task.WhenAll(delete, link);

            var deleted = results[0].StatusCode == HttpStatusCode.NoContent;
            var linked = results[1].StatusCode == HttpStatusCode.OK;
            Assert.True(deleted ^ linked, $"round {round}: delete {results[0].StatusCode}, link {results[1].StatusCode}");
            var ids = (await api.List()).Select(article => article.Id).ToList();
            Assert.Equal(linked, ids.Contains(target));
            Assert.Equal(linked, ids.Contains(linker));
        }
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-10")]
    public async Task A_dry_run_delete_gives_the_same_refusal_or_answer_and_changes_nothing()
    {
        var api = await NewBot();
        await api.Add("cennik", ArticleApi.Body());
        await api.Add("wycena", Linking("cennik"));
        await api.Add("free", ArticleApi.Body());

        var refused = await api.Delete("cennik", "?dryRun=true");
        var real = await api.Delete("cennik");
        var answered = await api.Delete("free", "?dryRun=true");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal(
            (await FactApi.MistakesOf(refused)).Select(mistake => mistake.GetRawText()),
            (await FactApi.MistakesOf(real)).Select(mistake => mistake.GetRawText())
        );
        Assert.Equal(HttpStatusCode.NoContent, answered.StatusCode);
        Assert.Equal(["cennik", "free", "wycena"], (await api.List()).Select(article => article.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-12")]
    public async Task A_fact_a_procedure_uses_is_refused_listing_the_procedure_with_its_step_and_field_and_the_fact_stays()
    {
        _ = database.Pool;
        var api = await ProcedureApi.NewBotAsync(app.CreateClient());
        await api.Add("orderValuation", ProcedureApi.Valuation);
        Assert.True(
            (
                await api.Add("secondOrder", """{"description":"D","firstStep":"ask","steps":{"ask":{"collect":["order.kind"]}}}""")
            ).IsSuccessStatusCode
        );

        var response = await api.Facts.Delete("order.kind");
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("reference.missing", mistake.GetProperty("code").GetString());
        Assert.Equal("order.kind", mistake.GetProperty("resource").GetProperty("id").GetString());
        var referrers = Referrers(mistake);
        Assert.All(
            referrers,
            referrer =>
                Assert.True(
                    referrer.StartsWith("orderValuation:steps.ask.", StringComparison.Ordinal)
                        || referrer == "secondOrder:steps.ask.collect[0]",
                    referrer
                )
        );
        Assert.Contains("secondOrder:steps.ask.collect[0]", referrers);
        Assert.Contains(referrers, referrer => referrer.StartsWith("orderValuation:", StringComparison.Ordinal));
        Assert.Contains(referrers, referrer => referrer.Contains("when.order.kind", StringComparison.Ordinal));
        Assert.Contains("order.kind", (await api.Facts.List()).Select(fact => fact.Name));
    }
}
