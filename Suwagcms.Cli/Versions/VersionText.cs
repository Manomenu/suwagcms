using System.Globalization;
using Suwagcms.Contracts;

namespace Suwagcms.Cli.Versions;

internal static class VersionText
{
    public static string Line(VersionSummary version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"v{version.Number}  {Date(version)}  {Counts(version)}  {Short(version.Sha256)}"
        );
    }

    public static string Table(IReadOnlyList<VersionSummary> versions)
    {
        ArgumentNullException.ThrowIfNull(versions);
        return versions.Count == 0
            ? "No versions yet. Create one: suwagcms versions create"
            : string.Join(Environment.NewLine, versions.Select(Line));
    }

    public static string Detail(VersionDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        return string.Join(
            Environment.NewLine,
            string.Create(CultureInfo.InvariantCulture, $"v{detail.Version.Number}  {Date(detail.Version)}  {Counts(detail.Version)}"),
            $"sha256 {detail.Version.Sha256}",
            detail.Document
        );
    }

    public static string Created(VersionCreated created)
    {
        ArgumentNullException.ThrowIfNull(created);
        var number = created.Version.Number.ToString(CultureInfo.InvariantCulture);
        return created.Written
            ? $"Created v{number}  sha256 {Short(created.Version.Sha256)}"
            : $"A version could be created: v{number}  sha256 {Short(created.Version.Sha256)}. Nothing was changed.";
    }

    private static string Counts(VersionSummary version) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{version.Facts} facts, {version.KnowledgeBases} knowledge bases, {version.Articles} articles, {version.Procedures} procedures"
        );

    private static string Date(VersionSummary version) =>
        version.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string Short(string sha256) => sha256.Length <= 9 ? sha256 : $"{sha256[..4]}…{sha256[^4..]}";
}
