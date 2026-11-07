using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Suwagcms.Cli.Changes;
using Suwagcms.Cli.Validation;
using Suwagcms.Contracts.Actions;
using Suwagcms.Contracts.Articles;
using Suwagcms.Contracts.Facts;
using Suwagcms.Contracts.KnowledgeBases;
using Suwagcms.Contracts.Personas;
using Suwagcms.Contracts.Procedures;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Actions;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.Procedures;

namespace Suwagcms.Cli.Tests.Changes;

// Why: YAML has no types and the CLI types a scalar by where it stands, so walking the contracts the server reads makes a new numeric or boolean field fail here until the reader knows its place.
public sealed class FieldTypesTests
{
    private const string AnyName = "someName";

    private const string Item = "[]";

    // Why: these types are written by a converter of their own, so the wire is not their properties.
    private static readonly Dictionary<Type, Func<string[], IEnumerable<Leaf>>> Wires = new()
    {
        [typeof(NextSteps)] = path => Leaves(typeof(IReadOnlyList<Transition>), path),
        [typeof(ActionImplementation)] = path => Leaves(typeof(HttpCall), [.. path, "http"]),
    };

    // Why: the kinds of a file of changes and the save each one's fields are read as, as the server's ChangeList maps them.
    private static readonly (string Kind, Type Save)[] Saves =
    [
        ("fact", typeof(SaveFact)),
        ("knowledgeBase", typeof(SaveKnowledgeBase)),
        ("article", typeof(SaveArticle)),
        ("procedure", typeof(SaveProcedure)),
        ("action", typeof(SaveAction)),
        ("persona", typeof(SavePersona)),
    ];

    private sealed record Leaf(string[] Path, bool IsBoolean);

    public static TheoryData<string, string, bool> ChangeLeaves()
    {
        var data = new TheoryData<string, string, bool>();
        foreach (var (kind, save) in Saves)
        {
            foreach (var leaf in Leaves(save, []))
            {
                data.Add(kind, string.Join('\n', leaf.Path), leaf.IsBoolean);
            }
        }
        return data;
    }

    public static TheoryData<string, string, bool> DefinitionLeaves()
    {
        var data = new TheoryData<string, string, bool>();
        foreach (var root in new[] { typeof(BotDefinition), typeof(BotDeclaration) })
        {
            foreach (var leaf in Leaves(root, []))
            {
                data.Add(root.Name, string.Join('\n', leaf.Path), leaf.IsBoolean);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(ChangeLeaves))]
    [Trait("criterion", "AC-batch-apply-22")]
    public async Task A_file_of_changes_reads_every_numeric_and_boolean_field_of_a_save_as_that_type(
        string kind,
        string path,
        bool isBoolean
    )
    {
        ArgumentNullException.ThrowIfNull(path);
        var segments = path.Split('\n');
        var file = Write($"changes:\n  - {{edit: {kind}, {Flow(segments, isBoolean)[1..^1]}}}\n");

        var (document, problem) = await ChangeFile.ReadAsync(file, TestContext.Current.CancellationToken);

        Assert.Equal("", problem);
        AssertTyped(At(document?["changes"]?[0], segments), segments, isBoolean);
    }

    [Theory]
    [MemberData(nameof(DefinitionLeaves))]
    [Trait("criterion", "AC-batch-apply-22")]
    public async Task A_definition_or_declaration_reads_every_numeric_and_boolean_field_as_that_type(
        string root,
        string path,
        bool isBoolean
    )
    {
        _ = root;
        ArgumentNullException.ThrowIfNull(path);
        var segments = path.Split('\n');
        // Why: a file that starts with a brace is read as JSON, so the top stays in block style.
        var file = Write($"{segments[0]}: {Flow(segments[1..], isBoolean)}\n");

        var (document, problem, _) = await DefinitionFile.ReadAsync(file, TestContext.Current.CancellationToken);

        Assert.Equal("", problem);
        AssertTyped(At(document is { } read ? JsonNode.Parse(read.GetRawText()) : null, segments), segments, isBoolean);
    }

    private static void AssertTyped(JsonNode? node, string[] segments, bool isBoolean)
    {
        var expected = isBoolean ? JsonValueKind.True : JsonValueKind.Number;
        Assert.True(
            node?.GetValueKind() == expected,
            $"`{string.Join('.', segments)}` reads as {node?.GetValueKind()}, not {expected}: teach FormatYaml.Scalar where this field stands, or add its name to the top names of the file's reader."
        );
    }

    // Why: flow style is still plain scalars to YAML, and it nests any path on one line.
    private static string Flow(string[] segments, bool isBoolean) =>
        segments.Length == 0 ? (isBoolean ? "true" : "7")
        : segments[0] == Item ? $"[{Flow(segments[1..], isBoolean)}]"
        : $"{{{segments[0]}: {Flow(segments[1..], isBoolean)}}}";

    private static JsonNode? At(JsonNode? node, string[] segments) =>
        segments.Aggregate(node, (current, segment) => segment == Item ? current?[0] : current?[segment]);

    private static string Write(string text)
    {
        var folder = Path.Combine(Path.GetTempPath(), "suwagcms-field-types", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "file.yaml");
        File.WriteAllText(path, text);
        return path;
    }

    private static IEnumerable<Leaf> Leaves(Type type, string[] path)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(bool))
        {
            return [new Leaf(path, true)];
        }
        if (type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(decimal))
        {
            return [new Leaf(path, false)];
        }
        if (type == typeof(string) || type == typeof(JsonElement) || type.IsEnum)
        {
            return [];
        }
        if (Wires.TryGetValue(type, out var wire))
        {
            return wire(path);
        }
        Assert.Null(type.GetCustomAttribute<JsonConverterAttribute>());
        if (Generic(type, typeof(IReadOnlyDictionary<,>)) is { } dictionary)
        {
            return Leaves(dictionary.GetGenericArguments()[1], [.. path, AnyName]);
        }
        if (Generic(type, typeof(IEnumerable<>)) is { } list)
        {
            return Leaves(list.GetGenericArguments()[0], [.. path, Item]);
        }
        Assert.True(path.Length < 12, $"{type} nests too deep at {string.Join('.', path)}: a recursive contract needs a stop here.");
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property =>
                property.SetMethod is not null
                && property.GetCustomAttribute<JsonExtensionDataAttribute>() is null
                && property.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always }
            )
            .SelectMany(property =>
                Leaves(
                    property.PropertyType,
                    [
                        .. path,
                        property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                            ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name),
                    ]
                )
            );
    }

    private static Type? Generic(Type type, Type definition) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == definition
            ? type
            : type.GetInterfaces().FirstOrDefault(face => face.IsGenericType && face.GetGenericTypeDefinition() == definition);
}
