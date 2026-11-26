using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Wpflow.Core;

namespace Wpflow.Wpf;

// API de l'instance (équivalent de useReactFlow) : transitions du viewport, recherche et mise à jour d'éléments,
// copier-coller, export d'image.
public partial class FlowView
{
    #region Viewport animé

    private DispatcherTimer? _animTimer;
    private Viewport _animFrom, _animTo;
    private DateTime _animStart;
    private TimeSpan _animDuration;

    /// <summary>
    ///     Déplace la vue vers <paramref name="target" />, avec une transition si <paramref name="duration" /> est positive
    ///     (<c>setViewport(viewport, { duration })</c>). Un geste de l'utilisateur interrompt la transition.
    /// </summary>
    public void SetViewport(Viewport target, TimeSpan duration = default)
    {
        StopViewportAnimation(false);
        if (duration <= TimeSpan.Zero || ActualWidth <= 0 || target == Viewport)
        {
            Viewport = target;
            return;
        }

        _animFrom = Viewport;
        _animTo = target;
        _animDuration = duration;
        _animStart = DateTime.UtcNow;
        ViewportMoveStarted?.Invoke(this, new ViewportMoveEventArgs(Viewport));
        _animTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => AnimationTick(), Dispatcher);
        _animTimer.Start();
    }

    /// <summary>Vrai pendant une transition de viewport.</summary>
    public bool IsViewportAnimating => _animTimer != null;

    private void AnimationTick()
    {
        var t = (DateTime.UtcNow - _animStart).TotalMilliseconds / _animDuration.TotalMilliseconds;
        if (t >= 1)
        {
            StopViewportAnimation(true);
            return;
        }

        Viewport = ViewportMath.Interpolate(_animFrom, _animTo, ViewportMath.EaseInOut(t));
    }

    /// <summary>Arrête la transition ; avec <paramref name="complete" />, la vue atteint sa destination.</summary>
    internal void StopViewportAnimation(bool complete)
    {
        if (_animTimer == null) return;
        _animTimer.Stop();
        _animTimer = null;
        if (complete) Viewport = _animTo;
        ViewportMoveEnded?.Invoke(this, new ViewportMoveEventArgs(Viewport));
    }

    /// <summary>Centre la vue sur le point flow donné (<c>setCenter</c>) ; <paramref name="zoom" /> <c>null</c> garde le zoom actuel.</summary>
    public void SetCenter(double x, double y, double? zoom = null, TimeSpan duration = default)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var z = ViewportMath.Clamp(zoom ?? Viewport.Zoom, MinZoom, MaxZoom);
        SetViewport(ViewportMath.GetViewportForCenter(x, y, z, ActualWidth, ActualHeight), duration);
    }

    /// <summary>Cadre un rectangle en coordonnées flow (<c>fitBounds</c>).</summary>
    public void FitBounds(Core.Rect bounds, double? padding = null, TimeSpan duration = default,
        double? minZoom = null, double? maxZoom = null)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0 || bounds.Width <= 0 || bounds.Height <= 0) return;
        SetViewport(ViewportMath.GetViewportForBounds(bounds, ActualWidth, ActualHeight, minZoom ?? MinZoom,
            maxZoom ?? MaxZoom, padding ?? FitViewPadding), duration);
    }

    /// <summary>Zoom absolu centré sur la vue (<c>zoomTo</c>).</summary>
    public void ZoomTo(double zoom, TimeSpan duration = default)
    {
        var center = new XYPosition(ActualWidth / 2, ActualHeight / 2);
        SetViewport(ViewportMath.ZoomAt(Viewport, center, zoom, MinZoom, MaxZoom), duration);
    }

    public void ZoomIn(TimeSpan duration) => ZoomTo(Viewport.Zoom * ViewportMath.ZoomStep, duration);
    public void ZoomOut(TimeSpan duration) => ZoomTo(Viewport.Zoom / ViewportMath.ZoomStep, duration);

    /// <summary>Rend visible un rectangle flow en faisant défiler le minimum nécessaire.</summary>
    internal void EnsureVisible(Core.Rect rect, double margin = 30)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var shown = ViewLogic.PanToShow(Viewport, rect, ActualWidth, ActualHeight, margin);
        if (shown != Viewport) Viewport = shown;
    }

    /// <summary>
    ///     Cadre les nodes (<c>fitView</c>) : tous les nodes visibles et mesurés, ou ceux de <see cref="FitViewOptions.NodeIds" />.
    ///     Sans <paramref name="options" />, utilise <see cref="FitViewOptions" /> puis <see cref="FitViewPadding" />.
    ///     Renvoie <c>false</c> s'il n'y a rien à cadrer.
    /// </summary>
    public bool FitView(FitViewOptions? options)
    {
        options ??= FitViewOptions;
        if (ViewFitting.Compute(_nodeIndex.Values, ActualWidth, ActualHeight, MinZoom, MaxZoom, FitViewPadding, options) is not { } viewport) return false;
        SetViewport(viewport, options?.Duration ?? default);
        return true;
    }

    /// <summary>Cadre tous les nodes mesurés dans la vue.</summary>
    public void FitView() => FitView(null);

    #endregion

    #region Recherche et mise à jour

    /// <summary>Node par son id (<c>getNode</c>) ; <c>null</c> s'il n'existe pas.</summary>
    public Node? GetNode(string id) => RawNode(id) ?? GraphQueries.GetNode(id, Nodes);

    public Edge? GetEdge(string id) => GraphQueries.GetEdge(id, Edges);

    /// <summary>Nodes en coordonnées absolues avec leur taille mesurée (<c>getInternalNode</c>), ou <c>null</c>.</summary>
    public Node? GetInternalNode(string id) => _nodeIndex.GetValueOrDefault(id);

    public void AddNodes(IEnumerable<Node> nodes) =>
        ApplyNodeChanges(nodes.Select(n => (NodeChange)new NodeAddChange(n)).ToList());

    /// <summary>Ajoute des edges sans doublon (<c>addEdges</c> + <c>addEdge</c>).</summary>
    public void AddEdges(IEnumerable<Edge> edges)
    {
        var known = Edges;
        var add = new List<EdgeChange>();
        foreach (var edge in edges)
        {
            var next = GraphQueries.AddEdge(edge, known);
            if (ReferenceEquals(next, known)) continue;
            known = next;
            add.Add(new EdgeAddChange(next[^1]));
        }

        ApplyEdgeChanges(add);
    }

    public void UpdateNode(string id, Func<Node, Node> update)
    {
        // L'index n'existe qu'une fois le gabarit appliqué : avant le premier affichage, on lit la liste.
        if ((RawNode(id) ?? Nodes.FirstOrDefault(n => n.Id == id)) is { } node) ApplyNodeChanges([new NodeReplaceChange(id, update(node))]);
    }

    /// <summary>
    ///     Remesure un node et ses handles (<c>useUpdateNodeInternals</c>). La vue le fait seule quand le node change de
    ///     taille ou qu'un handle apparaît, disparaît ou change de propriété. Appelez-la après avoir <b>déplacé</b> des handles
    ///     sans rien de cela (marge ou ordre changé par un déclencheur de style, animation…), sinon les edges restent à
    ///     l'ancienne place. Sans effet sur un node inconnu ou non affiché.
    /// </summary>
    public void UpdateNodeInternals(string id)
    {
        if (!_hosts.TryGetValue(id, out var host)) return;
        host.UpdateLayout();
        host.RequestMeasure();
    }

    /// <summary>Convertit des coordonnées flow en point de la vue, en pixels (inverse de <see cref="ScreenToFlow" />).</summary>
    public Point FlowToScreen(XYPosition position)
    {
        var s = ViewportMath.FlowToScreen(Viewport, position);
        return new Point(s.X, s.Y);
    }

    /// <summary>Centre la vue sur un point flow, sans changer le zoom.</summary>
    public void CenterOn(XYPosition position) => SetCenter(position.X, position.Y);

    /// <summary>Les nodes qui ont un edge vers ce node (<c>getIncomers</c>) ; vide si l'id est inconnu.</summary>
    public IReadOnlyList<Node> GetIncomers(string nodeId) =>
        GetNode(nodeId) is { } node ? GraphQueries.GetIncomers(node, Nodes, Edges) : [];

    /// <summary>Les nodes vers lesquels ce node a un edge (<c>getOutgoers</c>) ; vide si l'id est inconnu.</summary>
    public IReadOnlyList<Node> GetOutgoers(string nodeId) =>
        GetNode(nodeId) is { } node ? GraphQueries.GetOutgoers(node, Nodes, Edges) : [];

    /// <summary>Sélectionne ce seul node, ou ce seul edge ; avec deux <c>null</c>, désélectionne tout.</summary>
    public void SelectOnly(string? nodeId, string? edgeId) =>
        Select(nodeId == null ? [] : [nodeId], edgeId == null ? [] : [edgeId], false);

    /// <summary>
    ///     Supprime les nodes et edges sélectionnés, comme <see cref="DeleteElementsAsync" /> (que la vue soit verrouillée ou
    ///     non, contrairement à la touche Suppr). La tâche est déjà terminée sans <see cref="BeforeDeleteAsync" />.
    /// </summary>
    public Task<bool> DeleteSelected() =>
        DeleteElementsAsync(Nodes.Where(n => n.Selected).Select(n => n.Id).ToList(), Edges.Where(e => e.Selected).Select(e => e.Id).ToList());

    /// <summary>Verrouille ou déverrouille la vue ; une liaison sur <see cref="IsLocked" /> est conservée.</summary>
    public void SetLocked(bool locked) => SetCurrentValue(IsLockedProperty, locked);

    /// <summary>Remplace <see cref="Node.Data" /> (<c>updateNodeData</c>).</summary>
    public void UpdateNodeData(string id, object? data) => UpdateNode(id, n => n with { Data = data });

    public void UpdateNodeData(string id, Func<object?, object?> update) => UpdateNode(id, n => n with { Data = update(n.Data) });

    public void UpdateEdge(string id, Func<Edge, Edge> update)
    {
        if (GetEdge(id) is { } edge) ApplyEdgeChanges([new EdgeReplaceChange(id, update(edge))]);
    }

    public void UpdateEdgeData(string id, object? data) => UpdateEdge(id, e => e with { Data = data });

    public void UpdateEdgeData(string id, Func<object?, object?> update) => UpdateEdge(id, e => e with { Data = update(e.Data) });

    /// <summary>Nodes qui touchent <paramref name="node" /> (<c>getIntersectingNodes</c>), en coordonnées absolues.</summary>
    public IReadOnlyList<Node> GetIntersectingNodes(Node node, bool partially = true) =>
        _nodeIndex.TryGetValue(node.Id, out var abs)
            ? GraphQueries.GetIntersectingNodes(abs, _nodeIndex.Values, partially)
            : [];

    /// <summary>Nodes qui touchent une zone en coordonnées flow.</summary>
    public IReadOnlyList<Node> GetIntersectingNodes(Core.Rect area, bool partially = true) =>
        GraphQueries.GetIntersectingNodes(area, _nodeIndex.Values, partially);

    public bool IsNodeIntersecting(Node node, Core.Rect area, bool partially = true) =>
        GraphQueries.IsNodeIntersecting(_nodeIndex.GetValueOrDefault(node.Id) ?? node, area, partially);

    public IReadOnlyList<GraphQueries.NodeConnection> GetNodeConnections(string nodeId, HandleType? type = null,
        string? handleId = null) => GraphQueries.GetNodeConnections(nodeId, Edges, type, handleId);

    /// <summary>Rectangle englobant des nodes mesurés, en coordonnées absolues (<c>getNodesBounds</c>) ; tous si <paramref name="nodeIds" /> est <c>null</c>.</summary>
    public Core.Rect? GetNodesBounds(IEnumerable<string>? nodeIds = null)
    {
        var ids = nodeIds?.ToHashSet();
        return GraphUtils.GetNodesBounds(_nodeIndex.Values.Where(n => !n.Hidden && (ids == null || ids.Contains(n.Id))));
    }

    #endregion

    #region Suppression

    /// <summary>
    ///     Contrôle asynchrone avant suppression (<c>onBeforeDelete</c> est une promesse dans React Flow) : renvoie <c>false</c>
    ///     pour annuler. Utilisé par <see cref="DeleteElementsAsync" /> et par la touche de suppression ; <see cref="DeleteElements" />
    ///     (synchrone) ne connaît que <see cref="BeforeDelete" />.
    /// </summary>
    public Func<BeforeDeleteEventArgs, Task<bool>>? BeforeDeleteAsync { get; set; }

    private sealed record DeletePlan(HashSet<string> Nodes, HashSet<string> Edges, HashSet<string> EdgesOfNodes, BeforeDeleteEventArgs Args);

    private DeletePlan? PlanDelete(IEnumerable<string>? nodeIds, IEnumerable<string>? edgeIds)
    {
        if (GraphDeletion.Plan(Nodes, Edges, nodeIds, edgeIds) is not { } plan) return null;
        var all = new HashSet<string>(plan.Edges);
        all.UnionWith(plan.EdgesOfNodes);
        var args = new BeforeDeleteEventArgs(Nodes.Where(n => plan.Nodes.Contains(n.Id)).ToList(), Edges.Where(e => all.Contains(e.Id)).ToList());
        return new DeletePlan(plan.Nodes, plan.Edges, plan.EdgesOfNodes, args);
    }

    /// <summary>
    ///     Supprime des nodes et des edges par identifiant (<c>deleteElements</c>) : avec leurs descendants et les edges
    ///     reliés, hors éléments non supprimables, après <see cref="BeforeDelete" />. Renvoie <c>false</c> si rien n'est supprimé.
    /// </summary>
    public bool DeleteElements(IEnumerable<string>? nodeIds, IEnumerable<string>? edgeIds)
    {
        if (PlanDelete(nodeIds, edgeIds) is not { } plan) return false;
        BeforeDelete?.Invoke(this, plan.Args);
        return !plan.Args.Cancel && ExecuteDelete(plan);
    }

    /// <summary>Comme <see cref="DeleteElements" />, en attendant <see cref="BeforeDeleteAsync" /> (puis <see cref="BeforeDelete" />).</summary>
    public async Task<bool> DeleteElementsAsync(IEnumerable<string>? nodeIds, IEnumerable<string>? edgeIds)
    {
        if (PlanDelete(nodeIds, edgeIds) is not { } plan) return false;
        BeforeDelete?.Invoke(this, plan.Args);
        if (plan.Args.Cancel) return false;
        if (BeforeDeleteAsync is { } ask && !await ask(plan.Args)) return false;
        // L'état a pu changer pendant l'attente : on recalcule le plan.
        return PlanDelete(plan.Nodes, plan.Edges) is { } fresh && ExecuteDelete(fresh);
    }

    private bool ExecuteDelete(DeletePlan plan)
    {
        var (nodeSet, edgeSet, byNode) = (plan.Nodes, plan.Edges, plan.EdgesOfNodes);

        // Les commandes du ViewModel, si elles existent, remplacent la suppression automatique de leur partie ;
        // si l'une refuse (CanExecute faux), rien n'est supprimé.
        var nodeElements = ElementsOfNodes(nodeSet);
        var edgeElements = ElementsOfEdges(edgeSet);
        var nodesByCommand = NodesDeleteCommand != null && nodeElements.Count > 0;
        var edgesByCommand = EdgesDeleteCommand != null && edgeElements.Count > 0;
        if (nodesByCommand && !NodesDeleteCommand!.CanExecute(nodeElements)) return false;
        if (edgesByCommand && !EdgesDeleteCommand!.CanExecute(edgeElements)) return false;

        var gone = new DeletedEventArgs(Nodes.Where(n => nodeSet.Contains(n.Id)).ToList(),
            Edges.Where(e => edgeSet.Contains(e.Id) || byNode.Contains(e.Id)).ToList());
        PushHistory();
        if (edgesByCommand) EdgesDeleteCommand!.Execute(edgeElements);
        if (nodesByCommand) NodesDeleteCommand!.Execute(nodeElements);

        // Reste à supprimer par la vue : les edges demandés et ceux des nodes supprimés, puis les nodes.
        var byView = Edges.Where(e => (!edgesByCommand && edgeSet.Contains(e.Id)) ||
                                      (!nodesByCommand && byNode.Contains(e.Id)))
            .Select(e => (EdgeChange)new EdgeRemoveChange(e.Id)).ToList();
        ApplyEdgeChanges(byView);
        if (!nodesByCommand) ApplyNodeChanges(nodeSet.Select(id => (NodeChange)new NodeRemoveChange(id)).ToList());
        Deleted?.Invoke(this, gone);
        return true;
    }

    #endregion

    #region Copier-coller

    // Partagé entre les vues : on peut coller dans un autre FlowView.
    private static ClipboardData? _clipboard;
    private int _pasteCount;

    /// <summary>Vrai si <see cref="Paste" /> a quelque chose à coller.</summary>
    public bool CanPaste => _clipboard != null && !IsLocked;

    /// <summary>Copie les nodes sélectionnés, leurs descendants et les edges qui les relient.</summary>
    public bool Copy()
    {
        if (GraphClipboard.Copy(Nodes, Edges) is not { } data) return false;
        _clipboard = data;
        _pasteCount = 0;
        return true;
    }

    /// <summary>Copie puis supprime la sélection.</summary>
    public bool Cut()
    {
        if (IsLocked || !Copy()) return false;
        DeleteSelection();
        return true;
    }

    /// <summary>Colle le presse-papiers, décalé de 20 px à chaque collage ; les éléments collés deviennent la sélection.</summary>
    public bool Paste()
    {
        if (_clipboard is not { } data || IsLocked) return false;
        _pasteCount++;
        var pasted = GraphClipboard.Paste(data, Nodes, Edges, GraphClipboard.PasteOffset(_pasteCount));
        PushHistory();
        Select([], [], false);
        ApplyNodeChanges(pasted.Nodes.Select(n => (NodeChange)new NodeAddChange(n)).ToList());
        ApplyEdgeChanges(pasted.Edges.Select(e => (EdgeChange)new EdgeAddChange(e)).ToList());
        return true;
    }

    #endregion

    #region Export d'image

    /// <summary>
    ///     Rend le graphe entier (nodes et edges) en bitmap (<c>toPng</c> de l'exemple « Download Image »).
    ///     Avec <see cref="OnlyRenderVisibleElements" />, seuls les éléments déjà créés apparaissent.
    /// </summary>
    public RenderTargetBitmap? RenderToBitmap(double padding = 20, double scale = 1, Brush? background = null)
    {
        if (_viewportGrid == null || GetNodesBounds() is not { } bounds) return null;
        UpdateLayout();
        var area = new System.Windows.Rect(bounds.X - padding, bounds.Y - padding, bounds.Width + 2 * padding, bounds.Height + 2 * padding);
        var width = Math.Max(1, (int)Math.Ceiling(area.Width * scale));
        var height = Math.Max(1, (int)Math.Ceiling(area.Height * scale));

        var saved = _viewportGrid.RenderTransform;
        _viewportGrid.RenderTransform = Transform.Identity;
        try
        {
            // Un nouveau RenderTransform n'est appliqué à l'arbre de rendu qu'au passage de mise en page suivant : sans lui,
            // l'image est prise avec le viewport courant (décalée, tronquée, voire vide).
            _viewportGrid.InvalidateArrange();
            UpdateLayout();
            var brush = new VisualBrush(_viewportGrid)
            {
                Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top,
                ViewboxUnits = BrushMappingMode.Absolute, Viewbox = area,
                ViewportUnits = BrushMappingMode.Absolute, Viewport = new System.Windows.Rect(0, 0, width, height)
            };
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(background ?? ThemeBrush("Flow.Background"), null, new System.Windows.Rect(0, 0, width, height));
                dc.DrawRectangle(brush, null, new System.Windows.Rect(0, 0, width, height));
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            return bitmap;
        }
        finally
        {
            _viewportGrid.RenderTransform = saved;
        }
    }

    /// <summary>Enregistre le graphe en PNG (<see cref="RenderToBitmap" />) ; renvoie <c>false</c> s'il n'y a rien à rendre.</summary>
    public bool SaveImage(string path, double padding = 20, double scale = 1)
    {
        if (RenderToBitmap(padding, scale) is not { } bitmap) return false;
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return true;
    }

    #endregion

    #region Initialisation et erreurs

    private bool _initRaised;
    private readonly HashSet<string> _reportedErrors = new();

    internal void RaiseInitializedIfReady()
    {
        if (_initRaised || ActualWidth <= 0 || ActualHeight <= 0 || _nodeCanvas == null) return;
        _initRaised = true;
        FlowInitialized?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Signale une anomalie une seule fois par <paramref name="key" />.</summary>
    internal void ReportError(string key, string code, string message)
    {
        if (ErrorOccurred == null || !_reportedErrors.Add(key)) return;
        ErrorOccurred(this, new FlowErrorEventArgs(code, message));
    }

    private void UpdateNodesInitialized() =>
        SetNodesInitialized(Nodes.Count > 0 && _nodeIndex.Values.All(n => n.Hidden || n.Rect != null)
                            && (!_virtualized || _hosts.Count > 0));

    #endregion
}
