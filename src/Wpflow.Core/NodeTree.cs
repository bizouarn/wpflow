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
        foreach (var n in byId.Values) Resolve(n, byId, result, new HashSet<string>());
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

    /// <summary>Copie des nodes dont <see cref="Node.Position" /> est absolue (pratique pour dessiner edges et handles).</summary>
    public static List<Node> ToAbsolute(IReadOnlyList<Node> nodes)
    {
        var abs = AbsolutePositions(nodes);
        return nodes.Select(n => n.ParentId == null ? n : n with { Position = abs[n.Id] }).ToList();
    }

    /// <summary>Profondeur dans la hiérarchie (0 pour une racine) ; sert à ordonner l'empilement (parents sous leurs enfants).</summary>
    public static Dictionary<string, int> Depths(IEnumerable<Node> nodes)
    {
        var byId = new Dictionary<string, Node>();
        foreach (var n in nodes) byId[n.Id] = n;

        var result = new Dictionary<string, int>();
        foreach (var n in byId.Values)
        {
            var depth = 0;
            var current = n;
            var seen = new HashSet<string> { n.Id };
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
        var list = nodes.ToList();
        var result = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(id);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var child in list.Where(n => n.ParentId == current))
                if (result.Add(child.Id))
                    queue.Enqueue(child.Id);
        }

        result.Remove(id);
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
