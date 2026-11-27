namespace Wpflow.Core;

/// <summary>
///     Différence entre deux états en liste de changes (équivalent de <c>getNodesChangeset</c> / <c>getEdgesChangeset</c>) :
///     <c>Apply(Diff(a, b), a)</c> redonne <c>b</c> (au tri près : les ajouts vont à la fin).
/// </summary>
public static class Changesets
{
    public static IReadOnlyList<NodeChange> Diff(IReadOnlyList<Node> before, IReadOnlyList<Node> after) =>
        Walk<Node, NodeChange>(before, after, before.ToDictionary(n => n.Id), n => n.Id, id => new NodeRemoveChange(id),
            n => new NodeAddChange(n), (_, n, changes) => changes.Add(new NodeReplaceChange(n.Id, n)));

    public static IReadOnlyList<EdgeChange> Diff(IReadOnlyList<Edge> before, IReadOnlyList<Edge> after) =>
        Walk<Edge, EdgeChange>(before, after, before.ToDictionary(e => e.Id), e => e.Id, id => new EdgeRemoveChange(id),
            e => new EdgeAddChange(e), (_, e, changes) => changes.Add(new EdgeReplaceChange(e.Id, e)));

    /// <summary>
    ///     Le parcours commun des différences : les retraits, puis dans l'ordre de <paramref name="after" /> les ajouts et les
    ///     éléments modifiés. <paramref name="changed" /> dit en quels changes se traduit une modification.
    /// </summary>
    /// <param name="old">Les éléments de <paramref name="before" /> par id ; l'appelant décide du sort d'un id en double.</param>
    internal static List<TChange> Walk<T, TChange>(IReadOnlyList<T> before, IReadOnlyList<T> after, Dictionary<string, T> old,
        Func<T, string> id, Func<string, TChange> removed, Func<T, TChange> added, Action<T, T, List<TChange>> changed)
        where T : class
    {
        var now = new HashSet<string>(after.Select(id));
        var changes = new List<TChange>();
        changes.AddRange(before.Where(item => !now.Contains(id(item))).Select(item => removed(id(item))));
        foreach (var item in after)
        {
            if (!old.TryGetValue(id(item), out var was)) changes.Add(added(item));
            else if (!ReferenceEquals(was, item) && !was.Equals(item)) changed(was, item, changes);
        }

        return changes;
    }
}
