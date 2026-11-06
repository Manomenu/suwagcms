using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Actions;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Tests.Repository;

namespace Suwagcms.Sdk.Tests.Format;

public sealed class SchemaTests
{
    private const string WriteVariable = "SUWAGCMS_WRITE_SCHEMA";

    [Fact]
    [Trait("criterion", "AC-sdk-validator-11")]
    public async Task The_committed_schema_equals_the_one_generated_from_the_format()
    {
        var path = RepositoryFiles.Path("Suwagcms.Sdk/definition-format.schema.json");
        var generated = Schema().ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n") + "\n";

        if (Environment.GetEnvironmentVariable(WriteVariable) == "1")
        {
            await File.WriteAllTextAsync(path, generated, TestContext.Current.CancellationToken);
            return;
        }

        var committed = (await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)).ReplaceLineEndings("\n");
        Assert.True(
            committed == generated,
            "definition-format.schema.json no longer matches the format. Regenerate it with: just sdk-schema (scripts/.internal/sdk-schema.sh), and commit the result."
        );
    }

    [Fact]
    [Trait("criterion", "AC-sdk-validator-11")]
    public void The_schema_describes_the_envelope_and_the_five_lists()
    {
        var schema = Schema().ToJsonString();

        foreach (var name in new[] { "formatVersion", "facts", "knowledgeBases", "articles", "actions", "procedures" })
        {
            Assert.Contains($"\"{name}\"", schema, StringComparison.Ordinal);
        }
    }

    // Why: next is a step's id or a list of rules, and an implementation is the word code or an HTTP call, which the exporter cannot see through the converters, so the schema says so itself.
    private static readonly JsonSchemaExporterOptions ExporterOptions = new()
    {
        TransformSchemaNode = (context, node) =>
            context.TypeInfo.Type == typeof(NextSteps) ? OneOf(new JsonObject { ["type"] = "string" }, typeof(List<Transition>))
            : context.TypeInfo.Type == typeof(ActionImplementation) ? Implementation()
            : node,
    };

    private static JsonObject OneOf(JsonObject first, Type second) =>
        new()
        {
            ["oneOf"] = new JsonArray(
                first,
                JsonNode.Parse(JsonSchemaExporter.GetJsonSchemaAsNode(DefinitionFormat.Options, second).ToJsonString())
            ),
        };

    private static JsonObject Implementation() =>
        new()
        {
            ["oneOf"] = new JsonArray(
                new JsonObject { ["const"] = ActionImplementation.CodeWord },
                new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["http"] = JsonNode.Parse(
                            JsonSchemaExporter.GetJsonSchemaAsNode(DefinitionFormat.Options, typeof(HttpCall)).ToJsonString()
                        ),
                    },
                }
            ),
        };

    private static JsonNode Schema()
    {
        // Why: the exporter's own nodes are lazily typed; a parsed copy is plain JsonObject and JsonArray all the way down.
        var schema = JsonNode.Parse(
            JsonSchemaExporter.GetJsonSchemaAsNode(DefinitionFormat.Options, typeof(BotDefinition), ExporterOptions).ToJsonString()
        );
        WithoutNull(schema);
        return schema ?? throw new InvalidOperationException("The exporter returned no schema.");
    }

    // Why: the exporter allows null for the root and for list items, but the format refuses both (RespectNullableAnnotations).
    private static void WithoutNull(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject properties:
                if (properties["type"] is JsonArray types && types.Any(type => type?.GetValue<string>() == "object"))
                {
                    properties["type"] = "object";
                }

                foreach (var child in properties.Select(pair => pair.Value).ToList())
                {
                    WithoutNull(child);
                }

                break;
            case JsonArray items:
                foreach (var child in items)
                {
                    WithoutNull(child);
                }

                break;
            default:
                break;
        }
    }
}
