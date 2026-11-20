using Suwagcms.Contracts.Procedures;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Procedures;

internal static class ProcedureMapping
{
    public static Procedure ToRecord(string id, SaveProcedure body) =>
        new()
        {
            Id = id,
            Description = body.Description ?? "",
            FirstStep = body.FirstStep,
            Steps = body.Steps ?? new Dictionary<string, ProcedureStep>(),
            WhenStalled = body.WhenStalled,
            LocalFacts = body.LocalFacts,
            WhenInterrupted = body.WhenInterrupted,
        };

    public static ProcedureDetail Show(Procedure procedure, int revision, IReadOnlyList<Mistake> notReady) =>
        new(
            procedure.Id,
            procedure.Description,
            procedure.FirstStep,
            procedure.Steps,
            procedure.WhenStalled,
            revision,
            Of(notReady, procedure.Id),
            procedure.LocalFacts,
            procedure.WhenInterrupted
        );

    public static ProcedureDetail Show(StoredResource stored, IReadOnlyList<Mistake> notReady) =>
        Show((Procedure)stored.Record, stored.Revision, notReady);

    public static List<Mistake> Of(IEnumerable<Mistake> notReady, string procedure) =>
        [
            .. notReady.Where(mistake =>
                mistake.Stage == ValidationStage.Version
                && (
                    mistake.Resource == new ResourceRef(ResourceKind.Procedure, procedure)
                    || mistake.Resource.Kind == ResourceKind.Definition
                )
            ),
        ];

    public static ProcedureList List(IReadOnlyList<StoredResource> stored)
    {
        var found = NotReadyOf(stored);
        return new ProcedureList(
            [
                .. stored
                    .Where(item => item.Target.Kind == ResourceKind.Procedure)
                    .Select(item =>
                    {
                        var procedure = (Procedure)item.Record;
                        return new ProcedureSummary(
                            procedure.Id,
                            procedure.Description,
                            item.Revision,
                            found.Count(mistake => mistake.Resource == item.Target)
                        );
                    }),
            ],
            [.. found.Where(mistake => mistake.Resource.Kind == ResourceKind.Definition)]
        );
    }

    public static IReadOnlyList<Mistake> NotReadyOf(IEnumerable<StoredResource> stored) =>
        [
            .. DefinitionValidator
                .Validate(ResourceKinds.Assemble(stored), ValidationStage.Version)
                .Where(mistake => mistake.Stage == ValidationStage.Version),
        ];
}
