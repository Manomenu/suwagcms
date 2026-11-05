using System.Text.Json;
using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Format;

// Why: the wire has one property per block so that a step with two blocks, or an unknown one, can be read and refused with a fix.
public sealed record ProcedureStep
{
    public ListenArguments? Listen { get; init; }

    public IReadOnlyList<string>? Collect { get; init; }

    public string? Say { get; init; }

    public IReadOnlyDictionary<string, string>? Assign { get; init; }

    public string? Start { get; init; }

    public InformArguments? Inform { get; init; }
    public string? Action { get; init; }

    // Why: parameter name to the fact that gives its value; every parameter must be mapped before a version, since a call without its input is broken.
    public IReadOnlyDictionary<string, string>? With { get; init; }

    // Why: result name to the fact it writes; a result left unmapped is ignored, since a result nobody needs is harmless.
    public IReadOnlyDictionary<string, string>? Results { get; init; }

    public string? OnFailure { get; init; }

    public NextSteps? Next { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? UnknownBlocks { get; init; }

    [JsonIgnore]
    public IReadOnlyList<Block> Blocks =>
        [
            .. new Block?[]
            {
                Listen is null ? null : new ListenBlock(),
                Collect is null ? null : new CollectBlock(Collect),
                Say is null ? null : new SayBlock(Say),
                Assign is null ? null : new AssignBlock(Assign),
                Start is null ? null : new StartBlock(Start),
                Inform is null ? null : new InformBlock(Inform),
                Action is null
                    ? null
                    : new ActionBlock(Action, With ?? new Dictionary<string, string>(), Results ?? new Dictionary<string, string>()),
            }.OfType<Block>(),
        ];
}
