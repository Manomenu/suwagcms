using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Validation;

internal static class LoopRules
{
    private static readonly IComparer<ResourceRef> ByKindThenId = Comparer<ResourceRef>.Create(
        (left, right) => left.Kind != right.Kind ? left.Kind.CompareTo(right.Kind) : string.CompareOrdinal(left.Id, right.Id)
    );

    // Why: only references between articles and procedures can loop; a fact is a dead end, and the steps of one procedure are not resources, so their loops never appear here.
    public static IEnumerable<Mistake> Loops(BotDefinition definition)
    {
        var nodes = definition
            .Articles.Select(article => new ResourceRef(ResourceKind.Article, article.Id))
            .Concat(definition.Procedures.Select(procedure => new ResourceRef(ResourceKind.Procedure, procedure.Id)))
            .ToHashSet();
        var edges = new Dictionary<ResourceRef, List<ResourceRef>>();
        var paths = new Dictionary<(ResourceRef, ResourceRef), string>();
        foreach (var reference in ReferenceWalker.From(definition))
        {
            if (
                nodes.Contains(reference.From)
                && nodes.Contains(reference.To)
                && paths.TryAdd((reference.From, reference.To), reference.Path)
            )
            {
                if (!edges.TryGetValue(reference.From, out var targets))
                {
                    edges[reference.From] = targets = [];
                }
                targets.Add(reference.To);
            }
        }

        return Cycles(edges, ByKindThenId).Select(cycle => Loop(cycle, paths));
    }

    private static Mistake Loop(IReadOnlyList<ResourceRef> cycle, Dictionary<(ResourceRef, ResourceRef), string> paths)
    {
        string PathOut(int index)
        {
            return paths[(cycle[index], cycle[(index + 1) % cycle.Count])];
        }
        var shown = string.Join(" → ", cycle.Append(cycle[0]).Select(resource => resource.Id));
        var first = cycle[0];
        var second = cycle[1 % cycle.Count];
        var fix =
            cycle.Count == 1
                ? $"Remove the link from '{first.Id}' to itself."
                : $"Remove the link from '{first.Id}' to '{second.Id}', or another link on the path.";
        var related = cycle.Select((resource, index) => new RelatedResource(resource, PathOut(index))).Skip(1).ToList();
        return new Mistake(
            MistakeCodes.ReferenceLoop,
            ValidationStage.Save,
            first,
            PathOut(0),
            $"These links close a loop: {shown}.",
            fix,
            related
        );
    }

    // Why: one cycle per edge that closes a loop in a depth-first walk in a fixed order, iterative so a long chain cannot overflow the stack; linear in the edges and the same input always gives the same cycles, each starting at its smallest node.
    internal static IReadOnlyList<IReadOnlyList<TNode>> Cycles<TNode>(IReadOnlyDictionary<TNode, List<TNode>> edges, IComparer<TNode> order)
        where TNode : notnull
    {
        var found = new List<IReadOnlyList<TNode>>();
        var finished = new HashSet<TNode>();
        var onPath = new Dictionary<TNode, int>();
        var path = new List<TNode>();
        var cursors = new List<int>();
        var starts = edges.Keys.Order(order).ToList();
        var next = edges.ToDictionary(entry => entry.Key, entry => entry.Value.Order(order).ToList());
        foreach (var start in starts)
        {
            if (finished.Contains(start))
            {
                continue;
            }
            Enter(start);
            while (path.Count > 0)
            {
                var node = path[^1];
                var targets = next.GetValueOrDefault(node);
                if (targets is null || cursors[^1] >= targets.Count)
                {
                    finished.Add(node);
                    onPath.Remove(node);
                    path.RemoveAt(path.Count - 1);
                    cursors.RemoveAt(cursors.Count - 1);
                    continue;
                }
                var target = targets[cursors[^1]++];
                if (onPath.TryGetValue(target, out var position))
                {
                    found.Add(Rotated(path.GetRange(position, path.Count - position)));
                }
                else if (!finished.Contains(target))
                {
                    Enter(target);
                }
            }
        }
        return found;

        void Enter(TNode node)
        {
            onPath[node] = path.Count;
            path.Add(node);
            cursors.Add(0);
        }

        List<TNode> Rotated(List<TNode> cycle)
        {
            var smallest = 0;
            for (var index = 1; index < cycle.Count; index++)
            {
                if (order.Compare(cycle[index], cycle[smallest]) < 0)
                {
                    smallest = index;
                }
            }
            return [.. cycle.Skip(smallest), .. cycle.Take(smallest)];
        }
    }
}
