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
    [Trait("criterion", "AC-bot-declaration-3")]
    [Trait("criterion", "AC-version-rollback-15")]
    [Trait("criterion", "AC-availability-20")]
    [Trait("criterion", "AC-action-test-run-15")]
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
            "bot-runtime.schema.json no longer matches the state answer, the refusal, the version envelope, the request that makes a version current or the declaration. Regenerate it with: just sdk-schema (scripts/.internal/sdk-schema.sh), and commit the result."
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

    [Fact]
    [Trait("criterion", "AC-version-rollback-15")]
    public void The_schema_describes_the_request_that_makes_a_held_version_current()
    {
        var choice = Generate().Member("currentChoice").ToJsonString();

        Assert.Contains("\"number\"", choice, StringComparison.Ordinal);
        Assert.Contains("\"digest\"", choice, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-3")]
    public void The_schema_describes_the_declaration_with_its_blocks_and_its_code_actions()
    {
        var declaration = Generate().Member("declaration").ToJsonString();

        foreach (var member in new[] { "bot", "declaresNothing", "formatVersion", "blocks", "codeActions", "parameters", "results" })
        {
            Assert.Contains($"\"{member}\"", declaration, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("criterion", "AC-availability-20")]
    public void The_schema_describes_the_availability_message_and_the_state_reports_it()
    {
        var schema = Generate();
        var message = schema.Member("availability").ToJsonString();

        foreach (var member in new[] { "number", "off", "kind", "id", "upToVersion" })
        {
            Assert.Contains($"\"{member}\"", message, StringComparison.Ordinal);
        }
        Assert.Contains("\"availability\"", schema.Member("state").ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-action-test-run-15")]
    public void The_schema_describes_the_test_route()
    {
        var schema = Generate();

        foreach (var member in new[] { "action", "values" })
        {
            Assert.Contains($"\"{member}\"", schema.Member("actionTestRequest").ToJsonString(), StringComparison.Ordinal);
        }
        foreach (var member in new[] { "outcome", "results", "reason", "status", "elapsedMs", "answerStart" })
        {
            Assert.Contains($"\"{member}\"", schema.Member("actionTestAnswer").ToJsonString(), StringComparison.Ordinal);
        }
    }

    private static JsonObject Generate() =>
        new()
        {
            ["state"] = Schema(typeof(BotState)),
            ["refusal"] = Schema(typeof(BotRefusal)),
            ["availability"] = Schema(typeof(BotAvailability)),
            ["versionEnvelope"] = Schema(typeof(VersionEnvelope)),
            ["currentChoice"] = Schema(typeof(CurrentChoice)),
            ["declaration"] = Schema(typeof(BotDeclaration)),
            ["actionTestRequest"] = Schema(typeof(ActionTestRequest)),
            ["actionTestAnswer"] = Schema(typeof(ActionOutcome)),
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
