using System.Net;
using System.Text.Json;
using Suwagcms.Server.Tests.Procedures;

namespace Suwagcms.Server.Tests.Facts;

public sealed class EditGuardEndpointsTests(TestDatabase database) : IDisposable
{
    private const string Kind = """{"type":"enum","description":"What is valued","values":["house","flat","land"]}""";
    private const string Rooms = """{"type":"int","description":"Rooms","min":1,"max":50}""";
    private const string Note = """{"type":"text","description":"A note","maxLength":10}""";

    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    private async Task<ProcedureApi> NewBot()
    {
        _ = database.Pool;
        var api = await ProcedureApi.NewBotAsync(app.CreateClient());
        Assert.Equal(HttpStatusCode.OK, (await api.Facts.Add("kind", Kind)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Facts.Add("rooms", Rooms)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Facts.Add("note", Note)).StatusCode);
        return api;
    }

    private static string Procedure(string steps) => """{"description":"D","firstStep":"ask","steps":{""" + steps + "}}";

    private static string When(string fact, string condition) =>
        $$"""
            "ask":{"say":"Hi","next":[{"when":{"{{fact}}":{{condition}}},"to":"end"},{"to":"end"}]},"end":{"say":"Bye"}
            """;

    private static string Declaring(string steps, string fact) =>
        """{"description":"D","firstStep":"ask","steps":{""" + steps + """},"localFacts":{"mine":""" + fact + "}}";

    private static async Task<string> Stored(ProcedureApi api, string fact) =>
        await (await api.Facts.Get(fact)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    private static string Code(JsonElement mistake) => mistake.GetProperty("code").GetString() ?? "";

    private static string[] Referrers(JsonElement mistake) =>
        [
            .. mistake
                .GetProperty("related")
                .EnumerateArray()
                .Select(related =>
                    $"{related.GetProperty("resource").GetProperty("id").GetString()}:{related.GetProperty("location").GetString()}"
                ),
        ];

    [Fact]
    [Trait("criterion", "AC-edit-guard-1")]
    [Trait("criterion", "AC-edit-guard-9")]
    public async Task Removing_an_enum_value_a_when_and_an_assign_use_is_refused_with_every_referrer_and_the_fact_stays()
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("first", Procedure(When("kind", """{"oneOf":["flat"]}""")))).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await api.Add(
                    "second",
                    Procedure(
                        """
                        "ask":{"assign":{"kind":"flat"}}
                        """
                    )
                )
            ).StatusCode
        );
        var before = await Stored(api, "kind");

        var refused = await api.Facts.Put("kind", """{"type":"enum","description":"What is valued","values":["house","land"]}""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal("fact.enum.valueInUse", Code(mistake));
        Assert.Equal("values", mistake.GetProperty("path").GetString());
        Assert.Equal("GlobalFact", mistake.GetProperty("resource").GetProperty("kind").GetString());
        Assert.Contains("Value 'flat' of 'kind' is used", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("procedure first, step ask (when)", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("procedure second, step ask (assign)", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(["first:steps.ask.next[0].when.kind", "second:steps.ask.assign.kind"], Referrers(mistake));
        Assert.Equal(before, await Stored(api, "kind"));
    }

    [Fact]
    [Trait("criterion", "AC-edit-guard-2")]
    public async Task Removing_a_value_no_step_uses_as_a_value_adding_and_reordering_are_accepted()
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("first", Procedure(""" "ask":{"collect":["kind"]} """))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Add("second", Procedure(When("kind", """{"known":true}""")))).StatusCode);

        foreach (var values in new[] { """["house","flat"]""", """["flat","house","land","forest"]""" })
        {
            var edited = await api.Facts.Put("kind", $$"""{"type":"enum","description":"What is valued","values":{{values}}}""");
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        }
    }

    [Theory]
    [Trait("criterion", "AC-edit-guard-3")]
    [InlineData(""" "ask":{"collect":["note"]} """)]
    [InlineData(""" "ask":{"assign":{"note":"7"}} """)]
    public async Task Changing_the_type_of_a_fact_in_use_is_refused_even_when_the_use_would_still_fit(string steps)
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("first", Procedure(steps))).StatusCode);

        var refused = await api.Facts.Put("note", """{"type":"int","description":"A note","min":1,"max":9}""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal("fact.type.inUse", Code(mistake));
        Assert.Equal("type", mistake.GetProperty("path").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-edit-guard-3")]
    public async Task A_type_change_breaking_a_condition_is_one_mistake_and_a_fact_nothing_uses_may_change_type()
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("first", Procedure(When("rooms", """{"range":{"min":2}}""")))).StatusCode);

        var refused = await api.Facts.Put("rooms", Note);
        Assert.Equal(["fact.type.inUse"], await FactApi.CodesOf(refused));

        Assert.Equal(HttpStatusCode.OK, (await api.Facts.Put("note", Rooms)).StatusCode);
    }

    [Theory]
    [Trait("criterion", "AC-edit-guard-4")]
    [InlineData("""{"range":{"min":20}}""", "max")]
    [InlineData("""{"range":{"min":5,"max":40}}""", "max")]
    [InlineData("""{"oneOf":["20"]}""", "max")]
    public async Task Narrowing_an_int_past_a_value_or_a_bound_is_refused(string condition, string field)
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("first", Procedure(When("rooms", condition)))).StatusCode);

        var refused = await api.Facts.Put("rooms", """{"type":"int","description":"Rooms","min":1,"max":10}""");

        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal("fact.range.inUse", Code(mistake));
        Assert.Equal(field, mistake.GetProperty("path").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-edit-guard-4")]
    public async Task Narrowing_from_below_is_refused_beside_min_and_an_assign_counts()
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("first", Procedure(""" "ask":{"assign":{"rooms":"2"}} """))).StatusCode);

        var refused = await api.Facts.Put("rooms", """{"type":"int","description":"Rooms","min":5,"max":50}""");

        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal(("fact.range.inUse", "min"), (Code(mistake), mistake.GetProperty("path").GetString()));
    }

    [Fact]
    [Trait("criterion", "AC-edit-guard-5")]
    public async Task Narrowing_maxLength_below_a_value_in_assign_or_oneOf_is_refused()
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("first", Procedure(""" "ask":{"assign":{"note":"abcdefgh"}} """))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Add("second", Procedure(When("note", """{"oneOf":["abcdefg"]}""")))).StatusCode);

        var refused = await api.Facts.Put("note", """{"type":"text","description":"A note","maxLength":5}""");

        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal(("fact.range.inUse", "maxLength"), (Code(mistake), mistake.GetProperty("path").GetString()));
        Assert.Equal(2, Referrers(mistake).Length);
    }

    [Fact]
    [Trait("criterion", "AC-edit-guard-6")]
    public async Task Narrowing_that_leaves_everything_inside_or_a_fact_used_only_by_collect_or_known_and_any_widening_are_accepted()
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("first", Procedure(When("rooms", """{"range":{"min":2,"max":9}}""")))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Add("second", Procedure(""" "ask":{"collect":["note"]} """))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Add("third", Procedure(When("kind", """{"oneOf":["flat"]}""")))).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await api.Facts.Put("rooms", """{"type":"int","description":"Rooms","min":1,"max":10}""")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.OK,
            (await api.Facts.Put("rooms", """{"type":"int","description":"Rooms","min":0,"max":90}""")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.OK,
            (await api.Facts.Put("note", """{"type":"text","description":"A note","maxLength":2}""")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.OK,
            (await api.Facts.Put("note", """{"type":"text","description":"A note","maxLength":40}""")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await api.Facts.Put("kind", """{"type":"enum","description":"What is valued","values":["house","flat","land","office"]}""")
            ).StatusCode
        );
    }

    [Fact]
    [Trait("criterion", "AC-edit-guard-7")]
    public async Task Setting_assign_on_a_fact_a_collect_names_is_refused_and_moving_between_the_others_is_accepted()
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("first", Procedure(""" "ask":{"collect":["note"]} """))).StatusCode);

        var refused = await api.Facts.Put("note", """{"type":"text","description":"A note","maxLength":10,"writableBy":"assign"}""");

        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal(("fact.writableBy.inUse", "writableBy"), (Code(mistake), mistake.GetProperty("path").GetString()));
        Assert.Contains(
            "'collect' of step 'ask' in 'first' names fact 'note'",
            mistake.GetProperty("message").GetString(),
            StringComparison.Ordinal
        );
        Assert.Equal("Remove it from collect, or give the fact 'writableBy: set'.", mistake.GetProperty("fix").GetString());

        Assert.Equal(
            HttpStatusCode.OK,
            (await api.Facts.Put("note", """{"type":"text","description":"A note","maxLength":10,"writableBy":"correct"}""")).StatusCode
        );
        Assert.Equal(HttpStatusCode.OK, (await api.Facts.Put("note", Note)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await api.Facts.Put("rooms", """{"type":"int","description":"Rooms","min":1,"max":50,"writableBy":"assign"}""")).StatusCode
        );
        Assert.Equal(HttpStatusCode.OK, (await api.Facts.Put("rooms", Rooms)).StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-edit-guard-8")]
    public async Task A_name_starting_with_a_procedure_id_is_refused_from_either_side()
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("orderValuation", Procedure(""" "ask":{"say":"Hi"} """))).StatusCode);

        var fact = await api.Facts.Add("orderValuation.note", Note);
        Assert.Equal(["fact.name.procedurePrefix"], await FactApi.CodesOf(fact));

        var procedure = await api.Add("customer", Procedure(""" "ask":{"say":"Hi"} """));
        Assert.Equal(["fact.name.procedurePrefix"], await FactApi.CodesOf(procedure));

        Assert.Equal(HttpStatusCode.OK, (await api.Add("withLocal", Declaring(""" "ask":{"say":"Hi"} """, Note))).StatusCode);
        var factOfLocal = await api.Facts.Add("withLocal.note", Note);
        Assert.Equal(["fact.name.procedurePrefix"], await FactApi.CodesOf(factOfLocal));
        Assert.Equal(HttpStatusCode.OK, (await api.Facts.Add("later.note", Note)).StatusCode);
        var procedureOfFact = await api.Add("later", Declaring(""" "ask":{"say":"Hi"} """, Note));
        Assert.Equal(["fact.name.procedurePrefix"], await FactApi.CodesOf(procedureOfFact));
    }

    [Fact]
    [Trait("criterion", "AC-edit-guard-9")]
    public async Task One_edit_that_breaks_several_things_gets_a_mistake_for_each_at_once()
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("first", Procedure(When("kind", """{"oneOf":["flat","land"]}""")))).StatusCode);

        var refused = await api.Facts.Put("kind", """{"type":"enum","description":"What is valued","values":["house","forest"]}""");

        var mistakes = await FactApi.MistakesOf(refused);
        Assert.Equal(["fact.enum.valueInUse", "fact.enum.valueInUse"], mistakes.Select(Code));
        Assert.Contains(
            mistakes,
            mistake => mistake.GetProperty("message").GetString()?.Contains("'flat'", StringComparison.Ordinal) == true
        );
        Assert.Contains(
            mistakes,
            mistake => mistake.GetProperty("message").GetString()?.Contains("'land'", StringComparison.Ordinal) == true
        );
    }

    [Fact]
    [Trait("criterion", "AC-edit-guard-12")]
    public async Task A_dry_run_gives_the_same_refusal_or_answer_and_changes_nothing()
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("first", Procedure(When("kind", """{"oneOf":["flat"]}""")))).StatusCode);
        var narrowed = """{"type":"enum","description":"What is valued","values":["house","land"]}""";
        var before = await Stored(api, "kind");

        var dry = await api.Facts.Put("kind", narrowed, "?dryRun=true");
        var real = await api.Facts.Put("kind", narrowed);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, dry.StatusCode);
        Assert.Equal(
            (await FactApi.MistakesOf(real)).Select(mistake => mistake.GetRawText()),
            (await FactApi.MistakesOf(dry)).Select(mistake => mistake.GetRawText())
        );
        var typed = await api.Facts.Put("kind", Note, "?dryRun=true");
        Assert.Equal(["fact.type.inUse"], await FactApi.CodesOf(typed));

        var fine = await api.Facts.Put("note", """{"type":"text","description":"Other","maxLength":3}""", "?dryRun=true");
        Assert.Equal(HttpStatusCode.OK, fine.StatusCode);
        Assert.Equal(before, await Stored(api, "kind"));
        Assert.Contains("A note", await Stored(api, "note"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-edit-guard-13")]
    public async Task After_the_use_is_removed_from_the_step_the_same_edit_is_accepted()
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("first", Procedure(When("rooms", """{"range":{"min":20}}""")))).StatusCode);
        var narrowed = """{"type":"int","description":"Rooms","min":1,"max":10}""";
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await api.Facts.Put("rooms", narrowed)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await api.Put("first", Procedure(When("rooms", """{"range":{"min":5}}""")))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await api.Facts.Put("rooms", narrowed)).StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-edit-guard-14")]
    public async Task A_fact_edit_and_a_procedure_save_that_together_would_break_a_referrer_never_both_succeed()
    {
        var api = await NewBot();
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var procedure = $"race{attempt}";
            Assert.Equal(HttpStatusCode.OK, (await api.Facts.Put("rooms", Rooms)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await api.Add(procedure, Procedure(""" "ask":{"say":"Hi"} """))).StatusCode);

            var answers = await Task.WhenAll(
                api.Facts.Put("rooms", """{"type":"int","description":"Rooms","min":1,"max":10}"""),
                api.Put(procedure, Procedure(When("rooms", """{"range":{"min":20}}""")))
            );

            Assert.Single(answers, answer => answer.StatusCode == HttpStatusCode.OK);
            Assert.Single(answers, answer => answer.StatusCode == HttpStatusCode.UnprocessableEntity);
            Assert.Equal(HttpStatusCode.OK, (await api.Put(procedure, Procedure(""" "ask":{"say":"Hi"} """))).StatusCode);
        }
    }

    [Fact]
    [Trait("criterion", "AC-edit-guard-15")]
    public async Task A_procedure_of_another_bot_using_a_fact_of_the_same_name_does_not_block_the_edit()
    {
        var api = await NewBot();
        var other = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await other.Add("first", Procedure(When("kind", """{"oneOf":["flat"]}""")))).StatusCode);

        var edited = await api.Facts.Put("kind", """{"type":"enum","description":"What is valued","values":["house","land"]}""");

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
    }

    [Theory]
    [Trait("criterion", "AC-edit-guard-17")]
    [InlineData("""{"type":"enum","description":"K","values":["house","land"]}""", "fact.enum.valueInUse", "localFacts.mine.values")]
    [InlineData("""{"type":"text","description":"K","maxLength":9}""", "fact.type.inUse", "localFacts.mine.type")]
    public async Task A_save_of_a_procedure_breaking_its_own_step_through_a_local_fact_is_refused_the_same_way(
        string changed,
        string code,
        string path
    )
    {
        var api = await NewBot();
        var steps = When("mine", """{"oneOf":["flat"]}""");
        Assert.Equal(
            HttpStatusCode.OK,
            (await api.Add("first", Declaring(steps, """{"type":"enum","description":"K","values":["house","flat"]}"""))).StatusCode
        );

        var refused = await api.Put("first", Declaring(steps, changed));

        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal((code, path), (Code(mistake), mistake.GetProperty("path").GetString()));
        Assert.Equal(["first:steps.ask.next[0].when.mine"], Referrers(mistake));
    }

    [Theory]
    [Trait("criterion", "AC-edit-guard-17")]
    [InlineData(
        """ "ask":{"say":"Hi","next":[{"when":{"mine":{"range":{"min":20}}},"to":"end"},{"to":"end"}]},"end":{"say":"Bye"} """,
        """{"type":"int","description":"K","min":1,"max":50}""",
        """{"type":"int","description":"K","min":1,"max":10}""",
        "fact.range.inUse"
    )]
    [InlineData(
        """ "ask":{"collect":["mine"]} """,
        """{"type":"text","description":"K","maxLength":9}""",
        """{"type":"text","description":"K","maxLength":9,"writableBy":"assign"}""",
        "fact.writableBy.inUse"
    )]
    public async Task Narrowing_a_local_fact_or_setting_assign_is_refused_with_the_codes_of_a_global_one(
        string steps,
        string was,
        string now,
        string code
    )
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("first", Declaring(steps, was))).StatusCode);

        Assert.Equal([code], await FactApi.CodesOf(await api.Put("first", Declaring(steps, now))));
    }
}
