using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Versions;

namespace Suwagcms.Server.Tests.Actions;

public sealed class CodeActionTests(TestDatabase database) : IDisposable
{
    private static readonly CodeActionField Pesel = new()
    {
        Name = "pesel",
        Type = FactTypes.Text,
        Description = "The PESEL",
        MaxLength = 11,
    };

    private static readonly CodeAction Lookup = new()
    {
        Id = "lookupPolicy",
        Description = "Finds a policy",
        Parameters = [Pesel],
        Results = [Pesel with { Name = "policyNumber", MaxLength = 20 }],
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
        var client = app.CreateClient();
        disposables.Add(client);
        return client;
    }

    private static BotCapabilities Declaring(params CodeAction[] actions) =>
        new()
        {
            FormatVersion = 1,
            Blocks = Block.Names,
            CodeActions = actions,
        };

    private sealed record Scene(HttpClient Client, TestBot Bot, ActionApi Api)
    {
        public async Task AskAsync()
        {
            var answer = await Client.GetAsync($"/api/bots/{Api.Bot}/declaration", TestContext.Current.CancellationToken);
            answer.EnsureSuccessStatusCode();
        }

        public void Answers(params CodeAction[] actions) =>
            Bot.OnDeclaration = context => context.Response.WriteAsJsonAsync(BotDeclaration.Of(Bot.Id, Declaring(actions)));

        public async Task<string[]> HistoryOf(string id)
        {
            var page = await ActionApi.Json(
                await Client.GetAsync($"/api/bots/{Api.Bot}/actions/{id}/history", TestContext.Current.CancellationToken)
            );
            return [.. page.GetProperty("entries").EnumerateArray().Select(entry => entry.GetProperty("action").GetString() ?? "")];
        }
    }

    private async Task<Scene> SceneAsync(params CodeAction[] actions)
    {
        var client = Client();
        var id = PushScene.FreshId();
        var bot = await TestBot.StartAsync(id, declares: Declaring(actions));
        await PushScene.RegisterAsync(client, id, bot.Address);
        return new Scene(client, bot, new ActionApi(client, id));
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-1")]
    public async Task A_declared_code_action_appears_after_the_cms_asks_with_its_contract_and_no_call()
    {
        var scene = await SceneAsync(Lookup);
        await using var _ = scene.Bot;
        Assert.Empty((await scene.Api.List()).EnumerateArray());

        await scene.AskAsync();

        var shown = await scene.Api.Show("lookupPolicy");
        Assert.Equal("code", shown.GetProperty("implementation").GetString());
        Assert.Equal("Finds a policy", shown.GetProperty("description").GetString());
        Assert.Equal(11, shown.GetProperty("parameters").GetProperty("pesel").GetProperty("maxLength").GetInt32());
        Assert.Equal(20, shown.GetProperty("results").GetProperty("policyNumber").GetProperty("maxLength").GetInt32());
        Assert.False(shown.GetProperty("noLongerDeclared").GetBoolean());
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-2")]
    public async Task The_list_gives_both_kinds_each_with_its_implementation()
    {
        var scene = await SceneAsync(Lookup);
        await using var _ = scene.Bot;
        await scene.Api.Add("send", ActionApi.Least);
        await scene.AskAsync();

        var listed = (await scene.Api.List()).EnumerateArray().ToDictionary(action => action.GetProperty("id").GetString() ?? "");

        Assert.Equal("code", listed["lookupPolicy"].GetProperty("implementation").GetString());
        Assert.Equal(JsonValueKind.Object, listed["send"].GetProperty("implementation").ValueKind);
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-3")]
    public async Task When_the_declaration_changes_a_code_action_the_cms_shows_the_change_after_it_asks()
    {
        var scene = await SceneAsync(Lookup);
        await using var _ = scene.Bot;
        await scene.AskAsync();

        scene.Answers(Lookup with { Description = "Finds it", Parameters = [Pesel with { MaxLength = 12 }], Results = [] });
        Assert.Equal("Finds a policy", (await scene.Api.Show("lookupPolicy")).GetProperty("description").GetString());
        await scene.AskAsync();

        var shown = await scene.Api.Show("lookupPolicy");
        Assert.Equal("Finds it", shown.GetProperty("description").GetString());
        Assert.Equal(12, shown.GetProperty("parameters").GetProperty("pesel").GetProperty("maxLength").GetInt32());
        Assert.Equal(JsonValueKind.Null, shown.GetProperty("results").ValueKind);
        Assert.Equal(2, shown.GetProperty("revision").GetInt32());
        Assert.Equal(["edited", "added"], await scene.HistoryOf("lookupPolicy"));
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-4")]
    public async Task A_code_action_the_declaration_no_longer_lists_and_no_step_uses_is_gone_after_it_asks()
    {
        var scene = await SceneAsync(Lookup);
        await using var _ = scene.Bot;
        await scene.AskAsync();

        scene.Answers();
        await scene.AskAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await scene.Api.Get("lookupPolicy")).StatusCode);
        Assert.Equal(["deleted", "added"], await scene.HistoryOf("lookupPolicy"));
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-5")]
    public async Task A_declaration_equal_to_what_the_cms_has_creates_no_new_revision()
    {
        var scene = await SceneAsync(Lookup);
        await using var _ = scene.Bot;
        await scene.AskAsync();

        await scene.AskAsync();
        await scene.AskAsync();

        Assert.Equal(1, (await scene.Api.Show("lookupPolicy")).GetProperty("revision").GetInt32());
        Assert.Equal(["added"], await scene.HistoryOf("lookupPolicy"));
    }

    [Theory]
    [InlineData("unreachable")]
    [InlineData("invalid")]
    [Trait("criterion", "AC-code-actions-6")]
    public async Task With_the_bot_unreachable_or_its_answer_invalid_the_code_actions_stay_as_they_were(string how)
    {
        var scene = await SceneAsync(Lookup);
        await using var _ = scene.Bot;
        await scene.AskAsync();

        if (how == "unreachable")
        {
            await scene.Bot.StopAsync();
        }
        else
        {
            scene.Bot.OnDeclaration = context =>
                context.Response.WriteAsync("""{"bot":"another","formatVersion":1,"blocks":["say"],"codeActions":[]}""");
        }
        await scene.AskAsync();

        var shown = await scene.Api.Show("lookupPolicy");
        Assert.Equal(1, shown.GetProperty("revision").GetInt32());
        Assert.Equal("code", shown.GetProperty("implementation").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-7")]
    public async Task Editing_or_deleting_a_code_action_and_adding_one_are_refused_with_the_fix()
    {
        var scene = await SceneAsync(Lookup);
        await using var _ = scene.Bot;
        await scene.AskAsync();

        var refusals = new[]
        {
            await scene.Api.Put("lookupPolicy", ActionApi.Least),
            await scene.Api.Delete("lookupPolicy"),
            await scene.Api.Add("another", """{"description":"d","implementation":"code"}"""),
            await scene.Client.PostAsJsonAsync(
                $"/api/bots/{scene.Api.Bot}/changes",
                new { changes = new object[] { new { delete = "action", id = "lookupPolicy" } } },
                TestContext.Current.CancellationToken
            ),
        };

        foreach (var refused in refusals)
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
            var mistake = Assert.Single(await FactApi.MistakesOf(refused));
            Assert.Equal("action.code.readOnly", mistake.GetProperty("code").GetString());
            Assert.Contains("in the bot", mistake.GetProperty("fix").GetString(), StringComparison.Ordinal);
        }
        Assert.Equal(1, (await scene.Api.Show("lookupPolicy")).GetProperty("revision").GetInt32());
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-8")]
    public async Task Adding_an_http_action_with_the_id_of_a_copy_is_refused_naming_the_code_action()
    {
        var scene = await SceneAsync(Lookup);
        await using var _ = scene.Bot;
        await scene.AskAsync();

        var refused = await scene.Api.Add("lookupPolicy", ActionApi.Least);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var message = (await FactApi.MistakesOf(refused))[0].GetProperty("message").GetString();
        Assert.Contains("'lookupPolicy' is written in the bot's code", message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-8")]
    public async Task Adding_an_http_action_with_an_id_the_bot_declares_and_the_cms_has_no_copy_of_is_refused_naming_it()
    {
        var scene = await SceneAsync();
        await using var _ = scene.Bot;
        await scene.AskAsync();
        await scene.Api.Add("lookupPolicy", ActionApi.Least);
        scene.Answers(Lookup);
        await scene.AskAsync();
        await scene.Api.Delete("lookupPolicy");

        var refused = await scene.Api.Add("lookupPolicy", ActionApi.Least);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal("action.code.declared", mistake.GetProperty("code").GetString());
        Assert.Contains("code action 'lookupPolicy'", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-9")]
    public async Task A_declaration_naming_an_id_of_an_http_action_leaves_it_shows_the_conflict_and_refuses_no_save()
    {
        var scene = await SceneAsync();
        await using var _ = scene.Bot;
        await scene.AskAsync();
        await scene.Api.Add("lookupPolicy", ActionApi.Least);

        scene.Answers(Lookup);
        await scene.AskAsync();

        var shown = await scene.Api.Show("lookupPolicy");
        Assert.Equal("Sends.", shown.GetProperty("description").GetString());
        Assert.Equal(1, shown.GetProperty("revision").GetInt32());
        Assert.True(shown.GetProperty("alsoDeclared").GetBoolean());
        Assert.True(Assert.Single((await scene.Api.List()).EnumerateArray()).GetProperty("alsoDeclared").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await scene.Api.Put("lookupPolicy", ActionApi.Full)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await scene.Api.Add("another", ActionApi.Least)).StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-9")]
    public async Task Creating_a_version_reports_the_conflict_until_the_http_action_is_removed()
    {
        var scene = await SceneAsync();
        await using var _ = scene.Bot;
        await scene.AskAsync();
        (await PutConversation(scene)).EnsureSuccessStatusCode();
        await scene.Api.Add("lookupPolicy", ActionApi.Least);
        scene.Answers(Lookup);
        await scene.AskAsync();

        var refused = await scene.Client.PostAsync($"/api/bots/{scene.Api.Bot}/versions", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Contains(MistakeCodes.ActionCodeConflict, await FactApi.CodesOf(refused));

        await scene.Api.Delete("lookupPolicy");
        await scene.AskAsync();
        var created = await scene.Client.PostAsync($"/api/bots/{scene.Api.Bot}/versions", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    private static Task<HttpResponseMessage> PutConversation(Scene scene) =>
        scene.Client.PutAsJsonAsync(
            $"/api/bots/{scene.Api.Bot}/procedures/conversation",
            new
            {
                description = "The base.",
                firstStep = "wait",
                steps = new { wait = new { listen = new { }, next = "answer" }, answer = new { say = "Hi", next = "wait" } },
            },
            TestContext.Current.CancellationToken
        );

    [Fact]
    [Trait("criterion", "AC-code-actions-15")]
    public async Task A_version_holds_a_bots_code_actions_with_their_contracts_alongside_its_http_actions()
    {
        var scene = await SceneAsync(Lookup);
        await using var _ = scene.Bot;
        await scene.AskAsync();
        (await PutConversation(scene)).EnsureSuccessStatusCode();
        await scene.Api.Add("send", ActionApi.Least);

        var created = await ActionApi.Json(
            await scene.Client.PostAsync($"/api/bots/{scene.Api.Bot}/versions", null, TestContext.Current.CancellationToken)
        );

        var number = created.GetProperty("version").GetProperty("number").GetInt32();
        var shown = await ActionApi.Json(
            await scene.Client.GetAsync($"/api/bots/{scene.Api.Bot}/versions/{number}", TestContext.Current.CancellationToken)
        );
        using var document = JsonDocument.Parse(shown.GetProperty("document").GetString() ?? "");
        var actions = document.RootElement.GetProperty("actions").EnumerateArray().ToList();
        Assert.Equal(["lookupPolicy", "send"], actions.Select(action => action.GetProperty("id").GetString()));
        Assert.Equal("code", actions[0].GetProperty("implementation").GetString());
        Assert.Equal(11, actions[0].GetProperty("parameters").GetProperty("pesel").GetProperty("maxLength").GetInt32());
    }
}
