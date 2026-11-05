using System.Net;
using System.Text.Json;
using Suwagcms.Server.Tests.Articles;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Procedures;

namespace Suwagcms.Server.Tests.Changes;

public sealed class ApplyTests(TestDatabase database) : IDisposable
{
    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    private Task<ChangeScene> NewScene()
    {
        _ = database.Pool;
        return ChangeScene.NewAsync(app.CreateClient());
    }

    private static List<JsonElement> Mistakes(JsonElement problem) => [.. problem.GetProperty("mistakes").EnumerateArray()];

    private static string[] Codes(JsonElement problem) =>
        [.. Mistakes(problem).Select(mistake => mistake.GetProperty("code").GetString() ?? "").Order(StringComparer.Ordinal)];

    [Fact]
    [Trait("criterion", "AC-batch-apply-1")]
    public async Task A_file_with_adds_edits_and_deletes_saves_them_all_and_they_read_back_as_it_said()
    {
        var scene = await NewScene();
        await scene.Facts.Add("customer.city", FactApi.Text("Old description"));
        await scene.Facts.Add("order.gone", FactApi.Text());
        await scene.Post(
            ChangeScene.File(
                ChangeScene.AddBase("oferta", "Old title"),
                ChangeScene.AddBase("stara"),
                ChangeScene.AddArticle("stary", "oferta"),
                ChangeScene.AddArticle("rabaty", "oferta")
            )
        );

        var run = await scene.CliAsync(
            """
            changes:
              - add: fact
                name: order.floors
                type: int
                description: How many floors
                min: 1
                max: 40
              - add: article
                id: cennik
                knowledgeBase: oferta
                title: Cennik
                description: Prices.
                llmGuide: Ask first.
                content: A price list.
              - edit: fact
                name: customer.city
                description: The city of the property.
              - edit: knowledgeBase
                id: oferta
                title: New title
              - edit: article
                id: rabaty
                title: Rabaty
              - delete: fact
                name: order.gone
              - delete: article
                id: stary
              - delete: knowledgeBase
                id: stara
            """
        );

        Assert.Equal("", run.Error);
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("Applied: 2 added, 3 edited, 3 deleted.", run.Output, StringComparison.Ordinal);
        Assert.Contains("\"min\":1", await scene.ReadAsync("facts", "order.floors"), StringComparison.Ordinal);
        Assert.Contains("A price list.", await scene.ReadAsync("articles", "cennik"), StringComparison.Ordinal);
        Assert.Contains("The city of the property.", await scene.ReadAsync("facts", "customer.city"), StringComparison.Ordinal);
        Assert.Contains("New title", await scene.ReadAsync("knowledge-bases", "oferta"), StringComparison.Ordinal);
        Assert.Contains("\"title\":\"Rabaty\"", await scene.ReadAsync("articles", "rabaty"), StringComparison.Ordinal);
        Assert.Equal("absent", await scene.ReadAsync("facts", "order.gone"));
        Assert.Equal("absent", await scene.ReadAsync("articles", "stary"));
        Assert.Equal("absent", await scene.ReadAsync("knowledge-bases", "stara"));
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-2")]
    public async Task A_file_with_one_bad_change_saves_nothing_and_the_command_exits_1()
    {
        var scene = await NewScene();
        await scene.Facts.Add("customer.city", FactApi.Text("Before"));
        var before = await scene.RowsAsync(database.Pool);

        var run = await scene.CliAsync(
            """
            changes:
              - edit: fact
                name: customer.city
                description: After
              - add: fact
                name: order.floors
                type: text
                description: A good one
                maxLength: 9
              - add: fact
                name: order.bad
                type: text
                maxLength: 9
            """
        );

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("change 3, fact 'order.bad'", run.Error, StringComparison.Ordinal);
        Assert.Contains("fact.description.missing", run.Error, StringComparison.Ordinal);
        Assert.Equal(before, await scene.RowsAsync(database.Pool));
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-3")]
    public async Task Several_mistakes_come_back_at_once_each_naming_its_position_and_resource()
    {
        var scene = await NewScene();
        var response = await scene.Post(
            ChangeScene.File(
                ChangeScene.AddFact("order.good"),
                """{"add":"fact","name":"order.bad","type":"text","maxLength":9}""",
                ChangeScene.AddArticle("rabaty", "offer", "See [it](cms://article/wycena).")
            )
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var found = Mistakes(await ChangeScene.JsonOf(response)).Select(Placed).ToList();
        Assert.Contains((2, "fact.description.missing", "order.bad"), found);
        Assert.Contains((3, "article.knowledgeBase.unknown", "rabaty"), found);
        Assert.Contains((3, "reference.missing", "rabaty"), found);
        Assert.Equal(found.OrderBy(item => item.Item1), found);
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-3")]
    public async Task What_is_out_of_date_comes_back_at_once_before_the_rules_are_asked()
    {
        var scene = await NewScene();
        await scene.Facts.Add("customer.city", FactApi.Text());
        var response = await scene.Post(
            ChangeScene.File(
                """{"add":"fact","name":"order.bad","type":"text","maxLength":9}""",
                """{"edit":"fact","name":"customer.city","ifRevision":7,"description":"Late"}""",
                """{"delete":"article","id":"nobody"}""",
                ChangeScene.AddFact("customer.city2"),
                ChangeScene.AddFact("customer.city2")
            )
        );

        var found = Mistakes(await ChangeScene.JsonOf(response)).Select(Placed).ToList();
        Assert.Contains((2, "resource.stale", "customer.city"), found);
        Assert.Contains((3, "resource.missing", "nobody"), found);
        Assert.Contains((4, "change.twice", "customer.city2"), found);
        Assert.DoesNotContain(found, item => item.Item2 == "fact.description.missing");
    }

    private static (int, string, string) Placed(JsonElement mistake) =>
        (
            mistake.GetProperty("change").GetInt32(),
            mistake.GetProperty("code").GetString() ?? "",
            mistake.GetProperty("resource").GetProperty("id").GetString() ?? ""
        );

    [Fact]
    [Trait("criterion", "AC-batch-apply-4")]
    public async Task The_same_bad_resource_is_refused_with_the_same_codes_and_messages_as_through_its_own_save()
    {
        var scene = await NewScene();
        const string bad = """{"type":"enum","description":"","values":["only"],"writableBy":"nobody"}""";
        var own = await FactApi.MistakesOf(await scene.Facts.Add("order.bad", bad));

        var response = await scene.Post(
            ChangeScene.File("""{"add":"fact","name":"order.bad","type":"enum","description":"","values":["only"],"writableBy":"nobody"}""")
        );
        var through = Mistakes(await ChangeScene.JsonOf(response));

        static string Shown(JsonElement mistake)
        {
            return $"{mistake.GetProperty("code").GetString()}|{mistake.GetProperty("path").GetString()}|{mistake.GetProperty("message").GetString()}|{mistake.GetProperty("fix").GetString()}";
        }
        Assert.NotEmpty(own);
        Assert.Equal(own.Select(Shown).Order(StringComparer.Ordinal), through.Select(Shown).Order(StringComparer.Ordinal));

        var ownArticle = await FactApi.MistakesOf(await scene.Articles.Add("bad", ArticleApi.Body(knowledgeBase: "nope", description: "")));
        var articleResponse = await scene.Post(
            ChangeScene.File(
                """{"add":"article","id":"bad","knowledgeBase":"nope","title":"Price list","content":"What a valuation costs.","llmGuide":"Ask which kind of property first.","description":""}"""
            )
        );
        var throughArticle = Mistakes(await ChangeScene.JsonOf(articleResponse));
        Assert.NotEmpty(ownArticle);
        Assert.Equal(ownArticle.Select(Shown).Order(StringComparer.Ordinal), throughArticle.Select(Shown).Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-5")]
    public async Task The_changes_of_a_file_in_any_order_give_the_same_result()
    {
        string[] changes =
        [
            ChangeScene.AddArticle("cennik", "oferta", "See [it](cms://article/rabaty)."),
            ChangeScene.AddArticle("rabaty", "oferta"),
            ChangeScene.AddBase("oferta"),
            ChangeScene.AddFact("customer.city"),
        ];
        var results = new List<string>();
        foreach (var order in new[] { new[] { 0, 1, 2, 3 }, [3, 2, 1, 0], [1, 3, 0, 2] })
        {
            var scene = await NewScene();
            var response = await scene.Post(ChangeScene.File([.. order.Select(index => changes[index])]));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            results.Add(
                string.Join(
                    "\n",
                    await scene.ReadAsync("articles", "cennik"),
                    await scene.ReadAsync("articles", "rabaty"),
                    await scene.ReadAsync("knowledge-bases", "oferta"),
                    await scene.ReadAsync("facts", "customer.city")
                )
            );
        }

        Assert.Single(results.Distinct(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-6")]
    public async Task Two_changes_to_one_resource_are_refused_naming_both()
    {
        var scene = await NewScene();

        var response = await scene.Post(
            ChangeScene.File(
                ChangeScene.AddFact("order.first"),
                ChangeScene.AddFact("customer.city"),
                """{"edit":"fact","name":"customer.city","description":"Twice"}"""
            )
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var mistake = Assert.Single(Mistakes(await ChangeScene.JsonOf(response)));
        Assert.Equal("change.twice", mistake.GetProperty("code").GetString());
        Assert.Contains("Changes 2 and 3", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal("customer.city", mistake.GetProperty("resource").GetProperty("id").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-6")]
    public async Task A_delete_of_what_the_same_file_adds_says_it_is_changed_twice_and_never_that_it_is_missing()
    {
        var scene = await NewScene();

        var response = await scene.Post(
            ChangeScene.File(ChangeScene.AddFact("customer.city"), """{"delete":"fact","name":"customer.city"}""")
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(["change.twice"], Codes(await ChangeScene.JsonOf(response)));
        Assert.Equal("absent", await scene.ReadAsync("facts", "customer.city"));
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-7")]
    public async Task Resources_the_file_does_not_name_keep_every_byte_and_their_revision()
    {
        var scene = await NewScene();
        await scene.Facts.Add("order.left", FactApi.Text("Left alone"));
        var left = (await scene.RowsAsync(database.Pool)).Single();

        var response = await scene.Post(ChangeScene.File(ChangeScene.AddFact("customer.city"), ChangeScene.AddBase("oferta")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(left, await scene.RowsAsync(database.Pool));
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-8")]
    public async Task A_dry_run_answers_as_the_real_file_does_and_changes_nothing()
    {
        var scene = await NewScene();
        await scene.Facts.Add("customer.city", FactApi.Text());
        var before = await scene.RowsAsync(database.Pool);
        var good = ChangeScene.File(ChangeScene.AddFact("order.first"), """{"edit":"fact","name":"customer.city","description":"New"}""");

        var dry = await ChangeScene.JsonOf(await scene.Post(good, dryRun: true));
        Assert.Equal(before, await scene.RowsAsync(database.Pool));
        var real = await ChangeScene.JsonOf(await scene.Post(good));

        Assert.True(dry.GetProperty("dryRun").GetBoolean());
        Assert.False(real.GetProperty("dryRun").GetBoolean());
        Assert.Equal(dry.GetProperty("changes").GetRawText(), real.GetProperty("changes").GetRawText());
        Assert.Equal(1, real.GetProperty("added").GetInt32());
        Assert.Equal(1, real.GetProperty("edited").GetInt32());

        var bad = ChangeScene.File("""{"add":"fact","name":"order.bad","type":"text","maxLength":9}""");
        var dryRefusal = await scene.Post(bad, dryRun: true);
        var realRefusal = await scene.Post(bad);
        Assert.Equal(realRefusal.StatusCode, dryRefusal.StatusCode);
        Assert.Equal(
            (await ChangeScene.JsonOf(realRefusal)).GetProperty("mistakes").GetRawText(),
            (await ChangeScene.JsonOf(dryRefusal)).GetProperty("mistakes").GetRawText()
        );
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-8")]
    public async Task The_command_says_would_on_a_dry_run_and_the_same_counts_as_the_real_one()
    {
        var scene = await NewScene();
        var file = """
            changes:
              - add: fact
                name: customer.city
                type: text
                description: The city
                maxLength: 80
            """;

        var dry = await scene.CliAsync(file, "--dry-run");
        Assert.Equal("absent", await scene.ReadAsync("facts", "customer.city"));
        var real = await scene.CliAsync(file);

        Assert.Equal(0, dry.ExitCode);
        Assert.Contains("Would apply: 1 added, 0 edited, 0 deleted.", dry.Output, StringComparison.Ordinal);
        Assert.Contains("Applied: 1 added, 0 edited, 0 deleted.", real.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-9")]
    public async Task An_old_revision_refuses_the_whole_file_with_the_current_revision()
    {
        var scene = await NewScene();
        await scene.Facts.Add("customer.city", FactApi.Text("One"));
        await scene.Facts.Put("customer.city", FactApi.Text("Two"));
        var before = await scene.RowsAsync(database.Pool);

        var response = await scene.Post(
            ChangeScene.File(
                ChangeScene.AddFact("order.first"),
                """{"edit":"fact","name":"customer.city","ifRevision":1,"description":"Late"}"""
            )
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var mistake = Assert.Single(Mistakes(await ChangeScene.JsonOf(response)));
        Assert.Equal("resource.stale", mistake.GetProperty("code").GetString());
        Assert.Contains("it is now revision 2", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(before, await scene.RowsAsync(database.Pool));
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-9")]
    public async Task A_change_without_a_revision_applies_to_the_latest_and_an_edit_changes_only_the_fields_it_gives()
    {
        var scene = await NewScene();
        await scene.Facts.Add("customer.city", FactApi.Text("One", 80));
        await scene.Facts.Put("customer.city", FactApi.Text("Two", 80));

        var response = await scene.Post(
            ChangeScene.File(
                """{"edit":"fact","name":"customer.city","maxLength":99}""",
                """{"edit":"fact","name":"customer.gone","description":"x"}"""
            )
        );
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(["resource.missing"], Codes(await ChangeScene.JsonOf(response)));

        var edited = await scene.Post(ChangeScene.File("""{"edit":"fact","name":"customer.city","maxLength":99}"""));

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal(3, (await ChangeScene.JsonOf(edited)).GetProperty("changes")[0].GetProperty("revision").GetInt32());
        var read = await scene.ReadAsync("facts", "customer.city");
        Assert.Contains("\"description\":\"Two\"", read, StringComparison.Ordinal);
        Assert.Contains("\"maxLength\":99", read, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-10")]
    public async Task A_field_the_api_does_not_know_is_refused_naming_the_change_and_the_field()
    {
        var scene = await NewScene();

        var response = await scene.Post(
            ChangeScene.File(
                ChangeScene.AddFact("order.first"),
                """{"edit":"fact","name":"customer.city","descripton":"Typo"}""",
                """{"delete":"fact","name":"order.x","available":false}"""
            )
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var mistakes = Mistakes(await ChangeScene.JsonOf(response));
        Assert.Equal(2, mistakes.Count);
        Assert.Equal(2, mistakes[0].GetProperty("change").GetInt32());
        Assert.Equal("save.unknownField", mistakes[0].GetProperty("code").GetString());
        Assert.Equal("descripton", mistakes[0].GetProperty("path").GetString());
        Assert.Equal(3, mistakes[1].GetProperty("change").GetInt32());
        Assert.Equal("available", mistakes[1].GetProperty("path").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-19")]
    public async Task An_unreadable_file_exits_2_naming_the_line_and_an_empty_list_says_there_is_nothing_to_apply()
    {
        var scene = await NewScene();

        var broken = await scene.CliAsync("changes:\n  - add: fact\n    name: [unclosed\n");
        var noList = await scene.CliAsync("bot: x\n");
        var empty = await scene.CliAsync("changes: []\n");

        Assert.Equal(2, broken.ExitCode);
        Assert.Contains("line ", broken.Error, StringComparison.Ordinal);
        Assert.Equal(2, noList.ExitCode);
        Assert.Contains("no `changes` list", noList.Error, StringComparison.Ordinal);
        Assert.Equal(0, empty.ExitCode);
        Assert.Contains("Nothing to apply", empty.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-10")]
    public async Task A_bot_other_than_the_one_in_use_is_refused_naming_both()
    {
        var scene = await NewScene();

        var run = await scene.CliAsync(
            $"bot: another\nchanges:\n  - add: fact\n    name: customer.city\n    type: text\n    description: x\n    maxLength: 9\n"
        );

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("'another'", run.Error, StringComparison.Ordinal);
        Assert.Contains($"'{scene.Bot}'", run.Error, StringComparison.Ordinal);
        Assert.Equal("absent", await scene.ReadAsync("facts", "customer.city"));
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-11")]
    public async Task A_file_can_set_the_language_of_a_knowledge_base_and_en_applies_without_it()
    {
        var scene = await NewScene();

        var response = await scene.Post(
            ChangeScene.File(
                """{"add":"knowledgeBase","id":"oferta","title":"Oferta","language":"pl"}""",
                ChangeScene.AddBase("offer", "Offer")
            )
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"language\":\"pl\"", await scene.ReadAsync("knowledge-bases", "oferta"), StringComparison.Ordinal);
        Assert.Contains("\"language\":\"en\"", await scene.ReadAsync("knowledge-bases", "offer"), StringComparison.Ordinal);

        await scene.Post(ChangeScene.File("""{"edit":"knowledgeBase","id":"oferta","title":"Nowa"}"""));
        Assert.Contains("\"language\":\"pl\"", await scene.ReadAsync("knowledge-bases", "oferta"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-12")]
    public async Task A_file_and_a_save_at_the_same_moment_run_one_after_the_other_and_a_refused_file_leaves_no_trace()
    {
        var scene = await NewScene();
        await scene.Facts.Add("customer.city", FactApi.Text("Start"));

        for (var round = 1; round <= 5; round++)
        {
            var name = $"order.round{round}";
            var file = scene.Post(
                ChangeScene.File(
                    ChangeScene.AddFact(name),
                    $$"""{"edit":"fact","name":"customer.city","ifRevision":{{round}},"description":"File {{round}}"}"""
                )
            );
            var save = scene.Facts.Put("customer.city", FactApi.Text($"Save {round}"), $"?ifRevision={round}");
            await Task.WhenAll(file, save);

            Assert.True(
                (await file).IsSuccessStatusCode != (await save).IsSuccessStatusCode,
                "Both were checked against the same revision: exactly one must win."
            );
            var read = await scene.ReadAsync("facts", name);
            if ((await file).IsSuccessStatusCode)
            {
                Assert.NotEqual("absent", read);
            }
            else
            {
                Assert.Equal("absent", read);
            }
        }
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-13")]
    public async Task Json_returns_the_result_per_change_and_the_mistakes_as_a_save_returns_them()
    {
        var scene = await NewScene();
        var good = await scene.CliAsync(
            "changes:\n  - add: fact\n    name: customer.city\n    type: text\n    description: The city\n    maxLength: 80\n",
            "--json"
        );
        using var answer = JsonDocument.Parse(good.Output);
        var result = answer.RootElement.GetProperty("changes")[0];
        Assert.Equal(1, result.GetProperty("change").GetInt32());
        Assert.Equal("added", result.GetProperty("action").GetString());
        Assert.Equal("fact", result.GetProperty("kind").GetString());
        Assert.Equal("customer.city", result.GetProperty("id").GetString());
        Assert.Equal(1, result.GetProperty("revision").GetInt32());

        var bad = await scene.CliAsync("changes:\n  - add: fact\n    name: order.bad\n    type: text\n    maxLength: 9\n", "--json");
        Assert.Equal(1, bad.ExitCode);
        using var refusal = JsonDocument.Parse(bad.Output);
        var mistake = refusal.RootElement.GetProperty("mistakes")[0];
        Assert.Equal(1, mistake.GetProperty("change").GetInt32());
        foreach (var field in new[] { "code", "stage", "resource", "path", "message", "fix", "related" })
        {
            Assert.True(mistake.TryGetProperty(field, out _), $"A mistake of a file lacks '{field}', which a save's mistake has.");
        }
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-15")]
    public async Task A_used_article_and_every_reference_to_it_can_go_in_one_file()
    {
        var scene = await NewScene();
        await scene.Post(
            ChangeScene.File(
                ChangeScene.AddBase("oferta"),
                ChangeScene.AddArticle("stary", "oferta"),
                ChangeScene.AddArticle("cennik", "oferta", "See [it](cms://article/stary).")
            )
        );
        var delete = ChangeScene.File("""{"delete":"article","id":"stary"}""");

        var alone = await scene.Post(delete, dryRun: true);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, alone.StatusCode);
        Assert.Equal(["reference.missing"], Codes(await ChangeScene.JsonOf(alone)));

        var both = ChangeScene.File("""{"delete":"article","id":"stary"}""", """{"edit":"article","id":"cennik","content":"No link."}""");
        var dry = await scene.Post(both, dryRun: true);
        Assert.Equal(HttpStatusCode.OK, dry.StatusCode);
        Assert.NotEqual("absent", await scene.ReadAsync("articles", "stary"));
        Assert.Equal(HttpStatusCode.OK, (await scene.Post(both)).StatusCode);
        Assert.Equal("absent", await scene.ReadAsync("articles", "stary"));
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-15")]
    public async Task Two_new_articles_linking_to_each_other_are_refused_as_a_loop_also_on_a_dry_run()
    {
        var scene = await NewScene();
        var file = ChangeScene.File(
            ChangeScene.AddBase("oferta"),
            ChangeScene.AddArticle("alfa", "oferta", "See [it](cms://article/beta)."),
            ChangeScene.AddArticle("beta", "oferta", "See [it](cms://article/alfa).")
        );

        foreach (var dryRun in new[] { true, false })
        {
            var response = await scene.Post(file, dryRun);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var mistake = Assert.Single(Mistakes(await ChangeScene.JsonOf(response)));
            Assert.Equal("reference.loop", mistake.GetProperty("code").GetString());
            Assert.Equal(2, mistake.GetProperty("change").GetInt32());
            Assert.Contains("alfa → beta → alfa", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        }
        Assert.Equal("absent", await scene.ReadAsync("articles", "alfa"));
    }

    private const string UsesFlat =
        """{"description":"D","firstStep":"ask","steps":{"ask":{"say":"Hi","next":[{"when":{"order.type":{"oneOf":["flat"]}},"to":"end"},{"to":"end"}]},"end":{"say":"Bye"}}}""";

    private async Task<ChangeScene> SceneWithAFactInUse()
    {
        var scene = await NewScene();
        await scene.Facts.Add("order.type", """{"type":"enum","description":"What is valued","values":["house","flat","land"]}""");
        await scene.Post(ChangeScene.File(UsesFlat.Insert(1, "\"add\":\"procedure\",\"id\":\"first\",")));
        return scene;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("criterion", "AC-batch-apply-18")]
    public async Task A_file_that_narrows_a_used_fact_gets_the_refusal_of_the_edit_guard_and_saves_nothing(bool dryRun)
    {
        var scene = await SceneWithAFactInUse();

        var response = await scene.Post(ChangeScene.File("""{"edit":"fact","name":"order.type","values":["house","land"]}"""), dryRun);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var mistake = Assert.Single(Mistakes(await ChangeScene.JsonOf(response)));
        Assert.Equal("fact.enum.valueInUse", mistake.GetProperty("code").GetString());
        Assert.Contains("first", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("flat", await scene.ReadAsync("facts", "order.type"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-18")]
    public async Task A_file_that_changes_the_fact_and_the_procedure_using_it_is_judged_on_its_result_and_passes()
    {
        var scene = await SceneWithAFactInUse();
        var both = ChangeScene.File(
            """{"edit":"fact","name":"order.type","values":["house","land"]}""",
            UsesFlat.Replace("flat", "land", StringComparison.Ordinal).Insert(1, "\"edit\":\"procedure\",\"id\":\"first\",")
        );

        Assert.Equal(HttpStatusCode.OK, (await scene.Post(both, dryRun: true)).StatusCode);
        Assert.Contains("flat", await scene.ReadAsync("facts", "order.type"), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await scene.Post(both)).StatusCode);
        Assert.DoesNotContain("flat", await scene.ReadAsync("facts", "order.type"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-16")]
    [Trait("criterion", "AC-batch-apply-1")]
    public async Task A_file_covers_procedures_and_a_successful_answer_lists_what_still_stops_a_version()
    {
        var scene = await NewScene();
        var conversation = ProcedureApi.Conversation.Trim().Insert(1, "\"add\":\"procedure\",\"id\":\"conversation\",");

        var added = await scene.Post(
            ChangeScene.File(conversation, """{"add":"procedure","id":"unfinished","description":"No first step"}""")
        );

        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        var notReady = (await ChangeScene.JsonOf(added)).GetProperty("notReady").EnumerateArray().ToList();
        Assert.Contains(notReady, mistake => mistake.GetProperty("resource").GetProperty("id").GetString() == "unfinished");
        Assert.Contains("wait", await scene.ReadAsync("procedures", "conversation"), StringComparison.Ordinal);

        var edited = await scene.Post(ChangeScene.File("""{"edit":"procedure","id":"conversation","description":"Changed"}"""));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var read = await scene.ReadAsync("procedures", "conversation");
        Assert.Contains("Changed", read, StringComparison.Ordinal);
        Assert.Contains("wait", read, StringComparison.Ordinal);

        var refused = await scene.Post(ChangeScene.File("""{"delete":"procedure","id":"conversation"}"""));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal(["procedure.conversation.required"], Codes(await ChangeScene.JsonOf(refused)));
    }
}
