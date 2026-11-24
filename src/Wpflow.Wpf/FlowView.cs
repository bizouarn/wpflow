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
public partial class FlowView : ContentControl
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
    private readonly Dictionary<string, Node> _rawIndex = new(); // tels que fournis (position relative au parent) ; rempli seulement s'il y a des groupes
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
        InitCommands();
        IsManipulationEnabled = true; // pincement tactile
        ApplyColorMode();
        SizeChanged += (_, _) =>
        {
            CoerceValue(ViewportProperty);
            UpdateVirtualization();
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

        _undo.Add((Nodes, Edges));
        if (_undo.Count > 100) _undo.RemoveAt(0);
        _redo.Clear();
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

        if (!AutoApplyChanges || _undo.Count == 0) return; // en mode contrôlé, l'historique appartient à l'appelant
        _redo.Add((Nodes, Edges));
        Restore(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
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

        if (!AutoApplyChanges || _redo.Count == 0) return;
        _undo.Add((Nodes, Edges));
        Restore(_redo[^1]);
        _redo.RemoveAt(_redo.Count - 1);
        UpdateHistoryState();
    }

    private void Restore((IReadOnlyList<Node> Nodes, IReadOnlyList<Edge> Edges) state)
    {
        var (before, beforeEdges) = (Nodes, Edges);
        Nodes = state.Nodes;
        Edges = state.Edges;
        // Avec une source de ViewModels, les records font foi pour l'historique : on reporte la différence dans les ViewModels.
        _nodeSource?.WriteBack(NodeSourceAdapter.Diff(before, state.Nodes));
        _edgeSource?.WriteBack(EdgeSourceAdapter.Diff(beforeEdges, state.Edges));
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
        var abs = _hasTree ? NodeTree.AbsolutePositions(nodes) : null;
        _depths = _hasTree ? NodeTree.Depths(nodes) : null;
        _virtualized = OnlyRenderVisibleElements;
        ComputeViewRects();

        (_nodeIndex, _previousIndex) = (_previousIndex, _nodeIndex);
        _nodeIndex.Clear();
        if (_hasTree) _rawIndex.Clear(); // sans arbre, _nodeIndex contient déjà les nodes tels que fournis

        var changed = new HashSet<string>();
        var added = false;
        var kept = 0;
        foreach (var node in nodes)
        {
            // _nodeIndex contient des nodes en coordonnées absolues (edges, handles, minimap).
            var absolute = abs == null ? node : node with { Position = abs[node.Id], ParentId = null };
            if (_hasTree) _rawIndex[node.Id] = node;
            _nodeIndex[node.Id] = absolute;

            // Nodes immuables : même référence (ou, avec un arbre, même valeur) = rien n'a changé.
            if (_previousIndex.TryGetValue(node.Id, out var before))
            {
                kept++;
                if (!ReferenceEquals(before, absolute) && !(_hasTree && before == absolute)) changed.Add(node.Id);
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
        RaiseStateChanged();
    }

    private void OnNodesPropertyChanged()
    {
        OnNodesChanged();
        UpdateSelectedNodes();
    }

    private void OnEdgesPropertyChanged()
    {
        OnEdgesChanged();
        UpdateSelectedEdges();
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
        // Affichés : la vue + 300 px (on mesure un peu avant l'entrée). Gardés : + 900 px, pour ne pas recréer un node
        // à chaque va-et-vient du pan. Au-delà, plus aucun élément visuel n'existe.
        _showRect = Inflate(view, 300 / Viewport.Zoom);
        _keepRect = Inflate(view, 900 / Viewport.Zoom);
    }

    private static Core.Rect Inflate(Core.Rect r, double m) => new(r.X - m, r.Y - m, r.Width + 2 * m, r.Height + 2 * m);

    /// <summary>Taille supposée d'un node jamais mesuré, pour décider s'il faut le créer.</summary>
    private static Core.Rect AssumedRect(Node n) => new(n.Position.X, n.Position.Y, n.Width ?? 200, n.Height ?? 100);

    private Presence PresenceOf(Node absolute)
    {
        if (!_virtualized || absolute.Selected) return Presence.Shown;
        var rect = AssumedRect(absolute);
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

        var want = presence == Presence.Shown ? Visibility.Visible : Visibility.Collapsed;
        if (host.Visibility != want) host.Visibility = want;
        if (ReferenceEquals(host.Node, raw) && host.AbsolutePosition == absolute.Position) return;
        host.SetNode(raw, absolute.Position, _depths?.GetValueOrDefault(raw.Id) ?? 0);
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

    private Node? RawNode(string id) => (_hasTree ? _rawIndex : _nodeIndex).GetValueOrDefault(id);

    /// <summary>Nodes en coordonnées absolues (pour MiniMap et composants externes).</summary>
    public IReadOnlyCollection<Node> AbsoluteNodes => _nodeIndex.Values;

    internal Core.Rect? NodeRectOf(string id) => _nodeIndex.TryGetValue(id, out var n) ? n.Rect : null;

    /// <summary>Rectangle d'un node pour la virtualisation des edges : mesuré, sinon supposé.</summary>
    internal Core.Rect? ViewRectOf(string id) => _nodeIndex.TryGetValue(id, out var n) ? n.Rect ?? AssumedRect(n) : null;

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
        if (!AutoApplyChanges) return;
        Nodes = Changes.Apply(changes, Nodes);
        WriteBackNodes(changes);
    }

    internal void ApplyEdgeChanges(IReadOnlyList<EdgeChange> changes)
    {
        if (changes.Count == 0) return;
        EdgeChangesRequested?.Invoke(this, new EdgeChangesEventArgs(changes));
        if (!AutoApplyChanges) return;
        Edges = Changes.Apply(changes, Edges);
        WriteBackEdges(changes);
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
        if (RawNode(id) is not { } node) return relative;
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
            for (var p = n.ParentId; p != null && RawNode(p) is { } parent; p = parent.ParentId)
                if (selected.Contains(p)) { covered = true; break; }
            if (!covered) result[n.Id] = n.Position;
        }

        return result;
    }

    internal void DeleteSelection()
    {
        if (IsLocked) return;
        var nodeIds = Nodes.Where(n => n.Selected).Select(n => n.Id).ToHashSet();
        var selectedEdges = Edges.Where(e => e.Selected).Select(e => e.Id).ToList();
        if (nodeIds.Count == 0 && selectedEdges.Count == 0) return;

        // Supprimer un parent supprime ses descendants.
        nodeIds.UnionWith(NodeTree.DescendantsOf(nodeIds.ToList(), Nodes));

        // Les commandes du ViewModel, si elles existent, remplacent la suppression automatique de leur partie ;
        // si l'une refuse (CanExecute faux), rien n'est supprimé.
        var nodeElements = ElementsOfNodes(nodeIds);
        var edgeElements = ElementsOfEdges(selectedEdges);
        var nodesByCommand = NodesDeleteCommand != null && nodeElements.Count > 0;
        var edgesByCommand = EdgesDeleteCommand != null && edgeElements.Count > 0;
        if (nodesByCommand && !NodesDeleteCommand!.CanExecute(nodeElements)) return;
        if (edgesByCommand && !EdgesDeleteCommand!.CanExecute(edgeElements)) return;

        PushHistory();
        if (edgesByCommand) EdgesDeleteCommand!.Execute(edgeElements);
        if (nodesByCommand) NodesDeleteCommand!.Execute(nodeElements);

        // Reste à supprimer par la vue : les edges sélectionnés et ceux des nodes supprimés, puis les nodes.
        var byView = Edges.Where(e => (!edgesByCommand && e.Selected) ||
                                      (!nodesByCommand && (nodeIds.Contains(e.Source) || nodeIds.Contains(e.Target))))
            .Select(e => (EdgeChange)new EdgeRemoveChange(e.Id)).ToList();
        ApplyEdgeChanges(byView);
        if (!nodesByCommand) ApplyNodeChanges(nodeIds.Select(id => (NodeChange)new NodeRemoveChange(id)).ToList());
    }

    #endregion

    #region Mouse / keyboard

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        var (host, handle, edgeId) = HitTarget(e.OriginalSource as DependencyObject);
        var pos = e.GetPosition(this);
        var additive = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        _mouseStart = pos;
        _viewportStart = Viewport;
        _pressedNode = host?.Node?.Id;
        _pressedEdge = host == null && handle == null ? edgeId : null;
        _pressedPane = host == null && handle == null && edgeId == null;

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
        else if (edgeId != null && !IsLocked)
        {
            Select([], [edgeId], additive);
            TryBeginReconnect(edgeId, ScreenToFlow(pos));
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

        if (_mode == Mode.Connect) SetConnecting(true);
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
        var pos = e.GetPosition(this);
        var moved = Math.Abs(pos.X - _mouseStart.X) + Math.Abs(pos.Y - _mouseStart.Y) >= 4;
        var dragged = _mode == Mode.DragNodes && _dragMoved ? _dragStart.Keys.ToList() : null;
        var (nodeId, edgeId, pane) = (_pressedNode, _pressedEdge, _pressedPane);
        var wasConnect = _mode == Mode.Connect;
        if (wasConnect) FinishConnection(ScreenToFlow(pos));
        EndMode();

        // Commandes du ViewModel, une fois l'état de la vue revenu au repos.
        if (dragged != null) Run(NodeDragStopCommand, ElementsOfNodes(dragged));
        if (wasConnect || moved) return;
        if (nodeId != null) Run(NodeClickCommand, NodeElement(nodeId));
        else if (edgeId != null) Run(EdgeClickCommand, EdgeElement(edgeId));
        else if (pane) Run(PaneClickCommand, ScreenToFlow(pos));
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
        _pressedNode = _pressedEdge = null;
        _pressedPane = false;
        SetConnecting(false);
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

        if (ConnectCommand is { } command)
        {
            if (!command.CanExecute(connection)) return; // le ViewModel refuse (validateur)
            if (_reconnecting == null)
            {
                PushHistory();
                command.Execute(connection); // c'est lui qui ajoute le lien
                return;
            }
        }

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
