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

    private static readonly Dictionary<string, (string Collection, string Page)> Kinds = new(StringComparer.Ordinal)
    {
        ["fact"] = ("facts", "GlobalFact"),
        ["article"] = ("articles", "Article"),
        ["procedure"] = ("procedures", "Procedure"),
        ["action"] = ("actions", "Action"),
    };

    public static Command Create(GlobalOptions options, CliEnvironment environment)
    {
        var kind = new Argument<string>("kind") { Description = "fact, article, procedure or action." };
        kind.AcceptOnlyFromAmong([.. Kinds.Keys]);
        var id = new Argument<string>("id") { Description = "The resource's id or, for a fact, its name." };
        var command = new Command(
            "usage",
            "Show what uses a resource and what it uses, with the field of each reference (or --open its page, which lists them). Changes nothing."
        )
        {
            kind,
            id,
        };
        EditOptions.RefuseOpenWithJson(command, options);
        command.SetBotAction(options, environment, async (target, parsed, cancellationToken) =>
        {
            var kindWord = parsed.GetRequiredValue(kind);
            var resourceId = parsed.GetRequiredValue(id);
            var (collection, page) = Kinds[kindWord];
            if (parsed.GetValue(options.Open))
            {
                return await target.OpenAsync(target.PageAt(page, resourceId), parsed);
            }
            var answer = await ServerCall.GetAsync(
                environment,
                target.Address,
                target.ItemPath(collection, resourceId) + "/usage",
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
        });
        return command;
    }
}
