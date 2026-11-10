using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Suwagcms.Contracts.Bots;
using Suwagcms.Contracts.Facts;
using Suwagcms.Contracts.Validation;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Tests.Versions;

namespace Suwagcms.Server.Tests.Validation;

public sealed class ValidateEndpointTests(TestDatabase database, SharedApp shared) : IClassFixture<SharedApp>
{
    private const string Unfinished = """
        {"formatVersion":4,"facts":[{"name":"Bad Name","type":"blob","description":" "}],
         "procedures":[{"id":"conversation","description":"Base","firstStep":"ask","steps":{"ask":{"say":"Hello"}}}]}
        """;

    private const string Declares = """{"bot":"anything","formatVersion":4,"blocks":["listen"]}""";

    private TestApp App => shared.App;

    private HttpClient Client()
    {
        _ = database.Pool;
        return App.CreateClient();
    }

    private static async Task<ValidationAnswer> PostAsync(HttpClient client, string body)
    {
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/validate", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<ValidationAnswer>(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("no answer");
    }

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static string Comparable(IEnumerable<Mistake> mistakes) => JsonSerializer.Serialize(mistakes, Web);

    [Theory]
    [InlineData("Save", false)]
    [InlineData("Version", false)]
    [InlineData("Save", true)]
    [InlineData("Version", true)]
    [Trait("criterion", "AC-bot-declaration-14")]
    public async Task The_endpoint_returns_the_same_mistakes_as_the_library_for_the_same_definition_stage_and_declaration(
        string stage,
        bool declared
    )
    {
        var library = DefinitionValidator.ValidateJson(
            Unfinished,
            Enum.Parse<ValidationStage>(stage),
            declared ? DeclarationValidator.ParseJson(Declares).Declaration : null
        );

        var answer = await PostAsync(
            Client(),
            $$"""{"definition":{{Unfinished}},"stage":"{{stage}}"{{(declared ? $",\"declaration\":{Declares}" : "")}}}"""
        );

        Assert.NotEmpty(library);
        Assert.Equal(Comparable(library), Comparable(answer.Mistakes));
        Assert.Equal(declared && stage == "Version", answer.Mistakes.Any(mistake => mistake.Code == MistakeCodes.DeclarationBlock));
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-14")]
    public async Task The_stage_is_version_when_left_out_and_a_definition_with_no_mistakes_returns_none()
    {
        var answer = await PostAsync(
            Client(),
            """{"definition":{"formatVersion":4,"procedures":[{"id":"conversation","description":"Base","firstStep":"ask","steps":{"ask":{"say":"Hello"}}}]}}"""
        );
        Assert.Empty(answer.Mistakes);

        var missing = await PostAsync(Client(), """{"definition":{"formatVersion":4}}""");
        Assert.Equal(MistakeCodes.BotConversationMissing, Assert.Single(missing.Mistakes).Code);
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-14")]
    public async Task A_definition_or_declaration_that_cannot_be_read_gets_a_mistake_and_a_body_with_a_typo_is_refused()
    {
        var client = Client();
        Assert.Equal(MistakeCodes.FormatInvalid, Assert.Single((await PostAsync(client, """{"stage":"Version"}""")).Mistakes).Code);
        Assert.Equal(
            MistakeCodes.DeclarationInvalid,
            Assert
                .Single((await PostAsync(client, """{"definition":{"formatVersion":4},"declaration":{"blocks":"listen"}}""")).Mistakes)
                .Code
        );

        using var content = new StringContent("""{"definitoin":{}}""", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/validate", content, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-14")]
    public async Task Validating_stores_nothing_and_contacts_no_bot()
    {
        var client = Client();
        var id = PushScene.FreshId();
        await using var bot = await TestBot.StartAsync(id);
        var scene = await PushScene.RegisterAsync(client, id, bot.Address);
        var before = await scene.SnapshotAsync();

        await PostAsync(client, "{\"definition\":" + Unfinished + ",\"declaration\":" + Declares + "}");

        Assert.Equal(before, await scene.SnapshotAsync());
        Assert.Equal(0, bot.Requests);
        Assert.Equal(
            DeclarationStored.NeverAnswered,
            (
                await client.GetFromJsonAsync<DeclarationStatus>(
                    $"/api/bots/{id}/declaration?ask=false",
                    TestContext.Current.CancellationToken
                )
            )?.Stored
        );
    }
}
