using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wpflow.Core;
using Path = System.Windows.Shapes.Path;

namespace Wpflow.Wpf;

public sealed class NodeChangesEventArgs(IReadOnlyList<NodeChange> changes) : EventArgs
{
    public IReadOnlyList<NodeChange> Changes { get; } = changes;
}

public sealed class EdgeChangesEventArgs(IReadOnlyList<EdgeChange> changes) : EventArgs
{
    public IReadOnlyList<EdgeChange> Changes { get; } = changes;
}

public sealed class ConnectEventArgs(Connection connection) : EventArgs
{
    public Connection Connection { get; } = connection;
}

/// <summary>
///     Surface de graphe (équivalent de <c>&lt;ReactFlow&gt;</c>). Le <see cref="ContentControl.Content" /> est un overlay
///     (MiniMap, Controls, titres…) affiché au-dessus du graphe.
///     Par défaut (<see cref="AutoApplyChanges" />) les changements demandés par l'UI sont appliqués à
///     <see cref="Nodes" /> / <see cref="Edges" /> ; passer à <c>false</c> pour un mode « contrôlé ».
/// </summary>
public class FlowView : ContentControl
{
    private enum Mode
    {
        None,
        Pan,
        DragNodes,
        Connect,
        Select
    }

    private sealed record HandleRef(string NodeId, HandleInfo Info);

    #region Dependency properties

    public static readonly DependencyProperty NodesProperty = DependencyProperty.Register(
        nameof(Nodes), typeof(IReadOnlyList<Node>), typeof(FlowView),
        new FrameworkPropertyMetadata(Array.Empty<Node>(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((FlowView)d).OnNodesChanged()));

    public static readonly DependencyProperty EdgesProperty = DependencyProperty.Register(
        nameof(Edges), typeof(IReadOnlyList<Edge>), typeof(FlowView),
        new FrameworkPropertyMetadata(Array.Empty<Edge>(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((FlowView)d).OnEdgesChanged()));

    public static readonly DependencyProperty ViewportProperty = DependencyProperty.Register(
        nameof(Viewport), typeof(Viewport), typeof(FlowView),
        new FrameworkPropertyMetadata(Viewport.Identity, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((FlowView)d).OnViewportChanged(), (d, v) => ((FlowView)d).CoerceViewport((Viewport)v)));

    public static readonly DependencyProperty MinZoomProperty = DependencyProperty.Register(
        nameof(MinZoom), typeof(double), typeof(FlowView), new PropertyMetadata(ViewportMath.DefaultMinZoom));

    public static readonly DependencyProperty MaxZoomProperty = DependencyProperty.Register(
        nameof(MaxZoom), typeof(double), typeof(FlowView), new PropertyMetadata(ViewportMath.DefaultMaxZoom));

    public static readonly DependencyProperty SnapToGridProperty = DependencyProperty.Register(
        nameof(SnapToGrid), typeof(bool), typeof(FlowView), new PropertyMetadata(false));

    public static readonly DependencyProperty SnapGridProperty = DependencyProperty.Register(
        nameof(SnapGrid), typeof(double), typeof(FlowView), new PropertyMetadata(15.0));

    public static readonly DependencyProperty IsLockedProperty = DependencyProperty.Register(
        nameof(IsLocked), typeof(bool), typeof(FlowView), new PropertyMetadata(false, (d, _) =>
            ((FlowView)d).RaiseStateChanged()));

    public static readonly DependencyProperty FitViewOnInitProperty = DependencyProperty.Register(
        nameof(FitViewOnInit), typeof(bool), typeof(FlowView), new PropertyMetadata(false));

    public static readonly DependencyProperty FitViewPaddingProperty = DependencyProperty.Register(
        nameof(FitViewPadding), typeof(double), typeof(FlowView), new PropertyMetadata(0.1));

    public static readonly DependencyProperty ConnectionRadiusProperty = DependencyProperty.Register(
        nameof(ConnectionRadius), typeof(double), typeof(FlowView), new PropertyMetadata(20.0));

    public static readonly DependencyProperty AutoApplyChangesProperty = DependencyProperty.Register(
        nameof(AutoApplyChanges), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    public static readonly DependencyProperty BackgroundVariantProperty = DependencyProperty.Register(
        nameof(BackgroundVariant), typeof(BackgroundVariant), typeof(FlowView),
        new PropertyMetadata(BackgroundVariant.Dots, (d, _) => ((FlowView)d).ConfigureBackground()));

    public static readonly DependencyProperty BackgroundGapProperty = DependencyProperty.Register(
        nameof(BackgroundGap), typeof(double), typeof(FlowView),
        new PropertyMetadata(20.0, (d, _) => ((FlowView)d).ConfigureBackground()));

    /// <summary>Couleur des points/lignes du fond ; <c>null</c> = couleur du thème.</summary>
    public static readonly DependencyProperty BackgroundColorProperty = DependencyProperty.Register(
        nameof(BackgroundColor), typeof(Brush), typeof(FlowView),
        new PropertyMetadata(null, (d, _) => ((FlowView)d).ConfigureBackground()));

    public static readonly DependencyProperty ColorModeProperty = DependencyProperty.Register(
        nameof(ColorMode), typeof(ColorMode), typeof(FlowView),
        new PropertyMetadata(ColorMode.Light, (d, _) => ((FlowView)d).ApplyColorMode()));

    /// <summary>Mode de connexion : <c>Strict</c> (source vers cible) ou <c>Loose</c> (n'importe quel handle).</summary>
    public static readonly DependencyProperty ConnectionModeProperty = DependencyProperty.Register(
        nameof(ConnectionMode), typeof(ConnectionMode), typeof(FlowView), new PropertyMetadata(ConnectionMode.Strict));

    /// <summary>Limites de pan (<c>translateExtent</c>), en coordonnées flow ; <c>null</c> = illimité.</summary>
    public static readonly DependencyProperty TranslateExtentProperty = DependencyProperty.Register(
        nameof(TranslateExtent), typeof(Core.Rect?), typeof(FlowView),
        new PropertyMetadata(null, (d, _) => d.CoerceValue(ViewportProperty)));

    /// <summary>Ne rend que les nodes et edges visibles (<c>onlyRenderVisibleElements</c>) : indispensable au-delà de ~2 000 nodes.</summary>
    public static readonly DependencyProperty OnlyRenderVisibleElementsProperty = DependencyProperty.Register(
        nameof(OnlyRenderVisibleElements), typeof(bool), typeof(FlowView),
        new PropertyMetadata(false, (d, _) => ((FlowView)d).UpdateVisibility()));

    /// <summary>Modèle de contenu des labels d'edges (DataContext = l'<see cref="Edge" />) ; équivalent d'<c>EdgeLabelRenderer</c>.</summary>
    public static readonly DependencyProperty EdgeLabelTemplateProperty = DependencyProperty.Register(
        nameof(EdgeLabelTemplate), typeof(DataTemplate), typeof(FlowView),
        new PropertyMetadata(null, (d, _) => ((FlowView)d)._edgeLayer?.Refresh((FlowView)d)));

    public ColorMode ColorMode
    {
        get => (ColorMode)GetValue(ColorModeProperty);
        set => SetValue(ColorModeProperty, value);
    }

    public ConnectionMode ConnectionMode
    {
        get => (ConnectionMode)GetValue(ConnectionModeProperty);
        set => SetValue(ConnectionModeProperty, value);
    }

    public Core.Rect? TranslateExtent
    {
        get => (Core.Rect?)GetValue(TranslateExtentProperty);
        set => SetValue(TranslateExtentProperty, value);
    }

    public bool OnlyRenderVisibleElements
    {
        get => (bool)GetValue(OnlyRenderVisibleElementsProperty);
        set => SetValue(OnlyRenderVisibleElementsProperty, value);
    }

    public DataTemplate? EdgeLabelTemplate
    {
        get => (DataTemplate?)GetValue(EdgeLabelTemplateProperty);
        set => SetValue(EdgeLabelTemplateProperty, value);
    }

    /// <summary>Validation personnalisée d'une connexion (<c>isValidConnection</c>) ; renvoyer <c>false</c> la refuse.</summary>
    public Func<Connection, bool>? IsValidConnection { get; set; }

    public static readonly DependencyProperty EdgeStrokeProperty = DependencyProperty.Register(
        nameof(EdgeStroke), typeof(Brush), typeof(FlowView),
        new PropertyMetadata(null, (d, _) => ((FlowView)d)._edgeLayer?.Refresh((FlowView)d)));

    public static readonly DependencyProperty EdgeStrokeThicknessProperty = DependencyProperty.Register(
        nameof(EdgeStrokeThickness), typeof(double), typeof(FlowView),
        new PropertyMetadata(1.0, (d, _) => ((FlowView)d)._edgeLayer?.Refresh((FlowView)d)));

    public IReadOnlyList<Node> Nodes
    {
        get => (IReadOnlyList<Node>)GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    public IReadOnlyList<Edge> Edges
    {
        get => (IReadOnlyList<Edge>)GetValue(EdgesProperty);
        set => SetValue(EdgesProperty, value);
    }

    public Viewport Viewport
    {
        get => (Viewport)GetValue(ViewportProperty);
        set => SetValue(ViewportProperty, value);
    }

    public double MinZoom
    {
        get => (double)GetValue(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    public double MaxZoom
    {
        get => (double)GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    public bool SnapToGrid
    {
        get => (bool)GetValue(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    public double SnapGrid
    {
        get => (double)GetValue(SnapGridProperty);
        set => SetValue(SnapGridProperty, value);
    }

    /// <summary>Verrouille déplacement, connexion et sélection des éléments (pan/zoom restent actifs).</summary>
    public bool IsLocked
    {
        get => (bool)GetValue(IsLockedProperty);
        set => SetValue(IsLockedProperty, value);
    }

    public bool FitViewOnInit
    {
        get => (bool)GetValue(FitViewOnInitProperty);
        set => SetValue(FitViewOnInitProperty, value);
    }

    public double FitViewPadding
    {
        get => (double)GetValue(FitViewPaddingProperty);
        set => SetValue(FitViewPaddingProperty, value);
    }

    public double ConnectionRadius
    {
        get => (double)GetValue(ConnectionRadiusProperty);
        set => SetValue(ConnectionRadiusProperty, value);
    }

    public bool AutoApplyChanges
    {
        get => (bool)GetValue(AutoApplyChangesProperty);
        set => SetValue(AutoApplyChangesProperty, value);
    }

    public BackgroundVariant BackgroundVariant
    {
        get => (BackgroundVariant)GetValue(BackgroundVariantProperty);
        set => SetValue(BackgroundVariantProperty, value);
    }

    public double BackgroundGap
    {
        get => (double)GetValue(BackgroundGapProperty);
        set => SetValue(BackgroundGapProperty, value);
    }

    public Brush? BackgroundColor
    {
        get => (Brush?)GetValue(BackgroundColorProperty);
        set => SetValue(BackgroundColorProperty, value);
    }

    public Brush? EdgeStroke
    {
        get => (Brush?)GetValue(EdgeStrokeProperty);
        set => SetValue(EdgeStrokeProperty, value);
    }

    public double EdgeStrokeThickness
    {
        get => (double)GetValue(EdgeStrokeThicknessProperty);
        set => SetValue(EdgeStrokeThicknessProperty, value);
    }

    #endregion

    #region Events

    public event EventHandler<NodeChangesEventArgs>? NodeChangesRequested;
    public event EventHandler<EdgeChangesEventArgs>? EdgeChangesRequested;
    public event EventHandler<ConnectEventArgs>? ConnectRequested;

    /// <summary>Déclenché quand nodes, edges, viewport ou verrouillage changent (utilisé par MiniMap et Controls).</summary>
    public event EventHandler? StateChanged;

    #endregion

    private readonly Dictionary<string, NodeHost> _hosts = new();
    private readonly Dictionary<string, Node> _nodeIndex = new(); // coordonnées absolues
    private readonly Dictionary<string, Node> _rawIndex = new(); // tels que fournis (position relative au parent)
    private bool _virtualized;
    private readonly Dictionary<string, IReadOnlyList<HandleInfo>> _handleMap = new();
    private Canvas? _nodeCanvas;
    private EdgeLayer? _edgeLayer;
    private FlowBackground? _background;
    private Grid? _viewportGrid;
    private Path? _connectionPath;
    private Border? _selectionBox;

    private Mode _mode;
    private Point _mouseStart;
    private Viewport _viewportStart;
    private Dictionary<string, XYPosition> _dragStart = new();
    private HandleRef? _connectFrom;
    private bool _fitPending;
    private Edge? _reconnecting;
    private bool _dragMoved;
    private bool _flushQueued;
    private readonly Dictionary<string, (double W, double H)> _measured = new();
    private readonly List<(IReadOnlyList<Node> Nodes, IReadOnlyList<Edge> Edges)> _undo = new();
    private readonly List<(IReadOnlyList<Node> Nodes, IReadOnlyList<Edge> Edges)> _redo = new();

    /// <summary>Bornes de déplacement des nodes (<c>nodeExtent</c>) ; <c>null</c> = illimité.</summary>
    public static readonly DependencyProperty NodeExtentProperty = DependencyProperty.Register(
        nameof(NodeExtent), typeof(Core.Rect?), typeof(FlowView), new PropertyMetadata(null));

    public Core.Rect? NodeExtent
    {
        get => (Core.Rect?)GetValue(NodeExtentProperty);
        set => SetValue(NodeExtentProperty, value);
    }

    static FlowView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(FlowView), new FrameworkPropertyMetadata(typeof(FlowView)));
    }

    public FlowView()
    {
        Focusable = true;
        AllowDrop = true;
        IsManipulationEnabled = true; // pincement tactile
        ApplyColorMode();
        SizeChanged += (_, _) =>
        {
            CoerceValue(ViewportProperty);
            UpdateVisibility();
            TryFitOnInit();
            RaiseStateChanged();
        };
    }

    /// <summary>Déclenché quand un objet est déposé sur la surface (glisser-déposer depuis une palette).</summary>
    public event EventHandler<FlowDropEventArgs>? DropRequested;

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    protected override void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);
        DropRequested?.Invoke(this, new FlowDropEventArgs(ScreenToFlow(e.GetPosition(this)), e.Data));
        e.Handled = true;
    }

    protected override void OnManipulationStarting(ManipulationStartingEventArgs e)
    {
        e.ManipulationContainer = this;
        e.Mode = ManipulationModes.Scale;
        base.OnManipulationStarting(e);
    }

    // Pincement à deux doigts : zoom centré sur le point de pincement (le déplacement vient des événements souris promus).
    protected override void OnManipulationDelta(ManipulationDeltaEventArgs e)
    {
        base.OnManipulationDelta(e);
        var scale = e.DeltaManipulation.Scale.X;
        if (Math.Abs(scale - 1) < 0.0001) return;
        var o = e.ManipulationOrigin;
        Viewport = ViewportMath.ZoomAt(Viewport, new XYPosition(o.X, o.Y), Viewport.Zoom * scale, MinZoom, MaxZoom);
        e.Handled = true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FlowViewAutomationPeer(this);

    /// <summary>Sélectionne un node (utilisé par l'accessibilité et la navigation clavier).</summary>
    public void SelectNode(string id, bool additive = false) => Select([id], [], additive);

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        EnsureTheme();
        _background = GetTemplateChild("PART_Background") as FlowBackground;
        _viewportGrid = GetTemplateChild("PART_Viewport") as Grid;
        _edgeLayer = GetTemplateChild("PART_Edges") as EdgeLayer;
        _nodeCanvas = GetTemplateChild("PART_Nodes") as Canvas;
        _connectionPath = GetTemplateChild("PART_Connection") as Path;
        _selectionBox = GetTemplateChild("PART_Selection") as Border;

        // Un nouveau gabarit repart d'un canvas vide : les hôtes de l'ancien ne sont plus dans l'arbre.
        _hosts.Clear();
        _handleMap.Clear();
        _measured.Clear();

        _fitPending = FitViewOnInit;
        ConfigureBackground();
        OnViewportChanged();
        OnNodesChanged();
        OnEdgesChanged();
    }

    #region Public API

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Mémorise l'état courant avant une modification structurelle (déplacement, suppression, connexion).</summary>
    internal void PushHistory()
    {
        _undo.Add((Nodes, Edges));
        if (_undo.Count > 100) _undo.RemoveAt(0);
        _redo.Clear();
    }

    public void Undo()
    {
        if (!AutoApplyChanges || _undo.Count == 0) return; // en mode contrôlé, l'historique appartient à l'appelant
        _redo.Add((Nodes, Edges));
        Restore(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
    }

    public void Redo()
    {
        if (!AutoApplyChanges || _redo.Count == 0) return;
        _undo.Add((Nodes, Edges));
        Restore(_redo[^1]);
        _redo.RemoveAt(_redo.Count - 1);
    }

    private void Restore((IReadOnlyList<Node> Nodes, IReadOnlyList<Edge> Edges) state)
    {
        Nodes = state.Nodes;
        Edges = state.Edges;
    }

    public XYPosition ScreenToFlow(Point p) => ViewportMath.ScreenToFlow(Viewport, new XYPosition(p.X, p.Y));

    public void ZoomIn() => ZoomBy(1.2);
    public void ZoomOut() => ZoomBy(1 / 1.2);

    public void ZoomBy(double factor)
    {
        var center = new XYPosition(ActualWidth / 2, ActualHeight / 2);
        Viewport = ViewportMath.ZoomAt(Viewport, center, Viewport.Zoom * factor, MinZoom, MaxZoom);
    }

    /// <summary>Cadre tous les nodes mesurés dans la vue.</summary>
    public void FitView()
    {
        if (GraphUtils.GetNodesBounds(Nodes) is not { } bounds || ActualWidth <= 0 || ActualHeight <= 0) return;
        Viewport = ViewportMath.GetViewportForBounds(bounds, ActualWidth, ActualHeight, MinZoom, MaxZoom,
            FitViewPadding);
    }

    #endregion

    #region Sync with model

    private void OnNodesChanged()
    {
        if (_nodeCanvas == null) return;
        var seen = new HashSet<string>();
        var changed = new HashSet<string>();
        var structural = false;
        var nodes = Nodes;
        var hasTree = nodes.Any(n => n.ParentId != null);
        var abs = hasTree ? NodeTree.AbsolutePositions(nodes) : null;
        var depths = hasTree ? NodeTree.Depths(nodes) : null;
        _nodeIndex.Clear();
        _rawIndex.Clear();
        foreach (var node in nodes)
        {
            seen.Add(node.Id);
            _rawIndex[node.Id] = node;
            // _nodeIndex contient des nodes en coordonnées absolues (edges, handles, minimap).
            var absolute = abs == null ? node : node with { Position = abs[node.Id], ParentId = null };
            _nodeIndex[node.Id] = absolute;
            if (!_hosts.TryGetValue(node.Id, out var host))
            {
                host = new NodeHost(this);
                _hosts[node.Id] = host;
                _nodeCanvas.Children.Add(host);
                structural = true;
            }

            // Les nodes sont immuables : même référence et même position absolue = « rien à refaire ».
            if (ReferenceEquals(host.Node, node) && host.AbsolutePosition == absolute.Position) continue;
            changed.Add(node.Id);
            host.SetNode(node, absolute.Position, depths?.GetValueOrDefault(node.Id) ?? 0);
        }

        if (_hosts.Count != seen.Count)
        {
            foreach (var id in _hosts.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                _nodeCanvas.Children.Remove(_hosts[id]);
                _hosts.Remove(id);
                _handleMap.Remove(id);
            }

            structural = true;
        }

        // Ajout/retrait de node : recalcul complet ; sinon seuls les edges des nodes modifiés (et de leurs descendants).
        if (hasTree && !structural) changed.UnionWith(NodeTree.DescendantsOf(changed.ToList(), nodes));
        _edgeLayer?.Refresh(this, structural ? null : changed);
        UpdateVisibility();
        RaiseStateChanged();
    }

    private void OnEdgesChanged()
    {
        _edgeLayer?.Refresh(this);
        UpdateVisibility();
        RaiseStateChanged();
    }

    private void OnViewportChanged()
    {
        var v = Viewport;
        if (_viewportGrid != null)
            _viewportGrid.RenderTransform = new MatrixTransform(v.Zoom, 0, 0, v.Zoom, v.X, v.Y);
        _background?.SetViewport(v);
        UpdateVisibility();
        RaiseStateChanged();
    }

    private Viewport CoerceViewport(Viewport v)
    {
        if (TranslateExtent is { } extent && ActualWidth > 0 && ActualHeight > 0)
            return ViewportMath.ClampToExtent(v, ActualWidth, ActualHeight, extent);
        return v;
    }

    /// <summary>
    ///     Virtualisation (<see cref="OnlyRenderVisibleElements" />) : masque nodes et edges hors de la zone visible.
    ///     Un node jamais mesuré hors zone reste masqué (donc non mesuré) tant qu'il n'approche pas de la vue.
    /// </summary>
    private void UpdateVisibility()
    {
        if (_nodeCanvas == null) return;
        if (!OnlyRenderVisibleElements)
        {
            if (!_virtualized) return;
            _virtualized = false;
            foreach (var host in _hosts.Values) host.Visibility = Visibility.Visible;
            _edgeLayer?.ApplyVisibility(this, null);
            return;
        }

        _virtualized = true;
        var view = ViewportMath.GetVisibleRect(Viewport, Math.Max(ActualWidth, 1), Math.Max(ActualHeight, 1));
        var margin = 300 / Viewport.Zoom; // marge pour mesurer/afficher un peu avant l'entrée dans la vue
        var inflated = new Core.Rect(view.X - margin, view.Y - margin, view.Width + 2 * margin, view.Height + 2 * margin);
        foreach (var (id, host) in _hosts)
        {
            if (!_nodeIndex.TryGetValue(id, out var n)) continue;
            // Sans mesure, on suppose une taille par défaut pour décider.
            var rect = new Core.Rect(n.Position.X, n.Position.Y, n.Width ?? 200, n.Height ?? 100);
            var visible = n.Selected || rect.Overlaps(inflated);
            var want = visible ? Visibility.Visible : Visibility.Collapsed;
            if (host.Visibility != want) host.Visibility = want;
        }

        _edgeLayer?.ApplyVisibility(this, inflated);
    }

    /// <summary>Nodes en coordonnées absolues (pour MiniMap et composants externes).</summary>
    public IReadOnlyCollection<Node> AbsoluteNodes => _nodeIndex.Values;

    internal Core.Rect? NodeRectOf(string id) => _nodeIndex.TryGetValue(id, out var n) ? n.Rect : null;

    #region Theme

    private static readonly Dictionary<string, Brush> LightFallbacks = new()
    {
        ["Flow.Background"] = Brushes.White,
        ["Flow.Dots"] = new SolidColorBrush(Color.FromRgb(0x91, 0x91, 0x9a)),
        ["Flow.Edge"] = new SolidColorBrush(Color.FromRgb(0xb1, 0xb1, 0xb7)),
        ["Flow.EdgeSelected"] = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
        ["Flow.NodeBackground"] = Brushes.White,
        ["Flow.NodeBorder"] = new SolidColorBrush(Color.FromRgb(0x1a, 0x19, 0x2b)),
        ["Flow.NodeBorderSelected"] = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
        ["Flow.NodeText"] = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)),
        ["Flow.MiniMapBackground"] = Brushes.White,
        ["Flow.MiniMapNode"] = new SolidColorBrush(Color.FromRgb(0xe2, 0xe2, 0xe2)),
        ["Flow.MiniMapNodeSelected"] = new SolidColorBrush(Color.FromRgb(0xb1, 0xb1, 0xb7)),
        ["Flow.GroupBackground"] = new SolidColorBrush(Color.FromArgb(0x14, 0xf0, 0xf0, 0xf0)),
        ["Flow.MiniMapMask"] = new SolidColorBrush(Color.FromArgb(0x99, 0xf0, 0xf0, 0xf0))
    };

    /// <summary>Brosse du thème courant (clé <c>Flow.*</c>), avec repli sur le thème clair.</summary>
    public Brush ThemeBrush(string key) =>
        TryFindResource(key) as Brush ?? LightFallbacks.GetValueOrDefault(key) ?? Brushes.Gray;

    private ResourceDictionary? _themeDictionary;

    private void ApplyColorMode()
    {
        var uri = new Uri(
            $"pack://application:,,,/Wpflow.Wpf;component/Themes/{(ColorMode == ColorMode.Dark ? "Dark" : "Light")}.xaml");
        // On ne retire que notre dictionnaire : les clés Flow.* définies par l'utilisateur dans Resources restent prioritaires.
        if (_themeDictionary != null) Resources.MergedDictionaries.Remove(_themeDictionary);
        _themeDictionary = new ResourceDictionary { Source = uri };
        Resources.MergedDictionaries.Add(_themeDictionary);

        ConfigureBackground();
        foreach (var host in _hosts.Values) host.RefreshTheme();
        _edgeLayer?.Refresh(this);
        RaiseStateChanged();
    }

    // Le XAML peut remplacer FlowView.Resources après le constructeur : on remet alors le thème dans le nouveau dictionnaire.
    private void EnsureTheme()
    {
        if (_themeDictionary == null || !Resources.MergedDictionaries.Contains(_themeDictionary))
        {
            _themeDictionary = null;
            ApplyColorMode();
        }
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        EnsureTheme();
    }

    #endregion

    private void ConfigureBackground() =>
        _background?.Configure(BackgroundVariant, BackgroundGap, BackgroundColor ?? ThemeBrush("Flow.Dots"));

    internal void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Appelé par <see cref="NodeHost" /> après mesure : met à jour dimensions et handles.</summary>
    internal void OnNodeMeasured(NodeHost host, double width, double height)
    {
        if (host.Node is not { } node) return;
        _handleMap[node.Id] = host.Handles;
        _measured[node.Id] = (width, height);

        // Les mesures sont regroupées : une seule mise à jour pour N nodes (sinon coût quadratique au chargement).
        if (_flushQueued) return;
        _flushQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, FlushMeasures);
    }

    private void FlushMeasures()
    {
        _flushQueued = false;
        var measured = _measured.ToList();
        _measured.Clear();

        var changes = measured
            .Where(m => _nodeIndex.TryGetValue(m.Key, out var n) && (n.Width != m.Value.W || n.Height != m.Value.H))
            .Select(m => (NodeChange)new NodeDimensionsChange(m.Key, m.Value.W, m.Value.H))
            .ToList();

        if (changes.Count > 0) ApplyNodeChanges(changes);
        else _edgeLayer?.Refresh(this, measured.Select(m => m.Key).ToHashSet());

        TryFitOnInit();
    }

    private void TryFitOnInit()
    {
        if (!_fitPending || ActualWidth <= 0 || Nodes.Count == 0 || Nodes.Any(n => n.Rect is null)) return;
        _fitPending = false;
        FitView();
    }

    internal (XYPosition Source, Position SourcePos, XYPosition Target, Position TargetPos)? GetEdgeEnds(Edge edge)
    {
        if (!_nodeIndex.TryGetValue(edge.Source, out var sn) || !_nodeIndex.TryGetValue(edge.Target, out var tn))
            return null;
        var s = ResolveHandle(sn, edge.SourceHandle, HandleType.Source, Position.Right);
        var t = ResolveHandle(tn, edge.TargetHandle, HandleType.Target, Position.Left);
        return s is null || t is null ? null : (s.Value.Pos, s.Value.Side, t.Value.Pos, t.Value.Side);
    }

    private (XYPosition Pos, Position Side)? ResolveHandle(Node node, string? handleId, HandleType type,
        Position fallback)
    {
        if (_handleMap.TryGetValue(node.Id, out var list))
        {
            var info = list.FirstOrDefault(h => h.Type == type && (handleId == null || h.Id == handleId));
            if (info != null) return (GraphUtils.GetHandlePosition(node, info.Position, info), info.Position);
        }

        return node.Rect is null ? null : (GraphUtils.GetHandlePosition(node, fallback), fallback);
    }

    #endregion

    #region Changes

    internal void ApplyNodeChanges(IReadOnlyList<NodeChange> changes)
    {
        if (changes.Count == 0) return;
        NodeChangesRequested?.Invoke(this, new NodeChangesEventArgs(changes));
        if (AutoApplyChanges) Nodes = Changes.Apply(changes, Nodes);
    }

    internal void ApplyEdgeChanges(IReadOnlyList<EdgeChange> changes)
    {
        if (changes.Count == 0) return;
        EdgeChangesRequested?.Invoke(this, new EdgeChangesEventArgs(changes));
        if (AutoApplyChanges) Edges = Changes.Apply(changes, Edges);
    }

    private void Select(IEnumerable<string> nodeIds, IEnumerable<string> edgeIds, bool additive)
    {
        var n = nodeIds.ToHashSet();
        var e = edgeIds.ToHashSet();
        ApplyNodeChanges(Nodes
            .Where(x => x.Selectable)
            .Select(x => (x, want: n.Contains(x.Id) || (additive && x.Selected)))
            .Where(p => p.x.Selected != p.want)
            .Select(p => (NodeChange)new NodeSelectChange(p.x.Id, p.want)).ToList());
        ApplyEdgeChanges(Edges
            .Select(x => (x, want: e.Contains(x.Id) || (additive && x.Selected)))
            .Where(p => p.x.Selected != p.want)
            .Select(p => (EdgeChange)new EdgeSelectChange(p.x.Id, p.want)).ToList());
    }

    /// <summary>
    ///     Applique les contraintes de déplacement à une position <b>relative au parent</b> : <c>extent: parent</c>
    ///     puis <see cref="NodeExtent" /> (évalué en coordonnées absolues).
    /// </summary>
    private XYPosition ConstrainPosition(string id, XYPosition relative)
    {
        if (!_rawIndex.TryGetValue(id, out var node)) return relative;
        var parentAbs = XYPosition.Zero;
        if (node.ParentId != null && _nodeIndex.TryGetValue(node.ParentId, out var parent))
        {
            relative = NodeTree.ClampToParent(node, relative, parent);
            parentAbs = parent.Position;
        }

        if (NodeExtent is { } extent)
        {
            var abs = GraphUtils.ClampToExtent(relative + parentAbs, node.Width ?? 0, node.Height ?? 0, extent);
            relative = abs - parentAbs;
        }

        return relative;
    }

    /// <summary>Nodes déplaçables sélectionnés ; un enfant dont un ancêtre est aussi sélectionné suit déjà son parent.</summary>
    private Dictionary<string, XYPosition> CollectDragStart()
    {
        var selected = Nodes.Where(n => n.Selected).Select(n => n.Id).ToHashSet();
        var result = new Dictionary<string, XYPosition>();
        foreach (var n in Nodes)
        {
            if (!n.Selected || !n.Draggable) continue;
            var covered = false;
            for (var p = n.ParentId; p != null && _rawIndex.TryGetValue(p, out var parent); p = parent.ParentId)
                if (selected.Contains(p)) { covered = true; break; }
            if (!covered) result[n.Id] = n.Position;
        }

        return result;
    }

    private void DeleteSelection()
    {
        if (IsLocked) return;
        var nodeIds = Nodes.Where(n => n.Selected).Select(n => n.Id).ToHashSet();
        if (nodeIds.Count == 0 && !Edges.Any(e => e.Selected)) return;
        PushHistory();
        // Supprimer un parent supprime ses descendants.
        nodeIds.UnionWith(NodeTree.DescendantsOf(nodeIds.ToList(), Nodes));
        var edgeChanges = Edges.Where(e => e.Selected || nodeIds.Contains(e.Source) || nodeIds.Contains(e.Target))
            .Select(e => (EdgeChange)new EdgeRemoveChange(e.Id)).ToList();
        ApplyEdgeChanges(edgeChanges);
        ApplyNodeChanges(nodeIds.Select(id => (NodeChange)new NodeRemoveChange(id)).ToList());
    }

    #endregion

    #region Mouse / keyboard

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        var (host, handle, edge) = HitTarget(e.OriginalSource as DependencyObject);
        var pos = e.GetPosition(this);
        var additive = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        _mouseStart = pos;
        _viewportStart = Viewport;

        // Espace maintenu : le glisser déplace la vue, même au-dessus d'un node.
        if (Keyboard.IsKeyDown(Key.Space))
        {
            _mode = Mode.Pan;
            CaptureMouse();
            e.Handled = true;
            return;
        }

        if (handle != null && host?.Node != null && !IsLocked)
        {
            var info = host.Handles.FirstOrDefault(h => h.Id == handle.HandleId && h.Type == handle.Type
                                                         && h.Position == handle.Position);
            if (info != null)
            {
                _connectFrom = new HandleRef(host.Node.Id, info);
                _mode = Mode.Connect;
            }
        }
        else if (host?.Node != null)
        {
            if (IsLocked || !host.Node.Selectable)
            {
                _mode = Mode.Pan;
            }
            else if (host.Node.Selected && additive)
            {
                // Ctrl/Maj + clic sur un node déjà sélectionné : on le désélectionne, sans drag.
                ApplyNodeChanges([new NodeSelectChange(host.Node.Id, false)]);
            }
            else
            {
                if (!host.Node.Selected) Select([host.Node.Id], [], additive);
                _dragStart = CollectDragStart();
                if (_dragStart.Count > 0) _mode = Mode.DragNodes;
            }
        }
        else if (edge != null && !IsLocked)
        {
            Select([], [edge.EdgeId], additive);
            TryBeginReconnect(edge.EdgeId, ScreenToFlow(pos));
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && !IsLocked)
        {
            _mode = Mode.Select;
        }
        else
        {
            if (!additive) Select([], [], false);
            _mode = Mode.Pan;
        }

        if (_mode != Mode.None) CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton != MouseButton.Middle) return;
        Focus();
        _mouseStart = e.GetPosition(this);
        _viewportStart = Viewport;
        _mode = Mode.Pan;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.ChangedButton == MouseButton.Middle && _mode == Mode.Pan) EndMode();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_mode == Mode.None) return;
        var pos = e.GetPosition(this);

        switch (_mode)
        {
            case Mode.Pan:
                Viewport = ViewportMath.PanBy(_viewportStart, pos.X - _mouseStart.X, pos.Y - _mouseStart.Y);
                break;

            case Mode.DragNodes:
            {
                var zoom = Viewport.Zoom;
                var dx = (pos.X - _mouseStart.X) / zoom;
                var dy = (pos.Y - _mouseStart.Y) / zoom;
                if (!_dragMoved && (Math.Abs(dx) + Math.Abs(dy)) * zoom < 1) break;
                if (!_dragMoved)
                {
                    _dragMoved = true;
                    PushHistory();
                }

                ApplyNodeChanges(_dragStart.Select(kv =>
                {
                    var p = new XYPosition(kv.Value.X + dx, kv.Value.Y + dy);
                    if (SnapToGrid) p = ViewportMath.SnapPosition(p, SnapGrid, SnapGrid);
                    return (NodeChange)new NodePositionChange(kv.Key, ConstrainPosition(kv.Key, p));
                }).ToList());
                break;
            }

            case Mode.Connect:
                UpdateConnectionLine(ScreenToFlow(pos));
                break;

            case Mode.Select:
                UpdateSelectionBox(pos);
                break;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_mode == Mode.Connect) FinishConnection(ScreenToFlow(e.GetPosition(this)));
        EndMode();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        EndMode();
    }

    private void EndMode()
    {
        _mode = Mode.None;
        _connectFrom = null;
        _reconnecting = null;
        _dragMoved = false;
        _dragStart = new Dictionary<string, XYPosition>();
        if (_connectionPath != null) _connectionPath.Data = null;
        if (_selectionBox != null) _selectionBox.Visibility = Visibility.Collapsed;
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        var p = e.GetPosition(this);
        var factor = ViewportMath.WheelZoomFactor(-e.Delta);
        Viewport = ViewportMath.ZoomAt(Viewport, new XYPosition(p.X, p.Y), Viewport.Zoom * factor, MinZoom, MaxZoom);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (e.Key is Key.Delete or Key.Back)
        {
            DeleteSelection();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.Z && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            Undo();
            e.Handled = true;
        }
        else if (ctrl && (e.Key == Key.Y || (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))))
        {
            Redo();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.A)
        {
            Select(Nodes.Select(n => n.Id), Edges.Select(x => x.Id), false);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Select([], [], false);
            e.Handled = true;
        }
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            MoveSelectionByKeyboard(e.Key, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
    }

    // Tab / Maj+Tab : sélection du node suivant / précédent (accessibilité clavier).
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key != Key.Tab || Nodes.Count == 0 || IsLocked) return;
        // Si le focus est dans un node (TextBox…), on laisse la navigation standard.
        if (e.OriginalSource is DependencyObject src && FindAncestor<NodeHost>(src) != null) return;

        var back = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var current = Nodes.ToList().FindIndex(n => n.Selected);
        var next = current < 0 ? (back ? Nodes.Count - 1 : 0) : (current + (back ? -1 : 1) + Nodes.Count) % Nodes.Count;
        Select([Nodes[next].Id], [], false);
        e.Handled = true;
    }

    /// <summary>Flèches : déplace les nodes sélectionnés de 5 px (20 avec Maj), comme React Flow.</summary>
    private void MoveSelectionByKeyboard(Key key, bool shift)
    {
        if (IsLocked) return;
        var step = shift ? 20.0 : 5.0;
        var (dx, dy) = key switch
        {
            Key.Left => (-step, 0.0),
            Key.Right => (step, 0.0),
            Key.Up => (0.0, -step),
            _ => (0.0, step)
        };

        var start = CollectDragStart();
        if (start.Count == 0) return;
        PushHistory();
        ApplyNodeChanges(start.Select(kv => (NodeChange)new NodePositionChange(kv.Key,
            ConstrainPosition(kv.Key, new XYPosition(kv.Value.X + dx, kv.Value.Y + dy)))).ToList());
    }

    #endregion

    #region Connection & selection box

    private static Position Opposite(Position p) => p switch
    {
        Position.Left => Position.Right,
        Position.Right => Position.Left,
        Position.Top => Position.Bottom,
        _ => Position.Top
    };

    private void UpdateConnectionLine(XYPosition mouseFlow)
    {
        if (_connectFrom is null || _connectionPath == null || !_nodeIndex.TryGetValue(_connectFrom.NodeId, out var node))
            return;
        var start = GraphUtils.GetHandlePosition(node, _connectFrom.Info.Position, _connectFrom.Info);
        var path = EdgePaths.Bezier(start, _connectFrom.Info.Position, mouseFlow, Opposite(_connectFrom.Info.Position));
        _connectionPath.Data = Geometry.Parse(path.Path);
    }

    private void FinishConnection(XYPosition mouseFlow)
    {
        if (_connectFrom is not { } from) return;
        var candidates = _handleMap
            .Where(kv => kv.Key != from.NodeId && _nodeIndex.ContainsKey(kv.Key))
            .SelectMany(kv => kv.Value
                .Where(h => Connections.AreTypesCompatible(from.Info.Type, h.Type, ConnectionMode))
                .Select(h => (Handle: new HandleRef(kv.Key, h), Position: GraphUtils.GetHandleCenter(_nodeIndex[kv.Key], h))))
            // À distance égale on préfère le handle de type opposé (comme xyflow).
            .OrderBy(c => c.Handle.Info.Type == from.Info.Type ? 1 : 0)
            .ToList();

        var target = GraphUtils.GetClosestHandle(mouseFlow, ConnectionRadius, candidates);
        if (target is null) return;

        var connection = Connections.Create(from.NodeId, from.Info.Id, from.Info.Type, target.NodeId, target.Info.Id);
        if (GraphUtils.ConnectionExists(connection, Edges) || !Connections.IsValid(connection, IsValidConnection)) return;

        if (_reconnecting is { } old)
        {
            // Reconnexion : l'edge garde son identité, seules ses extrémités changent.
            PushHistory();
            ApplyEdgeChanges([new EdgeReplaceChange(old.Id, old with
            {
                Source = connection.Source, Target = connection.Target,
                SourceHandle = connection.SourceHandle, TargetHandle = connection.TargetHandle
            })]);
            return;
        }

        ConnectRequested?.Invoke(this, new ConnectEventArgs(connection));
        if (AutoApplyChanges)
        {
            PushHistory();
            var id = $"xy-edge__{connection.Source}{connection.SourceHandle}-{connection.Target}{connection.TargetHandle}";
            ApplyEdgeChanges([new EdgeAddChange(new Edge(id, connection.Source, connection.Target,
                connection.SourceHandle, connection.TargetHandle))]);
        }
    }

    private HandleRef? FindHandle(string nodeId, string? handleId, HandleType type) =>
        _handleMap.TryGetValue(nodeId, out var list)
            ? list.Where(h => h.Type == type && (handleId == null || h.Id == handleId))
                .Select(h => new HandleRef(nodeId, h)).FirstOrDefault()
            : null;

    /// <summary>Si le clic tombe près d'une extrémité de l'edge, démarre une reconnexion de cette extrémité.</summary>
    private void TryBeginReconnect(string edgeId, XYPosition mouseFlow)
    {
        var edge = Edges.FirstOrDefault(e => e.Id == edgeId);
        if (edge == null || GetEdgeEnds(edge) is not { } ends) return;
        var reach = 24 / Viewport.Zoom;
        double Dist(XYPosition p) => Math.Sqrt(Math.Pow(p.X - mouseFlow.X, 2) + Math.Pow(p.Y - mouseFlow.Y, 2));

        HandleRef? fixedEnd;
        if (Dist(ends.Target) <= reach && Dist(ends.Target) <= Dist(ends.Source))
            fixedEnd = FindHandle(edge.Source, edge.SourceHandle, HandleType.Source); // on déplace la cible
        else if (Dist(ends.Source) <= reach)
            fixedEnd = FindHandle(edge.Target, edge.TargetHandle, HandleType.Target); // on déplace la source
        else
            return;

        if (fixedEnd == null) return;
        _reconnecting = edge;
        _connectFrom = fixedEnd;
        _mode = Mode.Connect;
    }

    private void UpdateSelectionBox(Point pos)
    {
        if (_selectionBox == null) return;
        var x = Math.Min(_mouseStart.X, pos.X);
        var y = Math.Min(_mouseStart.Y, pos.Y);
        var w = Math.Abs(pos.X - _mouseStart.X);
        var h = Math.Abs(pos.Y - _mouseStart.Y);
        _selectionBox.Margin = new Thickness(x, y, 0, 0);
        _selectionBox.Width = w;
        _selectionBox.Height = h;
        _selectionBox.Visibility = Visibility.Visible;

        var a = ScreenToFlow(new Point(x, y));
        var b = ScreenToFlow(new Point(x + w, y + h));
        var rect = new Core.Rect(a.X, a.Y, b.X - a.X, b.Y - a.Y);
        Select(_nodeIndex.Values.Where(n => n.Rect is { } r && r.Overlaps(rect)).Select(n => n.Id), [], false);
    }

    #endregion

    #region Visual tree helpers

    private static DependencyObject? ParentOf(DependencyObject d) =>
        d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);

    internal static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        for (; d != null; d = ParentOf(d))
            if (d is T t) return t;
        return null;
    }

    /// <summary>Un seul parcours vers la racine : le handle (le plus proche) et le node ou l'edge qui le contient.</summary>
    private static (NodeHost? Host, Handle? Handle, EdgeVisual? Edge) HitTarget(DependencyObject? d)
    {
        Handle? handle = null;
        for (; d != null; d = ParentOf(d))
        {
            handle ??= d as Handle;
            if (d is NodeHost host) return (host, handle, null);
            if (d is EdgeVisual edge) return (null, handle, edge);
        }

        return (null, handle, null);
    }

    internal static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in FindDescendants<T>(child)) yield return d;
        }
    }

    #endregion
}
