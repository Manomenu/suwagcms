using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Cli.Output;
using Suwagcms.Contracts.History;

namespace Suwagcms.Cli.History;

internal static class HistoryCommand
{
    private const string PersonaWord = "persona";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // Why: the words a person types for a resource, mapped to the collection the API and the web serve it from.
    private static readonly Dictionary<string, string> Collections = new(StringComparer.Ordinal)
    {
        ["fact"] = "facts",
        ["kb"] = "knowledge-bases",
        ["article"] = "articles",
        ["procedure"] = "procedures",
        ["action"] = "actions",
        [PersonaWord] = PersonaWord,
        ["version"] = "versions",
    };

    public static Command Create(GlobalOptions options, CliEnvironment environment)
    {
        var kind = new Argument<string?>("kind")
        {
            Description = "fact, kb, article, procedure, action, persona or version. Left out with the id: every change to the bot.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        kind.AcceptOnlyFromAmong([.. Collections.Keys]);
        var id = new Argument<string?>("id")
        {
            Description = "The resource's id, a fact's name or a version's number; a persona, being one, has none.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var limit = new Option<int?>("--limit") { Description = "How many entries a page holds, 50 unless said otherwise." };
        var before = new Option<long?>("--before") { Description = "Go on from this entry: the id the last page says." };
        var command = new Command(
            "history",
            "Show what changed and when, newest first: every change to the bot, or to one resource, also after it is deleted (or --open the page)."
        )
        {
            kind,
            id,
            limit,
            before,
        };
        EditOptions.RefuseOpenWithJson(command, options);
        command.Validators.Add(result =>
        {
            if (result.GetValue(kind) is not (null or PersonaWord) && result.GetValue(id) is null)
            {
                result.AddError("Give the id too: `suwagcms history <kind> <id>`, or neither for the whole bot.");
            }
            if (result.GetValue(limit) is < 1)
            {
                result.AddError("--limit must be 1 or more.");
            }
        });
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                var word = parsed.GetValue(kind);
                // Why: a bot has one persona, so its history is asked for by the kind alone.
                var resource = word == PersonaWord ? PersonaWord : parsed.GetValue(id);
                if (parsed.GetValue(options.Open))
                {
                    return await WebPage.ShowAsync(
                        PageOf(target, word, resource),
                        parsed.InvocationConfiguration.Output,
                        parsed.InvocationConfiguration.Error,
                        target.Machine.OpenBrowser
                    );
                }
                var path = word is null
                    ? target.CollectionPath("history")
                    : target.ItemPath(Collections[word], resource ?? "") + "/history";
                var answer = await ServerCall.GetAsync(
                    environment,
                    target.Address,
                    path + Query(parsed.GetValue(limit), parsed.GetValue(before)),
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var page = answer.Body?.Deserialize<HistoryPage>(Web) ?? throw new InvalidOperationException("the server sent no history");
                return await target.Run.Report.DoneAsync(page, HistoryText.Page(page, word is null));
            }
        );
        return command;
    }

    // Why: a resource's page is its history, filtered on the bot's History page, because a deleted resource has no screen of its own to open.
    private static Uri PageOf(BotTarget target, string? word, string? resource)
    {
        var page = target.PageOf("history");
        return word is null ? page : new Uri(page, $"?kind={Collections[word]}&id={Uri.EscapeDataString(resource ?? "")}");
    }

    internal static string Query(int? limit, long? before)
    {
        var parts = new List<string>();
        if (limit is { } size)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"limit={size}"));
        }
        if (before is { } entry)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"before={entry}"));
        }
        return parts.Count == 0 ? "" : "?" + string.Join('&', parts);
    }
}
