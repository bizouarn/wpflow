using System.Windows;
using System.Windows.Controls;
using Wpflow.Core;
using Wpflow.Wpf;

namespace Scenarios.Wpf;

// Le journal de la matrice des évènements : chaque évènement public, avec son sujet (voir Scenarios.EventCatalog).
public partial class MainWindow
{
    internal EventJournal? Journal { get; private set; }

    /// <summary>Abonne le journal à la vue. Les compagnons (contrôles, minimap) sont ajoutés par <see cref="AddCompanions" />.</summary>
    internal EventJournal AttachJournal()
    {
        var j = Journal = new EventJournal();
        var f = Flow;

        f.FlowInitialized += (_, _) => j.Add("Init");
        f.NodesReady += (_, _) => j.Add("NodesReady");
        f.ErrorOccurred += (_, _) => j.Add("Error");
        f.StateChanged += (_, _) => j.Add("StateChange");
        f.NodeChangesRequested += (_, _) => j.Add("NodesChange");
        f.EdgeChangesRequested += (_, _) => j.Add("EdgesChange");
        f.ConnectRequested += (_, e) => j.Add("Connect", EventJournal.Link(e.Connection));
        f.DropRequested += (_, _) => j.Add("Drop");
        f.BeforeDelete += (_, e) => j.Add("BeforeDelete", EventJournal.Selection(e.Nodes, e.Edges));
        f.Deleted += (_, e) => j.Add("Delete", EventJournal.Selection(e.Nodes, e.Edges));
        f.SelectionChanged += (_, e) => j.Add("SelectionChange", EventJournal.Selection(e.Nodes, e.Edges));

        f.NodeClick += (_, e) => j.Add("NodeClick", Describe(e));
        f.EdgeClick += (_, e) => j.Add("EdgeClick", e.Id);
        f.PaneClick += (_, _) => j.Add("PaneClick");
        f.NodeDoubleClick += (_, e) => j.Add("NodeDoubleClick", Describe(e));
        f.EdgeDoubleClick += (_, e) => j.Add("EdgeDoubleClick", e.Id);
        f.NodeContextMenuRequested += (_, e) => j.Add("NodeContextMenu", Describe(e));
        f.EdgeContextMenuRequested += (_, e) => j.Add("EdgeContextMenu", e.Id);
        f.PaneContextMenuRequested += (_, _) => j.Add("PaneContextMenu");
        f.SelectionContextMenuRequested += (_, _) => j.Add("SelectionContextMenu");

        f.NodeMouseEnter += (_, e) => j.Add("NodeMouseEnter", e.Id);
        f.NodeMouseMove += (_, e) => j.Add("NodeMouseMove", e.Id);
        f.NodeMouseLeave += (_, e) => j.Add("NodeMouseLeave", e.Id);
        f.EdgeMouseEnter += (_, e) => j.Add("EdgeMouseEnter", e.Id);
        f.EdgeMouseMove += (_, e) => j.Add("EdgeMouseMove", e.Id);
        f.EdgeMouseLeave += (_, e) => j.Add("EdgeMouseLeave", e.Id);
        f.PaneMouseEnter += (_, _) => j.Add("PaneMouseEnter");
        f.PaneMouseMove += (_, _) => j.Add("PaneMouseMove");
        f.PaneMouseLeave += (_, _) => j.Add("PaneMouseLeave");
        f.PaneScroll += (_, _) => j.Add("PaneScroll");

        f.NodeDragStarted += (_, e) => j.Add("NodeDragStart", EventJournal.Ids(e.NodeIds));
        f.NodeDragging += (_, e) => j.Add("NodeDrag", EventJournal.Ids(e.NodeIds));
        f.NodeDragStopped += (_, e) => j.Add("NodeDragStop", EventJournal.Ids(e.NodeIds));
        f.SelectionDragStarted += (_, e) => j.Add("SelectionDragStart", EventJournal.Ids(e.NodeIds));
        f.SelectionDragging += (_, e) => j.Add("SelectionDrag", EventJournal.Ids(e.NodeIds));
        f.SelectionDragStopped += (_, e) => j.Add("SelectionDragStop", EventJournal.Ids(e.NodeIds));
        f.SelectionStarted += (_, _) => j.Add("SelectionStart");
        f.SelectionEnded += (_, _) => j.Add("SelectionEnd");

        f.ConnectStarted += (_, e) => j.Add("ConnectStart", EventJournal.Handle(e.NodeId, e.HandleId));
        f.ConnectEnded += (_, e) => j.Add("ConnectEnd", EventJournal.End(e.FromNodeId, e.FromHandleId, e.Connection));
        f.ClickConnectStarted += (_, e) => j.Add("ClickConnectStart", EventJournal.Handle(e.NodeId, e.HandleId));
        f.ClickConnectEnded += (_, e) =>
            j.Add("ClickConnectEnd", EventJournal.End(e.FromNodeId, e.FromHandleId, e.Connection));
        f.ReconnectStarted += (_, e) => j.Add("ReconnectStart", e.Id);
        f.EdgeReconnected += (_, e) => j.Add("Reconnect", e.OldEdge.Id + " " + EventJournal.Link(e.Connection));
        f.ReconnectEnded += (_, e) =>
            j.Add("ReconnectEnd", e.Edge.Id + " " + (e.Connection == null ? "-" : EventJournal.Link(e.Connection)));

        f.ViewportMoveStarted += (_, _) => j.Add("MoveStart");
        f.ViewportMoved += (_, _) => j.Add("Move");
        f.ViewportMoveEnded += (_, _) => j.Add("MoveEnd");
        return j;
    }

    /// <summary>Les contrôles et la minimap, posés sur la vue le temps de la matrice.</summary>
    internal (FlowControls Controls, MiniMap Map) AddCompanions()
    {
        var controls = new FlowControls();
        var map = new MiniMap { Zoomable = true };
        controls.ZoomedIn += (_, _) => Journal?.Add("ZoomIn");
        controls.ZoomedOut += (_, _) => Journal?.Add("ZoomOut");
        controls.FitViewClicked += (_, _) => Journal?.Add("FitView");
        controls.InteractiveChanged += (_, on) => Journal?.Add("InteractiveChange", on ? "true" : "false");
        map.NodeClick += (_, e) => Journal?.Add("MiniMapNodeClick", e.Node.Id);
        Flow.Content = new Grid { Children = { map, controls } };
        return (controls, map);
    }

    internal void RemoveCompanions() => Flow.Content = null;

    // Les évènements des compagnons placés dans un modèle de node (MainWindow.xaml).
    private void OnResizeStarted(object? sender, NodeResizeEventArgs e) => Journal?.Add("ResizeStart", e.NodeId);
    private void OnResizing(object? sender, NodeResizeEventArgs e) => Journal?.Add("Resize", e.NodeId);
    private void OnResizeEnded(object? sender, NodeResizeEventArgs e) => Journal?.Add("ResizeEnd", e.NodeId);
    private void OnControlResizeStarted(object? sender, NodeResizeEventArgs e) => Journal?.Add("ControlResizeStart", e.NodeId);
    private void OnControlResizing(object? sender, NodeResizeEventArgs e) => Journal?.Add("ControlResize", e.NodeId);
    private void OnControlResizeEnded(object? sender, NodeResizeEventArgs e) => Journal?.Add("ControlResizeEnd", e.NodeId);
    private void OnHandleConnected(object? sender, Connection c) => Journal?.Add("HandleConnected", EventJournal.Link(c));
    private void OnToolbarClick(object sender, RoutedEventArgs e) => ToolbarClicks++;

    internal int ToolbarClicks { get; private set; }
}
