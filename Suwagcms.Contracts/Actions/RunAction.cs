namespace Suwagcms.Contracts.Actions;

/// <summary>What a test run of a saved action takes: the value of each parameter, as text.</summary>
public sealed record RunAction(IReadOnlyDictionary<string, string>? Values);
