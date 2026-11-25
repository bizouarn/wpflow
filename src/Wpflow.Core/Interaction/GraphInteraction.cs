namespace Wpflow.Core;

/// <summary>Un handle d'un node (le node est désigné par son id).</summary>
public sealed record HandleRef(string NodeId, HandleInfo Info);

/// <summary>Recherche et validation du handle visé pendant qu'on tire un fil.</summary>
public static class ConnectionSearch
{
    /// <summary>
    ///     Handle accepté le plus proche du pointeur (dans <paramref name="radius" />, en unités flow), ou <c>null</c>.
    ///     <paramref name="getNode" /> donne un node avec sa position absolue.
    /// </summary>
    public static HandleRef? FindCandidate(HandleRef from, XYPosition flow, double radius, ConnectionMode mode,
        IEnumerable<KeyValuePair<string, IReadOnlyList<HandleInfo>>> handles, Func<string, Node?> getNode)
    {
        var candidates = new List<(HandleRef Handle, XYPosition Position)>();
        foreach (var (nodeId, list) in handles)
        {
            if (nodeId == from.NodeId || getNode(nodeId) is not { Connectable: true, Hidden: false } node) continue;
            foreach (var h in list)
                if (h is { Connectable: true, ConnectableEnd: true } && Connections.AreTypesCompatible(from.Info.Type, h.Type, mode))
                    candidates.Add((new HandleRef(nodeId, h), GraphUtils.GetHandleCenter(node, h)));
        }

        // À distance égale on préfère le handle de type opposé (comme xyflow).
        var ordered = candidates.OrderBy(c => c.Handle.Info.Type == from.Info.Type ? 1 : 0).ToList();
        return GraphUtils.GetClosestHandle(flow, radius, ordered);
    }

    /// <summary>
    ///     La connexion est-elle permise (doublon, validateurs de la vue et des handles) ? Avec
    ///     <paramref name="reconnectingEdgeId" />, l'edge déplacé n'est pas un doublon de lui-même.
    /// </summary>
    public static bool Validate(HandleRef from, HandleRef target, IEnumerable<Edge> edges, string? reconnectingEdgeId,
        Func<Connection, bool>? viewValidator, out Connection connection)
    {
        connection = Connections.Create(from.NodeId, from.Info.Id, from.Info.Type, target.NodeId, target.Info.Id);
        var c = connection;
        var others = reconnectingEdgeId == null ? edges : edges.Where(e => e.Id != reconnectingEdgeId);
        if (GraphUtils.ConnectionExists(c, others)) return false;
        return Connections.IsValid(c, viewValidator)
               && from.Info.IsValidConnection?.Invoke(c) != false
               && target.Info.IsValidConnection?.Invoke(c) != false;
    }
}

/// <summary>Extrémités d'un edge : points d'attache et côtés des handles (coordonnées flow).</summary>
public readonly record struct EdgeEnds(XYPosition Source, Position SourcePosition, XYPosition Target, Position TargetPosition);

/// <summary>Résolution des extrémités d'un edge à partir des nodes (positions absolues) et des handles mesurés.</summary>
public static class EdgeEndpoints
{
    /// <summary>
    ///     <c>null</c> si l'edge est caché, si un node manque ou est caché, ou si une extrémité n'est pas encore mesurée.
    ///     Taille et handles annoncés par le node (<c>initialWidth</c>, <c>handles</c>) : l'edge se dessine avant la première mesure.
    ///     Sans handle, l'extrémité est le milieu du côté <paramref name="sourceFallback" /> / <paramref name="targetFallback" />.
    /// </summary>
    public static EdgeEnds? Resolve(Edge edge, Func<string, Node?> getNode, Func<string, IReadOnlyList<HandleInfo>?> getHandles,
        Position sourceFallback, Position targetFallback)
    {
        if (edge.Hidden || getNode(edge.Source) is not { } sn || getNode(edge.Target) is not { } tn) return null;
        if (sn.Hidden || tn.Hidden) return null;
        (sn, tn) = (Virtualization.WithInitialSize(sn), Virtualization.WithInitialSize(tn));
        var s = ResolveHandle(sn, edge.SourceHandle, HandleType.Source, sourceFallback, getHandles);
        var t = ResolveHandle(tn, edge.TargetHandle, HandleType.Target, targetFallback, getHandles);
        return s is null || t is null ? null : new EdgeEnds(s.Value.Pos, s.Value.Side, t.Value.Pos, t.Value.Side);
    }

    /// <summary>L'id du node d'une extrémité introuvable (pour signaler l'erreur), ou <c>null</c> si les deux existent.</summary>
    public static string? MissingNode(Edge edge, Func<string, Node?> getNode) =>
        getNode(edge.Source) == null ? edge.Source : getNode(edge.Target) == null ? edge.Target : null;

    private static (XYPosition Pos, Position Side)? ResolveHandle(Node node, string? handleId, HandleType type, Position fallback,
        Func<string, IReadOnlyList<HandleInfo>?> getHandles)
    {
        var list = getHandles(node.Id) ?? node.Handles;
        if (list != null)
        {
            var info = list.FirstOrDefault(h => h.Type == type && (string.IsNullOrEmpty(handleId) || h.Id == handleId));
            if (info != null) return (GraphUtils.GetHandlePosition(node, info.Position, info), info.Position);
        }

        return node.Rect is null ? null : (GraphUtils.GetHandlePosition(node, fallback), fallback);
    }
}

/// <summary>Historique annuler / rétablir par instantanés (nodes et edges immuables : pas de copie).</summary>
public sealed class GraphHistory(int limit = 100)
{
    /// <summary>Nombre d'états gardés ; réduit, les plus anciens partent à la prochaine modification.</summary>
    public int Limit { get; set; } = limit;

    public readonly record struct Snapshot(IReadOnlyList<Node> Nodes, IReadOnlyList<Edge> Edges);

    private readonly List<Snapshot> _undo = new();
    private readonly List<Snapshot> _redo = new();

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>Mémorise l'état courant avant une modification ; efface les rétablissements.</summary>
    public void Push(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges)
    {
        _undo.Add(new Snapshot(nodes, edges));
        while (_undo.Count > Math.Max(0, Limit)) _undo.RemoveAt(0);
        _redo.Clear();
    }

    /// <summary>Dépile l'état précédent (l'état courant devient rétablissable) ; faux s'il n'y en a pas.</summary>
    public bool TryUndo(IReadOnlyList<Node> currentNodes, IReadOnlyList<Edge> currentEdges, out Snapshot snapshot)
    {
        snapshot = default;
        if (_undo.Count == 0) return false;
        snapshot = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(new Snapshot(currentNodes, currentEdges));
        return true;
    }

    public bool TryRedo(IReadOnlyList<Node> currentNodes, IReadOnlyList<Edge> currentEdges, out Snapshot snapshot)
    {
        snapshot = default;
        if (_redo.Count == 0) return false;
        snapshot = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(new Snapshot(currentNodes, currentEdges));
        return true;
    }

    /// <summary>
    ///     Réécrit tous les états gardés : sert à y reporter un changement qui ne doit pas être annulé (fait par
    ///     l'application, pas par un geste).
    /// </summary>
    public void Rewrite(Func<Snapshot, Snapshot> rewrite)
    {
        for (var i = 0; i < _undo.Count; i++) _undo[i] = rewrite(_undo[i]);
        for (var i = 0; i < _redo.Count; i++) _redo[i] = rewrite(_redo[i]);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}

/// <summary>Ce qu'une suppression emporte.</summary>
public sealed record DeletionPlan(HashSet<string> Nodes, HashSet<string> Edges, HashSet<string> EdgesOfNodes);

public static class GraphDeletion
{
    /// <summary>
    ///     Les nodes et edges à supprimer : seuls les éléments supprimables, les descendants d'un parent supprimé et les edges
    ///     de ces nodes. <c>null</c> s'il n'y a rien à supprimer.
    /// </summary>
    public static DeletionPlan? Plan(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges, IEnumerable<string>? nodeIds, IEnumerable<string>? edgeIds)
    {
        var wantNodes = nodeIds?.ToHashSet() ?? [];
        var wantEdges = edgeIds?.ToHashSet() ?? [];
        var nodeSet = nodes.Where(n => wantNodes.Contains(n.Id) && n.Deletable).Select(n => n.Id).ToHashSet();
        var edgeSet = edges.Where(e => wantEdges.Contains(e.Id) && e.Deletable).Select(e => e.Id).ToHashSet();
        if (nodeSet.Count == 0 && edgeSet.Count == 0) return null;

        // Supprimer un parent supprime ses descendants ; les edges de ces nodes partent avec eux.
        nodeSet.UnionWith(NodeTree.DescendantsOf(nodeSet.ToList(), nodes));
        var byNode = edges.Where(e => nodeSet.Contains(e.Source) || nodeSet.Contains(e.Target)).Select(e => e.Id).ToHashSet();
        return new DeletionPlan(nodeSet, edgeSet, byNode);
    }
}

/// <summary>Changes de sélection (clic, rectangle, tout sélectionner) et textes d'annonce.</summary>
public static class SelectionOps
{
    /// <summary>
    ///     Les changes qui rendent la sélection égale aux ids donnés (avec <paramref name="additive" />, la sélection actuelle est
    ///     conservée). Sans <paramref name="elementsSelectable" />, tout est désélectionné.
    /// </summary>
    public static (IReadOnlyList<NodeChange> Nodes, IReadOnlyList<EdgeChange> Edges) Select(IReadOnlyList<Node> nodes,
        IReadOnlyList<Edge> edges, IEnumerable<string> nodeIds, IEnumerable<string> edgeIds, bool additive, bool elementsSelectable = true)
    {
        var n = elementsSelectable ? nodeIds.ToHashSet() : [];
        var e = elementsSelectable ? edgeIds.ToHashSet() : [];
        additive &= elementsSelectable;
        var nodeChanges = nodes
            .Where(x => x.Selectable)
            .Select(x => (x, want: n.Contains(x.Id) || (additive && x.Selected)))
            .Where(p => p.x.Selected != p.want)
            .Select(p => (NodeChange)new NodeSelectChange(p.x.Id, p.want)).ToList();
        var edgeChanges = edges
            .Where(x => x.Selectable && !x.Hidden)
            .Select(x => (x, want: e.Contains(x.Id) || (additive && x.Selected)))
            .Where(p => p.x.Selected != p.want)
            .Select(p => (EdgeChange)new EdgeSelectChange(p.x.Id, p.want)).ToList();
        return (nodeChanges, edgeChanges);
    }

    /// <summary>
    ///     Nodes pris par un rectangle de sélection (coordonnées flow ; <paramref name="absolute" /> donne les nodes avec leur
    ///     position absolue) et, comme xyflow, les edges dont les deux extrémités le sont.
    /// </summary>
    public static (HashSet<string> Nodes, List<string> Edges) Box(Rect box, IEnumerable<Node> absolute, IReadOnlyList<Edge> edges,
        FlowSelectionMode mode)
    {
        var full = mode == FlowSelectionMode.Full;
        var picked = absolute.Where(n => !n.Hidden && n.Rect is { } r && (full ? box.Contains(r) : r.Overlaps(box))).Select(n => n.Id).ToHashSet();
        var pickedEdges = edges.Where(e => picked.Contains(e.Source) && picked.Contains(e.Target)).Select(e => e.Id).ToList();
        return (picked, pickedEdges);
    }

    /// <summary>Rectangle entre deux points flow.</summary>
    public static Rect Area(XYPosition a, XYPosition b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>« … sélectionné » pour un node (nom accessible, sinon « Node {data ou id} »).</summary>
    public static string AnnounceNode(Node node, Func<Node, string?>? ariaLabel) =>
        $"{node.AriaLabel ?? ariaLabel?.Invoke(node) ?? "Node " + (node.Data as string ?? node.Id)} sélectionné";

    public static string AnnounceEdge(Edge edge, Func<Edge, string?>? ariaLabel) =>
        (edge.AriaLabel ?? ariaLabel?.Invoke(edge)) is { } name ? $"{name} sélectionné" : $"Edge {edge.Id} sélectionné";
}
