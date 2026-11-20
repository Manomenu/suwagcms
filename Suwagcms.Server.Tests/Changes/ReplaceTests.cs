using System.Net;
using System.Text.Json;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Server.Tests.Versions;

namespace Suwagcms.Server.Tests.Changes;

public sealed class ReplaceTests(TestDatabase database, ClassServer server) : IClassFixture<ClassServer>
{
    private TestApp App => server.App;

    private Task<ChangeScene> NewScene()
    {
        _ = database.DataSource;
        return ChangeScene.NewAsync(App.CreateClient());
    }

    private static string[] Codes(JsonElement problem) =>
        [.. problem.GetProperty("mistakes").EnumerateArray().Select(mistake => mistake.GetProperty("code").GetString() ?? "")];

    private static List<(string Action, string Id, int? Revision)> Outcomes(JsonElement answer) =>
        [
            .. answer
                .GetProperty("changes")
                .EnumerateArray()
                .Select(change =>
                    (
                        change.GetProperty("action").GetString() ?? "",
                        change.GetProperty("id").GetString() ?? "",
                        change.GetProperty("revision").ValueKind == JsonValueKind.Number
                            ? change.GetProperty("revision").GetInt32()
                            : (int?)null
                    )
                ),
        ];

    private static string AddAction(string id, string description = "Sends.") =>
        $$$$"""{"add":"action","id":"{{{{id}}}}","description":"{{{{description}}}}","implementation":{"http":{"method":"POST","url":"https://crm.internal/send"}}}""";

    private static string AddProcedure(string id, string description, string extra = "") =>
        $$$"""{"add":"procedure","id":"{{{id}}}","description":"{{{description}}}","firstStep":"ask","steps":{"ask":{"collect":["customer.city"],"next":"bye"},"bye":{"say":"Thanks"}}{{{extra}}}}""";

    private const string Spare = ""","localFacts":{"spare":{"type":"int","description":"Not used","min":1,"max":5}}""";

    private static readonly string[] Held =
    [
        ChangeScene.AddFact("customer.city", "Old"),
        ChangeScene.AddBase("oferta", "Old title"),
        ChangeScene.AddArticle("cennik", "oferta", "Old content."),
        AddAction("send"),
        AddProcedure("ask", "Old", Spare),
    ];

    private async Task<ChangeScene> SceneHolding(params string[] changes)
    {
        var scene = await NewScene();
        var held = await scene.Post(ChangeScene.File(changes));
        Assert.Equal(HttpStatusCode.OK, held.StatusCode);
        return scene;
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-23")]
    public async Task An_add_of_each_kind_replaces_what_the_cms_holds_as_a_whole_and_adds_what_it_does_not()
    {
        var scene = await SceneHolding(Held);

        var response = await scene.Post(
            ChangeScene.File(
                ChangeScene.AddFact("customer.city", "New"),
                ChangeScene.AddBase("oferta", "New title"),
                ChangeScene.AddArticle("cennik", "oferta", "New content."),
                AddAction("send", "Sends again."),
                AddProcedure("ask", "New"),
                ChangeScene.AddFact("customer.street"),
                ChangeScene.AddBase("nowa"),
                ChangeScene.AddArticle("rabaty", "nowa"),
                AddAction("other"),
                AddProcedure("second", "Another")
            ),
            replace: true
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcomes = Outcomes(await ChangeScene.JsonOf(response));
        Assert.Equal(["replaced", "replaced", "replaced", "replaced", "replaced"], outcomes.Take(5).Select(outcome => outcome.Action));
        Assert.All(outcomes.Take(5), outcome => Assert.Equal(2, outcome.Revision));
        Assert.Equal(["added", "added", "added", "added", "added"], outcomes.Skip(5).Select(outcome => outcome.Action));
        Assert.Contains("\"description\":\"New\"", await scene.ReadAsync("facts", "customer.city"), StringComparison.Ordinal);
        Assert.Contains("New title", await scene.ReadAsync("knowledge-bases", "oferta"), StringComparison.Ordinal);
        Assert.Contains("New content.", await scene.ReadAsync("articles", "cennik"), StringComparison.Ordinal);
        Assert.Contains("Sends again.", await scene.ReadAsync("actions", "send"), StringComparison.Ordinal);
        var procedure = await scene.ReadAsync("procedures", "ask");
        Assert.Contains("\"description\":\"New\"", procedure, StringComparison.Ordinal);
        Assert.DoesNotContain("spare", procedure, StringComparison.Ordinal);
        foreach (
            var (collection, id) in new[]
            {
                ("facts", "customer.street"),
                ("knowledge-bases", "nowa"),
                ("articles", "rabaty"),
                ("actions", "other"),
                ("procedures", "second"),
            }
        )
        {
            Assert.NotEqual("absent", await scene.ReadAsync(collection, id));
        }
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-23")]
    public async Task Without_replace_an_add_of_what_the_cms_holds_is_still_refused()
    {
        var scene = await SceneHolding(ChangeScene.AddFact("customer.city", "Old"));

        var response = await scene.Post(ChangeScene.File(ChangeScene.AddFact("customer.city", "New")));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("\"description\":\"Old\"", await scene.ReadAsync("facts", "customer.city"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("criterion", "AC-batch-apply-24")]
    public async Task A_replacement_the_edit_guard_refuses_refuses_the_whole_file_and_nothing_changes(bool dryRun)
    {
        var scene = await SceneHolding(Held);
        var before = await scene.RowsAsync(database.DataSource);

        var response = await scene.Post(
            ChangeScene.File(
                ChangeScene.AddArticle("cennik", "oferta", "New content."),
                """{"add":"fact","name":"customer.city","type":"int","description":"Old","min":1,"max":9}""",
                ChangeScene.AddFact("customer.street")
            ),
            dryRun,
            replace: true
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(["fact.type.inUse"], Codes(await ChangeScene.JsonOf(response)));
        Assert.Equal(before, await scene.RowsAsync(database.DataSource));
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-24")]
    public async Task Replacements_that_make_a_loop_are_refused_as_a_whole()
    {
        var scene = await SceneHolding(
            ChangeScene.AddBase("oferta"),
            ChangeScene.AddArticle("alfa", "oferta"),
            ChangeScene.AddArticle("beta", "oferta")
        );
        var before = await scene.RowsAsync(database.DataSource);

        var response = await scene.Post(
            ChangeScene.File(
                ChangeScene.AddArticle("alfa", "oferta", "See [it](cms://article/beta)."),
                ChangeScene.AddArticle("beta", "oferta", "See [it](cms://article/alfa).")
            ),
            replace: true
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(["reference.loop"], Codes(await ChangeScene.JsonOf(response)));
        Assert.Equal(before, await scene.RowsAsync(database.DataSource));
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-24")]
    public async Task A_replacement_of_a_code_action_the_bot_declares_is_refused_with_the_whole_file()
    {
        _ = database.DataSource;
        using var client = App.CreateClient();
        var id = PushScene.FreshId();
        await using var bot = await TestBot.StartAsync(
            id,
            declares: new BotCapabilities
            {
                FormatVersion = 5,
                Blocks = Block.Names,
                CodeActions = [new CodeAction { Id = "lookupPolicy", Description = "Finds a policy" }],
            }
        );
        await PushScene.RegisterAsync(client, id, bot.Address);
        (await client.GetAsync($"/api/bots/{id}/declaration", TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        var scene = new ChangeScene(client, id);
        var before = await scene.RowsAsync(database.DataSource);

        var response = await scene.Post(ChangeScene.File(AddAction("lookupPolicy"), ChangeScene.AddFact("customer.city")), replace: true);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(["action.code.readOnly"], Codes(await ChangeScene.JsonOf(response)));
        Assert.Equal(before, await scene.RowsAsync(database.DataSource));
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-25")]
    public async Task The_same_file_applied_twice_changes_nothing_the_second_time()
    {
        var scene = await NewScene();
        var file = ChangeScene.File([.. Held, """{"edit":"persona","personality":"Calm.\n"}"""]);
        Assert.Equal(HttpStatusCode.OK, (await scene.Post(file, replace: true)).StatusCode);
        var rows = await scene.RowsAsync(database.DataSource);
        var entries = await scene.HistoryCountAsync(database.DataSource);

        var again = await scene.Post(file, replace: true);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var answer = await ChangeScene.JsonOf(again);
        Assert.All(Outcomes(answer), outcome => Assert.Equal(("unchanged", 1), (outcome.Action, outcome.Revision)));
        Assert.Equal(6, answer.GetProperty("unchanged").GetInt32());
        Assert.Equal(rows, await scene.RowsAsync(database.DataSource));
        Assert.Equal(entries, await scene.HistoryCountAsync(database.DataSource));
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-26")]
    public async Task Nothing_the_file_does_not_name_is_changed_and_the_answer_lists_it_for_the_kinds_the_file_changes()
    {
        var scene = await SceneHolding(
            ChangeScene.AddFact("customer.city", "Old"),
            ChangeScene.AddFact("customer.street"),
            ChangeScene.AddBase("oferta"),
            ChangeScene.AddArticle("cennik", "oferta")
        );
        var before = await scene.RowsAsync(database.DataSource);

        var response = await scene.Post(ChangeScene.File(ChangeScene.AddFact("customer.city", "New")), replace: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var notMentioned = (await ChangeScene.JsonOf(response)).GetProperty("notMentioned").EnumerateArray().ToList();
        var left = Assert.Single(notMentioned);
        Assert.Equal(("fact", "customer.street"), (left.GetProperty("kind").GetString(), left.GetProperty("id").GetString()));
        var after = await scene.RowsAsync(database.DataSource);
        Assert.Equal(before.Count, after.Count);
        Assert.Equal(
            before.Where(row => !row.Contains("customer.city", StringComparison.Ordinal)),
            after.Where(row => !row.Contains("customer.city", StringComparison.Ordinal))
        );
    }

    private const string Seed = """
        changes:
          - add: fact
            name: customer.city
            type: text
            description: Old
            maxLength: 80
          - add: procedure
            id: ask
            description: Asks
            firstStep: ask
            steps:
              ask:
                collect: [customer.city]
                next: bye
              bye:
                say: Thanks
        """;

    [Fact]
    [Trait("criterion", "AC-batch-apply-27")]
    public async Task Compare_changes_nothing_says_what_differs_per_change_and_its_exit_code_tells_same_from_different_from_refused()
    {
        var scene = await NewScene();
        Assert.Equal(
            0,
            (
                await scene.CliAsync(
                    Seed + "\n  - add: fact\n    name: customer.street\n    type: text\n    description: Kept\n    maxLength: 80\n"
                )
            ).ExitCode
        );
        var rows = await scene.RowsAsync(database.DataSource);

        var same = await scene.CliAsync(Seed, "--compare");
        Assert.Equal(("", 0), (same.Error, same.ExitCode));
        Assert.Contains("The same: applying the file would change nothing, 2 unchanged.", same.Output, StringComparison.Ordinal);
        Assert.Contains("Not in the file, left as they are: 1", same.Output, StringComparison.Ordinal);
        Assert.Contains("  fact 'customer.street'", same.Output, StringComparison.Ordinal);

        var changed =
            Seed.Replace("description: Old", "description: New", StringComparison.Ordinal)
                .Replace("say: Thanks", "say: Thank you", StringComparison.Ordinal)
            + "\n  - add: fact\n    name: customer.email\n    type: text\n    description: Mail\n    maxLength: 80\n";
        var differs = await scene.CliAsync(changed, "--compare");
        Assert.Equal(("", 4), (differs.Error, differs.ExitCode));
        Assert.Contains("Would apply: 1 added, 2 replaced, 0 unchanged, 0 edited, 0 deleted.", differs.Output, StringComparison.Ordinal);
        Assert.Contains(
            "  change 1, fact 'customer.city' replaced\n    description Old → New\n",
            differs.Output.ReplaceLineEndings("\n"),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "  change 2, procedure 'ask' replaced\n    step 'bye' changed\n      say Thanks → Thank you\n",
            differs.Output.ReplaceLineEndings("\n"),
            StringComparison.Ordinal
        );
        Assert.Contains("  change 3, fact 'customer.email' added", differs.Output, StringComparison.Ordinal);

        var json = await scene.CliAsync(changed, "--compare", "--json");
        Assert.Equal(4, json.ExitCode);
        using var document = JsonDocument.Parse(json.Output);
        var first = document.RootElement.GetProperty("changes")[0];
        Assert.Equal("replaced", first.GetProperty("action").GetString());
        Assert.Equal("description", first.GetProperty("fields")[0].GetProperty("field").GetString());
        Assert.Equal("customer.street", document.RootElement.GetProperty("notMentioned")[0].GetProperty("id").GetString());

        var refused = await scene.CliAsync(
            Seed.Replace("type: text", "type: int", StringComparison.Ordinal).Replace("maxLength: 80", "min: 1", StringComparison.Ordinal),
            "--compare"
        );
        Assert.Equal(1, refused.ExitCode);
        Assert.Contains("Would not apply,", refused.Error, StringComparison.Ordinal);
        Assert.Contains("fact.type.inUse", refused.Error, StringComparison.Ordinal);

        Assert.Equal(2, (await scene.CliAsync(Seed, "--compare", "--dry-run")).ExitCode);
        Assert.Equal(rows, await scene.RowsAsync(database.DataSource));
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-27")]
    public async Task Replace_applies_what_compare_said_and_compare_then_finds_it_the_same()
    {
        var scene = await NewScene();
        Assert.Equal(0, (await scene.CliAsync(Seed)).ExitCode);
        var changed = Seed.Replace("description: Old", "description: New", StringComparison.Ordinal);

        var applied = await scene.CliAsync(changed, "--replace");

        Assert.Equal(("", 0), (applied.Error, applied.ExitCode));
        Assert.Contains("Applied: 0 added, 1 replaced, 1 unchanged, 0 edited, 0 deleted.", applied.Output, StringComparison.Ordinal);
        Assert.Equal(0, (await scene.CliAsync(changed, "--compare")).ExitCode);
    }

    [Fact]
    [Trait("criterion", "AC-batch-apply-28")]
    public async Task Key_order_flow_style_comments_and_written_defaults_are_no_difference()
    {
        var scene = await NewScene();
        Assert.Equal(
            0,
            (
                await scene.CliAsync(
                    Seed + "\n  - add: knowledgeBase\n    id: oferta\n    title: Oferta\n    embedding: default\n    results: 5\n"
                )
            ).ExitCode
        );
        var rows = await scene.RowsAsync(database.DataSource);
        const string restyled = """
            # The same seed, written another way.
            changes:
              - add: procedure
                steps:
                  bye: { say: Thanks }   # flow style
                  ask:
                    next: bye
                    collect:
                      - customer.city
                firstStep: ask
                description: Asks
                id: ask
              - { add: fact, maxLength: 80, writableBy: set, description: Old, type: text, name: customer.city }
              - add: knowledgeBase
                results: 5
                language: en
                embedding: default
                title: Oferta
                id: oferta
            """;

        var compared = await scene.CliAsync(restyled, "--compare");
        var replaced = await scene.CliAsync(restyled, "--replace");

        Assert.Equal(("", 0), (compared.Error, compared.ExitCode));
        Assert.Contains("The same: applying the file would change nothing, 3 unchanged.", compared.Output, StringComparison.Ordinal);
        Assert.Contains("0 replaced, 3 unchanged", replaced.Output, StringComparison.Ordinal);
        Assert.Equal(rows, await scene.RowsAsync(database.DataSource));
    }
}
