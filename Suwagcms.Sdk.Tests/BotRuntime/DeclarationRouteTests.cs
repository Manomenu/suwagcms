using System.Net;
using Suwagcms.Sdk.BotRuntime;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Tests.BotRuntime;

public sealed class DeclarationRouteTests
{
    private static async Task<System.Text.Json.Nodes.JsonNode> DeclarationAsync(BotHost bot) =>
        System.Text.Json.Nodes.JsonNode.Parse(
            await bot.Client.GetStringAsync(new Uri("/suwagcms/declaration", UriKind.Relative), TestContext.Current.CancellationToken)
        ) ?? throw new InvalidOperationException("empty declaration");

    [Fact]
    [Trait("criterion", "AC-bot-declaration-1")]
    public async Task A_bot_whose_setup_states_what_it_supports_answers_with_it_and_its_own_id()
    {
        await using var bot = await BotHost.StartAsync(
            new InMemoryVersionStore(),
            declares: new BotCapabilities
            {
                FormatVersion = 1,
                Blocks = ["listen", "say"],
                CodeActions =
                [
                    new CodeAction
                    {
                        Id = "lookupPolicy",
                        Description = "Finds a policy",
                        Parameters =
                        [
                            new CodeActionField
                            {
                                Name = "city",
                                Type = FactTypes.Text,
                                Description = "The city",
                                MaxLength = 80,
                            },
                        ],
                    },
                ],
            }
        );

        var answer = await DeclarationAsync(bot);

        Assert.Equal("rzeczoznawca", answer.Member("bot").Text());
        Assert.False(answer.Member("declaresNothing").GetValue<bool>());
        Assert.Equal(1, answer.Member("formatVersion").GetValue<int>());
        Assert.Equal(["listen", "say"], answer.Member("blocks").Items().Select(block => block.Text()));
        var action = Assert.Single(answer.Member("codeActions").Items());
        Assert.Equal("lookupPolicy", action.Member("id").Text());
        Assert.Equal("Finds a policy", action.Member("description").Text());
        Assert.Equal(80, Assert.Single(action.Member("parameters").Items()).Member("maxLength").GetValue<int>());
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-2")]
    public async Task A_bot_whose_setup_states_nothing_says_so_and_still_accepts_and_reports_versions()
    {
        await using var bot = await BotHost.StartAsync(new InMemoryVersionStore());

        var answer = await DeclarationAsync(bot);

        Assert.Equal("rzeczoznawca", answer.Member("bot").Text());
        Assert.True(answer.Member("declaresNothing").GetValue<bool>());
        Assert.Empty(answer.Member("blocks").Items());
        var (status, _) = await bot.PutAsync(1, Documents.Make(1));
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(1, (await bot.StateAsync()).Current);
    }

    [Fact]
    [Trait("criterion", "AC-code-actions-12")]
    public async Task A_bot_whose_version_holds_a_code_action_it_does_not_declare_refuses_it_and_keeps_what_it_had()
    {
        await using var bot = await BotHost.StartAsync(
            new InMemoryVersionStore(),
            declares: new BotCapabilities { FormatVersion = 1, Blocks = Block.Names }
        );
        var good = Documents.Make(1);
        var (accepted, _) = await bot.PutAsync(1, good);
        Assert.Equal(HttpStatusCode.OK, accepted);
        var bad = Documents.Make(2);
        bad["actions"] = new System.Text.Json.Nodes.JsonArray(
            new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = "lookupPolicy",
                ["description"] = "Finds",
                ["implementation"] = "code",
            }
        );
        bad["digest"] = VersionDigest.Of(bad.ToJsonString());

        var (status, body) = await bot.PutAsync(2, bad);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Contains("action.code.undeclared", body.ToJsonString(), StringComparison.Ordinal);
        Assert.Equal(1, (await bot.StateAsync()).Current);
    }
}
