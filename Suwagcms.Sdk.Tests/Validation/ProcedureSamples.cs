using System.Text.Json.Nodes;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.Procedures.Blocks;

namespace Suwagcms.Sdk.Tests.Validation;

internal static class ProcedureSamples
{
    private static readonly System.Text.Json.JsonSerializerOptions Web = new(System.Text.Json.JsonSerializerDefaults.Web);

    public static GlobalFact Kind { get; } =
        new()
        {
            Name = "order.kind",
            Type = FactTypes.Enum,
            Description = "What is being valued",
            Values = ["house", "flat"],
        };

    public static GlobalFact Floors { get; } =
        new()
        {
            Name = "order.floors",
            Type = FactTypes.WholeNumber,
            Description = "How many floors",
            Min = 1,
            Max = 40,
        };

    public static GlobalFact Flag { get; } =
        new()
        {
            Name = "system.flag",
            Type = FactTypes.Text,
            Description = "Set by the procedure only",
            WritableBy = FactWriters.Assign,
            MaxLength = 5,
        };

    public static IReadOnlyList<GlobalFact> Facts { get; } = [Kind, Floors, Samples.CityFact, Flag];

    // Why: the procedure of the spec's own example, with all four blocks, every condition kind, a default rule and a loop back to its listen.
    public static Procedure All { get; } =
        new()
        {
            Id = "orderValuation",
            Description = "The customer orders a valuation of a property.",
            FirstStep = "wait",
            Steps = new Dictionary<string, ProcedureStep>
            {
                ["wait"] = new()
                {
                    Listen = new ListenArguments(),
                    Next = new NextSteps { Step = "ask" },
                },
                ["ask"] = new()
                {
                    Collect = ["customer.city", "order.kind"],
                    Next = new NextSteps
                    {
                        Rules =
                        [
                            new Transition
                            {
                                When = new Dictionary<string, Condition> { ["order.kind"] = new() { OneOf = ["house"] } },
                                To = "mark",
                            },
                            new Transition
                            {
                                When = new Dictionary<string, Condition>
                                {
                                    ["order.floors"] = new() { Range = new NumberRange { Min = 10 } },
                                    ["customer.city"] = new() { Known = true },
                                },
                                To = "mark",
                            },
                            new Transition { To = "goodbye" },
                        ],
                    },
                },
                ["mark"] = new()
                {
                    Assign = new Dictionary<string, string> { ["system.flag"] = "big" },
                    Next = new NextSteps { Step = "confirm" },
                },
                ["confirm"] = new()
                {
                    Say = "Thank you, we will call you.",
                    Next = new NextSteps { Step = "wait" },
                },
                ["goodbye"] = new() { Say = "Sorry, we only value houses." },
            },
            WhenStalled = new StallInstruction { Instruction = "Tell the customer you could not finish." },
        };

    public static BotDefinition Bot(params Procedure[] procedures) =>
        new()
        {
            FormatVersion = 5,
            Facts = Facts,
            Procedures = procedures,
        };

    public static Procedure WithStep(string name, ProcedureStep step, string firstStep = "wait") =>
        All with
        {
            FirstStep = firstStep,
            Steps = new Dictionary<string, ProcedureStep>(All.Steps) { [name] = step },
        };

    public static Procedure Alone(ProcedureStep step) =>
        Samples.Procedure("p") with
        {
            FirstStep = "only",
            Steps = new Dictionary<string, ProcedureStep> { ["only"] = step },
        };

    // Why: one definition that breaks every rule of the procedure model, so the shape test sees that each rule can fire.
    public static string EveryMistake { get; } =
        new JsonObject
        {
            ["formatVersion"] = 5,
            ["facts"] = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(Facts, Web)),
            ["procedures"] = new JsonArray(
                new JsonObject
                {
                    ["id"] = "Bad Id",
                    ["description"] = " ",
                    ["firstStep"] = "ghost",
                    ["whenInterrupted"] = "sometimes",
                    ["steps"] = new JsonObject
                    {
                        ["Bad Name"] = new JsonObject { ["say"] = "x", ["extra"] = 1 },
                        ["two"] = new JsonObject
                        {
                            ["listen"] = new JsonObject(),
                            ["say"] = "x",
                            ["next"] = "ghost",
                        },
                        ["none"] = new JsonObject(),
                        ["blank"] = new JsonObject { ["say"] = " " },
                        ["toBase"] = new JsonObject { ["start"] = "conversation" },
                        ["informNothing"] = new JsonObject { ["inform"] = new JsonObject { ["instruction"] = " " } },
                        ["informBadly"] = new JsonObject
                        {
                            ["inform"] = new JsonObject
                            {
                                ["instruction"] = "Say it.",
                                ["points"] = new JsonObject { ["Bad Name"] = " " },
                                ["knowledge"] = new JsonArray(
                                    new JsonObject { ["knowledgeBase"] = "nowhere", ["searchQueries"] = new JsonArray("how long", " ") }
                                ),
                            },
                        },
                        ["defaultFirst"] = new JsonObject
                        {
                            ["say"] = "x",
                            ["next"] = new JsonArray(new JsonObject { ["to"] = "mark" }, new JsonObject { ["to"] = "ask" }),
                        },
                        ["emptyAssign"] = new JsonObject { ["assign"] = new JsonObject() },
                        ["ask"] = new JsonObject
                        {
                            ["collect"] = new JsonArray("order.kind", "nowhere", "system.flag"),
                            ["next"] = new JsonArray(
                                new JsonObject
                                {
                                    ["when"] = new JsonObject
                                    {
                                        ["order.kind"] = new JsonObject { ["range"] = new JsonObject { ["min"] = 1 } },
                                        ["order.floors"] = new JsonObject { ["oneOf"] = new JsonArray("many"), ["known"] = true },
                                        ["customer.city"] = new JsonObject(),
                                    },
                                    ["to"] = "ghost",
                                },
                                new JsonObject
                                {
                                    ["when"] = new JsonObject
                                    {
                                        ["order.floors"] = new JsonObject { ["range"] = new JsonObject { ["min"] = 50 } },
                                        ["order.kind"] = new JsonObject { ["oneOf"] = new JsonArray() },
                                    },
                                    ["to"] = "ask",
                                }
                            ),
                        },
                        ["mark"] = new JsonObject
                        {
                            ["assign"] = new JsonObject { ["order.floors"] = "99", ["nowhere"] = "x" },
                        },
                    },
                },
                new JsonObject
                {
                    ["id"] = "noFirstStep",
                    ["description"] = "d",
                    ["steps"] = new JsonObject { ["ask"] = new JsonObject { ["collect"] = new JsonArray("order.kind") } },
                    ["whenStalled"] = new JsonObject { ["instruction"] = " " },
                },
                new JsonObject
                {
                    ["id"] = "tidy",
                    ["description"] = "d",
                    ["firstStep"] = "a",
                    ["steps"] = new JsonObject
                    {
                        ["a"] = new JsonObject { ["collect"] = new JsonArray("order.kind") },
                        ["b"] = new JsonObject { ["say"] = "lost" },
                    },
                }
            ),
        }.ToJsonString();
}
