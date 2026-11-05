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

    public static string AlreadyPushed(string bot, int version) =>
        string.Create(CultureInfo.InvariantCulture, $"Bot {bot} already has v{version}. Nothing was sent.");

    public static string Pushed(string bot, PushResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return string.Create(CultureInfo.InvariantCulture, $"Bot {bot} has v{result.BotHas}.");
    }

    public static string PushFailed(PushResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var heading = string.Create(CultureInfo.InvariantCulture, $"Push of v{result.Version} failed: {result.Reason}");
        return string.Join(
            Environment.NewLine,
            result
                .Mistakes.Select(mistake => $"  {mistake.Code} {mistake.Message}{Environment.NewLine}    Fix: {mistake.Fix}")
                .Prepend(heading)
        );
    }

    public static string Status(BotStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var lines = new List<string> { StatusLine(status) };
        if (status.Latest is null)
        {
            lines.Add("No version yet. Create one: suwagcms versions create");
        }
        else if (status.Reach != BotReach.Answered || status.Has != status.Latest)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"Push it: suwagcms push (v{status.Latest})"));
        }
        if (status.HasUnknownVersion)
        {
            lines.Add("The CMS has no such version: the bot holds a version this CMS never created.");
        }
        var inUse = status.InUse.Where(number => number != status.Has).ToList();
        if (inUse.Count > 0)
        {
            lines.Add($"Still in use by conversations: {string.Join(", ", inUse.Select(number => $"v{number}"))}");
        }
        if (status.Attempts.Count > 0)
        {
            lines.Add("Recent pushes:");
            lines.AddRange(status.Attempts.Select(Attempt));
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string StatusLine(BotStatus status)
    {
        var latest = status.Latest is { } number ? $"v{number}" : "none";
        return status.Reach switch
        {
            BotReach.Answered when status.Has == status.Latest && status.Has is not null => string.Create(
                CultureInfo.InvariantCulture,
                $"Bot {status.Bot} has v{status.Has}, up to date."
            ),
            BotReach.Answered => string.Create(
                CultureInfo.InvariantCulture,
                $"Bot {status.Bot} has {(status.Has is { } has ? $"v{has}" : "none")}. Latest {latest}.{(status.Reason is null ? "" : $" The bot cannot read its store: {status.Reason}.")}"
            ),
            BotReach.WrongAddress =>
                $"Bot {status.Bot} cannot be checked: {status.Address} answers as bot '{status.OtherBot}'. Register the right address for bot '{status.Bot}'.",
            BotReach.Unreachable => string.Create(
                CultureInfo.InvariantCulture,
                $"Bot {status.Bot} cannot be reached: {status.Reason}. {LastKnown(status)} Latest {latest}."
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(status), "unknown way of reaching a bot"),
        };
    }

    private static string LastKnown(BotStatus status) =>
        status.LastKnownAt is { } at
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"Last known: {(status.LastKnown is { } known ? $"v{known}" : "none")}, at {at.UtcDateTime:yyyy-MM-dd HH:mm} UTC."
            )
            : "The bot has not reported a version yet.";

    private static string Attempt(PushAttempt attempt) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"  {attempt.At.UtcDateTime:yyyy-MM-dd HH:mm}  v{attempt.Version}  {attempt.Outcome.ToString().ToLowerInvariant()}{(attempt.Error is null ? "" : $"  {attempt.Error}")}"
        );

    private static string Counts(VersionSummary version) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{version.Facts} facts, {version.KnowledgeBases} knowledge bases, {version.Articles} articles, {version.Procedures} procedures, {version.Actions} actions"
        );

    private static string Date(VersionSummary version) =>
        version.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string Short(string sha256) => sha256.Length <= 9 ? sha256 : $"{sha256[..4]}…{sha256[^4..]}";
}
