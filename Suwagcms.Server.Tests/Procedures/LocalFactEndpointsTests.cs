extern alias Cli;

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Server.Tests.Facts;
using ProcedureFile = Cli::Suwagcms.Cli.Procedures.ProcedureFile;
using ProcedureYaml = Cli::Suwagcms.Cli.Procedures.ProcedureYaml;

namespace Suwagcms.Server.Tests.Procedures;

public sealed class LocalFactEndpointsTests(TestDatabase database) : IDisposable
{
    private const string Yaml = """
        id: orderValuation
        description: Orders a valuation.
        firstStep: ask
        steps:
          ask:
            collect: [visitDate, rooms, kind]
            next: confirm
          confirm:
            say: Thank you.
        localFacts:
          kind: { type: enum, description: What is valued, values: [house, flat], writableBy: correct }
          rooms: { type: int, description: How many rooms, min: 1, max: 50 }
          visitDate: { type: text, description: When to visit, maxLength: 20 }
        """;

    private const string UsingLocal = """
        {"description":"Orders a valuation.","firstStep":"ask",
         "steps":{"ask":{"collect":["visitDate"],"next":"confirm"},"confirm":{"say":"Thanks","next":[{"when":{"visitDate":{"known":true}},"to":"ask"},{"to":"ask"}]}},
         "localFacts":{"visitDate":{"type":"text","description":"When to visit","maxLength":20}}}
        """;

    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    private async Task<ProcedureApi> NewBot()
    {
        _ = database.Pool;
        return await ProcedureApi.NewBotAsync(app.CreateClient());
    }

    private static string Body(string yaml)
    {
        var (document, problem) = ProcedureFile.Parse(yaml, "test");
        Assert.NotNull(document);
        Assert.Empty(problem);
        document.Remove("id");
        document.Remove("revision");
        document.Remove("notReady");
        return document.ToJsonString();
    }

    private static JsonElement Locals(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("localFacts").Clone();
    }

    [Fact]
    [Trait("criterion", "AC-local-facts-1")]
    public async Task Local_facts_read_back_as_written_in_yaml_and_json_and_what_show_prints_is_accepted_back()
    {
        var api = await NewBot();

        Assert.Equal(HttpStatusCode.OK, (await api.Add("orderValuation", Body(Yaml))).StatusCode);
        var stored = await (await api.Get("orderValuation")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var locals = Locals(stored);
        Assert.Equal(
            ["house", "flat"],
            locals.GetProperty("kind").GetProperty("values").EnumerateArray().Select(value => value.GetString())
        );
        Assert.Equal("enum", locals.GetProperty("kind").GetProperty("type").GetString());
        Assert.Equal("int", locals.GetProperty("rooms").GetProperty("type").GetString());
        Assert.Equal("text", locals.GetProperty("visitDate").GetProperty("type").GetString());
        Assert.Equal("correct", locals.GetProperty("kind").GetProperty("writableBy").GetString());
        Assert.Equal(50, locals.GetProperty("rooms").GetProperty("max").GetInt32());
        Assert.Equal("set", locals.GetProperty("rooms").GetProperty("writableBy").GetString());
        Assert.Equal(20, locals.GetProperty("visitDate").GetProperty("maxLength").GetInt32());
        Assert.Equal("When to visit", locals.GetProperty("visitDate").GetProperty("description").GetString());

        using var document = JsonDocument.Parse(stored);
        var shown = ProcedureYaml.From(document.RootElement);
        Assert.Equal(HttpStatusCode.OK, (await api.Put("orderValuation", Body(shown))).StatusCode);
        var fromJson = Assert.IsType<JsonObject>(JsonNode.Parse(stored));
        fromJson.Remove("id");
        fromJson.Remove("revision");
        fromJson.Remove("notReady");
        Assert.Equal(HttpStatusCode.OK, (await api.Put("orderValuation", fromJson.ToJsonString())).StatusCode);

        var after = await (await api.Get("orderValuation")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(locals.GetRawText(), Locals(after).GetRawText());
    }

    [Fact]
    [Trait("criterion", "AC-local-facts-5")]
    public async Task A_name_clash_is_refused_in_both_directions()
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("orderValuation", UsingLocal)).StatusCode);

        var fact = await api.Facts.Add("visitDate", FactApi.Text());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, fact.StatusCode);
        var mistake = Assert.Single(await FactApi.MistakesOf(fact));
        Assert.Equal("fact.local.nameClash", mistake.GetProperty("code").GetString());
        Assert.Contains("'orderValuation'", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.OK, (await api.Facts.Add("city", FactApi.Text())).StatusCode);
        var procedure = await api.Add(
            "second",
            """{"description":"D","localFacts":{"city":{"type":"text","description":"C","maxLength":5}}}"""
        );
        Assert.Equal(HttpStatusCode.UnprocessableEntity, procedure.StatusCode);
        Assert.Equal(["fact.local.nameClash"], await ProcedureApi.CodesOf(procedure));
    }

    [Fact]
    [Trait("criterion", "AC-local-facts-9")]
    public async Task Removing_a_local_fact_in_use_is_refused_listing_each_step_and_removing_its_uses_too_is_accepted()
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("orderValuation", UsingLocal)).StatusCode);

        var refused = await api.Put(
            "orderValuation",
            """
            {"description":"Orders a valuation.","firstStep":"ask",
             "steps":{"ask":{"collect":["visitDate"],"next":"confirm"},"confirm":{"say":"Thanks","next":[{"when":{"visitDate":{"known":true}},"to":"ask"},{"to":"ask"}]}}}
            """
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal("reference.missing", mistake.GetProperty("code").GetString());
        Assert.Contains(
            "step ask (collect), step confirm (condition)",
            mistake.GetProperty("message").GetString(),
            StringComparison.Ordinal
        );

        var cleaned = await api.Put(
            "orderValuation",
            """{"description":"Orders a valuation.","firstStep":"confirm","steps":{"confirm":{"say":"Thanks"}}}"""
        );
        Assert.Equal(HttpStatusCode.OK, cleaned.StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-local-facts-10")]
    public async Task After_a_procedure_is_deleted_its_local_fact_names_are_free_for_a_global_fact()
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("orderValuation", UsingLocal)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await api.Facts.Add("visitDate", FactApi.Text())).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await api.Delete("orderValuation")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await api.Facts.Add("visitDate", FactApi.Text())).StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-local-facts-11")]
    public async Task A_local_fact_is_not_among_the_facts_of_the_bot_and_is_not_shown_as_one_nor_counted_as_a_use()
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("orderValuation", UsingLocal)).StatusCode);

        Assert.DoesNotContain("visitDate", (await api.Facts.List()).Select(fact => fact.Name));
        var shown = await api.Facts.Get("visitDate");
        Assert.Equal(HttpStatusCode.NotFound, shown.StatusCode);
        Assert.Equal(["resource.missing"], await FactApi.CodesOf(shown));
    }

    [Fact]
    [Trait("criterion", "AC-local-facts-7")]
    public async Task Another_procedure_reaching_a_local_fact_is_refused_with_what_it_sees()
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("orderValuation", UsingLocal)).StatusCode);

        var refused = await api.Add("complaint", """{"description":"D","firstStep":"ask","steps":{"ask":{"collect":["visitDate"]}}}""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal("reference.missing", mistake.GetProperty("code").GetString());
        Assert.Contains("'complaint'", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("Facts it sees: customer.city", mistake.GetProperty("fix").GetString(), StringComparison.Ordinal);
    }
}
