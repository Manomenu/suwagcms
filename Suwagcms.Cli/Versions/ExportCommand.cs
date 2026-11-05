using System.CommandLine;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Cli.Output;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Cli.Versions;

// Why: the export goes one way: the CMS is the single source of truth, so this command only reads from it, and no command takes the files back.
internal static class ExportCommand
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static readonly UTF8Encoding NoMark = new(encoderShouldEmitUTF8Identifier: false);

    public static Command Create(GlobalOptions options, CliEnvironment environment)
    {
        var number = new Argument<int?>("number")
        {
            Description = "The version's number, like 14; the latest when left out.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var output = new Option<string?>("--out") { Description = "A new or empty folder to write the files to." };
        var command = new Command(
            "export",
            "Write a version to a folder of YAML files for reading and searching; changes to those files do nothing (or --open the version in the web app)."
        )
        {
            number,
            output,
        };
        EditOptions.RefuseOpenWithJson(command, options);
        command.Validators.Add(result =>
        {
            if (result.GetValue(output) is null && !result.GetValue(options.Open))
            {
                result.AddError("--out is required: name a new or empty folder, like `suwagcms export 14 --out ./v14`.");
            }
        });
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await BotTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                var folder = parsed.GetValue(output);
                if (folder is not null && FolderProblem(folder) is { } busy)
                {
                    return await RefusedAsync(target, busy);
                }
                var (version, failed) = await VersionAsync(target, parsed.GetValue(number), cancellationToken);
                if (version is not { } chosen)
                {
                    return failed;
                }
                var exported = 0;
                if (folder is not null)
                {
                    exported = await WriteAsync(target, chosen, folder, cancellationToken);
                    if (exported != ExitCodes.Done)
                    {
                        return exported;
                    }
                }
                return parsed.GetValue(options.Open)
                    ? await WebPage.ShowAsync(
                        new Uri(target.PageOf(VersionsCommand.Collection), string.Create(CultureInfo.InvariantCulture, $"?open={chosen}")),
                        parsed.InvocationConfiguration.Output,
                        parsed.InvocationConfiguration.Error,
                        target.Machine.OpenBrowser
                    )
                    : exported;
            }
        );
        return command;
    }

    private static async Task<(int? Version, int ExitCode)> VersionAsync(BotTarget target, int? number, CancellationToken cancellationToken)
    {
        if (number is { } given)
        {
            return (given, ExitCodes.Done);
        }
        var answer = await ServerCall.GetAsync(
            target.Machine,
            target.Address,
            target.CollectionPath(VersionsCommand.Collection),
            cancellationToken
        );
        if (answer.ExitCode != ExitCodes.Done)
        {
            return (null, await target.FailedAsync(answer, cancellationToken));
        }
        var versions = answer.Body?.Deserialize<List<VersionSummary>>(Web) ?? [];
        if (versions.Count == 0)
        {
            return (
                null,
                await RefusedAsync(target, $"Not exported: bot {target.Bot} has no version yet. Create one: suwagcms versions create")
            );
        }
        return (versions.Max(version => version.Number), ExitCodes.Done);
    }

    private static async Task<int> WriteAsync(BotTarget target, int version, string folder, CancellationToken cancellationToken)
    {
        var answer = await ServerCall.GetAsync(
            target.Machine,
            target.Address,
            $"{target.ItemPath(VersionsCommand.Collection, version.ToString(CultureInfo.InvariantCulture))}/export",
            cancellationToken
        );
        if (answer.ExitCode != ExitCodes.Done)
        {
            return await target.FailedAsync(answer, cancellationToken);
        }
        var export = answer.Body?.Deserialize<VersionExport>(Web) ?? throw new InvalidOperationException("the server sent no export");
        var root = Path.GetFullPath(folder);
        if (export.Files.FirstOrDefault(file => !Inside(root, file)) is { } escaping)
        {
            return await RefusedAsync(
                target,
                $"Not exported: the id '{escaping.Id}' would write outside {folder}. Nothing was written. Report it: a version should never hold such an id."
            );
        }
        try
        {
            Directory.CreateDirectory(root);
            foreach (var file in export.Files)
            {
                var path = Path.Combine(root, file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);
                // Why: two ids that differ only in case are one file on some systems, and one must not silently replace the other.
                await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                await stream.WriteAsync(NoMark.GetBytes(ExportYaml.Text(export, file)), cancellationToken);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return await RefusedAsync(
                target,
                $"Not exported: {folder} cannot be written: {exception.Message} Choose a folder you can write to."
            );
        }
        var counts = new ExportCounts(
            export.Files.Count(file => file.Kind == ResourceKind.GlobalFact),
            export.Files.Count(file => file.Kind == ResourceKind.KnowledgeBase),
            export.Files.Count(file => file.Kind == ResourceKind.Article),
            export.Files.Count(file => file.Kind == ResourceKind.Procedure),
            export.Files.Count(file => file.Kind == ResourceKind.Action)
        );
        return await target.Run.Report.DoneAsync(
            new ExportDocument(folder, target.Bot, version, counts),
            string.Create(
                CultureInfo.InvariantCulture,
                $"Exported v{version} of {target.Bot} to {folder}: {counts.Facts} facts, {counts.KnowledgeBases} knowledge bases, {counts.Articles} articles, {counts.Procedures} procedures, {counts.Actions} actions"
            )
        );
    }

    // Why: a path that only stays inside by passing through `..` is no file named by its id, so it is refused with the ones that leave.
    private static bool Inside(string root, ExportedFile file)
    {
        var path = Path.Combine(root, file.Path);
        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) && Path.GetFullPath(path) == path;
    }

    // Why: a file left over from another version would read as part of this one, and deleting a person's files is not this command's business.
    private static string? FolderProblem(string folder)
    {
        try
        {
            return Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any()
                ? $"Not exported: {folder} is not empty. Choose an empty or new folder, so files from another version cannot be read as part of this one."
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"Not exported: {folder} cannot be read: {exception.Message} Choose a folder you can write to.";
        }
    }

    private static Task<int> RefusedAsync(BotTarget target, string message) =>
        target.Run.Report.FailedAsync(ExitCodes.Refused, new FailureDocument(message), message);
}
