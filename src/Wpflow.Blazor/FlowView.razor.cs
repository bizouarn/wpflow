using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Wpflow.Blazor;

/// <summary>
///     Éditeur de graphe (équivalent de <c>&lt;ReactFlow&gt;</c>). L'état circule par « changes », comme en React Flow :
///     les interactions produisent des <see cref="NodeChange" />/<see cref="EdgeChange" />, appliqués automatiquement
///     (<see cref="AutoApplyChanges" />) puis publiés par <see cref="NodesChanged" />/<see cref="EdgesChanged" />
///     (utilisez <c>@bind-Nodes</c> / <c>@bind-Edges</c>).
/// </summary>
public partial class FlowView : ComponentBase, IAsyncDisposable
{
    private readonly string _uid = Guid.NewGuid().ToString("N")[..8];
    private readonly Dictionary<string, IReadOnlyList<HandleInfo>> _handles = new();
    private Dictionary<string, Node> _absNodes = new(); // nodes avec leur position absolue (coin haut gauche), sans parent
    private Dictionary<string, Node> _previousAbsNodes = new();
    private HashSet<string>? _elevated; // ancêtres d'un node sélectionné (ZIndexMode.Auto)
    private readonly HashSet<string> _observed = new();
    private Dictionary<string, Node> _nodeIndex = new();
    private Dictionary<string, Node> _previousIndex = new();
    private Dictionary<string, XYPosition> _abs = new();
    private bool _useAbs;
    private Dictionary<string, int> _depth = new();
    private IReadOnlyList<Node>? _indexedNodes;
    private readonly List<Node> _renderNodes = [];
    private readonly HashSet<string> _renderedIds = [];
    private readonly List<string> _staleKeys = [];
    private readonly HashSet<string> _edgeIdSet = [];

    private ElementReference _root;
    private IJSObjectReference? _module;
    private DotNetObjectReference<FlowView>? _self;
    private bool _attached;
    private bool _didFit;
    private bool _inited;
    private bool _nodesInitialized;
    private double _left, _top, _width, _height;
    private bool _space;
    private int _layerRevision;
    private bool _forceLayer; // un changement autre que le viewport : la couche des nodes et des edges est refaite
    private bool _keepsNextRender;// le prochain rendu ne change que le viewport (pan, zoom) : la couche des nodes est conservée
    private string? _announce;
    private readonly SelectionTracker _selectionTracker = new(); // au départ rien de sélectionné : rien à annoncer
    private (bool, bool, bool, bool, bool, bool)? _jsOptions;
    private readonly HashSet<string> _reportedErrors = new();

    [Inject] private IJSRuntime Js { get; set; } = default!;

    // ---------- Données ----------

    [Parameter] public IReadOnlyList<Node> Nodes { get; set; } = Array.Empty<Node>();
    [Parameter] public EventCallback<IReadOnlyList<Node>> NodesChanged { get; set; }
    [Parameter] public IReadOnlyList<Edge> Edges { get; set; } = Array.Empty<Edge>();
    [Parameter] public EventCallback<IReadOnlyList<Edge>> EdgesChanged { get; set; }
    [Parameter] public Viewport Viewport { get; set; } = Viewport.Identity;
    [Parameter] public EventCallback<Viewport> ViewportChanged { get; set; }

    /// <summary>Faux : les changes sont seulement publiés (mode contrôlé), à vous de les appliquer.</summary>
    [Parameter] public bool AutoApplyChanges { get; set; } = true;

    /// <summary>Taille du conteneur, en pixels (0 tant qu'il n'est pas mesuré).</summary>
    public (double Width, double Height) Size => (_width, _height);

    /// <summary>Connexion en cours (<c>useConnection</c>).</summary>
    public ConnectionState CurrentConnection => CurrentConnectionState();

    // ---------- Cycle de vie ----------

    protected override async Task OnParametersSetAsync()
    {
        // Un rendu du parent qui ne change rien garde la couche : ce qui la change est cherché ci-dessous.
        _keepsNextRender = true;
        ConfigureSources();
        var changedInPlace = !SameElements();
        if (changedInPlace) _forceLayer = true;
        if (changedInPlace || !ReferenceEquals(_indexedNodes, Nodes)) RebuildIndex();
        if (_edgeCache.Count > Edges.Count) PruneEdgeCache();
        // Ce qui change le contenu de la couche des nodes et des edges (hors viewport) : elle est alors refaite.
        var signature = HashCode.Combine(HashCode.Combine(Nodes, Edges, NodeTemplate, NodeTemplates, EdgeTemplates, EdgeTypes, EdgeLabelTemplate, MarkerTemplates),
            HashCode.Combine(IsLocked, ZIndexMode, ElevateNodesOnSelect, ElevateEdgesOnSelect, NodeOrigin, DefaultMarkerColor, NodesFocusable, EdgesFocusable),
            HashCode.Combine(DisableKeyboardA11y, NodeAriaLabelProvider, EdgeAriaLabelProvider, EdgesReconnectable, ReconnectRadius, ConnectionLineTemplate, ConnectionLineType),
            HashCode.Combine(OnlyRenderVisibleElements, OnNodeContextMenu.HasDelegate, OnSelectionContextMenu.HasDelegate, OnNodeMouseEnter.HasDelegate,
                OnNodeMouseLeave.HasDelegate, HashCode.Combine(OnEdgeMouseEnter.HasDelegate, OnEdgeMouseLeave.HasDelegate),
                HashCode.Combine(OnEdgeContextMenu.HasDelegate, OnNodeMouseMove.HasDelegate, OnEdgeMouseMove.HasDelegate, EdgeHitWidth,
                    PaneHoverWanted)));
        if (signature != _layerSignature) _forceLayer = true;
        _layerSignature = signature;
        await ApplyBoundSelection();

        // La sélection peut venir des paramètres : en mode contrôlé, c'est l'application qui applique les changes.
        if (!ReferenceEquals(_selectionNodes, Nodes) || !ReferenceEquals(_selectionEdges, Edges))
        {
            (_selectionNodes, _selectionEdges) = (Nodes, Edges);
            await AfterSelectionMaybeChanged();
        }
    }

    private int _layerSignature;
    private IReadOnlyList<Node>? _selectionNodes;
    private IReadOnlyList<Edge>? _selectionEdges;

    /// <summary>Redessine aussi la couche des nodes et des edges (tout ce qui n'est pas un simple pas de pan).</summary>
    private void Refresh()
    {
        _forceLayer = true;
        StateHasChanged();
    }

    /// <summary>Change avec la liste des nodes (contenu, positions, tailles) : la minimap redessine alors ses rectangles.</summary>
    internal int NodesVersion { get; private set; }

    private void RebuildIndex()
    {
        NodesVersion++;
        _indexedNodes = Nodes;
        (_nodeIndex, _previousIndex) = (_previousIndex, _nodeIndex);
        _nodeIndex.Clear();
        (_absNodes, _previousAbsNodes) = (_previousAbsNodes, _absNodes);
        _absNodes.Clear();

        var origin = NodeOrigin;
        var hasTree = false;
        var shifted = origin != XYPosition.Zero;
        for (var i = 0; i < Nodes.Count; i++)
        {
            var n = Nodes[i];
            _nodeIndex[n.Id] = n;
            if (n.ParentId != null) hasTree = true;
            if (n.Origin != null) shifted = true;
        }

        _useAbs = hasTree || shifted;
        _depth = hasTree ? NodeTree.Depths(Nodes) : NoDepth;
        if (!_useAbs)
        {
            _abs.Clear();
            foreach (var n in Nodes) _absNodes[n.Id] = n;
        }
        else
        {
            _abs = NodeConstraints.Absolute(Nodes, origin);
            foreach (var n in Nodes)
            {
                // Sans parent ni décalage, le node est sa propre version absolue ; inchangé à la même place, la copie précédente
                // est gardée. Seuls les nodes qui ont bougé en allouent une nouvelle.
                var position = _abs[n.Id];
                _absNodes[n.Id] = n.ParentId == null && n.Position == position ? n
                    : _previousAbsNodes.GetValueOrDefault(n.Id) is { } was && was.Position == position
                                                                         && ReferenceEquals(_previousIndex.GetValueOrDefault(n.Id), n) ? was
                    : n with { Position = position, ParentId = null };
            }
        }

        if (_styles.Count > Nodes.Count)
        {
            _staleKeys.Clear();
            foreach (var k in _styles.Keys)
                if (!_nodeIndex.ContainsKey(k))
                    _staleKeys.Add(k);
            for (var i = 0; i < _staleKeys.Count; i++) _styles.Remove(_staleKeys[i]);
            _staleKeys.Clear();
        }

        _elevated = NodeZOrder.ElevatedSet(Nodes, ZIndexMode);
    }

    private void PruneEdgeCache()
    {
        _edgeIdSet.Clear();
        for (var i = 0; i < Edges.Count; i++) _edgeIdSet.Add(Edges[i].Id);
        _staleKeys.Clear();
        foreach (var k in _edgeCache.Keys)
            if (!_edgeIdSet.Contains(k))
                _staleKeys.Add(k);
        for (var i = 0; i < _staleKeys.Count; i++) _edgeCache.Remove(_staleKeys[i]);
        _staleKeys.Clear();
        _edgeIdSet.Clear();
    }

    private static readonly Dictionary<string, int> NoDepth = new();

    /// <summary>Levé après chaque rendu : les panneaux (minimap) se rafraîchissent car Blazor saute les composants sans paramètre.</summary>
    private event Action? Rendered;

    /// <summary>
    ///     Appelle <paramref name="rendered" /> après chaque rendu de la vue, jusqu'à ce que le résultat soit libéré
    ///     (<c>Dispose</c> du composant).
    /// </summary>
    internal IDisposable Follow(Action rendered)
    {
        Rendered += rendered;
        return new Following(this, rendered);
    }

    private sealed class Following(FlowView flow, Action rendered) : IDisposable
    {
        public void Dispose() => flow.Rendered -= rendered;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        Rendered?.Invoke();
        await RaiseStateChangeIfNeeded();

        if (firstRender)
        {
            _module = await Js.InvokeAsync<IJSObjectReference>("import", "./_content/Wpflow.Blazor/wpflow.js");
            _self = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("attach", _root, _self);
            _attached = true;
            await PublishController();
        }

        if (!_attached || _module == null) return;

        var options = (PreventScrolling, ZoomOnPinch, ZoomOnScroll, PanOnScroll, DisableKeyboardA11y, OnPaneScroll.HasDelegate);
        if (options != _jsOptions)
        {
            _jsOptions = options;
            await _module.InvokeVoidAsync("setOptions", _root,
                new
                {
                    preventScrolling = PreventScrolling, pinch = ZoomOnPinch, zoomOnScroll = ZoomOnScroll, panOnScroll = PanOnScroll,
                    paneScroll = OnPaneScroll.HasDelegate
                });
        }

        // Les nodes apparus ou disparus du DOM : (dé)branche l'observation de leur taille.
        // Copie : chaque await cède la main, un rendu peut modifier _renderedIds pendant l'énumération.
        foreach (var id in _renderedIds.ToList())
            if (_observed.Add(id))
                await _module.InvokeVoidAsync("observeNode", _root, id);
        if (_observed.Count > _renderedIds.Count)
        {
            foreach (var id in _observed.Where(id => !_renderedIds.Contains(id)).ToList())
            {
                _observed.Remove(id);
                await _module.InvokeVoidAsync("unobserveNode", _root, id);
                if (!_nodeIndex.ContainsKey(id)) _handles.Remove(id);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        StopAutoPan();
        _wheelCts?.Cancel();
        DisposeSources();
        try
        {
            if (_module != null)
            {
                if (_attached) await _module.InvokeVoidAsync("detach", _root);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
        }

        _self?.Dispose();
    }

    // ---------- Callbacks du pont JS ----------

    private double _scaleX = 1, _scaleY = 1;

    /// <summary>Échelle imposée par les parents (<c>transform: scale()</c>), signalée par le pont JS avant la position.</summary>
    [JSInvokable]
    public Task OnRootScale(double scaleX, double scaleY) => InvokeAsync(() =>
    {
        _scaleX = scaleX > 0 ? scaleX : 1;
        _scaleY = scaleY > 0 ? scaleY : 1;
    });

    [JSInvokable]
    public Task OnRootRect(double left, double top, double width, double height) => InvokeAsync(async () =>
    {
        _left = left;
        _top = top;
        var changed = Math.Abs(_width - width) > 0.5 || Math.Abs(_height - height) > 0.5;
        _width = width;
        _height = height;
        if (changed) StateHasChanged();
        if (!_inited && width > 0)
        {
            _inited = true;
            await OnInit.InvokeAsync();
        }

        await TryFitOnInit();
    });

    private readonly Dictionary<string, List<MeasuredHandle>> _partialHandles = new();

    private readonly List<MeasuredNode> _pendingMeasures = [];

    [JSInvokable]
    public Task OnMeasured(List<MeasuredNode> measured) => InvokeAsync(async () =>
    {
        // Une série découpée en plusieurs messages (des dizaines pour un grand graphe) n'est appliquée qu'au dernier : un
        // changement et un rendu par message parcourraient tout le graphe à chaque fois.
        if (measured.Any(m => m.More))
        {
            _pendingMeasures.AddRange(measured);
            return;
        }

        if (_pendingMeasures.Count > 0)
        {
            measured = [.. _pendingMeasures, .. measured];
            _pendingMeasures.Clear();
        }

        var changes = new List<NodeChange>();
        foreach (var m in measured)
        {
            // Un node à très nombreux handles arrive en plusieurs messages : on les réunit avant de les appliquer.
            if (m.Partial)
            {
                if (!_partialHandles.TryGetValue(m.Id, out var pending)) _partialHandles[m.Id] = pending = [];
                pending.AddRange(m.Handles);
                continue;
            }

            if (_partialHandles.Remove(m.Id, out var first)) m.Handles.InsertRange(0, first);
            _handles[m.Id] = m.Handles.Select(h =>
            {
                var type = h.Type == "target" ? HandleType.Target : HandleType.Source;
                return WithExtras(new HandleInfo(h.Id, type, ParsePosition(h.Position), h.X, h.Y, h.Width, h.Height, h.Connectable,
                    h.ConnectableStart, h.ConnectableEnd), Extras(m.Id, h.Id, type));
            }).ToList();
            if (_nodeIndex.TryGetValue(m.Id, out var n)
                && (n.Width is not { } w || n.Height is not { } h || Math.Abs(w - m.Width) > 0.5 || Math.Abs(h - m.Height) > 0.5))
                changes.Add(new NodeDimensionsChange(m.Id, m.Width, m.Height));
        }

        await ApplyNodeChanges(changes);
        Refresh();
        if (!_nodesInitialized && Nodes.Count > 0 && Nodes.All(n => n.Hidden || n.Width != null))
        {
            _nodesInitialized = true;
            await OnNodesInitialized.InvokeAsync();
        }

        await TryFitOnInit();
    });

    private CancellationTokenSource? _wheelCts;
    private bool _wheeling;

    [JSInvokable]
    public Task OnWheel(double clientX, double clientY, double deltaX, double deltaY, bool ctrl, bool shift, bool meta, int deltaMode)
        => InvokeAsync(async () =>
        {
            var k = deltaMode == 1 ? 20 : 1; // lignes -> pixels
            if (OnPaneScroll.HasDelegate)
                await OnPaneScroll.InvokeAsync(new PaneScrollEventArgs(
                    MakePointer(Rel(clientX, clientY), shift, ctrl, false, meta), deltaX * k, deltaY * k));
            var wheel = WheelInput.Resolve(deltaX * k, deltaY * k, Held(ZoomActivationKey, shift, ctrl, false, meta), shift, ZoomOnScroll,
                PanOnScroll, PanOnScrollSpeed, PanOnScrollMode, PreventScrolling, pinch: ctrl || meta);
            switch (wheel.Kind)
            {
                case WheelKind.Zoom:
                    await WheelMove(ViewportMath.ZoomAt(Viewport, Rel(clientX, clientY), Viewport.Zoom * wheel.ZoomFactor, MinZoom, MaxZoom));
                    break;
                case WheelKind.Pan:
                    await WheelMove(ViewportMath.PanBy(Viewport, wheel.PanX, wheel.PanY));
                    break;
            }
        });

    private async Task WheelMove(Viewport v)
    {
        if (!_wheeling)
        {
            _wheeling = true;
            await NotifyKeepingLayer(() => OnMoveStart.InvokeAsync(Viewport));
        }

        await SetViewport(v);
        _wheelCts?.Cancel();
        var cts = _wheelCts = new CancellationTokenSource();
        _ = Task.Delay(150, cts.Token).ContinueWith(async t =>
        {
            if (t.IsCanceled) return;
            await InvokeAsync(async () =>
            {
                _wheeling = false;
                await NotifyKeepingLayer(() => OnMoveEnd.InvokeAsync(Viewport));
            });
        }, TaskScheduler.Default);
    }

    [JSInvokable]
    public Task OnPinch(double clientX, double clientY, double factor, double dx, double dy) => InvokeAsync(async () =>
    {
        if (!ZoomOnPinch) return;
        var p = Rel(clientX, clientY);
        var v = ViewportMath.ZoomAt(Viewport, p, Viewport.Zoom * factor, MinZoom, MaxZoom);
        var pan = Unscale(dx, dy);
        await SetViewport(ViewportMath.PanBy(v, pan.X, pan.Y));
    });

    private async Task TryFitOnInit()
    {
        if (_didFit || !FitViewOnInit || _width <= 0 || Nodes.Count == 0 || Nodes.Any(n => !n.Hidden && n.Width == null)) return;
        _didFit = true;
        await FitView(FitViewOptions);
    }

    // ---------- Coordonnées ----------

    // Pixels de mise en page, relatifs au conteneur : les coordonnées de la page sont ramenées à l'échelle des parents.
    private XYPosition Rel(double clientX, double clientY) => new((clientX - _left) / _scaleX, (clientY - _top) / _scaleY);

    /// <summary>Un déplacement ou une longueur de la page, en pixels de mise en page de la vue.</summary>
    internal XYPosition Unscale(double dx, double dy) => new(dx / _scaleX, dy / _scaleY);

    public XYPosition ScreenToFlow(XYPosition container) => ViewportMath.ScreenToFlow(Viewport, container);

    public XYPosition FlowToScreen(XYPosition flow) => ViewportMath.FlowToScreen(Viewport, flow);

    /// <summary>Convertit des coordonnées de la page (<c>clientX</c>/<c>clientY</c>) en coordonnées flow.</summary>
    public XYPosition ClientToFlow(double clientX, double clientY) => ScreenToFlow(Rel(clientX, clientY));

    internal double ContainerWidth => _width;
    internal double ContainerHeight => _height;

    private FlowPointer MakePointer(XYPosition screen, bool shift = false, bool ctrl = false, bool alt = false, bool meta = false) =>
        new(screen, ScreenToFlow(screen), shift, ctrl, alt, meta);

    private FlowPointer MakePointer(PointerEventArgs e) =>
        MakePointer(Rel(e.ClientX, e.ClientY), e.ShiftKey, e.CtrlKey, e.AltKey, e.MetaKey);

    private static Position ParsePosition(string s) => s switch
    {
        "left" => Position.Left,
        "right" => Position.Right,
        "bottom" => Position.Bottom,
        _ => Position.Top
    };

    // ---------- Rendu ----------

    internal static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private string RootClass => string.Join(' ', new[]
    {
        "wf-flow", Dark ? "wf-dark" : null, IsLocked ? "wf-locked" : null,
        _mode == Mode.Pan && _moved ? "wf-panning" : PanOnDrag ? "wf-pannable" : null, Class
    }.Where(s => s != null));

    /// <summary>
    ///     Virtualisation active dès que la taille de la vue est connue. Avec <see cref="FitViewOnInit" />, elle attend le
    ///     cadrage initial, qui a besoin que tous les nodes soient rendus pour être mesurés.
    /// </summary>
    private bool Virtualizes => OnlyRenderVisibleElements && _width > 0 && (_didFit || !FitViewOnInit);

    /// <summary>Calcule les nodes à rendre (virtualisation) ; appelé une fois par rendu.</summary>
    private List<Node> PrepareRender()
    {
        _renderNodes.Clear();
        _renderedIds.Clear();
        (Rect, Rect)? zones = Virtualizes ? ViewZones() : null;
        foreach (var n in Nodes)
            if (!n.Hidden && (zones is not { } z || NodePresence(n, z) == VirtualizationRule.Presence.Shown))
            {
                _renderNodes.Add(n);
                _renderedIds.Add(n.Id);
            }

        return _renderNodes;
    }

    // Les huit combinaisons, composées une fois : rien à allouer par node et par rendu.
    private static readonly string[] NodeClassNames = Enumerable.Range(0, 8).Select(i =>
        "wf-node" + ((i & 1) != 0 ? " wf-selected" : "") + ((i & 2) != 0 ? " wf-dragging" : "") + ((i & 4) != 0 ? " wf-has-handle" : "")).ToArray();

    internal string NodeClasses(Node n) =>
        NodeClassNames[(n.Selected ? 1 : 0) | (n.Dragging ? 2 : 0) | (n.DragHandle != null ? 4 : 0)];

    // Le style ne dépend que du node, de sa position absolue et de son empilement : il n'est recomposé que s'ils changent.
    private readonly Dictionary<string, (Node Node, XYPosition Position, int ZIndex, string Style)> _styles = new();

    internal string NodeStyle(Node n)
    {
        var p = _useAbs && _abs.TryGetValue(n.Id, out var a) ? a : n.Position;
        var z = NodeZIndex(n);
        if (_styles.TryGetValue(n.Id, out var known) && ReferenceEquals(known.Node, n) && known.Position == p && known.ZIndex == z)
            return known.Style;
        var t = $"translate({F(p.X)}px,{F(p.Y)}px)";
        if (n.Rotation != 0) t += $" rotate({F(n.Rotation)}deg)";
        var s = $"transform:{t};";
        if (n.FixedWidth is { } w) s += $"width:{F(w)}px;";
        if (n.FixedHeight is { } h) s += $"height:{F(h)}px;";
        s += $"z-index:{z};";
        _styles[n.Id] = (n, p, z, s);
        return s;
    }

    private int NodeZIndex(Node n) =>
        NodeZOrder.Compute(n, _depth.GetValueOrDefault(n.Id), ZIndexMode, ElevateNodesOnSelect, _elevated?.Contains(n.Id) == true);

    internal string? NodeLabel(Node n) => n.AriaLabel ?? NodeAriaLabelProvider?.Invoke(n) ?? n.Id;

    private string EdgeLabelText(EdgeView view) =>
        view.Edge.AriaLabel ?? EdgeAriaLabelProvider?.Invoke(view.Edge) ?? view.DefaultLabel;

    internal RenderFragment<Node>? TemplateFor(Node n) =>
        NodeTemplates != null && NodeTemplates.TryGetValue(n.Type, out var t) ? t : NodeTemplate;

    /// <summary>
    ///     Ce dont dépend le dessin d'un edge : l'edge (référence), ses extrémités, ses deux nodes (un modèle d'edge peut les
    ///     lire) et les types. Les autres réglages de la vue sont dans <see cref="EdgeSettings" />.
    /// </summary>
    private readonly record struct EdgeKey(Edge Edge, EdgeEnds Ends, Node SourceNode, Node TargetNode, object? Types, object? Templates,
        string? MarkerColor)
    {
        public bool Equals(EdgeKey other) => ReferenceEquals(Edge, other.Edge) && Ends == other.Ends
                                             && ReferenceEquals(SourceNode, other.SourceNode) && ReferenceEquals(TargetNode, other.TargetNode)
                                             && ReferenceEquals(Types, other.Types) && ReferenceEquals(Templates, other.Templates)
                                             && MarkerColor == other.MarkerColor;

        public override int GetHashCode() => Edge.GetHashCode();
    }

    private readonly Dictionary<string, (EdgeKey Key, EdgeView View)> _edgeCache = new();

    // Les réglages que lit le balisage d'un edge ou de son label : quand l'un change, tous sont redessinés.
    private object EdgeSettings() => (
        (EdgesFocusable, DisableKeyboardA11y, EdgeAriaLabelProvider, EdgeLabelTemplate, EdgeHitWidth),
        (OnEdgeContextMenu.HasDelegate, OnEdgeMouseEnter.HasDelegate, OnEdgeMouseLeave.HasDelegate, OnEdgeMouseMove.HasDelegate, PaneHoverWanted),
        (EdgesReconnectable, ReconnectRadius, IsLocked));

    private object? _edgeSettings;
    private int _edgeStamp;

    /// <param name="Stamp">Neuf à chaque fois que l'edge doit être redessiné (voir <c>FlowLayer</c>).</param>
    private sealed record EdgeView(Edge Edge, EdgeRenderContext Context, string? MarkerStart, string? MarkerEnd,
        RenderFragment<EdgeRenderContext>? Template, string Style, string LabelStyle, int Stamp)
    {
        /// <summary>Nom accessible quand ni l'edge ni la vue n'en donnent.</summary>
        public string DefaultLabel { get; } = $"Edge de {Edge.Source} vers {Edge.Target}";
    }

    // Créés une fois : sans cela, la résolution des extrémités allouerait ses délégués à chaque edge et à chaque rendu.
    private Func<string, Node?>? _rawLookup, _absoluteLookup;
    private Func<string, IReadOnlyList<HandleInfo>?>? _handleLookup;

    private IEnumerable<EdgeView> EdgeViews()
    {
        _rawLookup ??= id => _nodeIndex.GetValueOrDefault(id);
        _absoluteLookup ??= id => _absNodes.GetValueOrDefault(id);
        _handleLookup ??= id => _handles.GetValueOrDefault(id);
        (Rect, Rect)? zones = Virtualizes ? ViewZones() : null;
        _unresolvedEdges.Clear();
        _edgeContexts.Clear();
        var settings = EdgeSettings();
        if (!settings.Equals(_edgeSettings)) _edgeCache.Clear();
        _edgeSettings = settings;
        var needsSort = false;
        for (var i = 0; i < Edges.Count; i++)
        {
            var edge = Edges[i];
            if (edge.ZIndex != null || (ElevateEdgesOnSelect && edge.Selected))
            {
                needsSort = true;
                break;
            }
        }

        IEnumerable<Edge> ordered = needsSort ? Edges.OrderBy(e => EdgeZOrder.Compute(e, ElevateEdgesOnSelect)) : Edges;
        foreach (var e in ordered)
        {
            if (e.Hidden) continue;

            // Résolution partagée avec la vue WPF : handles mesurés, handles annoncés, sinon milieu du côté.
            if (EdgeEndpoints.Resolve(e, _absoluteLookup, _handleLookup, Position.Bottom, Position.Top) is not { } ends)
            {
                if (EdgeEndpoints.MissingNode(e, _rawLookup) is { } missing)
                    ReportError("error008", $"Edge {e.Id} : node {missing} introuvable.");
                _unresolvedEdges.Add(e.Id);
                continue;
            }

            // Un node jamais mesuré a une taille supposée, comme en WPF : sinon les edges des nodes absents resteraient tous.
            if (zones is { } z && EdgePresence(e, z) != VirtualizationRule.Presence.Shown)
                continue;

            // Un edge dont la clé n'a pas changé garde son tracé et n'est pas redessiné (déplacer un node ne refait que les
            // edges de ce node).
            var key = new EdgeKey(e, ends, _absNodes[e.Source], _absNodes[e.Target], EdgeTypes, EdgeTemplates, DefaultMarkerColor);
            if (_edgeCache.TryGetValue(e.Id, out var cached) && cached.Key.Equals(key))
            {
                _edgeContexts[e.Id] = cached.View.Context;
                yield return cached.View;
                continue;
            }

            var geometry = new EdgeGeometry(ends.Source, ends.SourcePosition, ends.Target, ends.TargetPosition, e);
            var provider = EdgeTypes != null && EdgeTypes.TryGetValue(e.Type, out var custom) ? custom : EdgePathProviders.ForType(e.Type);
            var ctx = new EdgeRenderContext(e, provider.GetPath(geometry), geometry, e.Selected);
            _edgeContexts[e.Id] = ctx;
            var view = new EdgeView(e, ctx, MarkerRef(e.MarkerStart, e.MarkerStartKey, e.MarkerStartOptions, e),
                MarkerRef(e.MarkerEnd, e.MarkerEndKey, e.MarkerEndOptions, e),
                EdgeTemplates != null && EdgeTemplates.TryGetValue(e.Type, out var tpl) ? tpl : null,
                EdgeStyle(e), EdgeLabelStyle(e, ctx.Path), ++_edgeStamp);
            _edgeCache[e.Id] = (key, view);
            yield return view;
        }
    }

    private Rect ViewRect(string id) => _absNodes[id].Rect ?? Virtualization.AssumedRect(_absNodes[id]);

    private void ReportError(string code, string message)
    {
        if (!_reportedErrors.Add(code + message)) return;
        if (OnError.HasDelegate) _ = InvokeAsync(() => OnError.InvokeAsync(new FlowError(code, message)));
    }

    // ---------- Marqueurs ----------

    /// <summary>Le dessin de la flèche (viewBox de 20) est plus grand que celui de WPF : 12,5 × 1,6 = 20.</summary>
    private const double MarkerViewScale = 1.6;

    private sealed record MarkerSpec(string Id, EdgeMarker Kind, string? Key, string Color, double Width, double Height,
        double StrokeWidth, double? Orient);

    private string EdgeColor(Edge e, EdgeMarkerOptions? options) =>
        options?.Color ?? DefaultMarkerColor ?? e.Style?.Stroke ?? (e.Selected ? "var(--wf-edge-selected)" : "var(--wf-edge)");

    private readonly Dictionary<(EdgeMarker, string?, string, double, double, double, double?), MarkerSpec> _markerSpecs = new();

    private MarkerSpec? SpecOf(EdgeMarker kind, string? key, EdgeMarkerOptions? options, Edge e)
    {
        if (kind == EdgeMarker.None && string.IsNullOrEmpty(key)) return null;
        var color = EdgeColor(e, options);
        var (scaleX, scaleY) = EdgeMarkerMath.Scale(options);
        var w = EdgeMarkerMath.DefaultSize * scaleX * MarkerViewScale;
        var h = EdgeMarkerMath.DefaultSize * scaleY * MarkerViewScale;
        var sw = EdgeMarkerMath.StrokeWidth(options, 1);
        var normalizedKey = string.IsNullOrEmpty(key) ? null : key;
        var cacheKey = (kind, normalizedKey, color, w, h, sw, options?.Orient);
        if (_markerSpecs.TryGetValue(cacheKey, out var cached)) return cached;
        if (_markerSpecs.Count >= 128) _markerSpecs.Clear();
        var id = $"wf{_uid}-{(normalizedKey == null ? kind.ToString() : "k" + normalizedKey)}-{(uint)HashCode.Combine(color, w, h, sw, options?.Orient)}";
        return _markerSpecs[cacheKey] = new MarkerSpec(id, kind, normalizedKey, color, w, h, sw, options?.Orient);
    }

    private string? MarkerRef(EdgeMarker kind, string? key, EdgeMarkerOptions? options, Edge e) =>
        SpecOf(kind, key, options, e) is { } spec ? $"url(#{spec.Id})" : null;

    private IEnumerable<MarkerSpec> MarkerDefs()
    {
        var seen = new HashSet<string>();
        foreach (var e in Edges)
        {
            if (SpecOf(e.MarkerStart, e.MarkerStartKey, e.MarkerStartOptions, e) is { } start && seen.Add(start.Id))
                yield return start;
            if (SpecOf(e.MarkerEnd, e.MarkerEndKey, e.MarkerEndOptions, e) is { } end && seen.Add(end.Id))
                yield return end;
        }
    }

    private static string EdgeStyle(Edge e)
    {
        if (e.Style == null) return "";
        var sb = new System.Text.StringBuilder();
        if (e.Style.Stroke is { } c) sb.Append("stroke:").Append(c).Append(';');
        if (e.Style.StrokeWidth is { } w) sb.Append("stroke-width:").Append(F(w)).Append(';');
        if (e.Style.DashArray is { Count: > 0 } d)
        {
            sb.Append("stroke-dasharray:");
            for (var i = 0; i < d.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(F(d[i]));
            }

            sb.Append(';');
        }

        return sb.ToString();
    }

    private static string EdgeLabelStyle(Edge edge, EdgePath path)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("left:").Append(F(path.LabelX)).Append("px;top:").Append(F(path.LabelY)).Append("px;");
        if (edge.LabelStyle is { } l)
        {
            if (l.Foreground != null) sb.Append("color:").Append(l.Foreground).Append(';');
            if (!l.ShowBackground) sb.Append("background:transparent;");
            else if (l.Background != null) sb.Append("background:").Append(l.Background).Append(';');
            if (l.FontSize is { } fs) sb.Append("font-size:").Append(F(fs)).Append("px;");
            if (l.Padding is { } p) sb.Append("padding:").Append(F(p)).Append("px;");
            if (l.CornerRadius is { } r) sb.Append("border-radius:").Append(F(r)).Append("px;");
        }

        return sb.ToString();
    }

    // ---------- Fond ----------

    private IEnumerable<(string Id, BackgroundLayer Layer)> BackgroundLayers()
    {
        if (ShowBackground && BackgroundVariant != BackgroundVariant.None)
            yield return ("main", new BackgroundLayer(BackgroundVariant, BackgroundGap, BackgroundColor, BackgroundSize,
                BackgroundLineWidth, BackgroundOffset));
        if (AdditionalBackgrounds != null)
        {
            var i = 0;
            foreach (var l in AdditionalBackgrounds)
                if (l.Variant != BackgroundVariant.None)
                    yield return ("extra" + i++, l);
        }
    }

    // ---------- Minimap ----------

    private int _nodesBoundsVersion = -1;
    private Rect? _nodesBounds;

    private Rect? CachedNodesBounds()
    {
        if (_nodesBoundsVersion != NodesVersion)
        {
            _nodesBoundsVersion = NodesVersion;
            _nodesBounds = GraphUtils.GetNodesBounds(_absNodes.Values);
        }

        return _nodesBounds;
    }

    /// <summary>Zone affichée par la minimap et zone visible (coordonnées flow).</summary>
    internal (Rect View, Rect Visible) MiniMapView(double width, double height)
    {
        var visible = ViewportMath.GetVisibleRect(Viewport, _width, _height);
        var l = MiniMapMath.Compute(CachedNodesBounds(), visible, width, height);
        return (new Rect(l.X, l.Y, l.Width, l.Height), visible);
    }

    /// <summary>Rectangles des nodes pour la minimap (coordonnées flow).</summary>
    internal List<(Node Node, Rect Rect)> MiniMapNodes()
    {
        var nodes = new List<(Node, Rect)>(Nodes.Count);
        foreach (var n in Nodes)
            if (!n.Hidden && n.Width is { } w && n.Height is { } h)
            {
                var p = _useAbs ? _abs[n.Id] : n.Position;
                nodes.Add((n, new Rect(p.X, p.Y, w, h)));
            }

        return nodes;
    }

    /// <summary>Rectangle englobant les nodes sélectionnés, en pixels écran (pour <see cref="SelectionToolbar" />).</summary>
    internal Rect? SelectionScreenBounds(IReadOnlyList<string>? ids = null)
    {
        Rect? bounds = null;
        foreach (var n in Nodes)
        {
            if (!n.Selected || n.Hidden || n.Width is not { } w || n.Height is not { } h) continue;
            if (ids != null && !ids.Contains(n.Id)) continue;
            var p = _useAbs ? _abs[n.Id] : n.Position;
            var r = new Rect(p.X * Viewport.Zoom + Viewport.X, p.Y * Viewport.Zoom + Viewport.Y, w * Viewport.Zoom, h * Viewport.Zoom);
            bounds = bounds is { } b ? Rect.Union(b, r) : r;
        }

        return bounds;
    }

    /// <summary>Vrai quand un fil est dessiné (glisser, reconnexion ou connexion par clics).</summary>
    internal bool ShowsConnection => _from != null && (_mode is Mode.Connect or Mode.Reconnect && _moved || _clickMode);

    /// <summary>Classe CSS du handle selon le fil en cours (pour le style).</summary>
    internal string? HandleState(string nodeId, HandleInfo? handle)
    {
        if (!ShowsConnection || _from is not { } f || handle == null) return null;
        if (f.NodeId == nodeId && f.Handle.Id == handle.Id && f.Handle.Type == handle.Type) return "wf-connecting";
        return _candidate is { } c && c.NodeId == nodeId && c.Handle.Id == handle.Id && c.Handle.Type == handle.Type ? (_candidateValid ? "wf-valid" : "wf-invalid") : null;
    }

    /// <summary>Handle mesuré correspondant à (<paramref name="nodeId" />, <paramref name="handleId" />, <paramref name="type" />).</summary>
    internal HandleInfo? FindHandle(string nodeId, string? handleId, HandleType type)
    {
        var list = _handles.TryGetValue(nodeId, out var h) ? h : null;
        if (list == null && _nodeIndex.TryGetValue(nodeId, out var node) && node.Handles != null) list = node.Handles.ToList();
        return list?.FirstOrDefault(x => x.Type == type && (string.IsNullOrEmpty(handleId) ? string.IsNullOrEmpty(x.Id) : x.Id == handleId))
               ?? (string.IsNullOrEmpty(handleId) ? list?.FirstOrDefault(x => x.Type == type) : null);
    }

    /// <summary>Coin haut gauche d'un élément (pixels page), mesuré par le navigateur.</summary>
    internal async Task<XYPosition> ElementOrigin(ElementReference element)
    {
        if (_module == null) return XYPosition.Zero;
        var r = await _module.InvokeAsync<double[]>("rectOf", element);
        return r is { Length: >= 2 } ? new XYPosition(r[0], r[1]) : XYPosition.Zero;
    }

    /// <summary>Bascule le verrou (cadenas des contrôles).</summary>
    public async Task SetLocked(bool locked)
    {
        IsLocked = locked;
        await IsLockedChanged.InvokeAsync(locked);
        Refresh();
    }

    private readonly Dictionary<(string, string, HandleType), HandleExtras> _handleExtras = new();

    internal void RegisterHandle((string NodeId, string? HandleId, HandleType Type) key, HandleExtras extras)
    {
        _handleExtras[(key.NodeId, key.HandleId ?? "", key.Type)] = extras;
        // Les réglages du composant complètent la mesure déjà faite (le handle peut changer sans changer de taille).
        if (_handles.TryGetValue(key.NodeId, out var list))
            _handles[key.NodeId] = list.Select(h => h.Type == key.Type && (h.Id ?? "") == (key.HandleId ?? "") ? WithExtras(h, extras) : h).ToList();
    }

    private static HandleInfo WithExtras(HandleInfo h, HandleExtras? e) => e == null
        ? h
        : h with
        {
            Connectable = h.Connectable && e.Connectable, ConnectableStart = h.ConnectableStart && e.ConnectableStart,
            ConnectableEnd = h.ConnectableEnd && e.ConnectableEnd, IsValidConnection = e.IsValidConnection ?? h.IsValidConnection
        };

    internal void UnregisterHandle((string NodeId, string? HandleId, HandleType Type) key) =>
        _handleExtras.Remove((key.NodeId, key.HandleId ?? "", key.Type));

    private HandleExtras? Extras(string nodeId, string? handleId, HandleType type) =>
        _handleExtras.TryGetValue((nodeId, handleId ?? "", type), out var e) ? e : null;

    internal Node? GetNodeInternal(string id) => _nodeIndex.TryGetValue(id, out var n) ? n : null;

    internal XYPosition AbsolutePosition(string id) =>
        _useAbs ? (_abs.TryGetValue(id, out var p) ? p : XYPosition.Zero)
        : (_nodeIndex.TryGetValue(id, out var n) ? n.Position : XYPosition.Zero);
}
