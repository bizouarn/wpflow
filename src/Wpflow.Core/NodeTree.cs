namespace Wpflow.Core;

/// <summary>
///     Hiérarchie de nodes (<c>parentId</c>) : la <see cref="Node.Position" /> d'un enfant est relative à son parent.
///     Ces utilitaires calculent les positions absolues (coordonnées flow).
/// </summary>
public static class NodeTree
{
    /// <summary>Position absolue de chaque node. Un parent introuvable ou un cycle font traiter le node comme une racine.</summary>
    public static Dictionary<string, XYPosition> AbsolutePositions(IEnumerable<Node> nodes)
    {
        var byId = new Dictionary<string, Node>();
        foreach (var n in nodes) byId[n.Id] = n;

        var result = new Dictionary<string, XYPosition>(byId.Count);
        var visiting = new HashSet<string>(); // vidé pour chaque node : un seul ensemble au lieu d'un par node
        foreach (var n in byId.Values)
        {
            visiting.Clear();
            Resolve(n, byId, result, visiting);
        }

        return result;
    }

    private static XYPosition Resolve(Node node, Dictionary<string, Node> byId,
        Dictionary<string, XYPosition> done, HashSet<string> visiting)
    {
        if (done.TryGetValue(node.Id, out var known)) return known;

        var pos = node.Position;
        if (node.ParentId is { } pid && byId.TryGetValue(pid, out var parent) && visiting.Add(node.Id))
            pos += Resolve(parent, byId, done, visiting);

        done[node.Id] = pos;
        return pos;
    }

    /// <summary>Profondeur dans la hiérarchie (0 pour une racine) ; sert à ordonner l'empilement (parents sous leurs enfants).</summary>
    public static Dictionary<string, int> Depths(IEnumerable<Node> nodes)
    {
        var byId = new Dictionary<string, Node>();
        foreach (var n in nodes) byId[n.Id] = n;

        var result = new Dictionary<string, int>(byId.Count);
        var seen = new HashSet<string>();
        foreach (var n in byId.Values)
        {
            var depth = 0;
            var current = n;
            seen.Clear();
            seen.Add(n.Id);
            while (current.ParentId is { } pid && byId.TryGetValue(pid, out var p) && seen.Add(pid))
            {
                depth++;
                current = p;
            }

            result[n.Id] = depth;
        }

        return result;
    }

    /// <summary>Identifiants de tous les descendants (enfants, petits-enfants…) d'un node.</summary>
    public static HashSet<string> Descendants(string id, IEnumerable<Node> nodes)
    {
        var result = DescendantsOf([id], nodes);
        result.Remove(id); // un cycle ne doit pas faire de ce node son propre descendant
        return result;
    }

    /// <summary>Ancêtres d'un node (parent, grand-parent…), du plus proche au plus lointain ; un cycle est coupé.</summary>
    public static List<string> Ancestors(string id, IEnumerable<Node> nodes)
    {
        var byId = new Dictionary<string, Node>();
        foreach (var n in nodes) byId[n.Id] = n;
        var result = new List<string>();
        var seen = new HashSet<string> { id };
        while (byId.TryGetValue(id, out var node) && node.ParentId is { } parent && seen.Add(parent))
        {
            result.Add(parent);
            id = parent;
        }

        return result;
    }

    /// <summary>Descendants d'un ensemble de nodes en un seul parcours (l'index parent → enfants n'est construit qu'une fois).</summary>
    public static HashSet<string> DescendantsOf(IEnumerable<string> ids, IEnumerable<Node> nodes)
    {
        var children = nodes.Where(n => n.ParentId != null).ToLookup(n => n.ParentId!, n => n.Id);
        var result = new HashSet<string>();
        var queue = new Queue<string>(ids);
        while (queue.Count > 0)
            foreach (var child in children[queue.Dequeue()])
                if (result.Add(child))
                    queue.Enqueue(child);
        return result;
    }

    /// <summary>
    ///     Contraint la position (relative) d'un enfant à l'intérieur de son parent quand <see cref="Node.ExtentParent" /> est actif
    ///     (<c>extent: 'parent'</c>). Sans taille de parent connue, la position est renvoyée telle quelle.
    /// </summary>
    public static XYPosition ClampToParent(Node child, XYPosition relativePosition, Node? parent)
    {
        if (!child.ExtentParent || parent is not { Width: { } pw, Height: { } ph }) return relativePosition;
        return GraphUtils.ClampToExtent(relativePosition, child.Width ?? 0, child.Height ?? 0, new Rect(0, 0, pw, ph));
    }
}
