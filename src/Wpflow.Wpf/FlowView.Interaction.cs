using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Wpflow.Core;

namespace Wpflow.Wpf;

/// <summary>Un geste de connexion commence (le fil part d'un handle) : <see cref="FlowView.ConnectStarted" />.</summary>
public sealed class ConnectStartEventArgs(string nodeId, string? handleId, HandleType handleType, bool isReconnection) : EventArgs
{
    public string NodeId { get; } = nodeId;
    public string? HandleId { get; } = handleId;
    public HandleType HandleType { get; } = handleType;

    /// <summary>Vrai quand on rattrape l'extrémité d'un edge existant (reconnexion) plutôt qu'un nouveau fil.</summary>
    public bool IsReconnection { get; } = isReconnection;
}

/// <summary>
///     Le fil est relâché : <see cref="FlowView.ConnectEnded" />. <see cref="Connection" /> est renseignée si une connexion
///     a été réalisée ; sinon (relâché dans le vide, sur un handle refusé…) <see cref="IsValid" /> est faux et
///     <see cref="Position" /> dit où — c'est ce qui permet « créer un node en lâchant le fil dans le vide ».
/// </summary>
public sealed class ConnectEndEventArgs(XYPosition position, string fromNodeId, string? fromHandleId, HandleType fromHandleType,
    string? toNodeId, string? toHandleId, Connection? connection) : EventArgs
{
    /// <summary>Position du relâchement, en coordonnées flow.</summary>
    public XYPosition Position { get; } = position;

    public string FromNodeId { get; } = fromNodeId;
    public string? FromHandleId { get; } = fromHandleId;
    public HandleType FromHandleType { get; } = fromHandleType;

    /// <summary>Node du handle le plus proche accepté, ou <c>null</c> si le fil est tombé dans le vide.</summary>
    public string? ToNodeId { get; } = toNodeId;

    public string? ToHandleId { get; } = toHandleId;
    public Connection? Connection { get; } = connection;
    public bool IsValid => Connection != null;
}

public enum FlowElementKind
{
    Node,
    Edge,
    Pane
}

/// <summary>Double-clic ou clic droit sur un node, un edge ou le fond.</summary>
public sealed class ElementEventArgs(FlowElementKind kind, string? id, object? element, XYPosition position) : EventArgs
{
    public FlowElementKind Kind { get; } = kind;

    /// <summary>Identifiant du node ou de l'edge ; <c>null</c> pour le fond.</summary>
    public string? Id { get; } = id;

    /// <summary>Le ViewModel (avec une source) ou le <see cref="Node" /> / <see cref="Edge" /> ; <c>null</c> pour le fond.</summary>
    public object? Element { get; } = element;

    /// <summary>Position du clic en coordonnées flow.</summary>
    public XYPosition Position { get; } = position;
}

// Options d'interaction globales, événements de connexion, double-clic, menu contextuel et auto-défilement.
public partial class FlowView
{
    #region Options globales

    /// <summary>Les nodes peuvent être déplacés (en plus de <see cref="Node.Draggable" />).</summary>
    public static readonly DependencyProperty NodesDraggableProperty = DependencyProperty.Register(
        nameof(NodesDraggable), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    /// <summary>On peut démarrer ou terminer une connexion sur les handles (en plus de <see cref="Node.Connectable" />).</summary>
    public static readonly DependencyProperty NodesConnectableProperty = DependencyProperty.Register(
        nameof(NodesConnectable), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    /// <summary>Nodes et edges peuvent être sélectionnés (en plus de <see cref="Node.Selectable" />).</summary>
    public static readonly DependencyProperty ElementsSelectableProperty = DependencyProperty.Register(
        nameof(ElementsSelectable), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    /// <summary>Les extrémités d'un edge peuvent être rattrapées pour le reconnecter.</summary>
    public static readonly DependencyProperty EdgesReconnectableProperty = DependencyProperty.Register(
        nameof(EdgesReconnectable), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    public bool NodesDraggable
    {
        get => (bool)GetValue(NodesDraggableProperty);
        set => SetValue(NodesDraggableProperty, value);
    }

    public bool NodesConnectable
    {
        get => (bool)GetValue(NodesConnectableProperty);
        set => SetValue(NodesConnectableProperty, value);
    }

    public bool ElementsSelectable
    {
        get => (bool)GetValue(ElementsSelectableProperty);
        set => SetValue(ElementsSelectableProperty, value);
    }

    public bool EdgesReconnectable
    {
        get => (bool)GetValue(EdgesReconnectableProperty);
        set => SetValue(EdgesReconnectableProperty, value);
    }

    #endregion

    #region Auto-défilement

    /// <summary>La vue défile quand on approche d'un bord en déplaçant des nodes.</summary>
    public static readonly DependencyProperty AutoPanOnNodeDragProperty = DependencyProperty.Register(
        nameof(AutoPanOnNodeDrag), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    /// <summary>La vue défile quand on approche d'un bord en tirant une connexion.</summary>
    public static readonly DependencyProperty AutoPanOnConnectProperty = DependencyProperty.Register(
        nameof(AutoPanOnConnect), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    /// <summary>La vue défile quand on approche d'un bord en traçant un rectangle de sélection.</summary>
    public static readonly DependencyProperty AutoPanOnSelectionProperty = DependencyProperty.Register(
        nameof(AutoPanOnSelection), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    /// <summary>Vitesse maximale du défilement, en pixels par image (≈ 16 ms) ; 15 comme React Flow.</summary>
    public static readonly DependencyProperty AutoPanSpeedProperty = DependencyProperty.Register(
        nameof(AutoPanSpeed), typeof(double), typeof(FlowView), new PropertyMetadata(15.0));

    public bool AutoPanOnNodeDrag
    {
        get => (bool)GetValue(AutoPanOnNodeDragProperty);
        set => SetValue(AutoPanOnNodeDragProperty, value);
    }

    public bool AutoPanOnConnect
    {
        get => (bool)GetValue(AutoPanOnConnectProperty);
        set => SetValue(AutoPanOnConnectProperty, value);
    }

    public bool AutoPanOnSelection
    {
        get => (bool)GetValue(AutoPanOnSelectionProperty);
        set => SetValue(AutoPanOnSelectionProperty, value);
    }

    public double AutoPanSpeed
    {
        get => (double)GetValue(AutoPanSpeedProperty);
        set => SetValue(AutoPanSpeedProperty, value);
    }

    private DispatcherTimer? _autoPanTimer;
    private XYPosition _mouseStartFlow; // position flow du bouton enfoncé : les gestes (déplacement, boîte) en sont indépendants du défilement automatique

    private void StartAutoPanIfEnabled()
    {
        var enabled = _mode switch
        {
            Mode.DragNodes => AutoPanOnNodeDrag,
            Mode.Connect => AutoPanOnConnect,
            Mode.Select => AutoPanOnSelection,
            _ => false
        };
        if (!enabled) return;
        _autoPanTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Input, (_, _) => AutoPanTick(), Dispatcher);
        _autoPanTimer.Start();
    }

    private void StopAutoPan() => _autoPanTimer?.Stop();

    private void AutoPanTick()
    {
        if (_mode is not (Mode.DragNodes or Mode.Connect or Mode.Select) || !IsMouseCaptured)
        {
            StopAutoPan();
            return;
        }

        var pos = Mouse.GetPosition(this);
        var (dx, dy) = AutoPan.Delta(pos.X, pos.Y, ActualWidth, ActualHeight, AutoPanSpeed);
        if (dx == 0 && dy == 0) return;
        var before = Viewport;
        Viewport = new Viewport(before.X + dx, before.Y + dy, before.Zoom);
        if (Viewport == before) return; // limite de TranslateExtent atteinte

        // Le curseur n'a pas bougé mais le contenu, si : le geste est recalculé pour la nouvelle position du fond.
        switch (_mode)
        {
            case Mode.DragNodes:
                UpdateDrag(pos);
                break;
            case Mode.Connect:
                UpdateConnectionLine(ScreenToFlow(pos));
                break;
            case Mode.Select:
                UpdateSelectionBox(pos);
                break;
        }
    }

    #endregion

    #region Événements et commandes de connexion

    /// <summary>Un fil est tiré depuis un handle.</summary>
    public event EventHandler<ConnectStartEventArgs>? ConnectStarted;

    /// <summary>Le fil est relâché : sur un handle (connexion faite ou refusée) ou dans le vide.</summary>
    public event EventHandler<ConnectEndEventArgs>? ConnectEnded;

    /// <summary>Début d'un fil. Paramètre : <see cref="ConnectStartEventArgs" />.</summary>
    public static readonly DependencyProperty ConnectStartCommandProperty = CommandProperty(nameof(ConnectStartCommand));

    /// <summary>Fin d'un fil, qu'il ait abouti ou non. Paramètre : <see cref="ConnectEndEventArgs" />.</summary>
    public static readonly DependencyProperty ConnectEndCommandProperty = CommandProperty(nameof(ConnectEndCommand));

    public ICommand? ConnectStartCommand
    {
        get => (ICommand?)GetValue(ConnectStartCommandProperty);
        set => SetValue(ConnectStartCommandProperty, value);
    }

    public ICommand? ConnectEndCommand
    {
        get => (ICommand?)GetValue(ConnectEndCommandProperty);
        set => SetValue(ConnectEndCommandProperty, value);
    }

    private void RaiseConnectStarted(HandleRef from, bool reconnection)
    {
        var args = new ConnectStartEventArgs(from.NodeId, from.Info.Id, from.Info.Type, reconnection);
        ConnectStarted?.Invoke(this, args);
        Run(ConnectStartCommand, args);
    }

    private void RaiseConnectEnded(HandleRef from, HandleRef? target, Connection? connection, XYPosition position)
    {
        var args = new ConnectEndEventArgs(position, from.NodeId, from.Info.Id, from.Info.Type, target?.NodeId,
            target?.Info.Id, connection);
        ConnectEnded?.Invoke(this, args);
        Run(ConnectEndCommand, args);
    }

    #endregion

    #region Double-clic et menu contextuel

    public event EventHandler<ElementEventArgs>? NodeDoubleClick;
    public event EventHandler<ElementEventArgs>? EdgeDoubleClick;
    public event EventHandler<ElementEventArgs>? NodeContextMenuRequested;
    public event EventHandler<ElementEventArgs>? EdgeContextMenuRequested;
    public event EventHandler<ElementEventArgs>? PaneContextMenuRequested;

    /// <summary>Double-clic sur un node. Paramètre : l'élément (ViewModel ou <see cref="Node" />).</summary>
    public static readonly DependencyProperty NodeDoubleClickCommandProperty = CommandProperty(nameof(NodeDoubleClickCommand));

    public static readonly DependencyProperty EdgeDoubleClickCommandProperty = CommandProperty(nameof(EdgeDoubleClickCommand));

    /// <summary>Clic droit sur un node. Paramètre : l'élément. La vue ne marque pas l'événement : un <c>ContextMenu</c> WPF s'ouvre aussi.</summary>
    public static readonly DependencyProperty NodeContextMenuCommandProperty = CommandProperty(nameof(NodeContextMenuCommand));

    public static readonly DependencyProperty EdgeContextMenuCommandProperty = CommandProperty(nameof(EdgeContextMenuCommand));

    /// <summary>Clic droit sur le fond. Paramètre : la position en coordonnées flow (<see cref="XYPosition" />).</summary>
    public static readonly DependencyProperty PaneContextMenuCommandProperty = CommandProperty(nameof(PaneContextMenuCommand));

    public ICommand? NodeDoubleClickCommand
    {
        get => (ICommand?)GetValue(NodeDoubleClickCommandProperty);
        set => SetValue(NodeDoubleClickCommandProperty, value);
    }

    public ICommand? EdgeDoubleClickCommand
    {
        get => (ICommand?)GetValue(EdgeDoubleClickCommandProperty);
        set => SetValue(EdgeDoubleClickCommandProperty, value);
    }

    public ICommand? NodeContextMenuCommand
    {
        get => (ICommand?)GetValue(NodeContextMenuCommandProperty);
        set => SetValue(NodeContextMenuCommandProperty, value);
    }

    public ICommand? EdgeContextMenuCommand
    {
        get => (ICommand?)GetValue(EdgeContextMenuCommandProperty);
        set => SetValue(EdgeContextMenuCommandProperty, value);
    }

    public ICommand? PaneContextMenuCommand
    {
        get => (ICommand?)GetValue(PaneContextMenuCommandProperty);
        set => SetValue(PaneContextMenuCommandProperty, value);
    }

    /// <summary>Notifie un double-clic ou un clic droit sur l'élément désigné (ou le fond).</summary>
    private void RaiseElementEvent(FlowElementKind kind, string? id, XYPosition position, bool doubleClick)
    {
        var element = kind switch
        {
            FlowElementKind.Node when id != null => NodeElement(id),
            FlowElementKind.Edge when id != null => EdgeElement(id),
            _ => null
        };
        var args = new ElementEventArgs(kind, id, element, position);
        switch (kind, doubleClick)
        {
            case (FlowElementKind.Node, true):
                NodeDoubleClick?.Invoke(this, args);
                Run(NodeDoubleClickCommand, element);
                break;
            case (FlowElementKind.Edge, true):
                EdgeDoubleClick?.Invoke(this, args);
                Run(EdgeDoubleClickCommand, element);
                break;
            case (FlowElementKind.Node, false):
                NodeContextMenuRequested?.Invoke(this, args);
                Run(NodeContextMenuCommand, element);
                break;
            case (FlowElementKind.Edge, false):
                EdgeContextMenuRequested?.Invoke(this, args);
                Run(EdgeContextMenuCommand, element);
                break;
            case (FlowElementKind.Pane, false):
                PaneContextMenuRequested?.Invoke(this, args);
                Run(PaneContextMenuCommand, position);
                break;
        }
    }

    private bool _rightPan, _rightPanMoved;

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        if (!CanPanWith(FlowMouseButtons.Right) || _mode != Mode.None) return;
        Focus();
        StopViewportAnimation(false);
        _mouseStart = e.GetPosition(this);
        _viewportStart = Viewport;
        _mode = Mode.Pan;
        _rightPan = true;
        CaptureMouse();
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        if (_rightPan)
        {
            _rightPan = false;
            _rightPanMoved = _panMoved;
            if (_mode == Mode.Pan) FinishPan();
            if (_rightPanMoved)
            {
                e.Handled = true; // un glisser n'est pas un clic droit : pas de menu contextuel
                return;
            }
        }

        var (host, _, edgeId) = HitTarget(e.OriginalSource as DependencyObject);
        var position = ScreenToFlow(e.GetPosition(this));
        if (host?.Node != null)
        {
            if (IsSelectionMenu(host.Node.Id)) RaiseSelectionContextMenu(position);
            RaiseElementEvent(FlowElementKind.Node, host.Node.Id, position, false);
        }
        else if (edgeId != null) RaiseElementEvent(FlowElementKind.Edge, edgeId, position, false);
        else RaiseElementEvent(FlowElementKind.Pane, null, position, false);
    }

    #endregion
}
