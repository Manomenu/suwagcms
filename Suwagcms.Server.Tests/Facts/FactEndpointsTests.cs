using System.Net;
using Suwagcms.Contracts.Facts;

namespace Suwagcms.Server.Tests.Facts;

public sealed class FactEndpointsTests(TestDatabase database, ClassServer server) : IClassFixture<ClassServer>
{
    private TestApp App => server.App;

    private async Task<FactApi> NewBot()
    {
        _ = database.Pool;
        return await FactApi.NewBotAsync(App.CreateClient());
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-1")]
    public async Task An_enum_an_int_and_a_text_fact_are_added_and_read_back_with_everything_they_were_saved_with()
    {
        var api = await NewBot();

        Assert.Equal(HttpStatusCode.OK, (await api.Add("customer.city", FactApi.Text("The city", 80))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Add("order.kind", FactApi.Kind)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Add("order.floors", FactApi.Floors)).StatusCode);

        var listed = await api.List();
        Assert.Equal(["customer.city", "order.floors", "order.kind"], listed.Select(fact => fact.Name));
        Assert.Equal(
            "order.kind|enum|What is valued|correct|house,flat||||1",
            FactApi.Describe(await FactApi.FactOf(await api.Get("order.kind")))
        );
        Assert.Equal(
            "order.floors|int|How many floors|assign||1|40||1",
            FactApi.Describe(await FactApi.FactOf(await api.Get("order.floors")))
        );
        Assert.Equal(
            "customer.city|text|The city|set||||80|1",
            FactApi.Describe(Assert.Single(listed, fact => fact.Name == "customer.city"))
        );
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-2")]
    public async Task A_fact_added_without_writableBy_has_set()
    {
        var api = await NewBot();

        var saved = await FactApi.FactOf(await api.Add("customer.city", FactApi.Text()));

        Assert.Equal("set", saved.WritableBy);
        Assert.Equal("set", (await FactApi.FactOf(await api.Get("customer.city"))).WritableBy);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-2")]
    public async Task Any_writableBy_other_than_the_three_levels_is_refused_listing_them_and_nothing_is_saved()
    {
        var api = await NewBot();

        var response = await api.Add("customer.city", """{"type":"text","description":"d","maxLength":3,"writableBy":"admin"}""");
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("fact.writableBy.invalid", mistake.GetProperty("code").GetString());
        Assert.Contains("assign, set, correct", mistake.GetProperty("fix").GetString(), StringComparison.Ordinal);
        Assert.Empty(await api.List());
    }

    [Theory]
    [Trait("criterion", "AC-global-facts-3")]
    [InlineData("""{"type":"text","maxLength":3}""", "fact.description.missing")]
    [InlineData("""{"type":"text","description":"  ","maxLength":3}""", "fact.description.missing")]
    [InlineData("""{"description":"d","maxLength":3}""", "fact.type.invalid")]
    [InlineData("""{"type":"date","description":"d"}""", "fact.type.invalid")]
    [InlineData("""{"type":"enum","description":"d","values":["a"]}""", "fact.enum.tooFewValues")]
    [InlineData("""{"type":"enum","description":"d","values":["a","a"]}""", "fact.enum.valueRepeated")]
    [InlineData("""{"type":"enum","description":"d","values":["a"," "]}""", "fact.enum.valueBlank")]
    [InlineData("""{"type":"int","description":"d","min":1}""", "fact.int.boundMissing")]
    [InlineData("""{"type":"int","description":"d","max":1}""", "fact.int.boundMissing")]
    [InlineData("""{"type":"int","description":"d","min":5,"max":4}""", "fact.int.rangeInverted")]
    [InlineData("""{"type":"text","description":"d","maxLength":0}""", "fact.text.maxLengthInvalid")]
    [InlineData("""{"type":"text","description":"d"}""", "fact.text.maxLengthInvalid")]
    [InlineData("""{"type":"int","description":"d","min":1,"max":2,"values":["a"]}""", "fact.constraint.foreign")]
    public async Task A_fact_that_breaks_one_rule_is_refused_with_that_mistake_and_its_fix_and_is_not_saved(string body, string code)
    {
        var api = await NewBot();

        var response = await api.Add("order.thing", body);
        var mistakes = await FactApi.MistakesOf(response);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(mistakes, mistake => mistake.GetProperty("code").GetString() == code);
        Assert.All(mistakes, mistake => Assert.False(string.IsNullOrWhiteSpace(mistake.GetProperty("fix").GetString())));
        Assert.Empty(await api.List());
    }

    [Theory]
    [Trait("criterion", "AC-global-facts-4")]
    [InlineData("Customer.city")]
    [InlineData("customer_city")]
    [InlineData("customer city")]
    [InlineData("customer..city")]
    public async Task A_name_that_is_not_camelCase_words_joined_by_dots_is_refused_with_the_name_to_use(string name)
    {
        var api = await NewBot();

        var response = await api.Add(name, FactApi.Text());
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("fact.name.invalid", mistake.GetProperty("code").GetString());
        Assert.Contains("Rename the fact to", mistake.GetProperty("fix").GetString(), StringComparison.Ordinal);
        Assert.Empty(await api.List());
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-4")]
    public async Task Adding_a_name_the_bot_already_has_is_refused_with_a_fix_and_keeps_the_first_fact()
    {
        var api = await NewBot();
        await api.Add("customer.city", FactApi.Text("First"));

        var response = await api.Add("customer.city", FactApi.Text("Second"));
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("resource.exists", mistake.GetProperty("code").GetString());
        Assert.Contains("edit the existing fact", mistake.GetProperty("fix").GetString(), StringComparison.Ordinal);
        Assert.Equal("First", Assert.Single(await api.List()).Description);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-5")]
    public async Task A_save_that_breaks_several_rules_returns_all_of_them_at_once_and_saves_nothing()
    {
        var api = await NewBot();

        var response = await api.Add("Order Kind", """{"type":"enum","description":" ","values":["house"],"writableBy":"all"}""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(
            ["fact.description.missing", "fact.enum.tooFewValues", "fact.name.invalid", "fact.writableBy.invalid"],
            await FactApi.CodesOf(response)
        );
        Assert.Empty(await api.List());
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-6")]
    public async Task The_same_bad_fact_gets_the_same_codes_messages_and_fixes_every_time_in_the_order_the_clients_show_them()
    {
        var api = await NewBot();
        const string body = """{"type":"enum","values":["house"]}""";

        var first = await FactApi.MistakesOf(await api.Add("order.kind", body));
        var second = await FactApi.MistakesOf(await api.Add("order.kind", body));

        Assert.Equal(
            ["fact.description.missing", "fact.enum.tooFewValues"],
            first.Select(mistake => mistake.GetProperty("code").GetString())
        );
        Assert.Equal(first.Select(mistake => mistake.GetRawText()), second.Select(mistake => mistake.GetRawText()));
        Assert.Equal(["description", "values"], first.Select(mistake => mistake.GetProperty("path").GetString()));
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-7")]
    public async Task A_save_with_a_field_the_api_does_not_know_is_refused_naming_the_field_and_saves_nothing()
    {
        var api = await NewBot();

        var response = await api.Add("customer.city", """{"type":"text","description":"d","maxLength":3,"maxLenght":9,"Colour":"red"}""");
        var mistakes = await FactApi.MistakesOf(response);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(
            ["Colour", "maxLenght"],
            mistakes.Select(mistake => mistake.GetProperty("path").GetString()).Order(StringComparer.Ordinal)
        );
        Assert.All(mistakes, mistake => Assert.Equal("save.unknownField", mistake.GetProperty("code").GetString()));
        Assert.Contains(
            "maxLength",
            mistakes[0].GetProperty("fix").GetString() + mistakes[1].GetProperty("fix").GetString(),
            StringComparison.Ordinal
        );
        Assert.Empty(await api.List());
    }

    [Theory]
    [Trait("criterion", "AC-global-facts-7")]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("""{"type":"text","description":"d","maxLength":"three"}""")]
    public async Task A_body_that_is_not_a_fact_is_refused_with_a_mistake_and_a_fix_and_saves_nothing(string body)
    {
        var api = await NewBot();

        var response = await api.Add("customer.city", body);
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("save.invalid", mistake.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(mistake.GetProperty("fix").GetString()));
        Assert.Empty(await api.List());
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-8")]
    public async Task A_fact_is_deleted_and_is_gone_from_the_list_and_from_its_address()
    {
        var api = await NewBot();
        await api.Add("customer.city", FactApi.Text());
        await api.Add("order.kind", FactApi.Kind);

        var response = await api.Delete("customer.city");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(["order.kind"], (await api.List()).Select(fact => fact.Name));
        Assert.Equal(HttpStatusCode.NotFound, (await api.Get("customer.city")).StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-8")]
    public async Task Deleting_a_fact_that_does_not_exist_is_refused_naming_it()
    {
        var api = await NewBot();

        var response = await api.Delete("customer.city");
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("resource.missing", mistake.GetProperty("code").GetString());
        Assert.Contains("'customer.city'", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-8")]
    public async Task Deleting_a_fact_that_does_not_exist_is_refused_at_its_name()
    {
        var api = await NewBot();

        var mistake = Assert.Single(await FactApi.MistakesOf(await api.Delete("customer.city")));

        Assert.Equal("name", mistake.GetProperty("path").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-9")]
    public async Task An_edit_replaces_the_description_constraints_and_writableBy_and_raises_the_revision()
    {
        var api = await NewBot();
        await api.Add("order.kind", FactApi.Kind);

        var response = await api.Put(
            "order.kind",
            """{"type":"enum","description":"The kind","values":["house","flat","plot"],"writableBy":"assign"}""",
            "?ifRevision=1"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        const string expected = "order.kind|enum|The kind|assign|house,flat,plot||||2";
        Assert.Equal(expected, FactApi.Describe(await FactApi.FactOf(response)));
        Assert.Equal(expected, FactApi.Describe(await FactApi.FactOf(await api.Get("order.kind"))));
        Assert.Single(await api.List());
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-9")]
    public async Task An_edit_without_an_expected_revision_applies_to_the_latest_one()
    {
        var api = await NewBot();
        await api.Add("customer.city", FactApi.Text("One"));
        await api.Put("customer.city", FactApi.Text("Two"));

        var response = await api.Put("customer.city", FactApi.Text("Three"));

        Assert.Equal(3, (await FactApi.FactOf(response)).Revision);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-10")]
    public async Task A_dry_run_of_an_add_answers_as_the_real_add_does_and_changes_nothing()
    {
        var api = await NewBot();

        var dry = await api.Add("customer.city", FactApi.Text(), "&dryRun=true");

        Assert.Empty(await api.List());
        var real = await api.Add("customer.city", FactApi.Text());
        Assert.Equal(HttpStatusCode.OK, dry.StatusCode);
        Assert.Equal(FactApi.Describe(await FactApi.FactOf(real)), FactApi.Describe(await FactApi.FactOf(dry)));
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-10")]
    public async Task A_dry_run_of_an_edit_and_of_a_delete_change_nothing_and_a_refused_one_is_refused_as_the_real_one_is()
    {
        var api = await NewBot();
        await api.Add("customer.city", FactApi.Text("Before"));

        var edit = await api.Put("customer.city", FactApi.Text("After"), "?dryRun=true&ifRevision=1");
        var delete = await api.Delete("customer.city", "?dryRun=true");
        var refused = await api.Put("customer.city", """{"type":"text"}""", "?dryRun=true");

        var answered = await FactApi.FactOf(edit);
        Assert.Equal(("After", 2), (answered.Description, answered.Revision));
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var kept = Assert.Single(await api.List());
        Assert.Equal(("Before", 1), (kept.Description, kept.Revision));
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-11")]
    public async Task An_edit_based_on_an_old_revision_is_refused_with_the_current_revision_and_changes_nothing()
    {
        var api = await NewBot();
        await api.Add("customer.city", FactApi.Text("First"));
        await api.Put("customer.city", FactApi.Text("Second"), "?ifRevision=1");

        var response = await api.Put("customer.city", FactApi.Text("From a stale screen"), "?ifRevision=1");
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("resource.stale", mistake.GetProperty("code").GetString());
        Assert.Contains("revision 2", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(("Second", 2), (Assert.Single(await api.List()).Description, Assert.Single(await api.List()).Revision));
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-11")]
    public async Task A_delete_based_on_an_old_revision_is_refused_and_the_fact_stays()
    {
        var api = await NewBot();
        await api.Add("customer.city", FactApi.Text());
        await api.Put("customer.city", FactApi.Text("Changed"));

        var response = await api.Delete("customer.city", "?ifRevision=1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var mistake = Assert.Single(await FactApi.MistakesOf(response));
        Assert.Equal("resource.stale", mistake.GetProperty("code").GetString());
        Assert.Contains("revision 2", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Single(await api.List());
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-11")]
    public async Task An_edit_of_a_fact_deleted_meanwhile_is_refused_as_stale()
    {
        var api = await NewBot();
        await api.Add("customer.city", FactApi.Text());
        await api.Delete("customer.city");

        var response = await api.Put("customer.city", FactApi.Text("Late"), "?ifRevision=1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("deleted", (await FactApi.MistakesOf(response))[0].GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Empty(await api.List());
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-12")]
    public async Task Two_adds_of_one_name_at_the_same_moment_leave_one_fact_and_one_refusal()
    {
        var api = await NewBot();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(number => api.Another().Add("customer.city", FactApi.Text($"Writer {number}")))
        );

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(5, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        Assert.Single(await api.List());
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-13")]
    public async Task A_fact_of_one_bot_is_not_listed_for_another_and_two_bots_can_each_have_the_same_name()
    {
        var first = await NewBot();
        var second = await NewBot();

        await first.Add("customer.city", FactApi.Text("First bot"));
        Assert.Empty(await second.List());
        Assert.Equal(HttpStatusCode.NotFound, (await second.Get("customer.city")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await second.Add("customer.city", FactApi.Text("Second bot"))).StatusCode);
        Assert.Equal("First bot", Assert.Single(await first.List()).Description);
        Assert.Equal("Second bot", Assert.Single(await second.List()).Description);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-13")]
    public async Task Deleting_a_fact_of_one_bot_leaves_the_same_name_of_another()
    {
        var first = await NewBot();
        var second = await NewBot();
        await first.Add("customer.city", FactApi.Text());
        await second.Add("customer.city", FactApi.Text());

        await first.Delete("customer.city");

        Assert.Empty(await first.List());
        Assert.Single(await second.List());
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-13")]
    public async Task A_bot_that_does_not_exist_is_a_404_with_a_mistake_on_every_route()
    {
        _ = database.Pool;
        var api = new FactApi(App.CreateClient(), "no-such-bot");

        var responses = new[]
        {
            await api.Get("customer.city"),
            await api.Add("customer.city", FactApi.Text()),
            await api.Delete("customer.city"),
            await App.CreateClient().GetAsync("/api/bots/no-such-bot/facts", TestContext.Current.CancellationToken),
        };

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        foreach (var response in responses)
        {
            Assert.Equal(["bot.notFound"], await FactApi.CodesOf(response));
        }
    }
}
