using System.Net;
using System.Net.Http.Json;
using Suwagcms.Contracts.Facts;
using Suwagcms.Contracts.Procedures;
using Suwagcms.Server.Tests.Procedures;

namespace Suwagcms.Server.Tests.ProcedureLayouts;

public sealed class ProcedureLayoutEndpointsTests(TestDatabase database) : IDisposable
{
    private const string Moved = """{"positions":{"ask":{"x":10.5,"y":20},"goodbye":{"x":300,"y":-40}}}""";
    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    private async Task<ProcedureApi> NewBot()
    {
        _ = database.Pool;
        return await ProcedureApi.NewBotAsync(app.CreateClient());
    }

    private static async Task<ProcedureLayout> LayoutOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<ProcedureLayout>(TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("no layout in the answer");

    private static async Task<string> TextOf(HttpResponseMessage response) =>
        ProcedureApi.Normalised(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

    [Fact]
    [Trait("criterion", "AC-procedure-creator-12")]
    public async Task Moving_a_node_keeps_the_revision_and_the_stored_procedure_and_the_position_is_there_after_a_reload()
    {
        var api = await NewBot();
        Assert.True((await api.Add("orderValuation", ProcedureApi.Valuation)).IsSuccessStatusCode);
        var before = await TextOf(await api.Get("orderValuation"));

        var put = await api.PutLayout("orderValuation", Moved);
        var after = await TextOf(await api.Get("orderValuation"));
        var layout = await LayoutOf(await api.GetLayout("orderValuation"));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(before, after);
        Assert.Equal(new NodePosition(10.5, 20), layout.Positions["ask"]);
        Assert.Equal(new NodePosition(300, -40), layout.Positions["goodbye"]);
        Assert.Equal(1, (await api.List()).Procedures.Single().Revision);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-creator-14")]
    public async Task A_procedure_with_no_stored_positions_reads_an_empty_layout_and_an_empty_put_clears_it()
    {
        var api = await NewBot();
        Assert.True((await api.Add("orderValuation", ProcedureApi.Valuation)).IsSuccessStatusCode);
        Assert.Empty((await LayoutOf(await api.GetLayout("orderValuation"))).Positions);
        Assert.True((await api.PutLayout("orderValuation", Moved)).IsSuccessStatusCode);

        var cleared = await api.PutLayout("orderValuation", """{"positions":{}}""");

        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Empty((await LayoutOf(await api.GetLayout("orderValuation"))).Positions);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-creator-14")]
    public async Task Deleting_a_procedure_takes_its_layout_with_it()
    {
        var api = await NewBot();
        Assert.True((await api.Add("extra", ProcedureApi.Valuation)).IsSuccessStatusCode);
        Assert.True((await api.PutLayout("extra", Moved)).IsSuccessStatusCode);
        Assert.True((await api.Delete("extra")).IsSuccessStatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await api.GetLayout("extra")).StatusCode);
        Assert.True((await api.Add("extra", ProcedureApi.Valuation)).IsSuccessStatusCode);
        Assert.Empty((await LayoutOf(await api.GetLayout("extra"))).Positions);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-creator-14")]
    public async Task A_layout_of_a_missing_procedure_is_not_found_and_a_misspelt_body_is_refused_with_a_fix()
    {
        var api = await NewBot();
        Assert.True((await api.Add("orderValuation", ProcedureApi.Valuation)).IsSuccessStatusCode);

        var missing = await api.PutLayout("nothing", Moved);
        var misspelt = await api.PutLayout("orderValuation", """{"position":{}}""");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, misspelt.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetLayout("nothing")).StatusCode);
    }
}
