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
        if (n.SourcePosition is { } sp) o["sourcePosition"] = sp.ToString().ToLowerInvariant();
        if (n.TargetPosition is { } tp) o["targetPosition"] = tp.ToString().ToLowerInvariant();
        if (!n.Connectable) o["connectable"] = false;
        if (!n.Deletable) o["deletable"] = false;
        if (n.Hidden) o["hidden"] = true;
        if (n.DragHandle != null) o["dragHandle"] = n.DragHandle;
        if (n.ExpandParent) o["expandParent"] = true;
        if (n.ZIndex is { } z) o["zIndex"] = z;
        if (n.AriaLabel != null) o["ariaLabel"] = n.AriaLabel;
        if (n.Origin is { } og) o["origin"] = new JsonArray(og.X, og.Y);
        if (n.Rotation != 0) o["rotation"] = n.Rotation;
        if (n.InitialWidth is { } iw) o["initialWidth"] = iw;
        if (n.InitialHeight is { } ih) o["initialHeight"] = ih;
        if (n.Extent is { } ex) o["extent"] = new JsonArray(new JsonArray(ex.X, ex.Y), new JsonArray(ex.Right, ex.Bottom));
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
            ExtentParent: Str(e, "extent") == "parent",
            SourcePosition: Side(e, "sourcePosition"),
            TargetPosition: Side(e, "targetPosition"),
            Connectable: Bool(e, "connectable") ?? true,
            Deletable: Bool(e, "deletable") ?? true,
            Hidden: Bool(e, "hidden") ?? false,
            DragHandle: Str(e, "dragHandle"),
            Extent: ExtentOf(e),
            ExpandParent: Bool(e, "expandParent") ?? false,
            ZIndex: Num(e, "zIndex") is { } zi ? (int)zi : null,
            AriaLabel: Str(e, "ariaLabel"),
            Rotation: Num(e, "rotation") ?? 0,
            InitialWidth: Num(e, "initialWidth"),
            InitialHeight: Num(e, "initialHeight"),
            Origin: e.TryGetProperty("origin", out var og) && og.ValueKind == JsonValueKind.Array && og.GetArrayLength() == 2
                ? new XYPosition(og[0].GetDouble(), og[1].GetDouble())
                : null);
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
        if (e.MarkerEnd != EdgeMarker.None || e.MarkerEndKey != null) o["markerEnd"] = MarkerToJson(e.MarkerEnd, e.MarkerEndKey, e.MarkerEndOptions);
        if (e.MarkerStart != EdgeMarker.None || e.MarkerStartKey != null) o["markerStart"] = MarkerToJson(e.MarkerStart, e.MarkerStartKey, e.MarkerStartOptions);
        if (e.Data != null) o["data"] = JsonSerializer.SerializeToNode(e.Data, DataOptions);
        if (e.Hidden) o["hidden"] = true;
        if (!e.Selectable) o["selectable"] = false;
        if (!e.Deletable) o["deletable"] = false;
        if (!e.Reconnectable) o["reconnectable"] = false;
        if (e.InteractionWidth is { } iw) o["interactionWidth"] = iw;
        if (e.ZIndex is { } z) o["zIndex"] = z;
        if (e.AriaLabel != null) o["ariaLabel"] = e.AriaLabel;
        if (e.Style is { } s)
        {
            var style = new JsonObject();
            if (s.Stroke != null) style["stroke"] = s.Stroke;
            if (s.StrokeWidth is { } sw) style["strokeWidth"] = sw;
            if (s.DashArray is { Count: > 0 } dash)
                style["strokeDasharray"] = string.Join(" ", dash.Select(d => d.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            o["style"] = style;
        }

        if (e.LabelStyle is { } l)
        {
            if (l.Foreground != null || l.FontSize != null)
            {
                var ls = new JsonObject();
                if (l.Foreground != null) ls["fill"] = l.Foreground;
                if (l.FontSize is { } fs) ls["fontSize"] = fs;
                o["labelStyle"] = ls;
            }

            if (!l.ShowBackground) o["labelShowBg"] = false;
            if (l.Background != null) o["labelBgStyle"] = new JsonObject { ["fill"] = l.Background };
            if (l.Padding is { } p) o["labelBgPadding"] = new JsonArray(p, p);
            if (l.CornerRadius is { } r) o["labelBgBorderRadius"] = r;
        }

        if (e.PathOptions is { } po)
        {
            var p = new JsonObject();
            if (po.Curvature is { } c) p["curvature"] = c;
            if (po.BorderRadius is { } br) p["borderRadius"] = br;
            if (po.Offset is { } off) p["offset"] = off;
            if (po.StepPosition is { } sp) p["stepPosition"] = sp;
            o["pathOptions"] = p;
        }

        return o;
    }

    private static JsonNode MarkerToJson(EdgeMarker m, string? key, EdgeMarkerOptions? options)
    {
        var o = new JsonObject { ["type"] = key ?? (m == EdgeMarker.ArrowClosed ? "arrowclosed" : "arrow") };
        if (options?.Color != null) o["color"] = options.Color;
        if (options?.Width is { } w) o["width"] = w;
        if (options?.Height is { } h) o["height"] = h;
        if (options?.StrokeWidth is { } sw) o["strokeWidth"] = sw;
        if (options?.Orient is { } orient) o["orient"] = orient;
        return o;
    }

    // React Flow accepte une chaîne ("arrowclosed") ou un objet { type: "arrowclosed", color, width, height, strokeWidth } ;
    // un autre type est une clé de marqueur personnalisé.
    private static (EdgeMarker Marker, string? Key, EdgeMarkerOptions? Options) MarkerFromJson(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var m)) return (EdgeMarker.None, null, null);
        var type = m.ValueKind == JsonValueKind.String ? m.GetString() : Str(m, "type");
        EdgeMarkerOptions? options = m.ValueKind == JsonValueKind.Object &&
                                     (Str(m, "color") != null || Num(m, "width") != null || Num(m, "height") != null || Num(m, "strokeWidth") != null || Num(m, "orient") != null)
            ? new EdgeMarkerOptions(Str(m, "color"), Num(m, "width"), Num(m, "height"), Num(m, "strokeWidth"), Num(m, "orient"))
            : null;
        return type switch
        {
            "arrowclosed" => (EdgeMarker.ArrowClosed, null, options),
            "arrow" => (EdgeMarker.Arrow, null, options),
            null or "" => (EdgeMarker.None, null, null),
            _ => (EdgeMarker.None, type, options)
        };
    }

    private static Edge EdgeFromJson(JsonElement e)
    {
        var end = MarkerFromJson(e, "markerEnd");
        var start = MarkerFromJson(e, "markerStart");

        EdgeStyleOptions? style = null;
        if (e.TryGetProperty("style", out var st) && st.ValueKind == JsonValueKind.Object)
        {
            var dash = Str(st, "strokeDasharray")?.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
                .Select(x => double.TryParse(x, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
                    System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0).ToList();
            style = new EdgeStyleOptions(Str(st, "stroke"), Num(st, "strokeWidth"), dash);
        }

        EdgeLabelOptions? label = null;
        e.TryGetProperty("labelStyle", out var ls);
        e.TryGetProperty("labelBgStyle", out var bg);
        if (ls.ValueKind == JsonValueKind.Object || bg.ValueKind == JsonValueKind.Object || e.TryGetProperty("labelShowBg", out _) ||
            e.TryGetProperty("labelBgPadding", out _) || e.TryGetProperty("labelBgBorderRadius", out _))
        {
            double? padding = e.TryGetProperty("labelBgPadding", out var pd) && pd.ValueKind == JsonValueKind.Array && pd.GetArrayLength() > 0
                ? pd[0].GetDouble()
                : null;
            label = new EdgeLabelOptions(Str(ls, "fill"), Str(bg, "fill"), Bool(e, "labelShowBg") ?? true, Num(ls, "fontSize"),
                padding, Num(e, "labelBgBorderRadius"));
        }

        EdgePathOptions? path = null;
        if (e.TryGetProperty("pathOptions", out var po) && po.ValueKind == JsonValueKind.Object)
            path = new EdgePathOptions(Num(po, "curvature"), Num(po, "borderRadius"), Num(po, "offset"), Num(po, "stepPosition"));

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
            MarkerStartKey: start.Key,
            Data: e.TryGetProperty("data", out var d) ? d.Clone() : null,
            Hidden: Bool(e, "hidden") ?? false,
            Selectable: Bool(e, "selectable") ?? true,
            Deletable: Bool(e, "deletable") ?? true,
            Reconnectable: Bool(e, "reconnectable") ?? true,
            InteractionWidth: Num(e, "interactionWidth"),
            ZIndex: Num(e, "zIndex") is { } zi ? (int)zi : null,
            AriaLabel: Str(e, "ariaLabel"),
            Style: style,
            LabelStyle: label,
            PathOptions: path,
            MarkerEndOptions: end.Options,
            MarkerStartOptions: start.Options);
    }

    // extent: "parent" (géré par ExtentParent) ou [[x1,y1],[x2,y2]].
    private static Rect? ExtentOf(JsonElement e)
    {
        if (!e.TryGetProperty("extent", out var x) || x.ValueKind != JsonValueKind.Array || x.GetArrayLength() != 2) return null;
        var (a, b) = (x[0], x[1]);
        if (a.ValueKind != JsonValueKind.Array || b.ValueKind != JsonValueKind.Array) return null;
        return new Rect(a[0].GetDouble(), a[1].GetDouble(), b[0].GetDouble() - a[0].GetDouble(), b[1].GetDouble() - a[1].GetDouble());
    }

    private static Position? Side(JsonElement e, string name) => Str(e, name)?.ToLowerInvariant() switch
    {
        "top" => Position.Top,
        "right" => Position.Right,
        "bottom" => Position.Bottom,
        "left" => Position.Left,
        _ => null
    };

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
