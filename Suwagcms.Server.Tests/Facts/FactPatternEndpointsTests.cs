using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Suwagcms.Contracts.Facts;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Server.Tests.Changes;
using Suwagcms.Server.Tests.Procedures;
using Suwagcms.Server.Tests.Versions;

namespace Suwagcms.Server.Tests.Facts;

public sealed class FactPatternEndpointsTests(TestDatabase database, ClassServer server) : IClassFixture<ClassServer>
{
    private const string Coded =
        """{"type":"text","description":"The code","maxLength":10,"pattern":{"regex":"[A-Z]{2}","means":"two capitals","example":"AB","tellCustomer":"afterRefusal"}}""";

    private static readonly TextPattern CodedPattern = new()
    {
        Regex = "[A-Z]{2}",
        Means = "two capitals",
        Example = "AB",
        TellCustomer = TellCustomers.AfterRefusal,
    };

    private TestApp App => server.App;

    private async Task<ProcedureApi> NewBot()
    {
        _ = database.DataSource;
        return await ProcedureApi.NewBotAsync(App.CreateClient());
    }

    private static string Procedure(string steps, string extra = "") =>
        """{"description":"D","firstStep":"ask","steps":{""" + steps + "}" + extra + "}";

    private static string Assign(string fact, string value) => "\"ask\":{\"assign\":{\"" + fact + "\":\"" + value + "\"}}";

    private static string When(string fact, string value) =>
        "\"ask\":{\"say\":\"Hi\",\"next\":[{\"when\":{\""
        + fact
        + "\":{\"oneOf\":[\""
        + value
        + "\"]}},\"to\":\"end\"},{\"to\":\"end\"}]},\"end\":{\"say\":\"Bye\"}";

    [Fact]
    [Trait("criterion", "AC-fact-pattern-1")]
    public async Task A_global_fact_saves_and_reads_back_its_pattern_and_a_save_without_one_clears_it()
    {
        var api = await NewBot();

        var added = await api.Facts.Add("order.code", Coded);
        var read = await FactApi.FactOf(await api.Facts.Get("order.code"));
        var cleared = await api.Facts.Put("order.code", FactApi.Text("The code", 10), "?ifRevision=1");
        var plain = await FactApi.FactOf(await api.Facts.Get("order.code"));

        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        Assert.Equal(CodedPattern, read.Pattern);
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Null(plain.Pattern);
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-1")]
    public async Task A_local_fact_saves_and_reads_back_its_pattern()
    {
        var api = await NewBot();

        var saved = await api.Add(
            "first",
            Procedure(
                Assign("code", "AB"),
                ""","localFacts":{"code":{"type":"text","description":"D","maxLength":5,"pattern":{"regex":"[A-Z]{2}","means":"two capitals"}}}"""
            )
        );
        var body = JsonDocument.Parse(await (await api.Get("first")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var pattern = body.RootElement.GetProperty("localFacts").GetProperty("code").GetProperty("pattern");
        Assert.Equal(
            ("[A-Z]{2}", "two capitals", "upfront"),
            (
                pattern.GetProperty("regex").GetString(),
                pattern.GetProperty("means").GetString(),
                pattern.GetProperty("tellCustomer").GetString()
            )
        );
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-1")]
    public async Task A_version_holds_the_pattern_and_a_fact_without_one_has_no_such_member_while_the_api_answers_null()
    {
        _ = database.DataSource;
        var versions = await (await VersionApi.NewBotAsync(App.CreateClient())).Ready();
        var facts = new FactApi(versions.Client, versions.Bot);
        Assert.Equal(HttpStatusCode.OK, (await facts.Add("order.code", Coded)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await facts.Add("order.note", FactApi.Text("A note", 10))).StatusCode);

        var created = await versions.Created();
        var document = await versions.DocumentOf(created.Version.Number);

        var found = BotDefinition.Read(document.GetRawText()).Facts;
        Assert.Equal(CodedPattern, found.Single(fact => fact.Name == "order.code").Pattern);
        var note = document.GetProperty("facts").EnumerateArray().Single(fact => fact.GetProperty("name").GetString() == "order.note");
        Assert.False(note.TryGetProperty("pattern", out _));
        var answered = JsonDocument.Parse(
            await (await facts.Get("order.note")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(JsonValueKind.Null, answered.RootElement.GetProperty("pattern").ValueKind);
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-15")]
    public async Task The_comparison_of_two_versions_names_a_changed_pattern()
    {
        _ = database.DataSource;
        var versions = await (await VersionApi.NewBotAsync(App.CreateClient())).Ready();
        var facts = new FactApi(versions.Client, versions.Bot);
        await facts.Add("order.code", Coded);
        var from = (await versions.Created()).Version.Number;
        await facts.Put("order.code", Coded.Replace("two capitals", "two big letters", StringComparison.Ordinal), "?ifRevision=1");
        var to = (await versions.Created()).Version.Number;

        var comparison = await versions.Client.GetFromJsonAsync<VersionComparison>(
            $"/api/bots/{versions.Bot}/versions/{from}/diff/{to}",
            TestContext.Current.CancellationToken
        );

        var changed = Assert.Single(comparison?.Resources ?? []);
        Assert.Equal("order.code", changed.Id);
        Assert.Contains(changed.Fields, field => field.Field.StartsWith("pattern", StringComparison.Ordinal));
    }

    [Theory]
    [Trait("criterion", "AC-fact-pattern-3")]
    [InlineData(
        """{"type":"text","description":"d","maxLength":5,"pattern":{"means":"m"}}""",
        "fact.pattern.regexMissing",
        "pattern.regex"
    )]
    [InlineData(
        """{"type":"text","description":"d","maxLength":5,"pattern":{"regex":"a","means":" "}}""",
        "fact.pattern.meansMissing",
        "pattern.means"
    )]
    [InlineData(
        """{"type":"text","description":"d","maxLength":5,"pattern":{"regex":"(?=a)b","means":"m"}}""",
        "fact.pattern.regexInvalid",
        "pattern.regex"
    )]
    [InlineData(
        """{"type":"int","description":"d","min":1,"max":2,"pattern":{"regex":"a","means":"m"}}""",
        "fact.constraint.foreign",
        "pattern"
    )]
    public async Task A_save_with_a_bad_pattern_is_refused_beside_the_field_and_nothing_is_saved(string body, string code, string path)
    {
        var api = await NewBot();

        var refused = await api.Facts.Add("order.code", body);

        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal((code, path), (mistake.GetProperty("code").GetString(), mistake.GetProperty("path").GetString()));
        Assert.Equal(HttpStatusCode.NotFound, (await api.Facts.Get("order.code")).StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-3")]
    public async Task A_field_a_pattern_does_not_have_is_refused_and_not_dropped()
    {
        var api = await NewBot();

        var refused = await api.Facts.Add(
            "order.code",
            """{"type":"text","description":"d","maxLength":5,"pattern":{"regex":"a","means":"m","hint":"h"}}"""
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Contains("save.invalid", await FactApi.CodesOf(refused));
        Assert.Equal(HttpStatusCode.NotFound, (await api.Facts.Get("order.code")).StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-3")]
    public async Task A_field_a_pattern_of_a_local_fact_does_not_have_is_refused_and_nothing_is_saved()
    {
        var api = await NewBot();

        var refused = await api.Add(
            "first",
            Procedure(
                Assign("code", "AB"),
                ""","localFacts":{"code":{"type":"text","description":"D","maxLength":5,"pattern":{"regex":"[A-Z]{2}","means":"m","hint":"h"}}}"""
            )
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Contains("save.invalid", await FactApi.CodesOf(refused));
        Assert.Equal(HttpStatusCode.NotFound, (await api.Get("first")).StatusCode);
    }

    [Theory]
    [Trait("criterion", "AC-fact-pattern-7")]
    [InlineData("assign.value.invalid", true)]
    [InlineData("condition.value.invalid", false)]
    public async Task A_procedure_with_a_value_the_pattern_refuses_is_refused_and_with_one_it_takes_is_saved(string code, bool assign)
    {
        var api = await NewBot();
        await api.Facts.Add("order.code", Coded);
        string Steps(string value)
        {
            return assign ? Assign("order.code", value) : When("order.code", value);
        }

        var refused = await api.Add("first", Procedure(Steps("ab")));
        var saved = await api.Add("first", Procedure(Steps("AB")));

        Assert.Equal(code, (await FactApi.CodesOf(refused)).Single());
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    }

    [Theory]
    [Trait("criterion", "AC-fact-pattern-8")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_pattern_edit_a_use_no_longer_satisfies_is_refused_with_its_steps_and_the_fact_stays(bool assign)
    {
        var api = await NewBot();
        await api.Facts.Add("order.code", Coded);
        await api.Add("first", Procedure(assign ? Assign("order.code", "AB") : When("order.code", "AB")));

        var refused = await api.Facts.Put(
            "order.code",
            """{"type":"text","description":"The code","maxLength":10,"pattern":{"regex":"[0-9]{2}","means":"two digits"}}""",
            "?ifRevision=1"
        );

        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal(
            ("fact.pattern.inUse", "pattern.regex"),
            (mistake.GetProperty("code").GetString(), mistake.GetProperty("path").GetString())
        );
        Assert.Equal(
            "first",
            Assert.Single(mistake.GetProperty("related").EnumerateArray().ToList()).GetProperty("resource").GetProperty("id").GetString()
        );
        Assert.Equal(CodedPattern, (await FactApi.FactOf(await api.Facts.Get("order.code"))).Pattern);
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-8")]
    public async Task An_edit_of_a_local_facts_pattern_is_refused_beside_it_naming_the_step_and_one_every_use_satisfies_is_saved()
    {
        var api = await NewBot();
        const string Local =
            ""","localFacts":{"code":{"type":"text","description":"D","maxLength":5,"pattern":{"regex":"REGEX","means":"m"}}}""";
        await api.Add("first", Procedure(Assign("code", "AB"), Local.Replace("REGEX", "[A-Z]{2}", StringComparison.Ordinal)));

        var refused = await api.Put(
            "first",
            Procedure(Assign("code", "AB"), Local.Replace("REGEX", "[0-9]{2}", StringComparison.Ordinal)),
            "?ifRevision=1"
        );

        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal(
            ("fact.pattern.inUse", "localFacts.code.pattern.regex"),
            (mistake.GetProperty("code").GetString(), mistake.GetProperty("path").GetString())
        );
        Assert.Contains("procedure first, step ask", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal("[A-Z]{2}", await LocalRegex(api));
        var accepted = await api.Put(
            "first",
            Procedure(Assign("code", "AB"), Local.Replace("REGEX", "[A-F]{2}", StringComparison.Ordinal)),
            "?ifRevision=1"
        );
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("[A-F]{2}", await LocalRegex(api));
    }

    private static async Task<string?> LocalRegex(ProcedureApi api)
    {
        var body = JsonDocument.Parse(await (await api.Get("first")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return body.RootElement.GetProperty("localFacts").GetProperty("code").GetProperty("pattern").GetProperty("regex").GetString();
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-13")]
    public async Task A_file_of_changes_adds_a_pattern_keeps_it_through_an_edit_and_takes_it_off_with_null()
    {
        _ = database.DataSource;
        var scene = await ChangeScene.NewAsync(App.CreateClient());

        var added = await scene.CliAsync(
            """
            changes:
              - add: fact
                name: order.code
                type: text
                description: The code
                maxLength: 10
                pattern: { regex: '[0-9]{4}', means: four digits, example: 1234, tellCustomer: afterRefusal }
            """
        );
        var first = (await FactApi.FactOf(await scene.Facts.Get("order.code"))).Pattern;
        var edited = await scene.CliAsync(
            """
            changes:
              - edit: fact
                name: order.code
                description: The code of the order
            """
        );
        var kept = (await FactApi.FactOf(await scene.Facts.Get("order.code"))).Pattern;
        var removed = await scene.CliAsync(
            """
            changes:
              - edit: fact
                name: order.code
                pattern: null
            """
        );
        var gone = (await FactApi.FactOf(await scene.Facts.Get("order.code"))).Pattern;

        Assert.Equal(0, added.ExitCode);
        Assert.Equal(
            new TextPattern
            {
                Regex = "[0-9]{4}",
                Means = "four digits",
                Example = "1234",
                TellCustomer = "afterRefusal",
            },
            first
        );
        Assert.Equal(0, edited.ExitCode);
        Assert.Equal(first, kept);
        Assert.Equal(0, removed.ExitCode);
        Assert.Null(gone);
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-13")]
    public async Task An_edit_in_a_file_of_changes_naming_a_pattern_replaces_the_whole_of_it()
    {
        _ = database.DataSource;
        var scene = await ChangeScene.NewAsync(App.CreateClient());
        await scene.Facts.Add("order.code", Coded);

        var edited = await scene.CliAsync(
            """
            changes:
              - edit: fact
                name: order.code
                pattern: { regex: '[A-Z]{3}', means: three capitals }
            """
        );

        Assert.Equal(0, edited.ExitCode);
        Assert.Equal(
            new TextPattern { Regex = "[A-Z]{3}", Means = "three capitals" },
            (await FactApi.FactOf(await scene.Facts.Get("order.code"))).Pattern
        );
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-13")]
    public async Task An_edit_in_a_file_of_changes_from_text_to_int_drops_the_pattern()
    {
        _ = database.DataSource;
        var scene = await ChangeScene.NewAsync(App.CreateClient());
        await scene.Facts.Add("order.code", Coded);

        var edited = await scene.Post(ChangeScene.File("""{"edit":"fact","name":"order.code","type":"int","min":1,"max":5}"""));

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var fact = await FactApi.FactOf(await scene.Facts.Get("order.code"));
        Assert.Equal("int", fact.Type);
        Assert.Null(fact.Pattern);
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-12")]
    public async Task The_commands_add_edit_show_and_take_off_a_pattern_on_the_real_server()
    {
        _ = database.DataSource;
        var scene = await ChangeScene.NewAsync(App.CreateClient());

        var added = await scene.CommandAsync(
            "facts",
            "add",
            "order.code",
            "--type",
            "text",
            "--max-length",
            "10",
            "--description",
            "The code",
            "--pattern-regex",
            "[A-Z]{2}",
            "--pattern-means",
            "two capitals",
            "--pattern-example",
            "AB",
            "--pattern-tell-customer",
            "afterRefusal"
        );
        var first = (await FactApi.FactOf(await scene.Facts.Get("order.code"))).Pattern;
        var edited = await scene.CommandAsync("facts", "edit", "order.code", "--pattern-means", "two big letters");
        var second = (await FactApi.FactOf(await scene.Facts.Get("order.code"))).Pattern;
        var shown = await scene.CommandAsync("facts", "show", "order.code");
        var removed = await scene.CommandAsync("facts", "edit", "order.code", "--no-pattern");
        var third = (await FactApi.FactOf(await scene.Facts.Get("order.code"))).Pattern;
        var shownBare = await scene.CommandAsync("facts", "show", "order.code");

        Assert.Equal(0, added.ExitCode);
        Assert.Equal(CodedPattern, first);
        Assert.Equal(0, edited.ExitCode);
        Assert.Equal(CodedPattern with { Means = "two big letters" }, second);
        Assert.Equal(0, shown.ExitCode);
        Assert.Contains("means:     two big letters", shown.Output, StringComparison.Ordinal);
        Assert.Contains("example:   AB", shown.Output, StringComparison.Ordinal);
        Assert.Contains("tell:      afterRefusal", shown.Output, StringComparison.Ordinal);
        Assert.Equal(0, removed.ExitCode);
        Assert.Null(third);
        Assert.DoesNotContain("means:", shownBare.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-16")]
    public async Task An_enum_listing_other_or_a_line_break_is_refused_at_the_value()
    {
        var api = await NewBot();

        var other = await api.Facts.Add("order.mode", """{"type":"enum","description":"d","values":["a","other"]}""");
        var lineBreak = await api.Facts.Add("order.mode", """{"type":"enum","description":"d","values":["a\nb","c"]}""");

        Assert.Equal(["fact.enum.valueReserved"], await FactApi.CodesOf(other));
        Assert.Equal("values[1]", Assert.Single(await FactApi.MistakesOf(other)).GetProperty("path").GetString());
        Assert.Equal(["fact.enum.valueUnusable"], await FactApi.CodesOf(lineBreak));
    }
}
