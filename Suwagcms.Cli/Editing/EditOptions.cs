using System.CommandLine;
using System.Globalization;

namespace Suwagcms.Cli.Editing;

internal static class EditOptions
{
    public static Option<int?> IfRevision() =>
        new("--if-revision")
        {
            Description = "Apply only if it is still at this revision (see `show`); refused with the current revision otherwise.",
        };

    public static Option<bool> DryRun() =>
        new("--dry-run") { Description = "Answer as the real command would, with the same refusals, and change nothing." };

    public static string Query(bool dryRun, int? ifRevision)
    {
        var parts = new List<string>();
        if (ifRevision is { } revision)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"ifRevision={revision}"));
        }
        if (dryRun)
        {
            parts.Add("dryRun=true");
        }
        return parts.Count == 0 ? "" : "?" + string.Join('&', parts);
    }

    public static void RefuseOpen(Command command, GlobalOptions options, string words, string why = "") =>
        command.Validators.Add(result =>
        {
            if (result.GetValue(options.Open))
            {
                result.AddError($"`suwagcms {words}` has no web page to open{why}: drop --open.");
            }
        });

    public static void OpenWhatItSaves(Command command, GlobalOptions options, Option<bool> dryRun)
    {
        RefuseOpenWithJson(command, options);
        command.Validators.Add(result =>
        {
            if (result.GetValue(options.Open) && result.GetValue(dryRun))
            {
                result.AddError("--dry-run and --open cannot be used together: a dry run saves nothing, so there is nothing to open.");
            }
        });
    }

    public static void RefuseOpenWithJson(Command command, GlobalOptions options) =>
        command.Validators.Add(result =>
        {
            if (result.GetValue(options.Json) && result.GetValue(options.Open))
            {
                result.AddError("--json and --open cannot be used together: one prints the data, the other opens the page.");
            }
        });
}
