namespace Scenarios;

/// <summary>
///     Tous les évènements publics des deux bibliothèques, sous un nom commun. Le test de garde
///     (<c>EventMatrixGuardTests</c>) compare cette liste, par réflexion, aux membres réels : un évènement ajouté sans y être
///     inscrit, ou inscrit sans geste dans <see cref="GestureMatrix" />, le fait échouer.
/// </summary>
public static class EventCatalog
{
    /// <summary>Nom commun, évènement WPF (« Type.Évènement ») et rappel Blazor (« Composant.Paramètre »).</summary>
    public sealed record Entry(string Name, string[] Wpf, string[] Blazor);

    private static Entry View(string name, string wpf, string blazor) =>
        new(name, ["FlowView." + wpf], ["FlowView." + blazor]);

    private static Entry Part(string name, string[] wpf, string[] blazor) => new(name, wpf, blazor);

    public static readonly IReadOnlyList<Entry> All =
    [
        View("Init", "FlowInitialized", "OnInit"),
        View("NodesReady", "NodesReady", "OnNodesInitialized"),
        View("Error", "ErrorOccurred", "OnError"),
        View("StateChange", "StateChanged", "OnStateChange"),
        View("NodesChange", "NodeChangesRequested", "OnNodesChange"),
        View("EdgesChange", "EdgeChangesRequested", "OnEdgesChange"),
        View("Connect", "ConnectRequested", "OnConnect"),
        View("Drop", "DropRequested", "OnDrop"),
        // En Blazor, OnBeforeDelete est une fonction qui répond, pas un EventCallback.
        new("BeforeDelete", ["FlowView.BeforeDelete"], []),
        View("Delete", "Deleted", "OnDelete"),
        View("SelectionChange", "SelectionChanged", "OnSelectionChange"),
        View("NodeClick", "NodeClick", "OnNodeClick"),
        View("EdgeClick", "EdgeClick", "OnEdgeClick"),
        View("PaneClick", "PaneClick", "OnPaneClick"),
        View("NodeDoubleClick", "NodeDoubleClick", "OnNodeDoubleClick"),
        View("EdgeDoubleClick", "EdgeDoubleClick", "OnEdgeDoubleClick"),
        View("NodeContextMenu", "NodeContextMenuRequested", "OnNodeContextMenu"),
        View("EdgeContextMenu", "EdgeContextMenuRequested", "OnEdgeContextMenu"),
        View("PaneContextMenu", "PaneContextMenuRequested", "OnPaneContextMenu"),
        View("SelectionContextMenu", "SelectionContextMenuRequested", "OnSelectionContextMenu"),
        View("NodeMouseEnter", "NodeMouseEnter", "OnNodeMouseEnter"),
        View("NodeMouseMove", "NodeMouseMove", "OnNodeMouseMove"),
        View("NodeMouseLeave", "NodeMouseLeave", "OnNodeMouseLeave"),
        View("EdgeMouseEnter", "EdgeMouseEnter", "OnEdgeMouseEnter"),
        View("EdgeMouseMove", "EdgeMouseMove", "OnEdgeMouseMove"),
        View("EdgeMouseLeave", "EdgeMouseLeave", "OnEdgeMouseLeave"),
        View("PaneMouseEnter", "PaneMouseEnter", "OnPaneMouseEnter"),
        View("PaneMouseMove", "PaneMouseMove", "OnPaneMouseMove"),
        View("PaneMouseLeave", "PaneMouseLeave", "OnPaneMouseLeave"),
        View("PaneScroll", "PaneScroll", "OnPaneScroll"),
        View("NodeDragStart", "NodeDragStarted", "OnNodeDragStart"),
        View("NodeDrag", "NodeDragging", "OnNodeDrag"),
        View("NodeDragStop", "NodeDragStopped", "OnNodeDragStop"),
        View("SelectionDragStart", "SelectionDragStarted", "OnSelectionDragStart"),
        View("SelectionDrag", "SelectionDragging", "OnSelectionDrag"),
        View("SelectionDragStop", "SelectionDragStopped", "OnSelectionDragStop"),
        View("SelectionStart", "SelectionStarted", "OnSelectionStart"),
        View("SelectionEnd", "SelectionEnded", "OnSelectionEnd"),
        View("ConnectStart", "ConnectStarted", "OnConnectStart"),
        View("ConnectEnd", "ConnectEnded", "OnConnectEnd"),
        View("ClickConnectStart", "ClickConnectStarted", "OnClickConnectStart"),
        View("ClickConnectEnd", "ClickConnectEnded", "OnClickConnectEnd"),
        View("ReconnectStart", "ReconnectStarted", "OnReconnectStart"),
        View("Reconnect", "EdgeReconnected", "OnReconnect"),
        View("ReconnectEnd", "ReconnectEnded", "OnReconnectEnd"),
        View("MoveStart", "ViewportMoveStarted", "OnMoveStart"),
        View("Move", "ViewportMoved", "OnMove"),
        View("MoveEnd", "ViewportMoveEnded", "OnMoveEnd"),
        Part("ZoomIn", ["FlowControls.ZoomedIn"], ["FlowControls.OnZoomIn"]),
        Part("ZoomOut", ["FlowControls.ZoomedOut"], ["FlowControls.OnZoomOut"]),
        Part("FitView", ["FlowControls.FitViewClicked"], ["FlowControls.OnFitView"]),
        Part("InteractiveChange", ["FlowControls.InteractiveChanged"], ["FlowControls.OnInteractiveChange"]),
        Part("MiniMapNodeClick", ["MiniMap.NodeClick"], ["FlowMiniMap.OnNodeClick"]),
        Part("HandleConnected", ["Handle.Connected"], ["FlowHandle.OnConnected"]),
        Part("ResizeStart", ["NodeResizer.ResizeStarted"], ["NodeResizer.OnResizeStart"]),
        Part("Resize", ["NodeResizer.Resizing"], ["NodeResizer.OnResize"]),
        Part("ResizeEnd", ["NodeResizer.ResizeEnded"], ["NodeResizer.OnResizeEnd"]),
        Part("ControlResizeStart", ["NodeResizeControl.ResizeStarted"], ["NodeResizeControl.OnResizeStart"]),
        Part("ControlResize", ["NodeResizeControl.Resizing"], ["NodeResizeControl.OnResize"]),
        Part("ControlResizeEnd", ["NodeResizeControl.ResizeEnded"], ["NodeResizeControl.OnResizeEnd"])
    ];

    /// <summary>Appelés en rafale : des appels consécutifs ne comptent qu'une fois (leur nombre dépend du navigateur).</summary>
    public static readonly IReadOnlySet<string> Continuous = new HashSet<string>
    {
        "NodeDrag", "SelectionDrag", "Move", "Resize", "ControlResize", "PaneScroll",
        "NodeMouseMove", "EdgeMouseMove", "PaneMouseMove", "StateChange", "NodesChange", "EdgesChange"
    };

    /// <summary>
    ///     Ignorés par défaut : ils accompagnent presque tout, et leur découpage n'est pas le même des deux côtés. Les gestes
    ///     qui les contrôlent les nomment dans <see cref="Gesture.Also" />.
    /// </summary>
    public static readonly IReadOnlySet<string> Background = new HashSet<string>
    {
        "StateChange", "NodesChange", "EdgesChange", "NodesReady",
        "NodeMouseEnter", "NodeMouseMove", "NodeMouseLeave", "EdgeMouseEnter", "EdgeMouseMove", "EdgeMouseLeave",
        "PaneMouseEnter", "PaneMouseMove", "PaneMouseLeave"
    };
}
