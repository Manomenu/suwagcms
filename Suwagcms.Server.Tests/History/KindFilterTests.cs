using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Suwagcms.Server.Tests.Actions;
using Suwagcms.Server.Tests.Procedures;
using Suwagcms.Server.Tests.Versions;

namespace Suwagcms.Server.Tests.History;

// Why: the panel under each list screen reads the bot's history narrowed to one kind, so each kind's narrowing is its own promise.
public sealed class KindFilterTests(TestDatabase database) : IDisposable
{
    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    // Why: one change of every kind and a version created and pushed; the push fails, since nothing answers at that address, and is recorded all the same.
    private async Task<HistoryScene> EveryKind()
    {
        _ = database.Pool;
        var client = app.CreateClient();
        var scene = await PushScene.RegisterAsync(client, PushScene.FreshId(), "http://127.0.0.1:1");
        await scene.FillAsync();
        Assert.True((await new ActionApi(client, scene.Bot).Add("send", ActionApi.Least)).IsSuccessStatusCode);
        Assert.True(
            (
                await client.PutAsJsonAsync(
                    $"/api/bots/{scene.Bot}/persona",
                    new { personality = "Calm" },
                    TestContext.Current.CancellationToken
                )
            ).IsSuccessStatusCode
        );
        await scene.CreateAsync();
        await scene.PushAsync(1);
        return new HistoryScene(client, scene.Bot);
    }

    [Theory]
    [Trait("criterion", "AC-change-history-36")]
    [InlineData("facts", "fact")]
    [InlineData("knowledge-bases", "knowledgeBase")]
    [InlineData("articles", "article")]
    [InlineData("procedures", "procedure")]
    [InlineData("actions", "action")]
    [InlineData("persona", "persona")]
    [InlineData("versions", "version")]
    public async Task A_kind_lists_every_change_of_that_kind_and_nothing_else(string collection, string kind)
    {
        var history = await EveryKind();

        var all = (await history.OfBot()).Entries;
        var narrowed = (await history.OfBot($"?kind={collection}")).Entries;

        Assert.NotEmpty(narrowed);
        Assert.All(narrowed, entry => Assert.Equal(kind, entry.Kind));
        Assert.Equal(all.Where(entry => entry.Kind == kind).Select(entry => entry.Id), narrowed.Select(entry => entry.Id));
    }

    [Fact]
    [Trait("criterion", "AC-change-history-36")]
    public async Task Versions_lists_the_versions_created_and_pushed()
    {
        var history = await EveryKind();

        var entries = (await history.OfBot("?kind=versions")).Entries;

        Assert.Equal([("pushed", "1"), ("created", "1")], entries.Select(entry => (entry.Action, entry.ResourceId)));
    }

    [Fact]
    [Trait("criterion", "AC-change-history-36")]
    public async Task Procedures_lists_deleted_and_renamed_ones_newest_first_paged_with_the_total_and_none_of_another_bot()
    {
        _ = database.Pool;
        var client = app.CreateClient();
        var procedures = await ProcedureApi.NewBotAsync(client);
        var other = await ProcedureApi.NewBotAsync(client);
        Assert.True((await other.Add("conversation", ProcedureApi.Conversation)).IsSuccessStatusCode);
        Assert.True((await procedures.Add("conversation", ProcedureApi.Conversation)).IsSuccessStatusCode);
        Assert.True((await procedures.Add("quote", ProcedureApi.Valuation)).IsSuccessStatusCode);
        Assert.True((await procedures.Add("old", ProcedureApi.Valuation)).IsSuccessStatusCode);
        Assert.True((await procedures.Delete("old", "?ifRevision=1")).IsSuccessStatusCode);
        (
            await client.PostAsJsonAsync(
                $"/api/bots/{procedures.Bot}/procedures/quote/rename",
                new { newId = "valuation" },
                TestContext.Current.CancellationToken
            )
        ).EnsureSuccessStatusCode();
        var history = new HistoryScene(client, procedures.Bot);

        var first = await history.OfBot("?kind=procedures&limit=2&page=1");
        var second = await history.OfBot("?kind=procedures&limit=2&page=2");
        var third = await history.OfBot("?kind=procedures&limit=2&page=3");

        Assert.Equal(5, first.Total);
        Assert.Equal(
            [("renamed", "valuation"), ("deleted", "old"), ("added", "old"), ("added", "quote"), ("added", "conversation")],
            first.Entries.Concat(second.Entries).Concat(third.Entries).Select(entry => (entry.Action, entry.ResourceId))
        );
        Assert.NotNull(first.Entries[0].Written);
    }

    [Fact]
    [Trait("criterion", "AC-change-history-36")]
    public async Task An_unknown_kind_is_refused_naming_the_kinds()
    {
        _ = database.Pool;
        var client = app.CreateClient();
        var procedures = await ProcedureApi.NewBotAsync(client);

        var response = await client.GetAsync($"/api/bots/{procedures.Bot}/history?kind=shoes", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains(
            "facts, knowledge-bases, articles, procedures, actions, persona, versions",
            problem.RootElement.GetProperty("detail").GetString(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    [Trait("criterion", "AC-change-history-38")]
    public async Task The_command_with_kind_lists_the_same_entries_as_the_route_and_without_it_prints_as_before()
    {
        var history = await EveryKind();
        var route = await history.OfBot("?kind=procedures");

        var json = await history.CliAsync("history", "--kind", "procedure", "--json");
        var text = await history.CliAsync("history", "--kind", "procedure");
        var both = await history.CliAsync("history", "procedure", "conversation", "--kind", "article");
        var whole = await history.CliAsync("history", "--json");

        Assert.Equal(0, json.ExitCode);
        using (var document = JsonDocument.Parse(json.Output))
        {
            Assert.Equal(
                route.Entries.Select(entry => entry.Id),
                document.RootElement.GetProperty("entries").EnumerateArray().Select(entry => entry.GetProperty("id").GetInt64())
            );
        }
        Assert.Equal(0, text.ExitCode);
        Assert.Contains("procedure conversation", text.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("fact customer.city", text.Output, StringComparison.Ordinal);
        Assert.Equal(2, both.ExitCode);
        Assert.Contains("--kind", both.Error, StringComparison.Ordinal);
        using var all = JsonDocument.Parse(whole.Output);
        Assert.Equal((await history.OfBot()).Entries.Count, all.RootElement.GetProperty("entries").GetArrayLength());
    }
}
