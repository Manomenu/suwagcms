using System.Text.Json;
using System.Text.Json.Serialization;
using Suwagcms.Sdk.Format.Procedures.Blocks;

namespace Suwagcms.Sdk.Format.Procedures;

public sealed record ProcedureStep
{
    public ListenArguments? Listen { get; init; }

    public IReadOnlyList<string>? Collect { get; init; }

    public string? Say { get; init; }

    public IReadOnlyDictionary<string, string>? Assign { get; init; }

    public string? Start { get; init; }

    public InformArguments? Inform { get; init; }
    public string? Action { get; init; }

    public IReadOnlyDictionary<string, string>? With { get; init; }

    public IReadOnlyDictionary<string, string>? Results { get; init; }

    public string? OnFailure { get; init; }

    public string? OnUnavailable { get; init; }

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
