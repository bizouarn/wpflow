namespace Wpflow.Core;

/// <summary>Disposition automatique simple (équivalent des exemples « layouting » de xyflow, sans dépendance à dagre/elk).</summary>
public static class GraphLayout
{
    /// <summary>
    ///     Disposition en couches de gauche à droite : la couche d'un node est la longueur du plus long chemin qui y mène
    ///     (les cycles sont ignorés), les nodes d'une couche sont empilés et centrés verticalement.
    ///     Renvoie les nouvelles positions ; les nodes sans dimensions utilisent <paramref name="defaultWidth" />/<paramref name="defaultHeight" />.
    /// </summary>
    public static Dictionary<string, XYPosition> LayeredLeftToRight(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges,
        double horizontalGap = 80, double verticalGap = 30, double defaultWidth = 150, double defaultHeight = 40)
    {
        var ids = nodes.Select(n => n.Id).ToHashSet();
        var incoming = nodes.ToDictionary(n => n.Id, _ => new List<string>());
        foreach (var e in edges)
            if (ids.Contains(e.Source) && ids.Contains(e.Target) && e.Source != e.Target)
                incoming[e.Target].Add(e.Source);

        var layer = new Dictionary<string, int>();
        int LayerOf(string id, HashSet<string> path)
        {
            if (layer.TryGetValue(id, out var known)) return known;
            if (!path.Add(id)) return 0; // cycle : on coupe
            var l = incoming[id].Count == 0 ? 0 : incoming[id].Max(s => LayerOf(s, path) + 1);
            path.Remove(id);
            layer[id] = l;
            return l;
        }

        foreach (var n in nodes) LayerOf(n.Id, new HashSet<string>());

        var result = new Dictionary<string, XYPosition>();
        var x = 0.0;
        foreach (var group in nodes.GroupBy(n => layer[n.Id]).OrderBy(g => g.Key))
        {
            var members = group.ToList();
            var width = members.Max(n => n.Width ?? defaultWidth);
            var totalHeight = members.Sum(n => n.Height ?? defaultHeight) + verticalGap * (members.Count - 1);
            var y = -totalHeight / 2;
            foreach (var n in members)
            {
                result[n.Id] = new XYPosition(x, y);
                y += (n.Height ?? defaultHeight) + verticalGap;
            }

            x += width + horizontalGap;
        }

        return result;
    }
}
