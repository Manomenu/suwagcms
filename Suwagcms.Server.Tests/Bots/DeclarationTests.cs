using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Tests.Versions;

namespace Suwagcms.Server.Tests.Bots;

public sealed class DeclarationTests(TestDatabase database) : IDisposable
{
    private static readonly BotCapabilities Everything = new()
    {
        FormatVersion = 1,
        Blocks = Block.Names,
        CodeActions = [new CodeAction { Id = "lookupPolicy", Description = "Finds a policy" }],
    };

    private readonly TestApp app = new();
    private readonly List<IDisposable> disposables = [];

    public void Dispose()
    {
        foreach (var item in disposables)
        {
            item.Dispose();
        }
        app.Dispose();
    }

    private HttpClient Client()
    {
        _ = database.Pool;
        return app.CreateClient();
    }

    private static async Task<(PushScene Scene, TestBot Bot)> SceneAsync(HttpClient client, BotCapabilities? declares)
    {
        var id = PushScene.FreshId();
        var bot = await TestBot.StartAsync(id, declares: declares);
        return (await PushScene.RegisterAsync(client, id, bot.Address), bot);
    }

    private static async Task<DeclarationStatus> AskAsync(PushScene scene, bool ask = true) =>
        await scene.Client.GetFromJsonAsync<DeclarationStatus>(
            $"/api/bots/{scene.Bot}/declaration{(ask ? "" : "?ask=false")}",
            TestContext.Current.CancellationToken
        ) ?? throw new InvalidOperationException("no declaration in the answer");

    private static Func<HttpContext, Task> Answering(string json) =>
        async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(json);
        };

    [Fact]
    [Trait("criterion", "AC-bot-declaration-4")]
    public async Task Asking_stores_the_answer_with_its_time_and_shows_it_and_the_cli_returns_the_same_as_data()
    {
        var (scene, bot) = await SceneAsync(Client(), Everything);
        await using var _ = bot;

        var asked = await AskAsync(scene);

        Assert.Equal((DeclarationAsk.Answered, DeclarationStored.Declared), (asked.Ask, asked.Stored));
        Assert.Equal(1, asked.Declaration?.FormatVersion);
        Assert.Equal(Block.Names, asked.Declaration?.Blocks);
        Assert.Equal("lookupPolicy", Assert.Single(asked.Declaration?.CodeActions ?? []).Id);
        Assert.NotNull(asked.AnsweredAt);

        var stored = await AskAsync(scene, ask: false);
        Assert.Equal(DeclarationAsk.NotAsked, stored.Ask);
        Assert.Equal(asked.AnsweredAt?.ToUnixTimeSeconds(), stored.AnsweredAt?.ToUnixTimeSeconds());
        Assert.Equal(Block.Names, stored.Declaration?.Blocks);

        var cli = await scene.CliAsync("bots", "declaration", "--json");
        Assert.Equal(0, cli.ExitCode);
        using var document = JsonDocument.Parse(cli.Output);
        Assert.Equal("Answered", document.RootElement.GetProperty("ask").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("declaration").GetProperty("formatVersion").GetInt32());
        Assert.Equal(
            "lookupPolicy",
            document.RootElement.GetProperty("declaration").GetProperty("codeActions")[0].GetProperty("id").GetString()
        );
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-5")]
    public async Task Registering_contacts_nothing_and_a_bot_that_has_never_answered_says_so_and_nothing_else_is_refused()
    {
        var client = Client();
        var (scene, bot) = await SceneAsync(client, Everything);
        Assert.Equal(0, bot.Requests);
        await bot.StopAsync();

        var status = await AskAsync(scene);

        Assert.Equal((DeclarationAsk.Unreachable, DeclarationStored.NeverAnswered), (status.Ask, status.Stored));
        Assert.Null(status.Declaration);
        Assert.Contains("cannot reach", status.Reason, StringComparison.Ordinal);
        var cli = await scene.CliAsync("bots", "declaration");
        Assert.Equal(1, cli.ExitCode);
        Assert.Contains("it has never answered", cli.Error, StringComparison.Ordinal);

        await scene.FillAsync();
        Assert.Equal(1, (await scene.CreateAsync()).Number);
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-6")]
    public async Task With_the_bot_stopped_the_last_answer_stays_and_is_shown_with_its_time_and_the_reason_and_the_cli_exits_1()
    {
        var (scene, bot) = await SceneAsync(Client(), Everything);
        var answered = await AskAsync(scene);
        await bot.StopAsync();

        var status = await AskAsync(scene);

        Assert.Equal((DeclarationAsk.Unreachable, DeclarationStored.Declared), (status.Ask, status.Stored));
        Assert.Equal(Block.Names, status.Declaration?.Blocks);
        Assert.Equal(answered.AnsweredAt?.ToUnixTimeSeconds(), status.AnsweredAt?.ToUnixTimeSeconds());
        Assert.Contains("cannot reach", status.Reason, StringComparison.Ordinal);
        var cli = await scene.CliAsync("bots", "declaration");
        Assert.Equal(1, cli.ExitCode);
        Assert.Contains("Last answer, from", cli.Error, StringComparison.Ordinal);
        Assert.Contains("blocks listen, collect, say, assign", cli.Error, StringComparison.Ordinal);
        Assert.Contains("Check that the bot is running", cli.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-6")]
    public async Task A_bot_that_does_not_answer_within_the_time_limit_keeps_the_last_answer_with_the_reason()
    {
        _ = database.Pool;
        var shorter = app.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Push:StateTimeoutSeconds"] = "1" })
            )
        );
        disposables.Add(shorter);
        var (scene, bot) = await SceneAsync(shorter.CreateClient(), Everything);
        await using var stopping = bot;
        await AskAsync(scene);
        bot.OnDeclaration = async context => await Task.Delay(TimeSpan.FromSeconds(10), context.RequestAborted);

        var status = await AskAsync(scene);

        Assert.Equal(DeclarationAsk.Unreachable, status.Ask);
        Assert.Contains("did not answer within 1 seconds", status.Reason, StringComparison.Ordinal);
        Assert.Equal(Block.Names, status.Declaration?.Blocks);
    }

    [Theory]
    [InlineData("""{"bot":"another","formatVersion":1,"blocks":["listen"]}""", MistakeCodes.DeclarationBot)]
    [InlineData("""{"bot":"SELF","formatVersion":1,"blocks":[]}""", MistakeCodes.DeclarationBlocksMissing)]
    [InlineData("""{"bot":"SELF","formatVersion":1,"blocks":["fly"]}""", MistakeCodes.DeclarationBlockUnknown)]
    [InlineData(
        """{"bot":"SELF","formatVersion":1,"blocks":["listen"],"codeActions":[{"id":"a"}]}""",
        MistakeCodes.CodeActionDescriptionMissing
    )]
    [InlineData(
        """{"bot":"SELF","formatVersion":1,"blocks":["listen"],"codeActions":[{"id":"a","description":"d"},{"id":"a","description":"d"}]}""",
        MistakeCodes.CodeActionRepeated
    )]
    [InlineData(
        """{"bot":"SELF","formatVersion":1,"blocks":["listen"],"codeActions":[{"id":"a","description":"d","parameters":[{"name":"n","type":"blob","description":"d"}]}]}""",
        MistakeCodes.FactTypeInvalid
    )]
    [InlineData("not json", MistakeCodes.DeclarationInvalid)]
    [Trait("criterion", "AC-bot-declaration-7")]
    public async Task An_invalid_answer_shows_its_mistakes_and_the_stored_declaration_stays_as_it_was(string answer, string code)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var (scene, bot) = await SceneAsync(Client(), Everything);
        await using var _ = bot;
        var before = await AskAsync(scene);
        bot.OnDeclaration = Answering(answer.Replace("SELF", scene.Bot, StringComparison.Ordinal));

        var status = await AskAsync(scene);

        Assert.Equal((DeclarationAsk.Invalid, DeclarationStored.Declared), (status.Ask, status.Stored));
        Assert.Contains(status.Mistakes, mistake => mistake.Code == code);
        Assert.Equal(Block.Names, status.Declaration?.Blocks);
        Assert.Equal(before.AnsweredAt?.ToUnixTimeSeconds(), status.AnsweredAt?.ToUnixTimeSeconds());
        Assert.Equal(1, (await scene.CliAsync("bots", "declaration")).ExitCode);
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-7")]
    public async Task An_answer_with_several_mistakes_shows_every_one_at_once()
    {
        var (scene, bot) = await SceneAsync(Client(), Everything);
        await using var _ = bot;
        bot.OnDeclaration = Answering("""{"bot":"another","formatVersion":1,"blocks":[],"codeActions":[{"id":"a"},{"id":"a"}]}""");

        var status = await AskAsync(scene);

        Assert.Equal(
            [
                MistakeCodes.CodeActionDescriptionMissing,
                MistakeCodes.CodeActionDescriptionMissing,
                MistakeCodes.CodeActionRepeated,
                MistakeCodes.DeclarationBlocksMissing,
                MistakeCodes.DeclarationBot,
            ],
            status.Mistakes.Select(mistake => mistake.Code).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-8")]
    public async Task A_format_version_the_cms_does_not_know_is_stored_and_shown_with_its_fix()
    {
        var (scene, bot) = await SceneAsync(Client(), null);
        await using var _ = bot;
        bot.OnDeclaration = Answering($$"""{"bot":"{{scene.Bot}}","formatVersion":2,"blocks":["fly"]}""");

        var status = await AskAsync(scene);

        Assert.Equal((DeclarationAsk.Answered, DeclarationStored.Declared), (status.Ask, status.Stored));
        Assert.Equal(2, status.Declaration?.FormatVersion);
        Assert.Contains("update Suwagcms.Sdk on the CMS", status.FormatFix, StringComparison.Ordinal);
        Assert.Contains("wants format 2", (await scene.CliAsync("bots", "declaration")).Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-9")]
    public async Task A_new_valid_answer_replaces_the_stored_declaration_as_a_whole()
    {
        var (scene, bot) = await SceneAsync(Client(), Everything);
        await using var _ = bot;
        await AskAsync(scene);
        bot.OnDeclaration = Answering($$"""{"bot":"{{scene.Bot}}","formatVersion":1,"blocks":["listen"]}""");

        var status = await AskAsync(scene);

        Assert.Equal(["listen"], status.Declaration?.Blocks);
        Assert.Equal(0, status.Declaration?.CodeActions.Count);
        Assert.Equal(["listen"], (await AskAsync(scene, ask: false)).Declaration?.Blocks);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("criterion", "AC-bot-declaration-10")]
    public async Task An_answer_that_the_bot_declares_nothing_or_a_route_it_does_not_have_clears_the_stored_declaration(bool hasTheRoute)
    {
        var (scene, bot) = await SceneAsync(Client(), Everything);
        await using var _ = bot;
        await AskAsync(scene);
        bot.OnDeclaration = hasTheRoute
            ? Answering($$"""{"bot":"{{scene.Bot}}","declaresNothing":true}""")
            : context =>
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            };

        var status = await AskAsync(scene);

        Assert.Equal((DeclarationAsk.Answered, DeclarationStored.DeclaresNothing), (status.Ask, status.Stored));
        Assert.Null(status.Declaration);
        Assert.Equal(DeclarationStored.DeclaresNothing, (await AskAsync(scene, ask: false)).Stored);
        Assert.Contains("declares nothing", (await scene.CliAsync("bots", "declaration")).Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-11")]
    public async Task Asking_the_bot_changes_no_fact_article_or_procedure_and_no_version()
    {
        var (scene, bot) = await SceneAsync(Client(), Everything);
        await using var _ = bot;
        await scene.FillAsync();
        await scene.CreateAsync();
        var before = await scene.SnapshotAsync();

        await AskAsync(scene);
        bot.OnDeclaration = Answering($$"""{"bot":"{{scene.Bot}}","formatVersion":1,"blocks":["listen"]}""");
        await AskAsync(scene);

        Assert.Equal(before, await scene.SnapshotAsync());
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-18")]
    public async Task A_declaration_that_leaves_out_a_block_in_use_lists_the_affected_procedures_on_the_profile_and_in_the_cli()
    {
        var (scene, bot) = await SceneAsync(Client(), Everything);
        await using var _ = bot;
        await scene.FillAsync();
        Assert.Empty((await AskAsync(scene)).CannotRun);
        bot.OnDeclaration = Answering($$"""{"bot":"{{scene.Bot}}","formatVersion":1,"blocks":["listen","say","assign"]}""");

        var status = await AskAsync(scene);

        var mistake = Assert.Single(status.CannotRun);
        Assert.Equal(new ResourceRef(ResourceKind.Procedure, "orderValuation"), mistake.Resource);
        Assert.Contains("uses block 'collect'", mistake.Message, StringComparison.Ordinal);
        Assert.Contains("uses block 'collect'", (await scene.CliAsync("bots", "declaration")).Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-create-16")]
    public async Task A_version_is_checked_against_the_stored_declaration_without_calling_the_bot_and_saves_go_on()
    {
        var (scene, bot) = await SceneAsync(Client(), new BotCapabilities { FormatVersion = 1, Blocks = ["listen", "say", "assign"] });
        await using var _ = bot;
        await AskAsync(scene);
        var asked = bot.Requests;

        await scene.FillAsync();
        var refused = await scene.Client.PostAsync($"/api/bots/{scene.Bot}/versions", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var codes = await Facts.FactApi.CodesOf(refused);
        Assert.Equal([MistakeCodes.DeclarationBlock], codes);
        Assert.Equal(asked, bot.Requests);
        Assert.Null((await scene.StatusAsync()).Latest);
    }

    [Fact]
    [Trait("criterion", "AC-version-create-16")]
    public async Task A_bot_wanting_another_format_refuses_a_version_and_one_that_declares_nothing_checks_nothing()
    {
        var (scene, bot) = await SceneAsync(Client(), null);
        await using var _ = bot;
        await scene.FillAsync();
        await AskAsync(scene);
        Assert.Equal(1, (await scene.CreateAsync()).Number);

        await scene.ChangeAsync();
        bot.OnDeclaration = Answering($$"""{"bot":"{{scene.Bot}}","formatVersion":2,"blocks":["fly"]}""");
        await AskAsync(scene);

        var refused = await scene.Client.PostAsync($"/api/bots/{scene.Bot}/versions", null, TestContext.Current.CancellationToken);
        Assert.Equal([MistakeCodes.DeclarationFormatVersion], await Facts.FactApi.CodesOf(refused));
    }

    [Fact]
    [Trait("criterion", "AC-version-push-17")]
    public async Task A_push_is_checked_against_what_the_bot_declares_now_and_a_stale_stored_answer_does_not_decide()
    {
        var (scene, bot) = await SceneAsync(Client(), Everything);
        await using var _ = bot;
        await scene.FillAsync();
        await AskAsync(scene);
        var version = await scene.CreateAsync();
        var puts = 0;
        bot.OnPush = _ =>
        {
            Interlocked.Increment(ref puts);
            return Task.CompletedTask;
        };
        bot.OnDeclaration = Answering(
            $$"""{"bot":"{{scene.Bot}}","formatVersion":1,"blocks":["listen","say","assign"],"codeActions":[{"id":"lookupPolicy","description":"Finds a policy"}]}"""
        );

        var result = await scene.PushAsync(version.Number);

        Assert.Equal(PushOutcome.Failed, result.Outcome);
        Assert.Contains("does not declare now", result.Reason, StringComparison.Ordinal);
        Assert.Equal(MistakeCodes.DeclarationBlock, Assert.Single(result.Mistakes).Code);
        Assert.Equal(0, puts);
        Assert.Null((await scene.StatusAsync()).Has);
        Assert.Equal(PushOutcome.Failed, Assert.Single(await scene.Attempts()).Outcome);

        bot.OnDeclaration = null;
        Assert.Equal(PushOutcome.Succeeded, (await scene.PushAsync(version.Number)).Outcome);
        Assert.Equal(1, puts);
    }

    [Fact]
    [Trait("criterion", "AC-version-push-17")]
    public async Task A_bot_that_declares_nothing_or_gives_a_bad_answer_is_left_to_its_own_validation()
    {
        var (scene, bot) = await SceneAsync(Client(), null);
        await using var _ = bot;
        await scene.FillAsync();
        var first = await scene.CreateAsync();
        Assert.Equal(PushOutcome.Succeeded, (await scene.PushAsync(first.Number)).Outcome);

        await scene.ChangeAsync();
        var second = await scene.CreateAsync();
        bot.OnDeclaration = Answering("not json");
        Assert.Equal(PushOutcome.Succeeded, (await scene.PushAsync(second.Number)).Outcome);
    }
}
