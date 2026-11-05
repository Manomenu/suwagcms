using System.Net;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Versions;

namespace Suwagcms.Server.Tests.Procedures;

public sealed class StartBlockTests(TestDatabase database) : IDisposable
{
    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    private async Task<VersionApi> NewBot()
    {
        _ = database.Pool;
        var versions = await VersionApi.NewBotAsync(app.CreateClient());
        Assert.True((await versions.Procedures.Add("conversation", ProcedureApi.Conversation)).IsSuccessStatusCode);
        return versions;
    }

    private static string Starting(string target) =>
        """
            {"description":"Starts another procedure.","firstStep":"get",
             "steps":{"get":{"start":"TARGET","next":"confirm"},"confirm":{"say":"Thank you."}}}
            """.Replace("TARGET", target, StringComparison.Ordinal);

    private const string Documents = """
        {"description":"Collects the documents.","firstStep":"hello","steps":{"hello":{"say":"Hello"}}}
        """;

    [Fact]
    [Trait("criterion", "AC-start-block-1")]
    public async Task A_procedure_with_a_start_step_reads_back_with_the_block_and_its_next_as_written()
    {
        var api = (await NewBot()).Procedures;
        Assert.True((await api.Add("documentsList", Documents)).IsSuccessStatusCode);

        var saved = await api.Add("orderValuation", Starting("documentsList"));
        var read = await ProcedureApi.DetailOf(await api.Get("orderValuation"));

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("documentsList", read.Steps["get"].Start);
        Assert.Equal("start", read.Steps["get"].Blocks.Single().Name);
        Assert.Equal("confirm", read.Steps["get"].Next?.Step);
        Assert.Empty(read.NotReady);
    }

    [Fact]
    [Trait("criterion", "AC-start-block-2")]
    public async Task A_target_only_another_bot_has_is_refused_with_reference_missing_listing_this_bots_procedures()
    {
        var api = (await NewBot()).Procedures;
        var other = await ProcedureApi.NewBotAsync(app.CreateClient());
        Assert.True((await other.Add("documentsList", Documents)).IsSuccessStatusCode);

        var response = await api.Add("orderValuation", Starting("documentsList"));
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("reference.missing", mistake.GetProperty("code").GetString());
        Assert.Contains("step 'get'", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal("Procedures: conversation, orderValuation.", mistake.GetProperty("fix").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await api.Get("orderValuation")).StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-start-block-3")]
    public async Task The_target_conversation_is_refused_whichever_procedure_holds_the_step()
    {
        var api = (await NewBot()).Procedures;

        Assert.Equal(["start.target.conversation"], await ProcedureApi.CodesOf(await api.Add("orderValuation", Starting("conversation"))));
        Assert.Equal(["start.target.conversation"], await ProcedureApi.CodesOf(await api.Put("conversation", Starting("conversation"))));
    }

    [Fact]
    [Trait("criterion", "AC-start-block-5")]
    public async Task A_blank_target_is_saved_and_listed_and_creating_a_version_reports_it()
    {
        var versions = await NewBot();

        var saved = await ProcedureApi.DetailOf(await versions.Procedures.Add("orderValuation", Starting(" ")));
        var refused = await versions.Create();

        Assert.Equal(["step.block.empty"], ProcedureApi.NotReadyCodes(saved));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal(["step.block.empty"], await VersionApi.CodesOf(refused));
    }

    [Fact]
    [Trait("criterion", "AC-start-block-7")]
    public async Task A_step_of_conversation_can_start_a_target_that_has_mistakes_of_its_own()
    {
        var api = (await NewBot()).Procedures;
        Assert.True((await api.Add("documentsList", """{"description":"Unfinished."}""")).IsSuccessStatusCode);

        var edited = await api.Put("conversation", Starting("documentsList"));

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-loop-guard-12")]
    public async Task A_loop_between_procedures_through_start_is_refused_with_the_path_and_nothing_is_saved()
    {
        var api = (await NewBot()).Procedures;
        Assert.True((await api.Add("one", Documents.Replace("hello", "get", StringComparison.Ordinal))).IsSuccessStatusCode);
        Assert.True((await api.Add("two", Starting("one"))).IsSuccessStatusCode);

        var response = await api.Put("one", Starting("two"));
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("reference.loop", mistake.GetProperty("code").GetString());
        Assert.Contains("one → two → one", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Null((await ProcedureApi.DetailOf(await api.Get("one"))).Steps["get"].Start);
    }

    [Fact]
    [Trait("criterion", "AC-loop-guard-13")]
    public async Task A_procedure_starting_itself_is_refused()
    {
        var api = (await NewBot()).Procedures;

        var response = await api.Add("orderValuation", Starting("orderValuation"));

        Assert.Equal(["reference.loop"], await ProcedureApi.CodesOf(response));
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-13")]
    public async Task A_procedure_another_one_starts_cannot_be_deleted_and_can_once_the_start_is_gone()
    {
        var api = (await NewBot()).Procedures;
        Assert.True((await api.Add("documentsList", Documents)).IsSuccessStatusCode);
        Assert.True((await api.Add("orderValuation", Starting("documentsList"))).IsSuccessStatusCode);

        var refused = await api.Delete("documentsList");
        var mistake = Assert.Single(await FactApi.MistakesOf(refused));

        Assert.Equal("reference.missing", mistake.GetProperty("code").GetString());
        Assert.Equal("documentsList", mistake.GetProperty("resource").GetProperty("id").GetString());
        var related = Assert.Single(mistake.GetProperty("related").EnumerateArray());
        Assert.Equal("orderValuation", related.GetProperty("resource").GetProperty("id").GetString());
        Assert.Equal("steps.get.start", related.GetProperty("location").GetString());

        Assert.True((await api.Delete("orderValuation")).IsSuccessStatusCode);
        Assert.True((await api.Delete("documentsList")).IsSuccessStatusCode);
    }
}
