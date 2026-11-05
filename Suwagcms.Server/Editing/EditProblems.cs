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
    public const string ResourceSameId = "resource.sameId";
    public const string ChangedTwice = "change.twice";
    public const string CodeReadOnly = "action.code.readOnly";
    public const string CodeDeclared = "action.code.declared";

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

    public static Mistake ReadOnly(ResourceRef target) =>
        new(
            CodeReadOnly,
            ValidationStage.Save,
            target,
            "implementation",
            $"'{target.Id}' is written in the bot's code, so the CMS only shows it.",
            "Change it in the bot and deploy; the CMS sees the change the next time it asks.",
            []
        );

    public static Mistake CodeWritten(ResourceRef target) =>
        new(
            CodeReadOnly,
            ValidationStage.Save,
            target,
            "implementation",
            $"'{target.Id}' says 'implementation: code', and a code action comes only from the bot's declaration.",
            "Write it in the bot, state it in the SDK's setup and deploy; the CMS sees it the next time it asks. Or give it an HTTP implementation.",
            []
        );

    public static Mistake DeclaredId(ResourceRef target, ResourceRef declared) =>
        new(
            CodeDeclared,
            ValidationStage.Save,
            target,
            "id",
            $"The bot declares a code action '{declared.Id}', so an HTTP action cannot take that id.",
            "Choose another id, or use the code action in your procedures.",
            [new RelatedResource(declared, "id")]
        );

    public static Mistake SameId(ResourceRef target) =>
        new(
            ResourceSameId,
            ValidationStage.Save,
            target,
            IdField(target),
            $"The {ResourceKinds.Noun(target.Kind)} '{target.Id}' already has this id, so nothing would change.",
            "Give another id.",
            []
        );

    public static Mistake Taken(ResourceRef target, ResourceRef wanted) =>
        new(
            MistakeCodes.DuplicateId,
            ValidationStage.Save,
            target,
            IdField(target),
            $"This bot already has {Noun(wanted)} '{wanted.Id}'.",
            $"Choose another id, or delete that {ResourceKinds.Noun(wanted.Kind)} first.",
            [new RelatedResource(wanted, IdField(wanted))]
        );

    private static string IdField(ResourceRef target) => target.Kind == ResourceKind.GlobalFact ? "name" : "id";

    private static string Noun(ResourceRef target) =>
        target.Kind == ResourceKind.Article ? "an article" : $"a {ResourceKinds.Noun(target.Kind)}";

    private static Mistake Stale(EditResult.Stale stale)
    {
        var noun = ResourceKinds.Noun(stale.Target.Kind);
        // Why: a persona is there before anyone saves it, so revision 0 means "I read it empty", and a save since then makes it out of date, not taken.
        if (stale.Expected == 0 && stale.Target.Kind != ResourceKind.Persona)
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
