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

    public static string ToJson(IEnumerable<Node> nodes, IEnumerable<Edge> edges, Viewport viewport,
        bool indented = true)
    {
        var root = new JsonObject
        {
            ["nodes"] = new JsonArray(nodes.Select(NodeToJson).ToArray()),
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

    private static JsonNode NodeToJson(Node n)
    {
        var o = new JsonObject
        {
            ["id"] = n.Id,
            ["type"] = n.Type,
            ["position"] = new JsonObject { ["x"] = n.Position.X, ["y"] = n.Position.Y }
        };
        if (n.Data != null) o["data"] = JsonSerializer.SerializeToNode(n.Data, DataOptions);
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
        if (e.MarkerEnd != EdgeMarker.None) o["markerEnd"] = MarkerToJson(e.MarkerEnd);
        if (e.MarkerStart != EdgeMarker.None) o["markerStart"] = MarkerToJson(e.MarkerStart);
        return o;
    }

    private static JsonNode MarkerToJson(EdgeMarker m) =>
        new JsonObject { ["type"] = m == EdgeMarker.ArrowClosed ? "arrowclosed" : "arrow" };

    // React Flow accepte une chaîne ("arrowclosed") ou un objet { type: "arrowclosed" }.
    private static EdgeMarker MarkerFromJson(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var m)) return EdgeMarker.None;
        var type = m.ValueKind == JsonValueKind.String ? m.GetString() : Str(m, "type");
        return type switch
        {
            "arrowclosed" => EdgeMarker.ArrowClosed,
            "arrow" => EdgeMarker.Arrow,
            _ => EdgeMarker.None
        };
    }

    private static Edge EdgeFromJson(JsonElement e) => new(
        Id: Str(e, "id") ?? throw new JsonException("Edge sans id."),
        Source: Str(e, "source") ?? throw new JsonException("Edge sans source."),
        Target: Str(e, "target") ?? throw new JsonException("Edge sans target."),
        SourceHandle: Str(e, "sourceHandle"),
        TargetHandle: Str(e, "targetHandle"),
        Type: Str(e, "type") ?? "default",
        Label: Str(e, "label"),
        Animated: Bool(e, "animated") ?? false,
        Selected: Bool(e, "selected") ?? false,
        MarkerEnd: MarkerFromJson(e, "markerEnd"),
        MarkerStart: MarkerFromJson(e, "markerStart"));

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
