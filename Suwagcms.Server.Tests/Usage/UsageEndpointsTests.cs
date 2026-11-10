using System.Net;
using Suwagcms.Contracts.Facts;
using Suwagcms.Contracts.Usage;
using Suwagcms.Server.Tests.Articles;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Procedures;

namespace Suwagcms.Server.Tests.Usage;

public sealed class UsageEndpointsTests(TestDatabase database, SharedApp shared) : IClassFixture<SharedApp>
{
    private TestApp App => shared.App;

    private async Task<ArticleApi> NewBot()
    {
        _ = database.Pool;
        return await ArticleApi.NewBotAsync(App.CreateClient());
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-7")]
    public async Task An_article_lists_who_links_to_it_and_what_it_links_to_each_with_its_field()
    {
        var api = await NewBot();
        await api.Add("warunki", ArticleApi.Body());
        await api.Add("cennik", ArticleApi.Body(content: "[w](cms://article/warunki)"));
        await api.Add("wycena", ArticleApi.Body(content: "[c](cms://article/cennik)"));
        await api.Add("rabaty", ArticleApi.Body(llmGuide: "[c](cms://article/cennik)"));

        var usage = await UsageApi.Of(App.CreateClient(), api.Bot, "articles", "cennik");

        Assert.Equal([new UsageLink("article", "rabaty", "llmGuide"), new UsageLink("article", "wycena", "content")], usage.UsedBy);
        Assert.Equal([new UsageLink("article", "warunki", "content")], usage.Uses);
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-7")]
    public async Task A_resource_nothing_relates_to_has_two_empty_lists()
    {
        var api = await NewBot();
        await api.Add("alone", ArticleApi.Body());

        var usage = await UsageApi.Of(App.CreateClient(), api.Bot, "articles", "alone");

        Assert.Empty(usage.UsedBy);
        Assert.Empty(usage.Uses);
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-7")]
    public async Task A_fact_lists_the_procedures_and_fields_that_name_it_and_a_procedure_lists_its_facts()
    {
        _ = database.Pool;
        var api = await ProcedureApi.NewBotAsync(App.CreateClient());
        await api.Add("orderValuation", ProcedureApi.Valuation);

        var fact = await UsageApi.Of(App.CreateClient(), api.Bot, "facts", "order.kind");
        var procedure = await UsageApi.Of(App.CreateClient(), api.Bot, "procedures", "orderValuation");

        Assert.All(fact.UsedBy, link => Assert.Equal(("procedure", "orderValuation"), (link.Kind, link.Id)));
        Assert.Contains(fact.UsedBy, link => link.Field.StartsWith("steps.ask.", StringComparison.Ordinal));
        Assert.Empty(fact.Uses);
        Assert.Empty(procedure.UsedBy);
        Assert.Equal(
            ["customer.city", "order.kind", "system.flag"],
            procedure.Uses.Select(link => link.Id).Distinct().Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-8")]
    public async Task Asking_for_the_usage_changes_nothing()
    {
        var api = await NewBot();
        await api.Add("cennik", ArticleApi.Body());
        await api.Add("wycena", ArticleApi.Body(content: "[c](cms://article/cennik)"));
        var before = (await api.List()).Select(ArticleApi.Describe).Order(StringComparer.Ordinal).ToList();

        await UsageApi.Of(App.CreateClient(), api.Bot, "articles", "cennik");

        Assert.Equal(before, (await api.List()).Select(ArticleApi.Describe).Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-13")]
    public async Task An_unknown_resource_or_bot_or_kind_is_a_404()
    {
        var api = await NewBot();
        var client = App.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await UsageApi.Get(client, api.Bot, "articles", "nothing")).StatusCode);
        var nobody = await UsageApi.Get(client, "nobody", "articles", "nothing");
        Assert.Equal(HttpStatusCode.NotFound, nobody.StatusCode);
        Assert.Equal(["bot.notFound"], await FactApi.CodesOf(nobody));
        Assert.Equal(HttpStatusCode.NotFound, (await UsageApi.Get(client, api.Bot, "versions", "1")).StatusCode);
    }
}
