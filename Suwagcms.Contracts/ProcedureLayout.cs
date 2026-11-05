namespace Suwagcms.Contracts;

/// <summary>Where the creator draws the steps of a procedure, by step name. It is not part of the procedure or of any version; a step with no position is arranged automatically.</summary>
public sealed record ProcedureLayout(IReadOnlyDictionary<string, NodePosition> Positions);
