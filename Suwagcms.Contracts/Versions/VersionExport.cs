namespace Suwagcms.Contracts.Versions;

public sealed record VersionExport(string Bot, int Number, IReadOnlyList<ExportedFile> Files);
