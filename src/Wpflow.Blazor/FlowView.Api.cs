using System.Security;
using Microsoft.JSInterop;
using System.Text;

namespace Wpflow.Blazor;

/// <summary>API d'instance (équivalent de <c>useReactFlow</c>) : viewport animé, requêtes, JSON, export d'image.</summary>
public partial class FlowView
{
    private CancellationTokenSource? _animation;

    // ---------- Viewport ----------

    /// <summary>Change le viewport ; avec une <paramref name="duration" />, la transition est animée (ease in-out).</summary>
    public Task SetViewport(Viewport target, TimeSpan duration = default) => AnimateTo(target, duration);

    private async Task AnimateTo(Viewport target, TimeSpan duration)
    {
        _animation?.Cancel();
        if (duration <= TimeSpan.Zero || _width <= 0)
        {
            await SetViewportCore(target);
            return;
        }

        var cts = _animation = new CancellationTokenSource();
        var from = Viewport;
        var started = DateTime.UtcNow;
        await NotifyKeepingLayer(() => OnMoveStart.InvokeAsync(from));
        _animating++;
        try
        {
            while (!cts.IsCancellationRequested)
            {
                var t = Math.Min(1, (DateTime.UtcNow - started).TotalMilliseconds / duration.TotalMilliseconds);
                await SetViewportCore(ViewportMath.Interpolate(from, target, ViewportMath.EaseInOut(t)));
                if (t >= 1) break;
                await Task.Delay(16, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            _animating--;
        }

        await NotifyKeepingLayer(() => OnMoveEnd.InvokeAsync(Viewport));
    }

    private async Task SetViewportCore(Viewport v)
    {
        if (TranslateExtent is { } extent && _width > 0) v = ViewportMath.ClampToExtent(v, _width, _height, extent);
        Viewport = v;
        await NotifyKeepingLayer(() => ViewportChanged.InvokeAsync(v));
        await NotifyKeepingLayer(() => OnMove.InvokeAsync(v));
        // Ici et non après le rendu : le parent s'y redessinerait plus tard, sans que la vue sache pourquoi.
        await RaiseStateChangeIfNeeded();
        _keepsNextRender = true;
        StateHasChanged();
    }

    public Task SetCenter(double x, double y, double? zoom = null, TimeSpan duration = default) =>
        SetViewport(ViewportMath.GetViewportForCenter(x, y, zoom ?? Viewport.Zoom, _width, _height), duration);

    public Task CenterOn(XYPosition flow) => SetCenter(flow.X, flow.Y);

    public Task FitBounds(Rect bounds, double? padding = null, TimeSpan duration = default, double? minZoom = null, double? maxZoom = null) =>
        SetViewport(ViewportMath.GetViewportForBounds(bounds, _width, _height, minZoom ?? MinZoom, maxZoom ?? MaxZoom, padding ?? FitViewPadding),
            duration);

    public Task ZoomTo(double zoom, TimeSpan duration = default)
    {
        var center = new XYPosition(_width / 2, _height / 2);
        return SetViewport(ViewportMath.ZoomAt(Viewport, center, zoom, MinZoom, MaxZoom), duration);
    }

    public Task ZoomBy(double factor, TimeSpan duration = default) => ZoomTo(Viewport.Zoom * factor, duration);

    public Task ZoomIn(TimeSpan duration = default) => ZoomBy(ViewportMath.ZoomStep, duration);

    public Task ZoomOut(TimeSpan duration = default) => ZoomBy(1 / ViewportMath.ZoomStep, duration);

    /// <summary>Cadre les nodes (tous, ou ceux de <see cref="FitViewOptions.NodeIds" />) ; faux s'il n'y en a aucun de mesuré.</summary>
    public async Task<bool> FitView(FitViewOptions? options = null)
    {
        options ??= FitViewOptions;
        if (ViewFitting.Compute(Nodes, _width, _height, MinZoom, MaxZoom, FitViewPadding, options) is not { } viewport) return false;
        await SetViewport(viewport, options?.Duration ?? default);
        return true;
    }

    // ---------- Requêtes ----------

    public Node? GetNode(string id) => _nodeIndex.TryGetValue(id, out var n) ? n : null;

    public Edge? GetEdge(string id) => Edges.FirstOrDefault(e => e.Id == id);

    /// <summary>Le node avec sa position absolue (coin haut gauche, coordonnées flow) à la place de la position relative.</summary>
    public Node? GetInternalNode(string id) =>
        _absNodes.GetValueOrDefault(id);

    private IEnumerable<Node> VisibleAbsoluteNodes() =>
        _absNodes.Values.Where(n => !n.Hidden);

    public IReadOnlyList<Node> GetIntersectingNodes(Rect area, bool partially = true) => GraphQueries.GetIntersectingNodes(area, VisibleAbsoluteNodes(), partially);

    public IReadOnlyList<Node> GetIntersectingNodes(Node node, bool partially = true) =>
        GetInternalNode(node.Id) is { } abs ? GraphQueries.GetIntersectingNodes(abs, VisibleAbsoluteNodes(), partially) : [];

    public bool IsNodeIntersecting(Node node, Rect area, bool partially = true) =>
        GetInternalNode(node.Id) is { } abs && GraphQueries.IsNodeIntersecting(abs, area, partially);

    public IReadOnlyList<GraphQueries.NodeConnection> GetNodeConnections(string nodeId, HandleType? type = null, string? handleId = null) =>
        GraphQueries.GetNodeConnections(nodeId, Edges, type, handleId);

    public IReadOnlyList<Node> GetIncomers(string nodeId) => _nodeIndex.TryGetValue(nodeId, out var n) ? GraphQueries.GetIncomers(n, Nodes, Edges) : [];

    public IReadOnlyList<Node> GetOutgoers(string nodeId) => _nodeIndex.TryGetValue(nodeId, out var n) ? GraphQueries.GetOutgoers(n, Nodes, Edges) : [];

    public Rect? GetNodesBounds(IEnumerable<string>? nodeIds = null)
    {
        var ids = nodeIds?.ToHashSet();
        return GraphUtils.GetNodesBounds(Nodes.Where(n => !n.Hidden && (ids == null || ids.Contains(n.Id))));
    }

    // ---------- JSON ----------

    /// <summary>Le graphe au format JSON de React Flow (<c>toObject</c>).</summary>
    /// <remarks>Avec <see cref="NodesSource" />, <c>Node.Data</c> n'est pas écrit : donnez un sélecteur pour choisir quoi écrire.</remarks>
    public string ToJson(bool indented = true) =>
        GraphSerializer.ToJson(Nodes, Edges, Viewport, indented, _nodeSource != null ? _ => null : null);

    /// <summary>Comme <see cref="ToJson(bool)" />, en écrivant <paramref name="data" /> à la place de <c>Node.Data</c>.</summary>
    public string ToJson(Func<Node, object?> data, bool indented = true) =>
        GraphSerializer.ToJson(Nodes, Edges, Viewport, indented, data);

    /// <summary>Remplace le graphe et le viewport par ceux du JSON ; l'historique est vidé.</summary>
    public async Task LoadJson(string json)
    {
        var graph = GraphSerializer.FromJson(json);
        // Avec une source, les collections sont vidées puis remplies par les fabriques (obligatoires).
        _nodeSource?.Load(graph.Nodes);
        _edgeSource?.Load(graph.Edges);
        if (_edgeSource == null) await ApplyEdgeChanges(Changesets.Diff(Edges, graph.Edges));
        if (_nodeSource == null) await ApplyNodeChanges(Changesets.Diff(Nodes, graph.Nodes));
        ClearHistory();
        await SetViewport(graph.Viewport);
    }

    // ---------- Export d'image ----------

    /// <summary>
    ///     Dessin vectoriel simplifié du graphe (nodes en rectangles avec leur texte, edges et flèches) ; les nodes à contenu
    ///     personnalisé apparaissent comme leur rectangle et leur <see cref="Node.Data" />.
    /// </summary>
    public string ExportSvg(double padding = 20, string? background = null) =>
        SvgExport.Build(Nodes.Where(n => !n.Hidden).Select(n => n with { Position = AbsolutePosition(n.Id) }),
            EdgeViews().Select(ev => new SvgEdge(ev.Edge, ev.Context.Path, ev.MarkerStart != null, ev.MarkerEnd != null)),
            Dark, padding, background);

    /// <summary>
    ///     Télécharge le graphe en image (<c>png</c> ou <c>svg</c>) par le navigateur. Par défaut le rendu réel est capturé (nodes
    ///     à contenu personnalisé, styles, thème) ; avec <paramref name="vector" />, ou si la capture échoue, c'est le dessin
    ///     simplifié de <see cref="ExportSvg" />.
    /// </summary>
    public async Task SaveImage(string fileName = "flow.png", double padding = 20, double scale = 2, bool vector = false)
    {
        if (_module == null) return;
        if (!vector && GraphUtils.GetNodesBounds(_absNodes.Values.Where(n => !n.Hidden)) is { } b)
        {
            try
            {
                await _module.InvokeVoidAsync("saveImageFromDom", _root, b.X - padding, b.Y - padding, b.Width + 2 * padding, b.Height + 2 * padding,
                    fileName, scale);
                return;
            }
            catch (JSException)
            {
                // La capture du DOM n'est pas possible (contenu non sérialisable) : retour au dessin vectoriel.
            }
        }

        await _module.InvokeVoidAsync("saveImage", ExportSvg(padding), fileName, scale);
    }
}
