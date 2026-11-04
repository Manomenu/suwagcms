using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Validation;

internal sealed record Rule(string Code, ValidationStage Stage, Func<BotDefinition, IEnumerable<Mistake>> Check);
