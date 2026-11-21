using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Sdk.Validation.Rules;

internal static class FactNameRules
{
    public static IReadOnlyList<Rule> All { get; } =
    [
        new(MistakeCode.FactLocalNameClash, ValidationStage.Save, LocalNameClashes),
        new(MistakeCode.FactNameProcedurePrefix, ValidationStage.Save, ProcedurePrefixes),
    ];

    private static IEnumerable<Mistake> ProcedurePrefixes(BotDefinition definition) =>
        definition.Facts.SelectMany(fact =>
            definition
                .Procedures.Where(procedure => fact.Name.StartsWith($"{procedure.Id}.", StringComparison.Ordinal))
                .Select(procedure => new Mistake(
                    MistakeCode.FactNameProcedurePrefix,
                    ValidationStage.Save,
                    new ResourceRef(ResourceKind.GlobalFact, fact.Name),
                    "name",
                    $"'{fact.Name}' starts with procedure id '{procedure.Id}', which is how a local fact of that procedure is named outside it.",
                    "Choose another name.",
                    [new RelatedResource(new ResourceRef(ResourceKind.Procedure, procedure.Id), "id")]
                ))
        );

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
            MistakeCode.FactLocalNameClash,
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
