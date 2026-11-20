using System.Text.Json.Nodes;
using Suwagcms.Sdk.Format.Actions;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Server.Editing;

internal static class CodeActionAlignment
{
    public static Dictionary<ResourceRef, StoredResource> Apply(Dictionary<ResourceRef, StoredResource> before, BotDeclaration declaration)
    {
        var after = new Dictionary<ResourceRef, StoredResource>(before);
        foreach (var code in declaration.CodeActions)
        {
            var target = new ResourceRef(ResourceKind.Action, code.Id);
            var wanted = code.ToAction();
            if (!before.TryGetValue(target, out var stored))
            {
                after[target] = new StoredResource(Guid.NewGuid(), target, 1, wanted);
            }
            else if (stored.Record is BotAction { IsCode: true } && !Same(stored.Record, wanted))
            {
                after[target] = stored with { Revision = stored.Revision + 1, Record = wanted };
            }
        }
        var used = ReferenceWalker.From(ResourceKinds.Assemble(before.Values)).Select(reference => reference.To).ToHashSet();
        var declared = declaration.CodeActions.Select(code => code.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var (target, stored) in before)
        {
            if (stored.Record is BotAction { IsCode: true } && !declared.Contains(target.Id) && !used.Contains(target))
            {
                after.Remove(target);
            }
        }
        return after;
    }

    private static bool Same(object stored, object wanted) =>
        JsonNode.DeepEquals(JsonNode.Parse(ResourceKinds.Write(stored)), JsonNode.Parse(ResourceKinds.Write(wanted)));
}
