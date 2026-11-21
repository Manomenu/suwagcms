using System.Net;
using Suwagcms.Server.Tests.Facts;

namespace Suwagcms.Server.Tests.KnowledgeBases;

public sealed class KnowledgeBaseEndpointsTests(TestDatabase database, ClassServer server) : IClassFixture<ClassServer>
{
    private TestApp App => server.App;

    private async Task<KnowledgeBaseApi> NewBot()
    {
        _ = database.DataSource;
        return await KnowledgeBaseApi.NewBotAsync(App.CreateClient());
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-1")]
    public async Task A_base_that_is_added_is_listed_and_shown_with_its_id_title_and_revision()
    {
        var api = await NewBot();

        Assert.Equal(HttpStatusCode.OK, (await api.Add("offer", "Offer")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Add("forValuers", "For valuers")).StatusCode);

        Assert.Equal(["forValuers|For valuers|1", "offer|Offer|1"], (await api.List()).Select(KnowledgeBaseApi.Describe));
        Assert.Equal("offer|Offer|1", KnowledgeBaseApi.Describe(await KnowledgeBaseApi.ViewOf(await api.Get("offer"))));
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-2")]
    public async Task A_rename_changes_the_title_and_raises_the_revision_and_the_id_stays()
    {
        var api = await NewBot();
        await api.Add("offer", "Offer");

        var response = await api.Rename("offer", "Our offer", "?ifRevision=1");

        Assert.Equal("offer|Our offer|2", KnowledgeBaseApi.Describe(await KnowledgeBaseApi.ViewOf(response)));
        Assert.Equal(["offer|Our offer|2"], (await api.List()).Select(KnowledgeBaseApi.Describe));
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-3")]
    public async Task An_empty_base_is_deleted_and_is_gone_from_the_list_and_from_its_address()
    {
        var api = await NewBot();
        await api.Add("offer", "Offer");
        await api.Add("forValuers", "For valuers");

        var response = await api.Delete("offer");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(["forValuers"], (await api.List()).Select(item => item.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await api.Get("offer")).StatusCode);
    }

    [Theory]
    [Trait("criterion", "AC-knowledge-bases-4")]
    [InlineData("Offer")]
    [InlineData("for_valuers")]
    [InlineData("for valuers")]
    public async Task An_id_that_is_not_camelCase_is_refused_with_the_id_to_use_and_nothing_is_saved(string id)
    {
        var api = await NewBot();

        var response = await api.Add(id, "Offer");
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("knowledgeBase.id.invalid", mistake.GetProperty("code").GetString());
        Assert.Contains("Rename the knowledge base to", mistake.GetProperty("fix").GetString(), StringComparison.Ordinal);
        Assert.Empty(await api.List());
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-3")]
    public async Task Deleting_a_base_that_does_not_exist_is_refused_at_its_id()
    {
        var api = await NewBot();

        var mistake = Assert.Single(await FactApi.MistakesOf(await api.Delete("offer")));

        Assert.Equal("resource.missing", mistake.GetProperty("code").GetString());
        Assert.Equal("id", mistake.GetProperty("at").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-4")]
    public async Task An_id_the_bot_already_has_is_refused_and_the_first_base_stays()
    {
        var api = await NewBot();
        await api.Add("offer", "First");

        var response = await api.Add("offer", "Second");
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("resource.exists", mistake.GetProperty("code").GetString());
        Assert.Contains("edit the existing knowledge base", mistake.GetProperty("fix").GetString(), StringComparison.Ordinal);
        Assert.Equal("First", Assert.Single(await api.List()).Title);
    }

    [Theory]
    [Trait("criterion", "AC-knowledge-bases-4")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_title_is_refused_and_so_is_a_body_without_one(string title)
    {
        var api = await NewBot();

        var blank = await api.Add("offer", title);
        var missing = await api.Put("offer", $$"""{{{KnowledgeBaseApi.SearchFields}}}""", "?ifRevision=0");

        foreach (var response in new[] { blank, missing })
        {
            var mistake = Assert.Single(await FactApi.MistakesOf(response));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("knowledgeBase.title.missing", mistake.GetProperty("code").GetString());
            Assert.False(string.IsNullOrWhiteSpace(mistake.GetProperty("fix").GetString()));
        }
        Assert.Empty(await api.List());
    }

    [Theory]
    [Trait("criterion", "AC-knowledge-bases-4")]
    [InlineData("Our offer")]
    [InlineData("our OFFER")]
    [InlineData("  Our offer  ")]
    public async Task A_title_another_base_has_is_refused_ignoring_case_and_surrounding_spaces_naming_that_base(string title)
    {
        var api = await NewBot();
        await api.Add("offer", "Our offer");

        var response = await api.Add("second", title);
        var mistakes = await FactApi.MistakesOf(response);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var own = Assert.Single(mistakes, mistake => mistake.GetProperty("resource").GetProperty("id").GetString() == "second");
        Assert.Equal("knowledgeBase.title.repeated", own.GetProperty("code").GetString());
        Assert.Contains("used by knowledge base 'offer'", own.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("Choose another title", own.GetProperty("fix").GetString(), StringComparison.Ordinal);
        Assert.Equal(["offer"], (await api.List()).Select(item => item.Id));
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-4")]
    public async Task Renaming_a_base_to_its_own_title_with_other_letter_case_is_allowed()
    {
        var api = await NewBot();
        await api.Add("offer", "Our offer");

        var response = await api.Rename("offer", "OUR OFFER");

        Assert.Equal("OUR OFFER", (await KnowledgeBaseApi.ViewOf(response)).Title);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-5")]
    public async Task Two_bots_can_each_have_a_base_with_the_same_id_and_title_and_neither_lists_the_others()
    {
        var first = await NewBot();
        var second = first.InAnotherBot((await FactApi.NewBotAsync(App.CreateClient())).Bot);

        Assert.Equal(HttpStatusCode.OK, (await first.Add("offer", "Offer")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.Add("offer", "Offer")).StatusCode);
        await second.Add("onlyHere", "Only here");

        Assert.Equal(["offer"], (await first.List()).Select(item => item.Id));
        Assert.Equal(["offer", "onlyHere"], (await second.List()).Select(item => item.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await first.Get("onlyHere")).StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-6")]
    public async Task A_rename_or_delete_based_on_an_old_revision_is_refused_with_the_current_revision_and_changes_nothing()
    {
        var api = await NewBot();
        await api.Add("offer", "First");
        await api.Rename("offer", "Second", "?ifRevision=1");

        var rename = await api.Rename("offer", "From a stale screen", "?ifRevision=1");
        var delete = await api.Delete("offer", "?ifRevision=1");

        foreach (var response in new[] { rename, delete })
        {
            var mistake = Assert.Single(await FactApi.MistakesOf(response));
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("resource.stale", mistake.GetProperty("code").GetString());
            Assert.Contains("revision 2", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        }
        Assert.Equal(["offer|Second|2"], (await api.List()).Select(KnowledgeBaseApi.Describe));
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-6")]
    public async Task A_dry_run_of_an_add_a_rename_and_a_delete_answers_as_the_real_one_and_changes_nothing()
    {
        var api = await NewBot();

        var dryAdd = await api.Add("offer", "Offer", "&dryRun=true");
        Assert.Empty(await api.List());
        var realAdd = await api.Add("offer", "Offer");
        var dryRename = await api.Rename("offer", "Our offer", "?dryRun=true&ifRevision=1");
        var dryDelete = await api.Delete("offer", "?dryRun=true");
        var refused = await api.Rename("offer", " ", "?dryRun=true");

        Assert.Equal(
            KnowledgeBaseApi.Describe(await KnowledgeBaseApi.ViewOf(realAdd)),
            KnowledgeBaseApi.Describe(await KnowledgeBaseApi.ViewOf(dryAdd))
        );
        Assert.Equal("offer|Our offer|2", KnowledgeBaseApi.Describe(await KnowledgeBaseApi.ViewOf(dryRename)));
        Assert.Equal(HttpStatusCode.NoContent, dryDelete.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal(["offer|Offer|1"], (await api.List()).Select(KnowledgeBaseApi.Describe));
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-7")]
    public async Task A_bot_with_no_bases_lists_an_empty_array()
    {
        var api = await NewBot();

        Assert.Empty(await api.List());
    }
}
