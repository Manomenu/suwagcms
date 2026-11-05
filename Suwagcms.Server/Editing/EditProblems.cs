using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Server.Editing;

// Why: every write route answers a refused edit the same way, so a client reads one shape and a new resource adds no mapping of its own.
internal static class EditProblems
{
    public const string BotNotFound = "bot.notFound";
    public const string ResourceMissing = "resource.missing";
    public const string ResourceExists = "resource.exists";
    public const string ResourceStale = "resource.stale";
    public const string ChangedTwice = "change.twice";

    public static ProblemHttpResult For(EditResult result, string bot, string title) =>
        Problem(
            result switch
            {
                EditResult.Refused => StatusCodes.Status422UnprocessableEntity,
                EditResult.Stale => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status404NotFound,
            },
            title,
            MistakesOf(result, bot)
        );

    public static IReadOnlyList<Mistake> MistakesOf(EditResult result, string bot) =>
        result switch
        {
            EditResult.Refused refused => refused.Mistakes,
            EditResult.Stale stale => [Stale(stale)],
            EditResult.Missing missing => [Missing(missing.Target, bot)],
            EditResult.UnknownBot => [UnknownBot(bot)],
            _ => throw new ArgumentException($"{result.GetType().Name} is not a refusal."),
        };

    public static Mistake Twice(ResourceRef target, IReadOnlyList<int> positions)
    {
        var noun = ResourceKinds.Noun(target.Kind);
        return new(
            ChangedTwice,
            ValidationStage.Save,
            target,
            "changes",
            $"Changes {string.Join(" and ", positions)} both change the {noun} '{target.Id}', and what they do together would depend on their order.",
            $"Keep one change to the {noun} '{target.Id}', with everything it should say.",
            []
        );
    }

    public static Mistake Missing(ResourceRef target, string bot) =>
        new(
            ResourceMissing,
            ValidationStage.Save,
            target,
            "name",
            $"Bot '{bot}' has no {ResourceKinds.Noun(target.Kind)} '{target.Id}'.",
            $"Check the name, or list the bot's {ResourceKinds.Noun(target.Kind)}s to see the ones that exist.",
            []
        );

    public static Mistake UnknownBot(string bot) =>
        new(
            BotNotFound,
            ValidationStage.Save,
            new ResourceRef(ResourceKind.Definition, bot),
            "bot",
            $"There is no bot '{bot}'.",
            "Check the bot's id, or register the bot first.",
            []
        );

    private static Mistake Stale(EditResult.Stale stale)
    {
        var noun = ResourceKinds.Noun(stale.Target.Kind);
        if (stale.Expected == 0)
        {
            return Make(
                ResourceExists,
                stale,
                $"The {noun} '{stale.Target.Id}' already exists (revision {Number(stale.Current)}).",
                $"Choose another name, or edit the existing {noun}."
            );
        }
        return Make(
            ResourceStale,
            stale,
            stale.Current is { } current
                ? $"The {noun} '{stale.Target.Id}' changed since you read it: you worked on revision {Number(stale.Expected)}, it is now revision {Number(current)}."
                : $"The {noun} '{stale.Target.Id}' was deleted since you read it (you worked on revision {Number(stale.Expected)}).",
            stale.Current is { } now
                ? $"Read it again (revision {Number(now)}), apply your change to what is there now, and save once more."
                : $"Read the bot's {noun}s again; add the {noun} again if you still want it."
        );
    }

    private static Mistake Make(string code, EditResult.Stale stale, string message, string fix) =>
        new(code, ValidationStage.Save, stale.Target, "revision", message, fix, []);

    private static string Number(int? value) => (value ?? 0).ToString(CultureInfo.InvariantCulture);

    private static ProblemHttpResult Problem(int status, string title, IReadOnlyList<Mistake> mistakes) =>
        TypedResults.Problem(
            title: title,
            detail: $"{mistakes.Count} mistake(s) in `mistakes`: fix each as its `fix` says, then try again.",
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["mistakes"] = mistakes }
        );
}
