namespace Wpflow.Core;

/// <summary>Disposition automatique simple (équivalent des exemples « layouting » de xyflow, sans dépendance à dagre/elk).</summary>
public enum LayoutDirection
{
    LeftToRight,
    RightToLeft,
    TopToBottom,
    BottomToTop
}

public static class GraphLayout
{
    /// <summary>
    ///     Disposition en couches dans la direction voulue (équivalent des options <c>rankdir</c> de dagre) :
    ///     <paramref name="rankGap" /> sépare les couches, <paramref name="nodeGap" /> les nodes d'une même couche.
    ///     Renvoie les positions (coin haut-gauche).
    /// </summary>
    public static Dictionary<string, XYPosition> Layered(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges,
        LayoutDirection direction = LayoutDirection.LeftToRight, double rankGap = 80, double nodeGap = 30,
        double defaultWidth = 150, double defaultHeight = 40)
    {
        var horizontal = direction is LayoutDirection.LeftToRight or LayoutDirection.RightToLeft;
        // On calcule toujours « de gauche à droite » dans des axes (couche, position dans la couche), puis on les ramène à l'écran.
        var basis = nodes.Select(n => n with
        {
            Width = horizontal ? n.Width ?? defaultWidth : n.Height ?? defaultHeight,
            Height = horizontal ? n.Height ?? defaultHeight : n.Width ?? defaultWidth
        }).ToList();
        var local = LayeredLeftToRight(basis, edges, rankGap, nodeGap, defaultWidth, defaultHeight);

        var result = new Dictionary<string, XYPosition>();
        var max = local.Count == 0 ? 0 : basis.Max(n => local[n.Id].X + n.Width!.Value);
        foreach (var n in basis)
        {
            var p = local[n.Id];
            var (w, h) = (n.Width!.Value, n.Height!.Value);
            result[n.Id] = direction switch
            {
                LayoutDirection.LeftToRight => p,
                LayoutDirection.RightToLeft => new XYPosition(max - p.X - w, p.Y),
                LayoutDirection.TopToBottom => new XYPosition(p.Y, p.X),
                _ => new XYPosition(p.Y, max - p.X - w)
            };
        }

        return result;
    }

    // Ordre des nodes dans chaque couche : tri par barycentre des voisins (heuristique de Sugiyama), balayages avant puis arrière.
    // Le tri est stable : sans voisin ou à barycentre égal, l'ordre d'entrée est conservé.
    private static void ReduceCrossings(List<List<Node>> layers, Dictionary<string, List<string>> incoming,
        Dictionary<string, List<string>> outgoing)
    {
        if (layers.Count < 2) return;
        var index = new Dictionary<string, double>();
        void Reindex()
        {
            foreach (var layer in layers)
                for (var i = 0; i < layer.Count; i++)
                    index[layer[i].Id] = i;
        }

        double Bary(Node n, Dictionary<string, List<string>> links) =>
            links.TryGetValue(n.Id, out var l) && l.Count > 0 ? l.Average(id => index[id]) : index[n.Id];

        Reindex();
        for (var sweep = 0; sweep < 4; sweep++)
        {
            for (var l = 1; l < layers.Count; l++)
            {
                layers[l] = layers[l].OrderBy(n => Bary(n, incoming)).ToList();
                Reindex();
            }

            for (var l = layers.Count - 2; l >= 0; l--)
            {
                layers[l] = layers[l].OrderBy(n => Bary(n, outgoing)).ToList();
                Reindex();
            }
        }
    }

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
        var layers = nodes.GroupBy(n => layer[n.Id]).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
        ReduceCrossings(layers, incoming, edges.Where(e => ids.Contains(e.Source) && ids.Contains(e.Target) && e.Source != e.Target)
            .GroupBy(e => e.Source).ToDictionary(g => g.Key, g => g.Select(e => e.Target).ToList()));
        foreach (var members in layers)
        {
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
