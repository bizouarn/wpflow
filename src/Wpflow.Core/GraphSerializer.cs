using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wpflow.Core;

/// <summary>État sérialisable d'un graphe.</summary>
public sealed record FlowGraph(IReadOnlyList<Node> Nodes, IReadOnlyList<Edge> Edges, Viewport Viewport);

/// <summary>
///     Sérialisation JSON proche de <c>toObject()</c> de React Flow (<c>nodes</c>, <c>edges</c>, <c>viewport</c>),
///     ce qui permet d'échanger des graphes avec une application React Flow.
///     <see cref="Node.Data" /> est écrit tel quel (types simples, dictionnaires, objets sérialisables) et relu comme
///     <see cref="JsonElement" />.
/// </summary>
public static class GraphSerializer
{
    private static readonly JsonSerializerOptions DataOptions = new() { WriteIndented = false };

    /// <summary>
    ///     Écrit le graphe. <paramref name="data" /> remplace <see cref="Node.Data" /> à l'écriture (par exemple pour écrire un
    ///     objet de transfert à la place d'un ViewModel) ; <c>null</c> = <see cref="Node.Data" /> tel quel.
    /// </summary>
    public static string ToJson(IEnumerable<Node> nodes, IEnumerable<Edge> edges, Viewport viewport,
        bool indented = true, Func<Node, object?>? data = null)
    {
        var root = new JsonObject
        {
            ["nodes"] = new JsonArray(nodes.Select(n => NodeToJson(n, data)).ToArray()),
            ["edges"] = new JsonArray(edges.Select(EdgeToJson).ToArray()),
            ["viewport"] = new JsonObject { ["x"] = viewport.X, ["y"] = viewport.Y, ["zoom"] = viewport.Zoom }
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = indented });
    }

    public static FlowGraph FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var nodes = root.TryGetProperty("nodes", out var n) && n.ValueKind == JsonValueKind.Array
            ? n.EnumerateArray().Select(NodeFromJson).ToList()
            : new List<Node>();
        var edges = root.TryGetProperty("edges", out var e) && e.ValueKind == JsonValueKind.Array
            ? e.EnumerateArray().Select(EdgeFromJson).ToList()
            : new List<Edge>();

        var viewport = Viewport.Identity;
        if (root.TryGetProperty("viewport", out var v) && v.ValueKind == JsonValueKind.Object)
            viewport = new Viewport(Num(v, "x") ?? 0, Num(v, "y") ?? 0, Num(v, "zoom") ?? 1);

        return new FlowGraph(nodes, edges, viewport);
    }

    private static JsonNode NodeToJson(Node n, Func<Node, object?>? dataOf)
    {
        var o = new JsonObject
        {
            ["id"] = n.Id,
            ["type"] = n.Type,
            ["position"] = new JsonObject { ["x"] = n.Position.X, ["y"] = n.Position.Y }
        };
        var payload = dataOf != null ? dataOf(n) : n.Data;
        if (payload != null) o["data"] = JsonSerializer.SerializeToNode(payload, DataOptions);
        if (n.FixedWidth is { } w) o["width"] = w;
        if (n.FixedHeight is { } h) o["height"] = h;
        if (n.Selected) o["selected"] = true;
        if (!n.Draggable) o["draggable"] = false;
        if (!n.Selectable) o["selectable"] = false;
        if (n.ParentId != null) o["parentId"] = n.ParentId;
        if (n.ExtentParent) o["extent"] = "parent";
        return o;
    }

    private static Node NodeFromJson(JsonElement e)
    {
        var pos = e.TryGetProperty("position", out var p)
            ? new XYPosition(Num(p, "x") ?? 0, Num(p, "y") ?? 0)
            : XYPosition.Zero;
        object? data = e.TryGetProperty("data", out var d) ? d.Clone() : null;

        return new Node(
            Id: Str(e, "id") ?? throw new JsonException("Node sans id."),
            Position: pos,
            Type: Str(e, "type") ?? "default",
            Data: data,
            FixedWidth: Num(e, "width"),
            FixedHeight: Num(e, "height"),
            Selected: Bool(e, "selected") ?? false,
            Draggable: Bool(e, "draggable") ?? true,
            Selectable: Bool(e, "selectable") ?? true,
            ParentId: Str(e, "parentId"),
            ExtentParent: Str(e, "extent") == "parent");
    }

    private static JsonNode EdgeToJson(Edge e)
    {
        var o = new JsonObject { ["id"] = e.Id, ["source"] = e.Source, ["target"] = e.Target };
        if (e.SourceHandle != null) o["sourceHandle"] = e.SourceHandle;
        if (e.TargetHandle != null) o["targetHandle"] = e.TargetHandle;
        if (e.Type != "default") o["type"] = e.Type;
        if (e.Label != null) o["label"] = e.Label;
        if (e.Animated) o["animated"] = true;
        if (e.Selected) o["selected"] = true;
        if (e.MarkerEnd != EdgeMarker.None || e.MarkerEndKey != null) o["markerEnd"] = MarkerToJson(e.MarkerEnd, e.MarkerEndKey);
        if (e.MarkerStart != EdgeMarker.None || e.MarkerStartKey != null) o["markerStart"] = MarkerToJson(e.MarkerStart, e.MarkerStartKey);
        return o;
    }

    private static JsonNode MarkerToJson(EdgeMarker m, string? key) =>
        new JsonObject { ["type"] = key ?? (m == EdgeMarker.ArrowClosed ? "arrowclosed" : "arrow") };

    // React Flow accepte une chaîne ("arrowclosed") ou un objet { type: "arrowclosed" } ; un autre type est une clé de marqueur personnalisé.
    private static (EdgeMarker Marker, string? Key) MarkerFromJson(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var m)) return (EdgeMarker.None, null);
        var type = m.ValueKind == JsonValueKind.String ? m.GetString() : Str(m, "type");
        return type switch
        {
            "arrowclosed" => (EdgeMarker.ArrowClosed, null),
            "arrow" => (EdgeMarker.Arrow, null),
            null or "" => (EdgeMarker.None, null),
            _ => (EdgeMarker.None, type)
        };
    }

    private static Edge EdgeFromJson(JsonElement e)
    {
        var end = MarkerFromJson(e, "markerEnd");
        var start = MarkerFromJson(e, "markerStart");
        return new Edge(
            Id: Str(e, "id") ?? throw new JsonException("Edge sans id."),
            Source: Str(e, "source") ?? throw new JsonException("Edge sans source."),
            Target: Str(e, "target") ?? throw new JsonException("Edge sans target."),
            SourceHandle: Str(e, "sourceHandle"),
            TargetHandle: Str(e, "targetHandle"),
            Type: Str(e, "type") ?? "default",
            Label: Str(e, "label"),
            Animated: Bool(e, "animated") ?? false,
            Selected: Bool(e, "selected") ?? false,
            MarkerEnd: end.Marker,
            MarkerStart: start.Marker,
            MarkerEndKey: end.Key,
            MarkerStartKey: start.Key);
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static double? Num(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDouble()
            : null;

    private static bool? Bool(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) &&
        v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.GetBoolean()
            : null;
}
