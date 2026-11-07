using System.Globalization;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Procedures;

namespace Suwagcms.Sdk.Validation.Rules;

internal static class KnowledgeRules
{
    public static IReadOnlyList<Rule> All { get; } =
    [
        ProcedureMistake.For(MistakeCodes.KnowledgeBaseMissing, ValidationStage.Save, (_, procedure) => NoBase(procedure)),
        ProcedureMistake.For(MistakeCodes.KnowledgeBaseRepeated, ValidationStage.Save, (_, procedure) => RepeatedBases(procedure)),
        ProcedureMistake.For(MistakeCodes.KnowledgeArticleOtherBase, ValidationStage.Save, ArticlesOfOtherBases),
        ProcedureMistake.For(MistakeCodes.KnowledgeEntryEmpty, ValidationStage.Version, (_, procedure) => EmptyEntries(procedure)),
        ProcedureMistake.For(MistakeCodes.KnowledgeQueryBlank, ValidationStage.Version, (_, procedure) => BlankQueries(procedure)),
        ProcedureMistake.For(MistakeCodes.KnowledgeQueryNoArticles, ValidationStage.Version, QueriesWithNothingToSearch),
    ];

    private sealed record EntryAt(string Path, string Where, KnowledgeEntry Entry);

    private static string Index(string path, int index) => string.Create(CultureInfo.InvariantCulture, $"{path}[{index}]");

    private static IEnumerable<(string Path, string Where, IReadOnlyList<KnowledgeEntry> Entries)> Instructions(Procedure procedure)
    {
        if (procedure.WhenStalled?.Knowledge is { } stalled)
        {
            yield return ("whenStalled.knowledge", $"'knowledge' of whenStalled in '{procedure.Id}'", stalled);
        }
        foreach (var (name, step) in procedure.Steps)
        {
            if (step.Inform?.Knowledge is { } knowledge)
            {
                yield return ($"steps.{name}.inform.knowledge", $"'knowledge' of step '{name}' in '{procedure.Id}'", knowledge);
            }
        }
    }

    // Why: every instruction has one shape, so the rules read each entry the same way.
    private static IEnumerable<EntryAt> Entries(Procedure procedure) =>
        Instructions(procedure)
            .SelectMany(found => found.Entries.Select((entry, index) => new EntryAt(Index(found.Path, index), found.Where, entry)));

    private static IEnumerable<Mistake> NoBase(Procedure procedure) =>
        Entries(procedure)
            .Where(found => !ProcedureMistake.Given(found.Entry.KnowledgeBase))
            .Select(found =>
                Make(
                    procedure,
                    MistakeCodes.KnowledgeBaseMissing,
                    ValidationStage.Save,
                    $"{found.Path}.knowledgeBase",
                    $"An entry of {found.Where} names no knowledge base.",
                    "Name the base in knowledgeBase: 'suwagcms knowledge-bases list' shows them."
                )
            );

    private static IEnumerable<Mistake> RepeatedBases(Procedure procedure) =>
        Instructions(procedure)
            .SelectMany(found =>
                found
                    .Entries.Select((entry, index) => (entry.KnowledgeBase, index))
                    .Where(item => ProcedureMistake.Given(item.KnowledgeBase))
                    .GroupBy(item => item.KnowledgeBase, StringComparer.Ordinal)
                    .SelectMany(group =>
                        group
                            .Skip(1)
                            .Select(item =>
                                Make(
                                    procedure,
                                    MistakeCodes.KnowledgeBaseRepeated,
                                    ValidationStage.Save,
                                    Index(found.Path, item.index),
                                    $"{found.Where} has two entries for the knowledge base '{group.Key}'.",
                                    "Merge them into one entry: an instruction names each base once."
                                )
                            )
                    )
            );

    private static IEnumerable<Mistake> ArticlesOfOtherBases(BotDefinition definition, Procedure procedure)
    {
        var baseOf = definition
            .Articles.GroupBy(article => article.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().KnowledgeBase, StringComparer.Ordinal);
        return Entries(procedure)
            .SelectMany(found =>
                (found.Entry.Articles ?? [])
                    .Select((id, index) => (id, index))
                    .Where(item =>
                        ProcedureMistake.Given(item.id)
                        && baseOf.TryGetValue(item.id, out var actual)
                        && ProcedureMistake.Given(found.Entry.KnowledgeBase)
                        && actual != found.Entry.KnowledgeBase
                    )
                    .Select(item =>
                        Make(
                            procedure,
                            MistakeCodes.KnowledgeArticleOtherBase,
                            ValidationStage.Save,
                            Index($"{found.Path}.articles", item.index),
                            $"{found.Where} names article '{item.id}' under the knowledge base '{found.Entry.KnowledgeBase}', and it is in '{baseOf[item.id]}'.",
                            $"Move it to an entry for '{baseOf[item.id]}', or pick an article of '{found.Entry.KnowledgeBase}'."
                        )
                    )
            );
    }

    private static IEnumerable<Mistake> EmptyEntries(Procedure procedure) =>
        Entries(procedure)
            .Where(found => (found.Entry.Articles?.Count ?? 0) == 0 && (found.Entry.SearchQueries?.Count ?? 0) == 0)
            .Select(found =>
                Make(
                    procedure,
                    MistakeCodes.KnowledgeEntryEmpty,
                    ValidationStage.Version,
                    found.Path,
                    $"An entry of {found.Where} has no articles and no search queries.",
                    "Add an article or a query, or remove the entry."
                )
            );

    private static IEnumerable<Mistake> BlankQueries(Procedure procedure) =>
        Entries(procedure)
            .SelectMany(found =>
                (found.Entry.SearchQueries ?? [])
                    .Select((query, index) => (query, index))
                    .Where(item => !ProcedureMistake.Given(item.query))
                    .Select(item =>
                        Make(
                            procedure,
                            MistakeCodes.KnowledgeQueryBlank,
                            ValidationStage.Version,
                            Index($"{found.Path}.searchQueries", item.index),
                            $"{found.Where} has a blank search query.",
                            "Write the query, or remove it."
                        )
                    )
            );

    private static IEnumerable<Mistake> QueriesWithNothingToSearch(BotDefinition definition, Procedure procedure)
    {
        var withArticles = definition.Articles.Select(article => article.KnowledgeBase).ToHashSet(StringComparer.Ordinal);
        return Entries(procedure)
            .Where(found =>
                (found.Entry.SearchQueries ?? []).Any(ProcedureMistake.Given)
                && ProcedureMistake.Given(found.Entry.KnowledgeBase)
                && !withArticles.Contains(found.Entry.KnowledgeBase)
            )
            .Select(found =>
                Make(
                    procedure,
                    MistakeCodes.KnowledgeQueryNoArticles,
                    ValidationStage.Version,
                    $"{found.Path}.searchQueries",
                    $"{found.Where} has a search query on the knowledge base '{found.Entry.KnowledgeBase}', which has no articles to search.",
                    $"Add an article to '{found.Entry.KnowledgeBase}', or remove the query."
                )
            );
    }

    // Why: a bot's code decides this, so it is the declaration's check at Version and never a rule at Save, as an undeclared block is.
    public static IEnumerable<Mistake> Undeclared(BotDefinition definition) =>
        definition
            .Procedures.OrderBy(procedure => procedure.Id, StringComparer.Ordinal)
            .SelectMany(procedure =>
                Instructions(procedure)
                    .Where(found => found.Entries.Count > 0)
                    .Select(found =>
                        Make(
                            procedure,
                            MistakeCodes.DeclarationKnowledge,
                            ValidationStage.Version,
                            found.Path,
                            $"{found.Where} holds knowledge, which this bot does not declare it reads.",
                            "Remove the knowledge, or add 'Knowledge = true' to the bot's setup and deploy."
                        )
                    )
            );

    // Why: a named article reaches the model whole, with no query to cut it by, so the bot says how much one instruction may hold; its content and guide are what it delivers, and an article named twice counts once.
    public static IEnumerable<Mistake> NamedArticlesTooLong(BotDefinition definition, int limit)
    {
        var lengths = definition
            .Articles.DistinctBy(article => article.Id, StringComparer.Ordinal)
            .ToDictionary(article => article.Id, article => article.Content.Length + article.LlmGuide.Length, StringComparer.Ordinal);
        return definition
            .Procedures.OrderBy(procedure => procedure.Id, StringComparer.Ordinal)
            .SelectMany(procedure =>
                Instructions(procedure)
                    .Select(found =>
                        (
                            found,
                            total: found
                                .Entries.SelectMany(entry => entry.Articles ?? [])
                                .Distinct(StringComparer.Ordinal)
                                .Sum(id => lengths.GetValueOrDefault(id))
                        )
                    )
                    .Where(item => item.total > limit)
                    .Select(item =>
                        Make(
                            procedure,
                            MistakeCodes.DeclarationNamedArticlesTooLong,
                            ValidationStage.Version,
                            item.found.Path,
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"The articles named in {item.found.Where} hold {item.total} characters, and this bot takes at most {limit} in one instruction."
                            ),
                            "Name fewer or shorter articles, and put a search query on their base for the rest: it finds the parts that answer."
                        )
                    )
            );
    }

    private static Mistake Make(Procedure procedure, string code, ValidationStage stage, string path, string message, string fix) =>
        ProcedureMistake.Make(procedure, code, stage, path, message, fix);
}
