using System.Text.Json.Nodes;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Tests.BotRuntime;

public sealed class PersonaDeliveryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("criterion", "AC-bot-persona-8")]
    public async Task A_version_holding_a_persona_gives_it_to_the_bots_code_with_its_line_breaks()
    {
        await using var bot = await BotHost.StartAsync(new Suwagcms.Sdk.BotRuntime.InMemoryVersionStore());
        var document = Documents.Make(1);
        document["persona"] = new JsonObject
        {
            ["personality"] = "First line\nSecond line\n",
            ["scope"] = new JsonObject { ["whenOutOfScope"] = "Decline\nOffer help" },
        };
        await bot.PutAsync(1, Documents.Sign(document));

        var persona = (await bot.Definitions.GetAsync(1, Token)).Persona;

        Assert.Equal("First line\nSecond line\n", persona?.Personality);
        Assert.Equal("Decline\nOffer help", persona?.Scope?.WhenOutOfScope);
        Assert.Null(persona?.Scope?.InScope);
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-8")]
    public async Task A_document_with_no_persona_loads_as_having_none()
    {
        await using var bot = await BotHost.StartAsync(new Suwagcms.Sdk.BotRuntime.InMemoryVersionStore());
        await bot.PutAsync(1, Documents.Make(1));

        Assert.Null((await bot.Definitions.GetAsync(1, Token)).Persona);
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-8")]
    public void The_definition_schema_offers_the_persona_as_an_optional_member()
    {
        var schema = JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "Suwagcms.Sdk", "definition-format.schema.json")));

        Assert.NotNull(schema?["properties"]?["persona"]?["properties"]?["scope"]?["properties"]?["whenOutOfScope"]);
        Assert.DoesNotContain("persona", (schema?["required"]?.AsArray() ?? []).Select(item => item?.GetValue<string>()));
        Assert.Equal(1, BotDefinition.CurrentFormatVersion);
    }

    private static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Suwagcms.slnx")))
        {
            folder = folder.Parent;
        }
        return folder?.FullName ?? throw new InvalidOperationException("no repository above the tests");
    }
}
