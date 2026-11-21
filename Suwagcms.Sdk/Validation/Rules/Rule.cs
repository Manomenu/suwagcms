using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Validation.Rules;

internal sealed record Rule(MistakeCode Code, ValidationStage Stage, Func<BotDefinition, IEnumerable<Mistake>> Check);
