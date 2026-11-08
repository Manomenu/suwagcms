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
        var only = new Option<string?>("--kind")
        {
            Description =
                "Only the changes of this kind of resource, every one of it: fact, kb, article, procedure, action, persona or version.",
        };
        only.AcceptOnlyFromAmong([.. Collections.Keys]);
        var limit = LimitOption();
        var before = BeforeOption();
        var page = PageOption();
        var command = new Command(
            "history",
            "Show what changed and when, newest first: every change to the bot, to one kind of resource, or to one resource, also after it is deleted (or --open the page)."
        )
        {
            kind,
            id,
            only,
            limit,
            before,
            page,
        };
        EditOptions.RefuseOpenWithJson(command, options);
        RefuseWrongPaging(command, limit, before, page);
        command.Validators.Add(result =>
        {
            if (result.GetValue(kind) is not (null or PersonaWord) && result.GetValue(id) is null)
            {
                result.AddError("Give the id too: `suwagcms history <kind> <id>`, or neither for the whole bot.");
            }
            if (result.GetValue(kind) is not null && result.GetValue(only) is not null)
            {
                result.AddError("--kind narrows the whole bot's history: leave out `<kind> <id>`, or leave out --kind for one resource.");
            }
        });
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                var word = parsed.GetValue(kind);
                var narrowed = parsed.GetValue(only);
                // Why: a bot has one persona, so its history is asked for by the kind alone.
                var resource = word == PersonaWord ? PersonaWord : parsed.GetValue(id);
                if (parsed.GetValue(options.Open))
                {
                    return await WebPage.ShowAsync(
                        narrowed is null ? PageOf(target, word, resource, parsed.GetValue(page)) : target.PageOf(Collections[narrowed]),
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
                    path
                        + Query(
                            parsed.GetValue(limit),
                            parsed.GetValue(before),
                            parsed.GetValue(page),
                            narrowed is null ? null : Collections[narrowed]
                        ),
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var shown = answer.Body?.Deserialize<HistoryPage>(Web) ?? throw new InvalidOperationException("the server sent no history");
                return await target.Run.Report.DoneAsync(shown, HistoryText.Page(shown, word is null, parsed.GetValue(page)));
            }
        );
        return command;
    }

    public static Option<int?> LimitOption() => new("--limit") { Description = "How many entries a page holds, 50 unless said otherwise." };

    public static Option<long?> BeforeOption() => new("--before") { Description = "Go on from this entry: the id the last page says." };

    public static Option<int?> PageOption() =>
        new("--page") { Description = "This numbered page, as the web counts them, with the total and each change written out." };

    public static void RefuseWrongPaging(Command command, Option<int?> limit, Option<long?> before, Option<int?> page) =>
        command.Validators.Add(result =>
        {
            if (result.GetValue(limit) is < 1)
            {
                result.AddError("--limit must be 1 or more.");
            }
            if (result.GetValue(page) is < 1)
            {
                result.AddError("--page must be 1 or more.");
            }
            if (result.GetValue(page) is not null && result.GetValue(before) is not null)
            {
                result.AddError("--page and --before cannot be used together: give the page's number, or the entry to go on from.");
            }
        });

    // Why: the changes of one kind are shown under that kind's own screen, so `--kind --open` opens it; a resource's page is its history, filtered on the bot's History page, because a deleted resource has no screen of its own to open.
    private static Uri PageOf(BotTarget target, string? word, string? resource, int? number)
    {
        var parts = new List<string>();
        if (word is not null)
        {
            parts.Add($"kind={Collections[word]}");
            parts.Add($"id={Uri.EscapeDataString(resource ?? "")}");
        }
        if (number is { } asked)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"page={asked}"));
        }
        var page = target.PageOf("history");
        return parts.Count == 0 ? page : new Uri(page, "?" + string.Join('&', parts));
    }

    internal static string Query(int? limit, long? before, int? page, string? collection = null)
    {
        var parts = new List<string>();
        if (collection is not null)
        {
            parts.Add($"kind={collection}");
        }
        if (limit is { } size)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"limit={size}"));
        }
        if (before is { } entry)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"before={entry}"));
        }
        if (page is { } number)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"page={number}"));
        }
        return parts.Count == 0 ? "" : "?" + string.Join('&', parts);
    }
}
