using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Format;

// Why: the format writes next either as a step's id or as a list of rules, and one record holds whichever the author wrote.
[JsonConverter(typeof(NextConverter))]
public sealed record NextSteps
{
    public string? Step { get; init; }

    public IReadOnlyList<Transition>? Rules { get; init; }

    public IEnumerable<(string Path, string Target)> Targets =>
        Step is not null ? [("next", Step)] : (Rules ?? []).Select((rule, index) => ($"next[{index}].to", rule.To));
}
