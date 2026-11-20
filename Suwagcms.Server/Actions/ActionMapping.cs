using Suwagcms.Contracts.Actions;
using Suwagcms.Sdk.Format.Actions;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Actions;

internal static class ActionMapping
{
    public static BotAction ToRecord(string id, SaveAction body) =>
        new()
        {
            Id = id,
            Description = body.Description ?? "",
            Parameters = body.Parameters,
            Results = body.Results,
            Implementation = body.Implementation,
        };

    public static ActionView Show(StoredResource stored, IReadOnlyList<StoredResource> all, BotDeclaration? declaration) =>
        Show((BotAction)stored.Record, stored.Revision, all, declaration);

    public static ActionView Show(BotAction action, int revision, IEnumerable<StoredResource> all, BotDeclaration? declaration)
    {
        var declared = declaration?.CodeActions.Any(code => code.Id == action.Id) ?? false;
        return new(
            action.Id,
            action.Description,
            action.Parameters,
            action.Results,
            action.Implementation,
            revision,
            UsedBy(action.Id, all),
            !action.IsCode && declared,
            action.IsCode && declaration is not null && !declared
        );
    }

    private static List<string> UsedBy(string id, IEnumerable<StoredResource> all) =>
        [
            .. ReferenceWalker
                .From(ResourceKinds.Assemble(all))
                .Where(reference => reference.To == new ResourceRef(ResourceKind.Action, id))
                .Select(reference => reference.From.Id)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
}
