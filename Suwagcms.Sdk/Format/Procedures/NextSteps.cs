using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Format.Procedures;

[JsonConverter(typeof(NextStepsConverter))]
public sealed record NextSteps
{
    public string? Step { get; init; }

    public IReadOnlyList<Transition>? Rules { get; init; }

    public IEnumerable<(string Path, string Target)> Targets =>
        Step is not null ? [("next", Step)] : (Rules ?? []).Select((rule, index) => ($"next[{index}].to", rule.To));
}
