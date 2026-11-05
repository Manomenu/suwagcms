using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Validation;

// Why: a name must mean one thing inside a procedure, and these rules look at the result of a save, so the order of two saves never decides whether it does.
internal static class FactNameRules
{
    public static IReadOnlyList<Rule> All { get; } = [new(MistakeCodes.FactLocalNameClash, ValidationStage.Save, LocalNameClashes)];

    private static IEnumerable<Mistake> LocalNameClashes(BotDefinition definition) =>
        definition.Facts.SelectMany(fact =>
        {
            var holders = definition.Procedures.Where(procedure => procedure.Locals.ContainsKey(fact.Name)).ToList();
            return holders.Count == 0 ? [] : new[] { LocalNameClash(fact, holders) };
        });

    private static Mistake LocalNameClash(GlobalFact fact, List<Procedure> holders)
    {
        var names = string.Join(", ", holders.Select(holder => $"'{holder.Id}'").Order(StringComparer.Ordinal));
        return new Mistake(
            MistakeCodes.FactLocalNameClash,
            ValidationStage.Save,
            new ResourceRef(ResourceKind.GlobalFact, fact.Name),
            "name",
            $"Local fact '{fact.Name}' of procedure {names} has the name of a global fact. Inside the procedure the name would be ambiguous.",
            "Rename one of them.",
            [
                .. holders.Select(holder => new RelatedResource(
                    new ResourceRef(ResourceKind.Procedure, holder.Id),
                    $"localFacts.{fact.Name}"
                )),
            ]
        );
    }
}
