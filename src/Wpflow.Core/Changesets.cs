namespace Wpflow.Core;

/// <summary>
///     Différence entre deux états en liste de changes (équivalent de <c>getNodesChangeset</c> / <c>getEdgesChangeset</c>) :
///     <c>Apply(Diff(a, b), a)</c> redonne <c>b</c> (au tri près : les ajouts vont à la fin).
/// </summary>
public static class Changesets
{
    public static IReadOnlyList<NodeChange> Diff(IReadOnlyList<Node> before, IReadOnlyList<Node> after)
    {
        var old = before.ToDictionary(n => n.Id);
        var now = after.Select(n => n.Id).ToHashSet();
        var changes = new List<NodeChange>();
        changes.AddRange(before.Where(n => !now.Contains(n.Id)).Select(n => (NodeChange)new NodeRemoveChange(n.Id)));
        foreach (var n in after)
        {
            if (!old.TryGetValue(n.Id, out var was)) changes.Add(new NodeAddChange(n));
            else if (!ReferenceEquals(was, n) && was != n) changes.Add(new NodeReplaceChange(n.Id, n));
        }

        return changes;
    }

    public static IReadOnlyList<EdgeChange> Diff(IReadOnlyList<Edge> before, IReadOnlyList<Edge> after)
    {
        var old = before.ToDictionary(e => e.Id);
        var now = after.Select(e => e.Id).ToHashSet();
        var changes = new List<EdgeChange>();
        changes.AddRange(before.Where(e => !now.Contains(e.Id)).Select(e => (EdgeChange)new EdgeRemoveChange(e.Id)));
        foreach (var e in after)
        {
            if (!old.TryGetValue(e.Id, out var was)) changes.Add(new EdgeAddChange(e));
            else if (!ReferenceEquals(was, e) && was != e) changes.Add(new EdgeReplaceChange(e.Id, e));
        }

        return changes;
    }
}
