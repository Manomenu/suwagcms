namespace Suwagcms.Contracts.Renames;

/// <summary>What changing a resource's id takes: the new id, which is checked by the same rules as a new resource's.</summary>
public sealed record RenameResource(string? NewId);
