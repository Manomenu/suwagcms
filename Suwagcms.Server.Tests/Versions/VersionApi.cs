using System.Net.Http.Json;
using System.Text.Json;
using Suwagcms.Contracts.Versions;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Procedures;

namespace Suwagcms.Server.Tests.Versions;

internal sealed class VersionApi(HttpClient client, ProcedureApi procedures)
{
    public HttpClient Client { get; } = client;

    public ProcedureApi Procedures { get; } = procedures;

    public string Bot => Procedures.Bot;

    public static async Task<VersionApi> NewBotAsync(HttpClient client, string address = FactApi.NoBotAddress) =>
        new(client, await ProcedureApi.NewBotAsync(client, address));

    public async Task<VersionApi> Ready()
    {
        Assert.True((await Procedures.Add("conversation", ProcedureApi.Conversation)).IsSuccessStatusCode);
        return this;
    }

    public Task<HttpResponseMessage> Create(bool dryRun = false) =>
        Client.PostAsync($"/api/bots/{Bot}/versions{(dryRun ? "?dryRun=true" : "")}", null, TestContext.Current.CancellationToken);

    public async Task<VersionCreated> Created()
    {
        var response = await Create();
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<VersionCreated>(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("no version in the answer");
    }

    public async Task<List<VersionSummary>> List() =>
        await Client.GetFromJsonAsync<List<VersionSummary>>($"/api/bots/{Bot}/versions", TestContext.Current.CancellationToken) ?? [];

    public Task<HttpResponseMessage> Get(int number) =>
        Client.GetAsync($"/api/bots/{Bot}/versions/{number}", TestContext.Current.CancellationToken);

    public async Task<VersionDetail> Detail(int number) =>
        await Client.GetFromJsonAsync<VersionDetail>($"/api/bots/{Bot}/versions/{number}", TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("no version in the answer");

    public async Task<JsonElement> DocumentOf(int number)
    {
        using var parsed = JsonDocument.Parse((await Detail(number)).Document);
        return parsed.RootElement.Clone();
    }

    public static Task<string[]> CodesOf(HttpResponseMessage response) => FactApi.CodesOf(response);
}
