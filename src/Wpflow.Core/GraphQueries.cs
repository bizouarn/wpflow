namespace Wpflow.Core;

/// <summary>Requêtes sur le graphe (équivalents de <c>getIncomers</c>, <c>getOutgoers</c>, <c>getConnectedEdges</c>, <c>addEdge</c>).</summary>
public static class GraphQueries
{
    /// <summary>Nodes qui ont un edge <b>vers</b> <paramref name="node" /> (ses prédécesseurs).</summary>
    public static IReadOnlyList<Node> GetIncomers(Node node, IEnumerable<Node> nodes, IEnumerable<Edge> edges)
    {
        var ids = edges.Where(e => e.Target == node.Id).Select(e => e.Source).ToHashSet();
        return nodes.Where(n => ids.Contains(n.Id)).ToList();
    }

    /// <summary>Nodes vers lesquels <paramref name="node" /> a un edge (ses successeurs).</summary>
    public static IReadOnlyList<Node> GetOutgoers(Node node, IEnumerable<Node> nodes, IEnumerable<Edge> edges)
    {
        var ids = edges.Where(e => e.Source == node.Id).Select(e => e.Target).ToHashSet();
        return nodes.Where(n => ids.Contains(n.Id)).ToList();
    }

    /// <summary>Edges dont une extrémité est l'un des <paramref name="nodes" />.</summary>
    public static IReadOnlyList<Edge> GetConnectedEdges(IEnumerable<Node> nodes, IEnumerable<Edge> edges)
    {
        var ids = nodes.Select(n => n.Id).ToHashSet();
        return edges.Where(e => ids.Contains(e.Source) || ids.Contains(e.Target)).ToList();
    }

    /// <summary>
    ///     Ajoute un edge sans doublon : renvoie la liste inchangée si l'id existe déjà ou si la même connexion existe.
    ///     Un edge sans id (<c>""</c>) reçoit l'id <c>xy-edge__{source}{handle}-{target}{handle}</c> de React Flow.
    /// </summary>
    public static IReadOnlyList<Edge> AddEdge(Edge edge, IReadOnlyList<Edge> edges)
    {
        if (edge.Id == "") edge = edge with { Id = ConnectionId(edge.Source, edge.SourceHandle, edge.Target, edge.TargetHandle) };
        var connection = new Connection(edge.Source, edge.Target, edge.SourceHandle, edge.TargetHandle);
        if (edges.Any(e => e.Id == edge.Id) || GraphUtils.ConnectionExists(connection, edges)) return edges;
        return [.. edges, edge];
    }

    /// <summary>Identifiant d'un edge créé par une connexion (même forme que React Flow).</summary>
    public static string ConnectionId(string source, string? sourceHandle, string target, string? targetHandle) =>
        $"xy-edge__{source}{sourceHandle}-{target}{targetHandle}";

    /// <summary>Node d'identifiant donné (<c>getNode</c>), ou <c>null</c>.</summary>
    public static Node? GetNode(string id, IEnumerable<Node> nodes) => nodes.FirstOrDefault(n => n.Id == id);

    /// <summary>Edge d'identifiant donné (<c>getEdge</c>), ou <c>null</c>.</summary>
    public static Edge? GetEdge(string id, IEnumerable<Edge> edges) => edges.FirstOrDefault(e => e.Id == id);

    /// <summary>
    ///     Vrai si le node touche <paramref name="area" /> (<c>isNodeIntersecting</c>) ; avec <paramref name="partially" /> faux,
    ///     le node doit être entièrement dans la zone. Un node non mesuré n'intersecte rien.
    /// </summary>
    public static bool IsNodeIntersecting(Node node, Rect area, bool partially = true) =>
        node.Rect is { } r && (partially ? r.Overlaps(area) : area.Contains(r));

    /// <summary>Nodes qui touchent la zone (<c>getIntersectingNodes</c>) ; les nodes cachés sont ignorés.</summary>
    public static IReadOnlyList<Node> GetIntersectingNodes(Rect area, IEnumerable<Node> nodes, bool partially = true) =>
        nodes.Where(n => !n.Hidden && IsNodeIntersecting(n, area, partially)).ToList();

    /// <summary>Nodes qui touchent <paramref name="node" />, lui-même exclu.</summary>
    public static IReadOnlyList<Node> GetIntersectingNodes(Node node, IEnumerable<Node> nodes, bool partially = true) =>
        node.Rect is { } r ? GetIntersectingNodes(r, nodes.Where(n => n.Id != node.Id), partially) : [];

    /// <summary>Connexion portée par un edge (<c>NodeConnection</c>).</summary>
    public sealed record NodeConnection(Connection Connection, string EdgeId);

    /// <summary>
    ///     Connexions d'un node (<c>getNodeConnections</c>) : avec <paramref name="type" /> <c>Source</c>, les edges qui en
    ///     partent ; <c>Target</c>, ceux qui y arrivent ; <c>null</c> les deux. <paramref name="handleId" /> filtre sur le handle.
    /// </summary>
    public static IReadOnlyList<NodeConnection> GetNodeConnections(string nodeId, IEnumerable<Edge> edges,
        HandleType? type = null, string? handleId = null) =>
        edges.Where(e => (type != HandleType.Target && e.Source == nodeId && (handleId == null || e.SourceHandle == handleId))
                         || (type != HandleType.Source && e.Target == nodeId && (handleId == null || e.TargetHandle == handleId)))
            .Select(e => new NodeConnection(new Connection(e.Source, e.Target, e.SourceHandle, e.TargetHandle), e.Id))
            .ToList();

    /// <summary>
    ///     Change les extrémités d'un edge (<c>reconnectEdge</c>). Avec <paramref name="shouldReplaceId" />, l'edge reçoit l'id
    ///     de la nouvelle connexion ; sinon il garde le sien. Renvoie la liste inchangée si la connexion existe déjà.
    /// </summary>
    public static IReadOnlyList<Edge> ReconnectEdge(Edge oldEdge, Connection connection, IReadOnlyList<Edge> edges,
        bool shouldReplaceId = true)
    {
        if (GraphUtils.ConnectionExists(connection, edges.Where(e => e.Id != oldEdge.Id))) return edges;
        var id = shouldReplaceId
            ? ConnectionId(connection.Source, connection.SourceHandle, connection.Target, connection.TargetHandle)
            : oldEdge.Id;
        var updated = oldEdge with
        {
            Id = id, Source = connection.Source, Target = connection.Target,
            SourceHandle = connection.SourceHandle, TargetHandle = connection.TargetHandle
        };
        return edges.Select(e => e.Id == oldEdge.Id ? updated : e).ToList();
    }
}
