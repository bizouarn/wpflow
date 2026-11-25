using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Wpflow.Core;

namespace Wpflow.Wpf;

public enum FlowSelectionMode
{
    /// <summary>Un node est sélectionné dès que le rectangle le touche (<c>SelectionMode.Partial</c>).</summary>
    Partial,

    /// <summary>Le rectangle doit contenir le node entier (<c>SelectionMode.Full</c>).</summary>
    Full
}

public enum PanOnScrollMode
{
    Free,
    Vertical,
    Horizontal
}

/// <summary>Forme de l'aperçu d'une connexion en cours (<c>connectionLineType</c>).</summary>
public enum ConnectionLineType
{
    Bezier,
    Straight,
    Step,
    SmoothStep,
    SimpleBezier
}

/// <summary>État visuel d'un <see cref="Handle" /> pendant qu'un fil est tiré.</summary>
public enum HandleConnectionStatus
{
    None,

    /// <summary>Le fil part de ce handle.</summary>
    Origin,

    /// <summary>Le fil, relâché ici, créerait une connexion valide.</summary>
    Valid,

    /// <summary>Le fil est près de ce handle mais la connexion serait refusée.</summary>
    Invalid
}

/// <summary>
///     Options de cadrage (<c>fitViewOptions</c>) : marge, bornes de zoom, sous-ensemble de nodes et durée de la transition
///     (zéro = immédiat).
/// </summary>
public sealed record FitViewOptions(double? Padding = null, double? MinZoom = null, double? MaxZoom = null,
    TimeSpan Duration = default, IReadOnlyList<string>? NodeIds = null, bool IncludeHiddenNodes = false);

/// <summary>Empilement des nodes (<c>zIndexMode</c>).</summary>
/// <summary>Boutons de souris (combinables) qui déplacent la vue (<c>panOnDrag</c> accepte une liste de boutons).</summary>
[Flags]
public enum FlowMouseButtons
{
    None = 0,
    Left = 1,
    Middle = 2,
    Right = 4
}

public enum FlowZIndexMode
{
    /// <summary>Profondeur, plus le premier plan pour les nodes sélectionnés (selon <c>ElevateNodesOnSelect</c>).</summary>
    Basic,

    /// <summary>Comme <c>Basic</c>, et un parent dont un descendant est sélectionné passe aussi devant ses voisins.</summary>
    Auto,

    /// <summary>Seul <c>Node.ZIndex</c> compte : ni profondeur ni élévation à la sélection.</summary>
    Manual
}

public sealed class ViewportMoveEventArgs(Viewport viewport) : EventArgs
{
    public Viewport Viewport { get; } = viewport;
}

/// <summary>Déplacement de nodes : <see cref="FlowView.NodeDragStarted" />, <see cref="FlowView.NodeDragging" />, <see cref="FlowView.NodeDragStopped" />.</summary>
public sealed class NodeDragEventArgs(IReadOnlyList<string> nodeIds, IReadOnlyList<object> elements, XYPosition position) : EventArgs
{
    public IReadOnlyList<string> NodeIds { get; } = nodeIds;

    /// <summary>Les ViewModels (avec une source) ou les <see cref="Node" />.</summary>
    public IReadOnlyList<object> Elements { get; } = elements;

    /// <summary>Position de la souris en coordonnées flow.</summary>
    public XYPosition Position { get; } = position;
}

/// <summary>Suppression imminente (<c>onBeforeDelete</c>) : mettre <see cref="Cancel" /> à vrai l'annule.</summary>
public sealed class BeforeDeleteEventArgs(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges) : EventArgs
{
    public IReadOnlyList<Node> Nodes { get; } = nodes;
    public IReadOnlyList<Edge> Edges { get; } = edges;
    public bool Cancel { get; set; }
}

public sealed class FlowErrorEventArgs(string code, string message) : EventArgs
{
    /// <summary>Code stable : <c>edge-node-missing</c>, <c>edge-handle-missing</c>.</summary>
    public string Code { get; } = code;

    public string Message { get; } = message;
}

public sealed class ReconnectEventArgs(Edge oldEdge, Connection connection) : EventArgs
{
    public Edge OldEdge { get; } = oldEdge;
    public Connection Connection { get; } = connection;
}

/// <summary>Reconnexion d'un edge terminée (<c>onReconnectEnd</c>) : <see cref="Connection" /> est <c>null</c> si l'extrémité a été relâchée sans nouvelle connexion.</summary>
public sealed class ReconnectEndEventArgs(Edge edge, Connection? connection, XYPosition position) : EventArgs
{
    public Edge Edge { get; } = edge;
    public Connection? Connection { get; } = connection;
    public XYPosition Position { get; } = position;
    public bool Succeeded => Connection != null;
}

/// <summary>Sélection par rectangle : début et fin.</summary>
public sealed class SelectionBoxEventArgs(Core.Rect area) : EventArgs
{
    /// <summary>Zone en coordonnées flow.</summary>
    public Core.Rect Area { get; } = area;
}

/// <summary>État de la connexion en cours (<c>useConnection</c>), lisible par <see cref="FlowView.ConnectionState" />.</summary>
public sealed record FlowConnectionState(string FromNodeId, string? FromHandleId, HandleType FromHandleType,
    Position FromPosition, XYPosition From, XYPosition To, string? ToNodeId, string? ToHandleId, bool? IsValid,
    bool IsReconnection);

/// <summary>
///     Contexte d'un aperçu de connexion rendu par <see cref="FlowView.ConnectionLineTemplate" /> : c'est le <c>DataContext</c>
///     du template (équivalent des props de <c>connectionLineComponent</c>).
/// </summary>
public sealed record ConnectionLineContext(Geometry Geometry, string PathData, Point From, Point To, Position FromSide,
    Position ToSide, bool? IsValid, FlowConnectionState State);

public partial class FlowView
{
    #region Options d'interaction (P2)

    public static readonly DependencyProperty NodesFocusableProperty = DependencyProperty.Register(
        nameof(NodesFocusable), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    public static readonly DependencyProperty EdgesFocusableProperty = DependencyProperty.Register(
        nameof(EdgesFocusable), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    /// <summary>Désactive la navigation au clavier (Tab, flèches).</summary>
    public static readonly DependencyProperty DisableKeyboardA11yProperty = DependencyProperty.Register(
        nameof(DisableKeyboardA11y), typeof(bool), typeof(FlowView), new PropertyMetadata(false));

    /// <summary>Fait défiler la vue pour montrer un node qu'on atteint au clavier (<c>autoPanOnNodeFocus</c>).</summary>
    public static readonly DependencyProperty AutoPanOnNodeFocusProperty = DependencyProperty.Register(
        nameof(AutoPanOnNodeFocus), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    /// <summary>Touches qui suppriment la sélection ; vide = suppression au clavier désactivée.</summary>
    public static readonly DependencyProperty DeleteKeysProperty = DependencyProperty.Register(
        nameof(DeleteKeys), typeof(IReadOnlyList<Key>), typeof(FlowView),
        new PropertyMetadata(new[] { Key.Delete, Key.Back }));

    /// <summary>Modificateur qui lance la sélection par rectangle (<c>selectionKeyCode</c>).</summary>
    public static readonly DependencyProperty SelectionKeyProperty = DependencyProperty.Register(
        nameof(SelectionKey), typeof(ModifierKeys), typeof(FlowView), new PropertyMetadata(ModifierKeys.Shift));

    /// <summary>Modificateur qui ajoute à la sélection (<c>multiSelectionKeyCode</c>).</summary>
    public static readonly DependencyProperty MultiSelectionKeyProperty = DependencyProperty.Register(
        nameof(MultiSelectionKey), typeof(ModifierKeys), typeof(FlowView), new PropertyMetadata(ModifierKeys.Control));

    /// <summary>Touche maintenue pour déplacer la vue même au-dessus d'un node (<c>panActivationKeyCode</c>).</summary>
    public static readonly DependencyProperty PanActivationKeyProperty = DependencyProperty.Register(
        nameof(PanActivationKey), typeof(Key), typeof(FlowView), new PropertyMetadata(Key.Space));

    /// <summary>Modificateur qui force le zoom à la molette quand <see cref="PanOnScroll" /> est actif (<c>zoomActivationKeyCode</c>).</summary>
    public static readonly DependencyProperty ZoomActivationKeyProperty = DependencyProperty.Register(
        nameof(ZoomActivationKey), typeof(ModifierKeys), typeof(FlowView), new PropertyMetadata(ModifierKeys.Control));

    /// <summary>Glisser sur le fond trace le rectangle de sélection au lieu de déplacer la vue (<c>selectionOnDrag</c>).</summary>
    public static readonly DependencyProperty SelectionOnDragProperty = DependencyProperty.Register(
        nameof(SelectionOnDrag), typeof(bool), typeof(FlowView), new PropertyMetadata(false));

    public static readonly DependencyProperty SelectionModeProperty = DependencyProperty.Register(
        nameof(SelectionMode), typeof(FlowSelectionMode), typeof(FlowView), new PropertyMetadata(FlowSelectionMode.Partial));

    /// <summary>Appuyer sur un node le sélectionne (<c>selectNodesOnDrag</c>) ; faux : il se déplace sans être sélectionné.</summary>
    public static readonly DependencyProperty SelectNodesOnDragProperty = DependencyProperty.Register(
        nameof(SelectNodesOnDrag), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    /// <summary>Glisser sur le fond déplace la vue (<c>panOnDrag</c>).</summary>
    public static readonly DependencyProperty PanOnDragProperty = DependencyProperty.Register(
        nameof(PanOnDrag), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    /// <summary>La molette déplace la vue au lieu de zoomer (<c>panOnScroll</c>).</summary>
    public static readonly DependencyProperty PanOnScrollProperty = DependencyProperty.Register(
        nameof(PanOnScroll), typeof(bool), typeof(FlowView), new PropertyMetadata(false));

    public static readonly DependencyProperty PanOnScrollSpeedProperty = DependencyProperty.Register(
        nameof(PanOnScrollSpeed), typeof(double), typeof(FlowView), new PropertyMetadata(0.5));

    public static readonly DependencyProperty PanOnScrollModeProperty = DependencyProperty.Register(
        nameof(PanOnScrollMode), typeof(PanOnScrollMode), typeof(FlowView), new PropertyMetadata(PanOnScrollMode.Free));

    public static readonly DependencyProperty ZoomOnScrollProperty = DependencyProperty.Register(
        nameof(ZoomOnScroll), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    public static readonly DependencyProperty ZoomOnPinchProperty = DependencyProperty.Register(
        nameof(ZoomOnPinch), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    public static readonly DependencyProperty ZoomOnDoubleClickProperty = DependencyProperty.Register(
        nameof(ZoomOnDoubleClick), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    /// <summary>Distance (pixels) à franchir avant qu'un node suive la souris (<c>nodeDragThreshold</c>).</summary>
    public static readonly DependencyProperty NodeDragThresholdProperty = DependencyProperty.Register(
        nameof(NodeDragThreshold), typeof(double), typeof(FlowView), new PropertyMetadata(1.0));

    /// <summary>Distance avant que l'aperçu d'une connexion suive la souris (<c>connectionDragThreshold</c>).</summary>
    public static readonly DependencyProperty ConnectionDragThresholdProperty = DependencyProperty.Register(
        nameof(ConnectionDragThreshold), typeof(double), typeof(FlowView), new PropertyMetadata(1.0));

    /// <summary>Distance avant que la vue suive la souris (<c>paneDragThreshold</c>).</summary>
    public static readonly DependencyProperty PaneDragThresholdProperty = DependencyProperty.Register(
        nameof(PaneDragThreshold), typeof(double), typeof(FlowView), new PropertyMetadata(1.0));

    /// <summary>Origine des nodes (<c>nodeOrigin</c>) : (0,0) = coin haut-gauche, (0.5,0.5) = centre. Surchargée par <see cref="Node.Origin" />.</summary>
    public static readonly DependencyProperty NodeOriginProperty = DependencyProperty.Register(
        nameof(NodeOrigin), typeof(XYPosition), typeof(FlowView), new PropertyMetadata(XYPosition.Zero, (d, _) =>
        {
            var f = (FlowView)d;
            f.OnNodesChanged();
        }));

    /// <summary>Boutons qui déplacent la vue quand <see cref="PanOnDrag" /> est vrai ; le clic droit déplace sans ouvrir le menu contextuel.</summary>
    public static readonly DependencyProperty PanOnDragButtonsProperty = DependencyProperty.Register(
        nameof(PanOnDragButtons), typeof(FlowMouseButtons), typeof(FlowView),
        new PropertyMetadata(FlowMouseButtons.Left | FlowMouseButtons.Middle));

    /// <summary>La molette est consommée par la vue (<c>preventScrolling</c>) ; faux : elle remonte au parent (un <c>ScrollViewer</c>…).</summary>
    public static readonly DependencyProperty PreventScrollingProperty = DependencyProperty.Register(
        nameof(PreventScrolling), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    /// <summary>Nom UI Automation des edges sans <see cref="Edge.AriaLabel" />.</summary>
    public static readonly DependencyProperty EdgeAriaLabelProviderProperty = DependencyProperty.Register(
        nameof(EdgeAriaLabelProvider), typeof(Func<Edge, string?>), typeof(FlowView),
        new PropertyMetadata(null, (d, _) => ((FlowView)d)._edgeLayer?.Refresh((FlowView)d)));

    /// <summary>Annonce aux lecteurs d'écran la sélection d'un node ou d'un edge (notification UI Automation).</summary>
    public static readonly DependencyProperty AnnounceSelectionProperty = DependencyProperty.Register(
        nameof(AnnounceSelection), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    public FlowMouseButtons PanOnDragButtons
    {
        get => (FlowMouseButtons)GetValue(PanOnDragButtonsProperty);
        set => SetValue(PanOnDragButtonsProperty, value);
    }

    public bool PreventScrolling
    {
        get => (bool)GetValue(PreventScrollingProperty);
        set => SetValue(PreventScrollingProperty, value);
    }

    public Func<Edge, string?>? EdgeAriaLabelProvider
    {
        get => (Func<Edge, string?>?)GetValue(EdgeAriaLabelProviderProperty);
        set => SetValue(EdgeAriaLabelProviderProperty, value);
    }

    public bool AnnounceSelection
    {
        get => (bool)GetValue(AnnounceSelectionProperty);
        set => SetValue(AnnounceSelectionProperty, value);
    }

    private bool CanPanWith(FlowMouseButtons button) => PanOnDrag && PanOnDragButtons.HasFlag(button);

    /// <summary>Écrit les changes appliqués dans la trace (<c>System.Diagnostics.Trace</c>) : équivalent de <c>debug</c>.</summary>
    public static readonly DependencyProperty DebugProperty = DependencyProperty.Register(
        nameof(Debug), typeof(bool), typeof(FlowView), new PropertyMetadata(false));

    public bool Debug
    {
        get => (bool)GetValue(DebugProperty);
        set => SetValue(DebugProperty, value);
    }

    public static readonly DependencyProperty ZIndexModeProperty = DependencyProperty.Register(
        nameof(ZIndexMode), typeof(FlowZIndexMode), typeof(FlowView), new PropertyMetadata(FlowZIndexMode.Basic, (d, _) =>
            ((FlowView)d).OnNodesChanged()));

    public FlowZIndexMode ZIndexMode
    {
        get => (FlowZIndexMode)GetValue(ZIndexModeProperty);
        set => SetValue(ZIndexModeProperty, value);
    }

    /// <summary>Un node sélectionné passe au premier plan (<c>elevateNodesOnSelect</c>).</summary>
    public static readonly DependencyProperty ElevateNodesOnSelectProperty = DependencyProperty.Register(
        nameof(ElevateNodesOnSelect), typeof(bool), typeof(FlowView), new PropertyMetadata(true, (d, _) =>
            ((FlowView)d).OnNodesChanged()));

    /// <summary>Un edge sélectionné passe devant les autres (<c>elevateEdgesOnSelect</c>).</summary>
    public static readonly DependencyProperty ElevateEdgesOnSelectProperty = DependencyProperty.Register(
        nameof(ElevateEdgesOnSelect), typeof(bool), typeof(FlowView), new PropertyMetadata(false, (d, _) =>
            ((FlowView)d)._edgeLayer?.Refresh((FlowView)d)));

    /// <summary>Connecter par deux clics : un clic sur un handle, puis un clic sur un autre (<c>connectOnClick</c>).</summary>
    public static readonly DependencyProperty ConnectOnClickProperty = DependencyProperty.Register(
        nameof(ConnectOnClick), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    public static readonly DependencyProperty ConnectionLineTypeProperty = DependencyProperty.Register(
        nameof(ConnectionLineType), typeof(ConnectionLineType), typeof(FlowView), new PropertyMetadata(ConnectionLineType.Bezier));

    /// <summary>Rendu libre de l'aperçu de connexion ; <c>DataContext</c> = <see cref="ConnectionLineContext" /> (<c>connectionLineComponent</c>).</summary>
    public static readonly DependencyProperty ConnectionLineTemplateProperty = DependencyProperty.Register(
        nameof(ConnectionLineTemplate), typeof(DataTemplate), typeof(FlowView), new PropertyMetadata(null));

    /// <summary>Portée (en pixels écran) dans laquelle on rattrape l'extrémité d'un edge pour le reconnecter (<c>reconnectRadius</c>).</summary>
    public static readonly DependencyProperty ReconnectRadiusProperty = DependencyProperty.Register(
        nameof(ReconnectRadius), typeof(double), typeof(FlowView), new PropertyMetadata(24.0));

    /// <summary>
    ///     Valeurs par défaut des edges créés par une connexion (<c>defaultEdgeOptions</c>) : <c>Type</c>, <c>Animated</c>,
    ///     marqueurs, style… (<c>Id</c>, <c>Source</c>, <c>Target</c> et handles sont ignorés).
    /// </summary>
    public static readonly DependencyProperty DefaultEdgeOptionsProperty = DependencyProperty.Register(
        nameof(DefaultEdgeOptions), typeof(Edge), typeof(FlowView), new PropertyMetadata(null));

    /// <summary>Couleur par défaut des marqueurs d'edges ; <c>null</c> = couleur du trait (<c>defaultMarkerColor</c>).</summary>
    public static readonly DependencyProperty DefaultMarkerColorProperty = DependencyProperty.Register(
        nameof(DefaultMarkerColor), typeof(Brush), typeof(FlowView), new PropertyMetadata(null, (d, _) =>
            ((FlowView)d)._edgeLayer?.Refresh((FlowView)d)));

    /// <summary>Options utilisées par <see cref="FitViewOnInit" /> et le bouton de cadrage de <see cref="FlowControls" />.</summary>
    public static readonly DependencyProperty FitViewOptionsProperty = DependencyProperty.Register(
        nameof(FitViewOptions), typeof(FitViewOptions), typeof(FlowView), new PropertyMetadata(null));

    private static readonly DependencyPropertyKey NodesInitializedPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(NodesInitialized), typeof(bool), typeof(FlowView), new PropertyMetadata(false));

    /// <summary>Vrai quand tous les nodes visibles ont été mesurés (<c>useNodesInitialized</c>).</summary>
    public static readonly DependencyProperty NodesInitializedProperty = NodesInitializedPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey ConnectionStatePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(ConnectionState), typeof(FlowConnectionState), typeof(FlowView), new PropertyMetadata(null));

    /// <summary>Connexion en cours (<c>useConnection</c>) ; <c>null</c> hors d'un geste de connexion.</summary>
    public static readonly DependencyProperty ConnectionStateProperty = ConnectionStatePropertyKey.DependencyProperty;

    public bool NodesFocusable
    {
        get => (bool)GetValue(NodesFocusableProperty);
        set => SetValue(NodesFocusableProperty, value);
    }

    public bool EdgesFocusable
    {
        get => (bool)GetValue(EdgesFocusableProperty);
        set => SetValue(EdgesFocusableProperty, value);
    }

    public bool DisableKeyboardA11y
    {
        get => (bool)GetValue(DisableKeyboardA11yProperty);
        set => SetValue(DisableKeyboardA11yProperty, value);
    }

    public bool AutoPanOnNodeFocus
    {
        get => (bool)GetValue(AutoPanOnNodeFocusProperty);
        set => SetValue(AutoPanOnNodeFocusProperty, value);
    }

    public IReadOnlyList<Key> DeleteKeys
    {
        get => (IReadOnlyList<Key>)GetValue(DeleteKeysProperty);
        set => SetValue(DeleteKeysProperty, value);
    }

    public ModifierKeys SelectionKey
    {
        get => (ModifierKeys)GetValue(SelectionKeyProperty);
        set => SetValue(SelectionKeyProperty, value);
    }

    public ModifierKeys MultiSelectionKey
    {
        get => (ModifierKeys)GetValue(MultiSelectionKeyProperty);
        set => SetValue(MultiSelectionKeyProperty, value);
    }

    public Key PanActivationKey
    {
        get => (Key)GetValue(PanActivationKeyProperty);
        set => SetValue(PanActivationKeyProperty, value);
    }

    public ModifierKeys ZoomActivationKey
    {
        get => (ModifierKeys)GetValue(ZoomActivationKeyProperty);
        set => SetValue(ZoomActivationKeyProperty, value);
    }

    public bool SelectionOnDrag
    {
        get => (bool)GetValue(SelectionOnDragProperty);
        set => SetValue(SelectionOnDragProperty, value);
    }

    public FlowSelectionMode SelectionMode
    {
        get => (FlowSelectionMode)GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    public bool SelectNodesOnDrag
    {
        get => (bool)GetValue(SelectNodesOnDragProperty);
        set => SetValue(SelectNodesOnDragProperty, value);
    }

    public bool PanOnDrag
    {
        get => (bool)GetValue(PanOnDragProperty);
        set => SetValue(PanOnDragProperty, value);
    }

    public bool PanOnScroll
    {
        get => (bool)GetValue(PanOnScrollProperty);
        set => SetValue(PanOnScrollProperty, value);
    }

    public double PanOnScrollSpeed
    {
        get => (double)GetValue(PanOnScrollSpeedProperty);
        set => SetValue(PanOnScrollSpeedProperty, value);
    }

    public PanOnScrollMode PanOnScrollMode
    {
        get => (PanOnScrollMode)GetValue(PanOnScrollModeProperty);
        set => SetValue(PanOnScrollModeProperty, value);
    }

    public bool ZoomOnScroll
    {
        get => (bool)GetValue(ZoomOnScrollProperty);
        set => SetValue(ZoomOnScrollProperty, value);
    }

    public bool ZoomOnPinch
    {
        get => (bool)GetValue(ZoomOnPinchProperty);
        set => SetValue(ZoomOnPinchProperty, value);
    }

    public bool ZoomOnDoubleClick
    {
        get => (bool)GetValue(ZoomOnDoubleClickProperty);
        set => SetValue(ZoomOnDoubleClickProperty, value);
    }

    public double NodeDragThreshold
    {
        get => (double)GetValue(NodeDragThresholdProperty);
        set => SetValue(NodeDragThresholdProperty, value);
    }

    public double ConnectionDragThreshold
    {
        get => (double)GetValue(ConnectionDragThresholdProperty);
        set => SetValue(ConnectionDragThresholdProperty, value);
    }

    public double PaneDragThreshold
    {
        get => (double)GetValue(PaneDragThresholdProperty);
        set => SetValue(PaneDragThresholdProperty, value);
    }

    public XYPosition NodeOrigin
    {
        get => (XYPosition)GetValue(NodeOriginProperty);
        set => SetValue(NodeOriginProperty, value);
    }

    public bool ElevateNodesOnSelect
    {
        get => (bool)GetValue(ElevateNodesOnSelectProperty);
        set => SetValue(ElevateNodesOnSelectProperty, value);
    }

    public bool ElevateEdgesOnSelect
    {
        get => (bool)GetValue(ElevateEdgesOnSelectProperty);
        set => SetValue(ElevateEdgesOnSelectProperty, value);
    }

    public bool ConnectOnClick
    {
        get => (bool)GetValue(ConnectOnClickProperty);
        set => SetValue(ConnectOnClickProperty, value);
    }

    public ConnectionLineType ConnectionLineType
    {
        get => (ConnectionLineType)GetValue(ConnectionLineTypeProperty);
        set => SetValue(ConnectionLineTypeProperty, value);
    }

    public DataTemplate? ConnectionLineTemplate
    {
        get => (DataTemplate?)GetValue(ConnectionLineTemplateProperty);
        set => SetValue(ConnectionLineTemplateProperty, value);
    }

    public double ReconnectRadius
    {
        get => (double)GetValue(ReconnectRadiusProperty);
        set => SetValue(ReconnectRadiusProperty, value);
    }

    public Edge? DefaultEdgeOptions
    {
        get => (Edge?)GetValue(DefaultEdgeOptionsProperty);
        set => SetValue(DefaultEdgeOptionsProperty, value);
    }

    public Brush? DefaultMarkerColor
    {
        get => (Brush?)GetValue(DefaultMarkerColorProperty);
        set => SetValue(DefaultMarkerColorProperty, value);
    }

    public FitViewOptions? FitViewOptions
    {
        get => (FitViewOptions?)GetValue(FitViewOptionsProperty);
        set => SetValue(FitViewOptionsProperty, value);
    }

    public bool NodesInitialized => (bool)GetValue(NodesInitializedProperty);

    public FlowConnectionState? ConnectionState => (FlowConnectionState?)GetValue(ConnectionStateProperty);

    internal void SetNodesInitialized(bool value)
    {
        if (NodesInitialized != value) SetValue(NodesInitializedPropertyKey, value);
    }

    internal void SetConnectionState(FlowConnectionState? state) => SetValue(ConnectionStatePropertyKey, state);

    #endregion

    #region Marqueurs d'interaction (noDrag / noPan / noWheel)

    /// <summary>Un élément marqué (ou ses descendants) ne déclenche pas le déplacement du node (<c>noDragClassName</c>).</summary>
    public static readonly DependencyProperty NoDragProperty = DependencyProperty.RegisterAttached(
        "NoDrag", typeof(bool), typeof(FlowView), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Un élément marqué ne déclenche pas le déplacement de la vue (<c>noPanClassName</c>).</summary>
    public static readonly DependencyProperty NoPanProperty = DependencyProperty.RegisterAttached(
        "NoPan", typeof(bool), typeof(FlowView), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Un élément marqué reçoit la molette sans que la vue zoome (<c>noWheelClassName</c>).</summary>
    public static readonly DependencyProperty NoWheelProperty = DependencyProperty.RegisterAttached(
        "NoWheel", typeof(bool), typeof(FlowView), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetNoDrag(DependencyObject d) => (bool)d.GetValue(NoDragProperty);
    public static void SetNoDrag(DependencyObject d, bool value) => d.SetValue(NoDragProperty, value);
    public static bool GetNoPan(DependencyObject d) => (bool)d.GetValue(NoPanProperty);
    public static void SetNoPan(DependencyObject d, bool value) => d.SetValue(NoPanProperty, value);
    public static bool GetNoWheel(DependencyObject d) => (bool)d.GetValue(NoWheelProperty);
    public static void SetNoWheel(DependencyObject d, bool value) => d.SetValue(NoWheelProperty, value);

    #endregion

    #region Événements (P2)

    /// <summary>La vue est prête : taille connue et premier rendu fait (<c>onInit</c>).</summary>
    public event EventHandler? FlowInitialized;

    /// <summary>Anomalie dans les données (edge vers un node absent…) (<c>onError</c>).</summary>
    public event EventHandler<FlowErrorEventArgs>? ErrorOccurred;

    /// <summary>Suppression imminente ; peut être annulée (<c>onBeforeDelete</c>).</summary>
    public event EventHandler<BeforeDeleteEventArgs>? BeforeDelete;

    public event EventHandler<ElementEventArgs>? NodeClick;
    public event EventHandler<ElementEventArgs>? EdgeClick;
    public event EventHandler<ElementEventArgs>? PaneClick;

    public event EventHandler<ElementEventArgs>? NodeMouseEnter;
    public event EventHandler<ElementEventArgs>? NodeMouseMove;
    public event EventHandler<ElementEventArgs>? NodeMouseLeave;
    public event EventHandler<ElementEventArgs>? EdgeMouseEnter;
    public event EventHandler<ElementEventArgs>? EdgeMouseMove;
    public event EventHandler<ElementEventArgs>? EdgeMouseLeave;
    public event EventHandler<ElementEventArgs>? PaneMouseEnter;
    public event EventHandler<ElementEventArgs>? PaneMouseMove;
    public event EventHandler<ElementEventArgs>? PaneMouseLeave;

    /// <summary>Clic droit sur la sélection (plusieurs nodes sélectionnés).</summary>
    public event EventHandler<ElementEventArgs>? SelectionContextMenuRequested;

    public event EventHandler<NodeDragEventArgs>? NodeDragStarted;
    public event EventHandler<NodeDragEventArgs>? NodeDragging;
    public event EventHandler<NodeDragEventArgs>? NodeDragStopped;

    /// <summary>Déplacement de <b>plusieurs</b> nodes à la fois (<c>onSelectionDragStart</c>, <c>onSelectionDrag</c>, <c>onSelectionDragStop</c>).</summary>
    public event EventHandler<NodeDragEventArgs>? SelectionDragStarted;

    public event EventHandler<NodeDragEventArgs>? SelectionDragging;
    public event EventHandler<NodeDragEventArgs>? SelectionDragStopped;

    public event EventHandler<SelectionBoxEventArgs>? SelectionStarted;
    public event EventHandler<SelectionBoxEventArgs>? SelectionEnded;

    /// <summary>Une extrémité d'edge a été déplacée vers un autre handle.</summary>
    public event EventHandler<ReconnectEventArgs>? EdgeReconnected;

    /// <summary>On rattrape l'extrémité d'un edge pour le reconnecter (<c>onReconnectStart</c>).</summary>
    public event EventHandler<ElementEventArgs>? ReconnectStarted;

    /// <summary>L'extrémité est relâchée, avec ou sans nouvelle connexion (<c>onReconnectEnd</c>).</summary>
    public event EventHandler<ReconnectEndEventArgs>? ReconnectEnded;

    public event EventHandler<ConnectStartEventArgs>? ClickConnectStarted;
    public event EventHandler<ConnectEndEventArgs>? ClickConnectEnded;

    /// <summary>Un geste (glisser, molette, transition) commence à déplacer la vue.</summary>
    public event EventHandler<ViewportMoveEventArgs>? ViewportMoveStarted;

    /// <summary>La vue a bougé, quelle qu'en soit la cause.</summary>
    public event EventHandler<ViewportMoveEventArgs>? ViewportMoved;

    public event EventHandler<ViewportMoveEventArgs>? ViewportMoveEnded;

    /// <summary>Molette tournée sur le fond (<c>onPaneScroll</c>).</summary>
    public event EventHandler<MouseWheelEventArgs>? PaneScroll;

    #endregion
}
