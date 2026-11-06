namespace Suwagcms.Contracts.Versions;

/// <summary>A version split into the files of an export, for reading and searching; nothing takes them back. The same version always gives the same files.</summary>
public sealed record VersionExport(string Bot, int Number, IReadOnlyList<ExportedFile> Files);
