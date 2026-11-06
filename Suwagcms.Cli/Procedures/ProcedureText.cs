using System.Globalization;
using Suwagcms.Contracts.Procedures;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Cli.Procedures;

internal static class ProcedureText
{
    public static string Table(ProcedureList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (list.Procedures.Count == 0)
        {
            return "No procedures yet. Write one in a YAML file and add it: suwagcms procedures add --file <file>";
        }
        var idWidth = list.Procedures.Max(procedure => procedure.Id.Length);
        var descriptionWidth = list.Procedures.Max(procedure => procedure.Description.Length);
        var lines = list.Procedures.Select(procedure =>
            $"{procedure.Id.PadRight(idWidth)}  {procedure.Description.PadRight(descriptionWidth)}  {Readiness(procedure.NotReady)}"
        );
        return string.Join(
            Environment.NewLine,
            list.BotNotReady.Count == 0 ? lines : [.. lines, "", Mistakes("The bot is not ready for a version", list.BotNotReady)]
        );
    }

    public static string Readiness(int count) =>
        count == 0 ? "ready" : string.Create(CultureInfo.InvariantCulture, $"{count} mistake{(count == 1 ? "" : "s")} before a version");

    // Why: a save never refuses for these, so the answer says them in the same words every time: the code, what is wrong, and the fix.
    public static string Mistakes(string head, IReadOnlyList<Mistake> mistakes)
    {
        ArgumentNullException.ThrowIfNull(mistakes);
        var width = mistakes.Max(mistake => mistake.Code.Length);
        return string.Join(
            Environment.NewLine,
            [
                string.Create(CultureInfo.InvariantCulture, $"{head}, {mistakes.Count} mistake{(mistakes.Count == 1 ? "" : "s")}:"),
                .. mistakes.Select(mistake => $"  {mistake.Code.PadRight(width)}  {mistake.Message} {mistake.Fix}"),
            ]
        );
    }

    public static string Saved(ProcedureDetail procedure, bool dryRun, string past, string word)
    {
        ArgumentNullException.ThrowIfNull(procedure);
        var head = dryRun
            ? $"Would {word} '{procedure.Id}'. Nothing was changed."
            : string.Create(CultureInfo.InvariantCulture, $"{past} {procedure.Id}, revision {procedure.Revision}.");
        return procedure.NotReady.Count == 0
            ? $"{head} Ready for a version."
            : $"{head} {Mistakes("Not ready for a version", procedure.NotReady)}";
    }
}
