using System.Net;
using System.Text.Json;
using Suwagcms.Contracts.Facts;
using Suwagcms.Contracts.Procedures;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Tests.Facts;

namespace Suwagcms.Server.Tests.Procedures;

public sealed class ProcedureEndpointsTests(TestDatabase database, ClassServer server) : IClassFixture<ClassServer>
{
    private TestApp App => server.App;

    private async Task<ProcedureApi> NewBot()
    {
        _ = database.DataSource;
        return await ProcedureApi.NewBotAsync(App.CreateClient());
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-1")]
    public async Task A_procedure_with_all_four_blocks_and_rule_based_next_reads_back_as_it_was_saved()
    {
        var api = await NewBot();

        var saved = await ProcedureApi.DetailOf(await api.Add("orderValuation", ProcedureApi.Valuation));
        var read = await ProcedureApi.DetailOf(await api.Get("orderValuation"));

        Assert.Equal(saved with { NotReady = [] }, read with { NotReady = [] }, new DetailComparer());
        Assert.Equal(["ask", "confirm", "goodbye", "mark"], read.Steps.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("collect", read.Steps["ask"].Blocks.Single().Name);
        Assert.Equal("assign", read.Steps["mark"].Blocks.Single().Name);
        Assert.Equal("say", read.Steps["goodbye"].Blocks.Single().Name);
        var rules = read.Steps["ask"].Next?.Rules;
        Assert.NotNull(rules);
        Assert.Equal(["mark", "goodbye"], rules.Select(rule => rule.To));
        Assert.Equal(["house"], rules[0].When?["order.kind"].OneOf);
        Assert.Equal("Tell the customer you could not finish.", read.WhenStalled?.Instruction);
        Assert.Equal(1, read.Revision);
    }

    private static string Interrupted(string value) => ProcedureApi.Valuation.Trim().Insert(1, $"\"whenInterrupted\":\"{value}\",");

    [Fact]
    [Trait("criterion", "AC-chatbot-contract-1")]
    public async Task A_procedure_saves_and_reads_back_what_happens_when_it_is_interrupted_and_a_save_without_it_clears_it()
    {
        var api = await NewBot();

        Assert.Equal(HttpStatusCode.OK, (await api.Add("orderValuation", Interrupted("discard"))).StatusCode);
        var read = await ProcedureApi.DetailOf(await api.Get("orderValuation"));
        Assert.Equal("discard", read.WhenInterrupted);

        Assert.Equal(HttpStatusCode.OK, (await api.Put("orderValuation", ProcedureApi.Valuation, "?ifRevision=1")).StatusCode);
        Assert.Null((await ProcedureApi.DetailOf(await api.Get("orderValuation"))).WhenInterrupted);
    }

    [Fact]
    [Trait("criterion", "AC-chatbot-contract-2")]
    public async Task Another_value_of_when_interrupted_is_refused_and_nothing_is_saved()
    {
        var api = await NewBot();

        var answer = await api.Add("orderValuation", Interrupted("sometimes"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, answer.StatusCode);
        Assert.Contains("procedure.whenInterrupted.invalid", await ProcedureApi.CodesOf(answer));
        Assert.Equal(HttpStatusCode.NotFound, (await api.Get("orderValuation")).StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-3")]
    public async Task A_procedure_without_a_description_is_refused_with_a_fix_and_not_saved()
    {
        var api = await NewBot();

        var response = await api.Add("orderValuation", """{"firstStep":"a","steps":{"a":{"say":"Hi"}}}""");
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("procedure.description.missing", mistake.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(mistake.GetProperty("fix").GetString()));
        Assert.Empty((await api.List()).Procedures);
    }

    [Theory]
    [Trait("criterion", "AC-procedure-model-3")]
    [InlineData("order_valuation", "procedure.id.invalid")]
    [InlineData("OrderValuation", "procedure.id.invalid")]
    public async Task An_id_that_is_not_camelCase_is_refused_with_the_id_to_use(string id, string code)
    {
        var api = await NewBot();

        var response = await api.Add(id, ProcedureApi.Valuation);
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(code, mistake.GetProperty("code").GetString());
        Assert.Contains("'orderValuation'", mistake.GetProperty("fix").GetString(), StringComparison.Ordinal);
        Assert.Empty((await api.List()).Procedures);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-3")]
    public async Task A_step_name_that_is_not_camelCase_is_refused()
    {
        var api = await NewBot();

        var response = await api.Add("p", """{"description":"d","firstStep":"Say hi","steps":{"Say hi":{"say":"Hi"}}}""");

        Assert.Equal(["step.name.invalid"], await ProcedureApi.CodesOf(response));
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-3")]
    public async Task Adding_an_id_the_bot_already_has_is_refused_with_a_fix_and_keeps_the_first()
    {
        var api = await NewBot();
        Assert.True((await api.Add("orderValuation", ProcedureApi.Valuation)).IsSuccessStatusCode);

        var response = await api.Add("orderValuation", ProcedureApi.Conversation);
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("resource.exists", mistake.GetProperty("code").GetString());
        Assert.Contains("edit the existing procedure", mistake.GetProperty("fix").GetString(), StringComparison.Ordinal);
        Assert.Equal(
            "The customer orders a valuation of a property.",
            (await ProcedureApi.DetailOf(await api.Get("orderValuation"))).Description
        );
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-4")]
    public async Task An_unknown_block_and_a_step_with_two_blocks_are_each_refused_with_the_blocks_that_exist()
    {
        var api = await NewBot();

        var unknown = await api.Add("p", """{"description":"d","firstStep":"a","steps":{"a":{"speak":"Hi"}}}""");
        var two = await api.Add("p", """{"description":"d","firstStep":"a","steps":{"a":{"say":"Hi","listen":{}}}}""");

        Assert.Equal(["step.block.unknown"], await ProcedureApi.CodesOf(unknown));
        Assert.Equal(["step.block.multiple"], await ProcedureApi.CodesOf(two));
        Assert.All(
            (await FactApi.MistakesOf(unknown)).Concat(await FactApi.MistakesOf(two)),
            mistake => Assert.Contains("listen, collect, say, assign", mistake.GetProperty("fix").GetString(), StringComparison.Ordinal)
        );
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-5")]
    public async Task A_firstStep_next_or_to_naming_a_missing_step_is_refused_with_the_steps()
    {
        var api = await NewBot();

        var response = await api.Add(
            "p",
            """{"description":"d","firstStep":"ghost","steps":{"a":{"say":"Hi","next":"b"},"c":{"say":"x","next":[{"to":"d"}]}}}"""
        );
        var mistakes = await FactApi.MistakesOf(response);

        Assert.Equal(
            ["firstStep", "steps.a.next", "steps.c.next[0].to"],
            mistakes.Select(mistake => mistake.GetProperty("at").GetString() ?? "").Order(StringComparer.Ordinal)
        );
        Assert.All(mistakes, mistake => Assert.Contains("a, c", mistake.GetProperty("fix").GetString(), StringComparison.Ordinal));
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-6")]
    public async Task A_fact_the_bot_does_not_have_is_refused_with_reference_missing_and_the_facts_of_the_bot()
    {
        var api = await NewBot();

        var response = await api.Add("p", """{"description":"d","firstStep":"a","steps":{"a":{"collect":["customer.cty"]}}}""");
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal("reference.missing", mistake.GetProperty("code").GetString());
        Assert.Contains("which this procedure does not see", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains(
            "customer.city, order.floors, order.kind, system.flag",
            mistake.GetProperty("fix").GetString(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-7")]
    public async Task A_collect_of_a_fact_only_an_assign_may_write_is_refused()
    {
        var api = await NewBot();

        var response = await api.Add("p", """{"description":"d","firstStep":"a","steps":{"a":{"collect":["system.flag"]}}}""");
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal("fact.collect.notWritable", mistake.GetProperty("code").GetString());
        Assert.Equal("Remove it from collect, or give the fact 'writableBy: set'.", mistake.GetProperty("fix").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-8")]
    public async Task A_condition_or_value_the_fact_cannot_take_is_refused_with_what_is_valid()
    {
        var api = await NewBot();

        var response = await api.Add(
            "p",
            """
            {"description":"d","firstStep":"a","steps":{
              "a":{"say":"x","next":[{"when":{"order.kind":{"range":{"min":1}},"order.floors":{"oneOf":["99"]}},"to":"b"},{"to":"b"}]},
              "b":{"assign":{"order.kind":"castle"}}}}
            """
        );

        Assert.Equal(["assign.value.invalid", "condition.type.misfit", "condition.value.invalid"], await ProcedureApi.CodesOf(response));
        Assert.All(
            await FactApi.MistakesOf(response),
            mistake => Assert.False(string.IsNullOrWhiteSpace(mistake.GetProperty("fix").GetString()))
        );
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-10")]
    public async Task An_unfinished_procedure_is_saved_and_the_answer_lists_what_still_stops_a_version()
    {
        var api = await NewBot();

        var response = await api.Add(
            "orderValuation",
            """{"description":"d","steps":{"ask":{"collect":["customer.city"]},"goodbye":{"say":"Bye"}}}"""
        );
        var saved = await ProcedureApi.DetailOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            ["bot.conversation.missing", "procedure.collect.noWhenStalled", "procedure.firstStep.missing"],
            ProcedureApi.NotReadyCodes(saved)
        );
        Assert.Equal(
            ProcedureApi.NotReadyCodes(saved),
            ProcedureApi.NotReadyCodes(await ProcedureApi.DetailOf(await api.Get("orderValuation")))
        );
        Assert.Equal(2, (await api.List()).Procedures.Single().NotReady);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-9")]
    public async Task The_bot_has_no_conversation_until_one_is_saved_and_then_the_procedure_is_ready()
    {
        var api = await NewBot();
        await api.Add("conversation", ProcedureApi.Conversation);

        var before = await api.List();
        var detail = await ProcedureApi.DetailOf(await api.Get("conversation"));

        Assert.Empty(before.BotNotReady);
        Assert.Empty(detail.NotReady);
        Assert.Equal(0, Assert.Single(before.Procedures).NotReady);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-9")]
    public async Task A_bot_with_procedures_and_no_conversation_says_so_in_the_list()
    {
        var api = await NewBot();
        await api.Add("orderValuation", ProcedureApi.Valuation);

        var list = await api.List();

        Assert.Equal(MistakeCode.BotConversationMissing, Assert.Single(list.BotNotReady).Code);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-11")]
    public async Task A_loop_between_steps_produces_no_mistake()
    {
        var api = await NewBot();

        var saved = await ProcedureApi.DetailOf(await api.Add("conversation", ProcedureApi.Conversation));

        Assert.Equal("wait", saved.Steps["answer"].Next?.Step);
        Assert.Empty(saved.NotReady);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-12")]
    public async Task Deleting_conversation_is_refused_with_the_reason_and_another_procedure_is_deleted()
    {
        var api = await NewBot();
        await api.Add("conversation", ProcedureApi.Conversation);
        await api.Add("orderValuation", ProcedureApi.Valuation);

        var refused = await api.Delete("conversation");
        var refusedDry = await api.Delete("conversation", "?dryRun=true");
        var deleted = await api.Delete("orderValuation");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal(["procedure.conversation.required"], await ProcedureApi.CodesOf(refused));
        Assert.Contains(
            "base of every conversation",
            Assert.Single(await FactApi.MistakesOf(refusedDry)).GetProperty("message").GetString(),
            StringComparison.Ordinal
        );
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(["conversation"], (await api.List()).Procedures.Select(procedure => procedure.Id));
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-13")]
    public async Task A_dry_run_gives_the_same_answer_and_changes_nothing()
    {
        var api = await NewBot();

        var dry = await ProcedureApi.DetailOf(await api.Add("orderValuation", ProcedureApi.Valuation, "&dryRun=true"));
        Assert.Empty((await api.List()).Procedures);
        var real = await ProcedureApi.DetailOf(await api.Add("orderValuation", ProcedureApi.Valuation));

        Assert.Equal(real, dry, new DetailComparer());
        var editedDry = await api.Put("orderValuation", ProcedureApi.Conversation, "?dryRun=true");
        Assert.Equal(HttpStatusCode.OK, editedDry.StatusCode);
        Assert.Equal(1, (await ProcedureApi.DetailOf(await api.Get("orderValuation"))).Revision);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-13")]
    public async Task An_edit_based_on_an_old_revision_is_refused_with_the_current_one()
    {
        var api = await NewBot();
        await api.Add("orderValuation", ProcedureApi.Valuation);
        Assert.Equal(2, (await ProcedureApi.DetailOf(await api.Put("orderValuation", ProcedureApi.Valuation, "?ifRevision=1"))).Revision);

        var stale = await api.Put("orderValuation", ProcedureApi.Conversation, "?ifRevision=1");
        var mistake = Assert.Single(await FactApi.MistakesOf(stale));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("resource.stale", mistake.GetProperty("code").GetString());
        Assert.Contains("revision 2", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-14")]
    public async Task The_list_gives_each_procedure_with_its_description_and_how_many_mistakes_stop_a_version()
    {
        var api = await NewBot();
        await api.Add("conversation", ProcedureApi.Conversation);
        await api.Add(
            "orderValuation",
            """{"description":"The customer orders a valuation","steps":{"ask":{"collect":["customer.city"]},"goodbye":{"say":"Bye"}}}"""
        );

        var list = await api.List();

        Assert.Equal(
            [("conversation", "The base of every conversation", 0), ("orderValuation", "The customer orders a valuation", 2)],
            list.Procedures.Select(procedure => (procedure.Id, procedure.Description, procedure.NotReady))
        );
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-15")]
    public async Task A_fact_shows_the_procedures_that_use_it_in_the_list_and_when_shown_and_when_saved()
    {
        var api = await NewBot();
        await api.Add("orderValuation", ProcedureApi.Valuation);
        await api.Add("other", """{"description":"d","firstStep":"a","steps":{"a":{"assign":{"system.flag":"x"}}}}""");

        var listed = await api.Facts.List();
        var shown = await FactApi.FactOf(await api.Facts.Get("order.kind"));
        var edited = await FactApi.FactOf(await api.Facts.Put("order.kind", FactApi.Kind, "?ifRevision=1"));

        Assert.Equal(["orderValuation"], listed.Single(fact => fact.Name == "customer.city").UsedBy);
        Assert.Equal(["orderValuation", "other"], listed.Single(fact => fact.Name == "system.flag").UsedBy);
        Assert.Empty(listed.Single(fact => fact.Name == "order.floors").UsedBy);
        Assert.Equal(["orderValuation"], shown.UsedBy);
        Assert.Equal(["orderValuation"], edited.UsedBy);
    }

    private sealed class DetailComparer : IEqualityComparer<ProcedureDetail>
    {
        public bool Equals(ProcedureDetail? x, ProcedureDetail? y) => x is not null && y is not null && Json(x) == Json(y);

        public int GetHashCode(ProcedureDetail obj) => Json(obj).GetHashCode(StringComparison.Ordinal);

        private static string Json(ProcedureDetail detail) => JsonSerializer.Serialize(detail with { NotReady = [] });
    }
}
