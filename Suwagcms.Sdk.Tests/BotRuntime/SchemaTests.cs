using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using Suwagcms.Sdk.BotRuntime;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Tests.Repository;

namespace Suwagcms.Sdk.Tests.BotRuntime;

public sealed class SchemaTests
{
    private const string WriteVariable = "SUWAGCMS_WRITE_SCHEMA";

    [Fact]
    [Trait("criterion", "AC-bot-runtime-sdk-16")]
    public async Task The_committed_schema_of_the_state_answer_and_the_refusal_equals_the_one_generated()
    {
        var path = RepositoryFiles.Path("Suwagcms.Sdk/bot-runtime.schema.json");
        var generated = Generate().ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n") + "\n";

        if (Environment.GetEnvironmentVariable(WriteVariable) == "1")
        {
            await File.WriteAllTextAsync(path, generated, TestContext.Current.CancellationToken);
            return;
        }

        var committed = (await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)).ReplaceLineEndings("\n");
        Assert.True(
            committed == generated,
            "bot-runtime.schema.json no longer matches the state answer, the refusal or the version envelope. Regenerate it with: just sdk-schema (scripts/.internal/sdk-schema.sh), and commit the result."
        );
    }

    [Fact]
    [Trait("criterion", "AC-bot-runtime-sdk-16")]
    public void The_schema_describes_the_members_a_bot_outside_dotnet_has_to_read()
    {
        JsonNode schema = Generate();

        Assert.Contains("\"storeError\"", schema.Member("state").ToJsonString(), StringComparison.Ordinal);
        Assert.Contains("\"mistakes\"", schema.Member("refusal").ToJsonString(), StringComparison.Ordinal);
        Assert.Contains("\"digest\"", schema.Member("versionEnvelope").ToJsonString(), StringComparison.Ordinal);
    }

    private static JsonObject Generate() =>
        new()
        {
            ["state"] = Schema(typeof(BotState)),
            ["refusal"] = Schema(typeof(BotRefusal)),
            ["versionEnvelope"] = Schema(typeof(VersionEnvelope)),
        };

    private static JsonNode Schema(Type type)
    {
        var schema =
            JsonNode.Parse(JsonSchemaExporter.GetJsonSchemaAsNode(BotRuntimeJson.Options, type).ToJsonString())
            ?? throw new InvalidOperationException("The exporter returned no schema.");
        // Why: the exporter allows null for the root, but no route answers with null.
        if (schema["type"] is JsonArray types && types.Any(entry => entry?.GetValue<string>() == "object"))
        {
            schema["type"] = "object";
        }

        return schema;
    }
}
