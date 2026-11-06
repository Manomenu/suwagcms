using System.CommandLine;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Contracts.Usage;

namespace Suwagcms.Cli.Usage;

internal static class UsageCommand
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // Why: the words a person types for a resource, mapped to the collection the API serves it from.
    private static readonly Dictionary<string, string> Collections = new(StringComparer.Ordinal)
    {
        ["fact"] = "facts",
        ["article"] = "articles",
        ["procedure"] = "procedures",
        ["action"] = "actions",
    };

    public static Command Create(GlobalOptions options, CliEnvironment environment)
    {
        var kind = new Argument<string>("kind") { Description = "fact, article, procedure or action." };
        kind.AcceptOnlyFromAmong([.. Collections.Keys]);
        var id = new Argument<string>("id") { Description = "The resource's id or, for a fact, its name." };
        var command = new Command("usage", "Show what uses a resource and what it uses, with the field of each reference. Changes nothing.")
        {
            kind,
            id,
        };
        EditOptions.RefuseOpen(command, options, "usage");
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                var kindWord = parsed.GetRequiredValue(kind);
                var resourceId = parsed.GetRequiredValue(id);
                var answer = await ServerCall.GetAsync(
                    environment,
                    target.Address,
                    target.ItemPath(Collections[kindWord], resourceId) + "/usage",
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var usage = answer.Body?.Deserialize<UsageView>(Web) ?? throw new InvalidOperationException("the server sent no usage");
                return await target.Run.Report.DoneAsync(
                    new UsageDocument(target.Bot, kindWord, resourceId, usage.UsedBy, usage.Uses),
                    UsageText.Lists(usage)
                );
            }
        );
        return command;
    }
}
