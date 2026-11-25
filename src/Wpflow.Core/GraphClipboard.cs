namespace Wpflow.Core;

/// <summary>Contenu copié : nodes (positions absolues pour les racines) et edges qui les relient.</summary>
public sealed record ClipboardData(IReadOnlyList<Node> Nodes, IReadOnlyList<Edge> Edges);

/// <summary>Copier / couper / coller de nodes et d'edges (absent de <c>@xyflow/system</c> : l'exemple « Copy Paste » le fait à la main).</summary>
public static class GraphClipboard
{
    /// <summary>
    ///     Copie les nodes sélectionnés avec leurs descendants et les edges dont les deux extrémités sont copiées.
    ///     Un node dont le parent n'est pas copié devient une racine, à sa position absolue. <c>null</c> si rien n'est sélectionné.
    /// </summary>
    public static ClipboardData? Copy(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges)
    {
        var selected = nodes.Where(n => n.Selected && !n.Hidden).Select(n => n.Id).ToList();
        if (selected.Count == 0) return null;
        var ids = new HashSet<string>(selected);
        ids.UnionWith(NodeTree.DescendantsOf(selected, nodes));

        var abs = NodeTree.AbsolutePositions(nodes);
        var copied = nodes.Where(n => ids.Contains(n.Id)).Select(n =>
            n.ParentId != null && ids.Contains(n.ParentId)
                ? n
                : n with { Position = abs[n.Id], ParentId = null, ExtentParent = false }).ToList();
        var links = edges.Where(e => ids.Contains(e.Source) && ids.Contains(e.Target)).ToList();
        return new ClipboardData(copied, links);
    }

    /// <summary>
    ///     Copies prêtes à ajouter : ids neufs (<c>{id}-copy</c>, <c>-copy2</c>… absents de <paramref name="existingNodes" /> et
    ///     <paramref name="existingEdges" />), racines décalées de <paramref name="offset" />, parents et extrémités d'edges remappés,
    ///     nodes collés sélectionnés (les edges collés non).
    /// </summary>
    public static ClipboardData Paste(ClipboardData data, IEnumerable<Node> existingNodes, IEnumerable<Edge> existingEdges,
        XYPosition offset)
    {
        var usedNodes = existingNodes.Select(n => n.Id).ToHashSet();
        var usedEdges = existingEdges.Select(e => e.Id).ToHashSet();
        var map = new Dictionary<string, string>();
        foreach (var n in data.Nodes)
        {
            var id = n.Id + "-copy";
            for (var i = 2; usedNodes.Contains(id); i++) id = $"{n.Id}-copy{i}";
            usedNodes.Add(id);
            map[n.Id] = id;
        }

        var nodes = data.Nodes.Select(n => n with
        {
            Id = map[n.Id],
            ParentId = n.ParentId != null && map.TryGetValue(n.ParentId, out var p) ? p : null,
            Position = n.ParentId != null && map.ContainsKey(n.ParentId) ? n.Position : n.Position + offset,
            Selected = true
        }).ToList();

        var edges = data.Edges.Select(e =>
        {
            var id = e.Id + "-copy";
            for (var i = 2; usedEdges.Contains(id); i++) id = $"{e.Id}-copy{i}";
            usedEdges.Add(id);
            return e with { Id = id, Source = map[e.Source], Target = map[e.Target], Selected = false };
        }).ToList();
        return new ClipboardData(nodes, edges);
    }
}
