using System.CommandLine;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Cli.Output;
using Suwagcms.Contracts.Renames;

namespace Suwagcms.Cli.Renames;

internal static class RenameCommand
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // Why: the words a person types for a resource, mapped to the collection the API serves it from and the kind a page is found by.
    private static readonly Dictionary<string, (string Collection, string Kind)> Kinds = new(StringComparer.Ordinal)
    {
        ["fact"] = ("facts", "GlobalFact"),
        ["kb"] = ("knowledge-bases", "KnowledgeBase"),
        ["article"] = ("articles", "Article"),
        ["procedure"] = ("procedures", "Procedure"),
        ["action"] = ("actions", "Action"),
    };

    public static Command Create(GlobalOptions options, CliEnvironment environment)
    {
        var kind = new Argument<string>("kind") { Description = "fact, kb, article, procedure or action." };
        kind.AcceptOnlyFromAmong([.. Kinds.Keys]);
        var id = new Argument<string>("id") { Description = "The resource's id or, for a fact, its name." };
        var newId = new Argument<string>("newId") { Description = "The id it gets, in the form that kind of resource requires." };
        var ifRevision = EditOptions.IfRevision();
        var dryRun = EditOptions.DryRun();
        var command = new Command(
            "rename",
            "Change a resource's id and rewrite every reference to it, all or nothing; --dry-run lists what would be rewritten, --open opens the resource at its new address."
        )
        {
            kind,
            id,
            newId,
            ifRevision,
            dryRun,
        };
        EditOptions.RefuseOpenWithJson(command, options);
        command.Validators.Add(result =>
        {
            if (result.GetValue(options.Open) && result.GetValue(dryRun))
            {
                result.AddError(
                    "--dry-run and --open cannot be used together: a dry run changes nothing, so there is no new address to open."
                );
            }
        });
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                var (collection, pageKind) = Kinds[parsed.GetRequiredValue(kind)];
                var dry = parsed.GetValue(dryRun);
                var answer = await ServerCall.PostAsync(
                    environment,
                    target.Address,
                    $"{target.ItemPath(collection, parsed.GetRequiredValue(id))}/rename{EditOptions.Query(dry, parsed.GetValue(ifRevision))}",
                    new RenameResource(parsed.GetRequiredValue(newId)),
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var renamed =
                    answer.Body?.Deserialize<RenameAnswer>(Web) ?? throw new InvalidOperationException("the server sent no answer");
                var reported = await target.Run.Report.DoneAsync(
                    new RenameDocument(target.Bot, renamed.Kind, renamed.OldId, renamed.Id, renamed.Revision, renamed.Referrers, dry),
                    RenameText.Of(renamed, dry)
                );
                return parsed.GetValue(options.Open)
                    ? await WebPage.ShowAsync(
                        target.PageAt(pageKind, renamed.Id),
                        parsed.InvocationConfiguration.Output,
                        parsed.InvocationConfiguration.Error,
                        environment.OpenBrowser
                    )
                    : reported;
            }
        );
        return command;
    }
}
