using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Wpflow.Core;
using Path = System.Windows.Shapes.Path;

namespace Wpflow.Wpf;

public sealed class NodeChangesEventArgs(IReadOnlyList<NodeChange> changes) : EventArgs
{
    /// <summary>Les changes à appliquer. Un gestionnaire peut les remplacer (filtrer, ajouter) avant leur application : équivalent du middleware <c>useOnNodesChangeMiddleware</c>.</summary>
    public IReadOnlyList<NodeChange> Changes { get; set; } = changes;
}

public sealed class EdgeChangesEventArgs(IReadOnlyList<EdgeChange> changes) : EventArgs
{
    /// <summary>Les changes à appliquer ; un gestionnaire peut les remplacer avant leur application (middleware).</summary>
    public IReadOnlyList<EdgeChange> Changes { get; set; } = changes;
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
public partial class FlowView : ContentControl
{
    private enum Mode
    {
        None,
        Pan,
        DragNodes,
        Connect,
        Select,

        /// <summary>Pression près d'une extrémité d'edge : c'est une reconnexion si la souris s'en éloigne, sinon un clic.</summary>
        ReconnectPending
    }


    #region Dependency properties

    public static readonly DependencyProperty NodesProperty = DependencyProperty.Register(
        nameof(Nodes), typeof(IReadOnlyList<Node>), typeof(FlowView),
        new FrameworkPropertyMetadata(Array.Empty<Node>(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((FlowView)d).OnNodesPropertyChanged()));

    public static readonly DependencyProperty EdgesProperty = DependencyProperty.Register(
        nameof(Edges), typeof(IReadOnlyList<Edge>), typeof(FlowView),
        new FrameworkPropertyMetadata(Array.Empty<Edge>(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((FlowView)d).OnEdgesPropertyChanged()));

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
        new PropertyMetadata(false, (d, _) => ((FlowView)d).UpdateVirtualization()));

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


    public static readonly DependencyProperty EdgeStrokeProperty = DependencyProperty.Register(
        nameof(EdgeStroke), typeof(Brush), typeof(FlowView),
        new PropertyMetadata(null, (d, _) => ((FlowView)d)._edgeLayer?.Refresh((FlowView)d)));

    public static readonly DependencyProperty EdgeStrokeThicknessProperty = DependencyProperty.Register(
        nameof(EdgeStrokeThickness), typeof(double), typeof(FlowView),
        new PropertyMetadata(1.0, (d, _) => ((FlowView)d)._edgeLayer?.Refresh((FlowView)d)));

    /// <summary>Largeur (en pixels écran) de la zone cliquable d'un edge, autour de son tracé.</summary>
    public static readonly DependencyProperty EdgeHitWidthProperty = DependencyProperty.Register(
        nameof(EdgeHitWidth), typeof(double), typeof(FlowView),
        new PropertyMetadata(10.0, (d, _) => ((FlowView)d)._edgeLayer?.Refresh((FlowView)d)));

    /// <summary>Écart entre un node sélectionné (à template personnalisé) et son contour.</summary>
    public static readonly DependencyProperty SelectionOutlineGapProperty = DependencyProperty.Register(
        nameof(SelectionOutlineGap), typeof(double), typeof(FlowView),
        new PropertyMetadata(3.0, (d, _) => ((FlowView)d).InvalidateNodes()));

    public static readonly DependencyProperty SelectionOutlineThicknessProperty = DependencyProperty.Register(
        nameof(SelectionOutlineThickness), typeof(double), typeof(FlowView),
        new PropertyMetadata(1.5, (d, _) => ((FlowView)d).InvalidateNodes()));

    private static readonly Style DefaultConnectionLineStyle = CreateConnectionLineStyle();

    private static Style CreateConnectionLineStyle()
    {
        var style = new Style(typeof(Path));
        style.Setters.Add(new Setter(Shape.StrokeProperty, new SolidColorBrush(Color.FromRgb(0xb1, 0xb1, 0xb7))));
        style.Setters.Add(new Setter(Shape.StrokeThicknessProperty, 1.5));
        style.Seal();
        return style;
    }

    /// <summary>Style (<c>TargetType="Path"</c>) de l'aperçu de la connexion en cours : couleur, épaisseur, pointillés.</summary>
    public static readonly DependencyProperty ConnectionLineStyleProperty = DependencyProperty.Register(
        nameof(ConnectionLineStyle), typeof(Style), typeof(FlowView), new PropertyMetadata(DefaultConnectionLineStyle));

    public double EdgeHitWidth
    {
        get => (double)GetValue(EdgeHitWidthProperty);
        set => SetValue(EdgeHitWidthProperty, value);
    }

    public double SelectionOutlineGap
    {
        get => (double)GetValue(SelectionOutlineGapProperty);
        set => SetValue(SelectionOutlineGapProperty, value);
    }

    public double SelectionOutlineThickness
    {
        get => (double)GetValue(SelectionOutlineThicknessProperty);
        set => SetValue(SelectionOutlineThicknessProperty, value);
    }

    public Style ConnectionLineStyle
    {
        get => (Style)GetValue(ConnectionLineStyleProperty);
        set => SetValue(ConnectionLineStyleProperty, value);
    }

    private void InvalidateNodes()
    {
        foreach (var host in _hosts.Values) host.InvalidateVisual();
    }

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
    private Dictionary<string, Node> _nodeIndex = new(); // coordonnées absolues
    // Tels que fournis (position relative au parent) ; rempli seulement s'il y a des groupes ou une origine.
    private Dictionary<string, Node> _rawIndex = new(), _previousRaw = new();
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
    private string? _pressedNode, _pressedEdge; // sous le bouton enfoncé : sert aux commandes de clic
    private bool _pressedPane;
    private bool _flushQueued;
    private readonly Dictionary<string, (double W, double H)> _measured = new();
    private readonly GraphHistory _history = new();

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
        InitCommands();
        IsManipulationEnabled = true; // pincement tactile
        ApplyColorMode();
        SizeChanged += (_, _) =>
        {
            CoerceValue(ViewportProperty);
            UpdateVirtualization();
            TryFitOnInit();
            RaiseInitializedIfReady();
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
        var args = new FlowDropEventArgs(ScreenToFlow(e.GetPosition(this)), e.Data);
        DropRequested?.Invoke(this, args);
        Run(DropCommand, args);
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
        if (!ZoomOnPinch || Math.Abs(scale - 1) < 0.0001) return;
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
        _portal = GetTemplateChild("PART_Portal") as Canvas;

        // Un nouveau gabarit repart d'un canvas vide : les hôtes de l'ancien ne sont plus dans l'arbre.
        _hosts.Clear();
        _nodeIndex.Clear();
        _previousIndex.Clear();
        _handleMap.Clear();
        _measured.Clear();

        _fitPending = FitViewOnInit;
        ConfigureBackground();
        OnViewportChanged();
        OnNodesChanged();
        OnEdgesChanged();
    }

    #region Public API


    /// <summary>Mémorise l'état courant avant une modification structurelle (déplacement, suppression, connexion).</summary>
    internal void PushHistory()
    {
        if (History is { } history) // historique du ViewModel : il mémorise ce qu'il veut
        {
            history.Checkpoint();
            UpdateHistoryState();
            return;
        }

        // Pas d'historique en mode contrôlé : il appartient à l'application, et la vue ne pourrait pas l'appliquer.
        if (!HistoryEnabled || !AutoApplyChanges) return;
        _history.Push(Nodes, Edges);
        UpdateHistoryState();
    }

    public void Undo()
    {
        if (History is { } history)
        {
            history.Undo();
            UpdateHistoryState();
            return;
        }

        if (!AutoApplyChanges || !_history.TryUndo(Nodes, Edges, out var snapshot)) return; // en mode contrôlé, l'historique appartient à l'appelant
        Restore(snapshot);
        UpdateHistoryState();
    }

    public void Redo()
    {
        if (History is { } history)
        {
            history.Redo();
            UpdateHistoryState();
            return;
        }

        if (!AutoApplyChanges || !_history.TryRedo(Nodes, Edges, out var snapshot)) return;
        Restore(snapshot);
        UpdateHistoryState();
    }

    private void Restore(GraphHistory.Snapshot state)
    {
        var (before, beforeEdges) = (Nodes, Edges);
        Nodes = state.Nodes;
        Edges = state.Edges;
        // Avec une source de ViewModels, les records font foi pour l'historique : on reporte la différence dans les ViewModels.
        _nodeSource?.WriteBack(NodeSourceAdapter.Diff(before, state.Nodes));
        _edgeSource?.WriteBack(EdgeSourceAdapter.Diff(beforeEdges, state.Edges));
    }

    public XYPosition ScreenToFlow(Point p) => ViewportMath.ScreenToFlow(Viewport, new XYPosition(p.X, p.Y));

    public void ZoomIn() => ZoomBy(ViewportMath.ZoomStep);
    public void ZoomOut() => ZoomBy(1 / ViewportMath.ZoomStep);

    public void ZoomBy(double factor)
    {
        var center = new XYPosition(ActualWidth / 2, ActualHeight / 2);
        Viewport = ViewportMath.ZoomAt(Viewport, center, Viewport.Zoom * factor, MinZoom, MaxZoom);
    }

    #endregion

    #region Sync with model

    // Deux index permutés à chaque mise à jour (aucune réallocation) : l'actuel et le précédent (pour détecter ajouts,
    // suppressions et nodes modifiés sans HashSet temporaire).
    private Dictionary<string, Node> _previousIndex = new();
    private Dictionary<string, int>? _depths;
    private bool _hasTree;
    private Core.Rect _showRect, _keepRect;

    private enum Presence
    {
        Absent, // pas d'élément visuel du tout (c'est ce qui économise la mémoire)
        Hidden, // élément gardé mais masqué : évite de le recréer à chaque aller-retour du pan
        Shown
    }

    private void OnNodesChanged()
    {
        if (_nodeCanvas == null) return;
        var nodes = Nodes;
        _hasTree = nodes.Any(n => n.ParentId != null);
        _depths = _hasTree ? NodeTree.Depths(nodes) : null;
        _elevated = NodeZOrder.ElevatedSet(nodes, ZIndexMode);
        var origin = NodeOrigin;
        var shifted = origin != XYPosition.Zero || nodes.Any(n => n.Origin != null); // origine non nulle : positions décalées
        // Avec une origine, le coin haut-gauche d'un enfant part du coin haut-gauche (déjà décalé) de son parent.
        var abs = !_hasTree ? null : shifted ? NodeConstraints.AbsoluteWithOrigin(nodes, origin) : NodeTree.AbsolutePositions(nodes);
        _virtualized = OnlyRenderVisibleElements;
        ComputeViewRects();

        (_nodeIndex, _previousIndex) = (_previousIndex, _nodeIndex);
        _nodeIndex.Clear();
        (_rawIndex, _previousRaw) = (_previousRaw, _rawIndex);
        _rawIndex.Clear();
        _useRaw = _hasTree || shifted; // sans arbre ni origine, _nodeIndex contient déjà les nodes tels que fournis

        var changed = new HashSet<string>();
        var added = false;
        var kept = 0;
        foreach (var node in nodes)
        {
            // _nodeIndex contient des nodes en coordonnées absolues (edges, handles, minimap).
            var existed = _previousIndex.TryGetValue(node.Id, out var before);
            var absolute = node;
            if (_useRaw)
            {
                var position = abs != null ? abs[node.Id] : node.Position - NodeConstraints.OriginOffset(node, origin);
                // Même node qu'au passage précédent, à la même place : la copie absolue est gardée (aucune allocation).
                absolute = existed && before!.Position == position && ReferenceEquals(_previousRaw.GetValueOrDefault(node.Id), node)
                    ? before
                    : node with { Position = position, ParentId = abs != null ? null : node.ParentId };
                _rawIndex[node.Id] = node;
            }

            _nodeIndex[node.Id] = absolute;

            // Nodes immuables : même référence (ou, avec un arbre ou une origine, même valeur) = rien n'a changé.
            if (existed)
            {
                kept++;
                if (!ReferenceEquals(before, absolute) && !((_hasTree || shifted) && before == absolute)) changed.Add(node.Id);
            }
            else
            {
                added = true;
                changed.Add(node.Id);
            }

            SyncHost(node, absolute);
        }

        var removed = kept != _previousIndex.Count;
        if (removed)
        {
            foreach (var id in _hosts.Keys.Where(k => !_nodeIndex.ContainsKey(k)).ToList())
            {
                _nodeCanvas.Children.Remove(_hosts[id]);
                _hosts.Remove(id);
            }

            foreach (var id in _handleMap.Keys.Where(k => !_nodeIndex.ContainsKey(k)).ToList()) _handleMap.Remove(id);
        }

        // Ajout/retrait de node : recalcul complet ; sinon seuls les edges des nodes modifiés (et de leurs descendants).
        var structural = added || removed;
        if (_hasTree && !structural) changed.UnionWith(NodeTree.DescendantsOf(changed.ToList(), nodes));
        RefreshEdges(structural ? null : changed);
        UpdateNodesInitialized();
        RaiseStateChanged();
    }

    private void OnNodesPropertyChanged()
    {
        OnNodesChanged();
        UpdateSelectedNodes();
        RaiseSelectionChangedIfNeeded();
    }

    private void OnEdgesPropertyChanged()
    {
        OnEdgesChanged();
        UpdateSelectedEdges();
        RaiseSelectionChangedIfNeeded();
    }

    private void OnEdgesChanged()
    {
        RefreshEdges(null);
        RaiseStateChanged();
    }

    private void RefreshEdges(IReadOnlySet<string>? touchedNodes) =>
        _edgeLayer?.Refresh(this, touchedNodes, _virtualized ? _showRect : null, _keepRect);

    private void OnViewportChanged()
    {
        var v = Viewport;
        if (_viewportGrid != null)
            _viewportGrid.RenderTransform = new MatrixTransform(v.Zoom, 0, 0, v.Zoom, v.X, v.Y);
        _background?.SetViewport(v);
        UpdateVirtualization();
        SyncZoom();
        ViewportMoved?.Invoke(this, new ViewportMoveEventArgs(v));
        RaiseStateChanged();
    }

    private Viewport CoerceViewport(Viewport v)
    {
        if (TranslateExtent is { } extent && ActualWidth > 0 && ActualHeight > 0)
            return ViewportMath.ClampToExtent(v, ActualWidth, ActualHeight, extent);
        return v;
    }

    private void ComputeViewRects()
    {
        if (!_virtualized) return;
        var view = ViewportMath.GetVisibleRect(Viewport, Math.Max(ActualWidth, 1), Math.Max(ActualHeight, 1));
        (_showRect, _keepRect) = Virtualization.Zones(view, Viewport.Zoom);
    }

    private Presence PresenceOf(Node absolute)
    {
        if (!_virtualized || absolute.Selected) return Presence.Shown;
        var rect = Virtualization.AssumedRect(absolute);
        return rect.Overlaps(_showRect) ? Presence.Shown : rect.Overlaps(_keepRect) ? Presence.Hidden : Presence.Absent;
    }

    /// <summary>
    ///     Met l'élément visuel d'un node en accord avec son état (créé, masqué, supprimé, à jour).
    ///     Seul le mode virtualisé produit <see cref="Presence.Hidden" /> et <see cref="Presence.Absent" />.
    /// </summary>
    private void SyncHost(Node raw, Node absolute)
    {
        var presence = PresenceOf(absolute);
        if (!_hosts.TryGetValue(raw.Id, out var host))
        {
            if (presence == Presence.Absent) return;
            host = new NodeHost(this);
            _hosts[raw.Id] = host;
            _nodeCanvas!.Children.Add(host);
        }
        else if (presence == Presence.Absent)
        {
            _nodeCanvas!.Children.Remove(host);
            _hosts.Remove(raw.Id);
            return;
        }

        var want = presence == Presence.Shown && !raw.Hidden ? Visibility.Visible : Visibility.Collapsed;
        if (host.Visibility != want) host.Visibility = want;
        var elevated = _elevated?.Contains(raw.Id) == true;
        if (ReferenceEquals(host.Node, raw) && host.AbsolutePosition == absolute.Position && host.Elevated == elevated) return;
        host.SetNode(raw, absolute.Position, _depths?.GetValueOrDefault(raw.Id) ?? 0, elevated);
    }

    /// <summary>
    ///     Virtualisation (<see cref="OnlyRenderVisibleElements" />) : seuls les nodes et edges proches de la vue ont un
    ///     élément visuel. Appelé quand la vue ou le mode changent. Un node jamais mesuré hors zone n'est pas créé, donc
    ///     pas mesuré, tant qu'il n'approche pas.
    /// </summary>
    private void UpdateVirtualization()
    {
        if (_nodeCanvas == null || (!OnlyRenderVisibleElements && !_virtualized)) return;
        _virtualized = OnlyRenderVisibleElements;
        ComputeViewRects();
        foreach (var absolute in _nodeIndex.Values) SyncHost(RawNode(absolute.Id) ?? absolute, absolute);
        // Aucun node n'a changé : on ne crée que les edges qui deviennent proches, et on retire ceux qui s'éloignent.
        RefreshEdges(_virtualized ? EmptySet : null);
    }

    private static readonly IReadOnlySet<string> EmptySet = new HashSet<string>();

    private bool _useRaw;
    private HashSet<string>? _elevated; // ancêtres d'un node sélectionné (ZIndexMode.Auto)

    private Node? RawNode(string id) => (_useRaw ? _rawIndex : _nodeIndex).GetValueOrDefault(id);

    /// <summary>Nodes en coordonnées absolues (pour MiniMap et composants externes).</summary>
    public IEnumerable<Node> AbsoluteNodes => _nodeIndex.Values.Where(n => !n.Hidden);

    internal Core.Rect? NodeRectOf(string id) => _nodeIndex.TryGetValue(id, out var n) ? n.Rect : null;

    /// <summary>Rectangle d'un node pour la virtualisation des edges : mesuré, sinon supposé.</summary>
    internal Core.Rect? ViewRectOf(string id) => _nodeIndex.TryGetValue(id, out var n) ? n.Rect ?? Virtualization.AssumedRect(n) : null;

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
        UpdateNodesInitialized();
        RaiseInitializedIfReady();
    }

    private void TryFitOnInit()
    {
        if (!_fitPending || ActualWidth <= 0 || Nodes.Count == 0 || Nodes.Any(n => !n.Hidden && n.Rect is null)) return;
        _fitPending = false;
        FitView(null);
    }

    /// <summary>Extrémités d'un edge (point d'attache et côté du handle) ; la résolution est partagée avec Blazor (<see cref="EdgeEndpoints" />).</summary>
    internal (XYPosition Source, Position SourcePos, XYPosition Target, Position TargetPos)? GetEdgeEnds(Edge edge)
    {
        if (!edge.Hidden && Nodes.Count > 0 && EdgeEndpoints.MissingNode(edge, id => _nodeIndex.GetValueOrDefault(id)) is { } missing)
            ReportError("node:" + edge.Id, "edge-node-missing", $"Edge {edge.Id} : node {missing} introuvable.");
        return EdgeEndpoints.Resolve(edge, id => _nodeIndex.GetValueOrDefault(id), id => _handleMap.GetValueOrDefault(id), Position.Right, Position.Left)
            is { } e
            ? (e.Source, e.SourcePosition, e.Target, e.TargetPosition)
            : null;
    }

    #endregion

    #region Changes

    /// <summary>
    ///     Injecte des changes de nodes, comme le ferait un geste : <see cref="NodeChangesRequested" /> est levé, puis ils sont
    ///     appliqués (et reportés dans les ViewModels) si <see cref="AutoApplyChanges" /> est vrai. N'ajoute rien à l'historique.
    /// </summary>
    public void ApplyNodeChanges(IReadOnlyList<NodeChange> changes)
    {
        if (changes.Count == 0) return;
        var args = new NodeChangesEventArgs(changes);
        NodeChangesRequested?.Invoke(this, args);
        changes = args.Changes; // un gestionnaire peut avoir remplacé les changes (middleware)
        if (Debug) System.Diagnostics.Trace.WriteLine($"[wpflow] {changes.Count} change(s) de nodes : {string.Join(", ", changes.Select(c => c.GetType().Name))}");
        if (!AutoApplyChanges || changes.Count == 0) return;
        Nodes = Changes.Apply(changes, Nodes);
        _nodeSource?.WriteBack(changes); // un ViewModel qui ajuste ou refuse une valeur est relu
        if (AnnounceSelection)
            foreach (var c in changes.OfType<NodeSelectChange>().Where(c => c.Selected))
                if (Nodes.FirstOrDefault(x => x.Id == c.Id) is { } picked)
                    Announce(SelectionOps.AnnounceNode(picked, NodeAriaLabelProvider));
    }

    /// <summary>Comme <see cref="ApplyNodeChanges" />, pour les edges.</summary>
    public void ApplyEdgeChanges(IReadOnlyList<EdgeChange> changes)
    {
        if (changes.Count == 0) return;
        var args = new EdgeChangesEventArgs(changes);
        EdgeChangesRequested?.Invoke(this, args);
        changes = args.Changes; // un gestionnaire peut avoir remplacé les changes (middleware)
        if (Debug) System.Diagnostics.Trace.WriteLine($"[wpflow] {changes.Count} change(s) d'edges : {string.Join(", ", changes.Select(c => c.GetType().Name))}");
        if (!AutoApplyChanges || changes.Count == 0) return;
        Edges = Changes.Apply(changes, Edges);
        _edgeSource?.WriteBack(changes);
        if (AnnounceSelection)
            foreach (var c in changes.OfType<EdgeSelectChange>().Where(c => c.Selected))
                Announce(SelectionOps.AnnounceEdge(Edges.First(x => x.Id == c.Id), EdgeAriaLabelProvider));
    }

    private void Select(IEnumerable<string> nodeIds, IEnumerable<string> edgeIds, bool additive)
    {
        // ElementsSelectable faux : on ne peut plus que désélectionner.
        var (nodeChanges, edgeChanges) = SelectionOps.Select(Nodes, Edges, nodeIds, edgeIds, additive, ElementsSelectable);
        ApplyNodeChanges(nodeChanges);
        ApplyEdgeChanges(edgeChanges);
    }

    /// <summary>
    ///     Applique les contraintes de déplacement à une position <b>relative au parent</b> : <c>extent: parent</c>
    ///     puis <see cref="NodeExtent" /> (évalué en coordonnées absolues).
    /// </summary>
    private XYPosition ConstrainPosition(string id, XYPosition relative)
    {
        if (RawNode(id) is not { } node) return relative;
        var parent = node.ParentId != null ? _nodeIndex.GetValueOrDefault(node.ParentId) : null;
        return NodeConstraints.Constrain(node, relative, parent, NodeExtent, NodeOrigin);
    }

    /// <summary>Nodes déplaçables sélectionnés ; un enfant dont un ancêtre est aussi sélectionné suit déjà son parent.</summary>
    private Dictionary<string, XYPosition> CollectDragStart(string? pressedId = null, bool additive = false) =>
        DragSelection.CollectStart(Nodes, NodesDraggable, ElementsSelectable, SelectNodesOnDrag, pressedId, additive);

    internal void DeleteSelection()
    {
        if (IsLocked) return;
        var nodeIds = Nodes.Where(n => n.Selected).Select(n => n.Id).ToList();
        var edgeIds = Edges.Where(e => e.Selected).Select(e => e.Id).ToList();
        if (BeforeDeleteAsync != null) _ = DeleteElementsAsync(nodeIds, edgeIds); // la suppression suit la réponse du contrôle asynchrone
        else DeleteElements(nodeIds, edgeIds);
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
    private static (NodeHost? Host, Handle? Handle, string? EdgeId) HitTarget(DependencyObject? d)
    {
        Handle? handle = null;
        for (; d != null; d = ParentOf(d))
        {
            handle ??= d as Handle;
            if (d is NodeHost host) return (host, handle, null);
            if (d is IEdgePart edge) return (null, handle, edge.EdgeId);
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
