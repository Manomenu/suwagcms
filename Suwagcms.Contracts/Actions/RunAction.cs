namespace Suwagcms.Contracts.Actions;

public sealed record RunAction(IReadOnlyDictionary<string, string>? Values);
