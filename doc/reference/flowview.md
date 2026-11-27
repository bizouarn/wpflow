# FlowView

`Wpflow.Wpf.FlowView`: `ContentControl`. The surface of the graph (equivalent of `<ReactFlow>` and `useReactFlow`).

The `Content` is an **overlay** displayed above the graph; it only accepts one element (use a `Grid` to put several).

```xml
<flow:FlowView Nodes="{Binding Nodes}" Edges="{Binding Edges}" FitViewOnInit="True" />
```

## Data

| Property | Type | Default | Description |
|---|---|---|---|
| `Nodes` | `IReadOnlyList<Node>` | empty | The nodes. Bindable in `TwoWay`. |
| `Edges` | `IReadOnlyList<Edge>` | empty | The edges. Bindable in `TwoWay`. |
| `Viewport` | `Viewport` | `(0, 0, 1)` | Position and zoom. Bindable in `TwoWay`. Constrained by `TranslateExtent`. |
| `AutoApplyChanges` | `bool` | `true` | Applies the changes to `Nodes`/`Edges` itself. `false` = controlled mode. |
| `NodesSource` / `EdgesSource` | `IEnumerable?` | `null` | Collections of ViewModels projected into `Nodes`/`Edges` (the [MVVM](../learn/13-mvvm.md) mode). Undo/Redo stay active. |
| `NodeMapping` / `EdgeMapping` | `NodeMapping?` / `EdgeMapping?` | `Id`, `X`, `Y` / `Id`, `Source`, `Target` | Names of the ViewModel properties; all the properties of `Node` and `Edge` can be mapped. |
| `NodeFactory` / `EdgeFactory` | `Func<Node, object?>?` / `Func<Edge, object?>?` | `null` | Create the ViewModel of a node or an edge added by the view (drop, connection). Dependency properties: can be bound to a ViewModel property. |
| `SelectedNodes` / `SelectedEdges` | `IList` | empty | Selected elements (ViewModels when there is a source, otherwise `Node`/`Edge`). Writing = selecting; a bound `ObservableCollection` is updated in place. |
| `SelectedNode` | `object?` | `null` | First selected node; writing it selects it alone. |

## View

| Property | Type | Default | Description |
|---|---|---|---|
| `MinZoom` / `MaxZoom` | `double` | 0.5 / 2 | Zoom bounds. |
| `TranslateExtent` | `Rect?` | `null` | Area (flow coordinates) outside which the view cannot go. Centred if smaller than the view. |
| `FitViewOnInit` | `bool` | `false` | Frames the graph once all nodes have been measured. |
| `FitViewPadding` | `double` | 0.1 | Relative margin of `FitView`. |

## Interaction

| Property | Type | Default | Description |
|---|---|---|---|
| `IsLocked` | `bool` | `false` | Forbids moving, connecting, selecting (mouse) and deleting; panning and zooming stay active. |
| `NodesDraggable` | `bool` | `true` | Nodes can be moved (in addition to `Node.Draggable`). |
| `NodesConnectable` | `bool` | `true` | Connections can start or end on the handles (in addition to `Node.Connectable`, `Handle.IsConnectable*`). |
| `ElementsSelectable` | `bool` | `true` | Nodes and edges are selectable (in addition to `Node.Selectable`); false: they can only be deselected. |
| `EdgesReconnectable` | `bool` | `true` | The ends of an edge can be grabbed to reconnect it. |
| `AutoPanOnNodeDrag` / `AutoPanOnConnect` / `AutoPanOnSelection` | `bool` | `true` | The view scrolls when the mouse approaches an edge (40 px) during a move, a connection or a selection box. |
| `AutoPanSpeed` | `double` | 15 | Maximum scrolling, in pixels per frame (~16 ms). |
| `SnapToGrid` / `SnapGrid` | `bool` / `double` | `false` / 15 | Snapping when moving. |
| `NodeExtent` | `Rect?` | `null` | Area in which nodes must stay (flow coordinates). |
| `ConnectionMode` | `ConnectionMode` | `Strict` | `Strict` or `Loose`. |
| `ConnectionRadius` | `double` | 20 | Snap distance of a connection. |
| `IsValidConnection` | `Func<Connection,bool>?` | `null` | Custom validation (dependency property, bindable). |

## Appearance

| Property | Type | Default | Description |
|---|---|---|---|
| `ColorMode` | `ColorMode` | `Light` | `Light` or `Dark`. |
| `BackgroundVariant` | `BackgroundVariant` | `Dots` | `None`, `Dots`, `Lines`, `Cross`. |
| `BackgroundGap` | `double` | 20 | Spacing of the pattern (flow units). |
| `BackgroundColor` | `Brush?` | `null` | Colour of the pattern; `null` = `Flow.Dots`. |
| `EdgeStroke` | `Brush?` | `null` | Colour of the edges; `null` = `Flow.Edge`. |
| `EdgeStrokeThickness` | `double` | 1 | Thickness of the edges. |
| `EdgeLabelTemplate` | `DataTemplate?` | `null` | Content of edge labels (DataContext = `Edge`). |
| `EdgeHitWidth` | `double` | 10 | Width of the clickable area of an edge. |
| `SelectionOutlineGap` / `SelectionOutlineThickness` | `double` | 3 / 1.5 | Selection outline of nodes with a template. |
| `ConnectionLineStyle` | `Style` | grey, 1.5 | Style (`TargetType="Path"`) of the connection preview. |
| `Background` | `Brush` | `Flow.Background` | Background of the surface (WPF property). |

## Performance

| Property | Type | Default | Description |
|---|---|---|---|
| `OnlyRenderVisibleElements` | `bool` | `false` | Hides nodes and edges outside the visible area. |

## Events

| Event | Arguments | Description |
|---|---|---|
| `NodeChangesRequested` | `NodeChangesEventArgs` (`Changes`) | A batch of `NodeChange`s is about to be applied. |
| `EdgeChangesRequested` | `EdgeChangesEventArgs` (`Changes`) | A batch of `EdgeChange`s is about to be applied. |
| `ConnectRequested` | `ConnectEventArgs` (`Connection`) | Valid connection created with the mouse. |
| `DropRequested` | `FlowDropEventArgs` (`Position`, `Data`) | Object dropped; `Position` in flow coordinates. |
| `ConnectStarted` | `ConnectStartEventArgs` (`NodeId`, `HandleId`, `HandleType`, `IsReconnection`) | A wire is drawn from a handle (or from a grabbed edge end). |
| `ConnectEnded` | `ConnectEndEventArgs` (`Position`, `FromNodeId`, `ToNodeId`, `Connection`, `IsValid`) | Wire released, successful or not; `Position` (flow coordinates) allows creating a node when released in empty space. |
| `NodeDoubleClick` / `EdgeDoubleClick` | `ElementEventArgs` (`Kind`, `Id`, `Element`, `Position`, `OriginalSource`, `TargetData`) | Double click. |
| `NodeContextMenuRequested` / `EdgeContextMenuRequested` / `PaneContextMenuRequested` | `ElementEventArgs` | Right click (the event is not marked handled: a WPF `ContextMenu` also opens). |
| `StateChanged` | `EventArgs` | Nodes, edges, viewport, lock or theme changed. |

The first two are raised **before** the changes are applied, whether or not they are applied (controlled mode).

### Knowing which part of a node is targeted

Mark elements of a template with the attached property `FlowView.TargetData` (any object). On click, double click and context menu, `ElementEventArgs.TargetData` gives the nearest mark of the pressed element (`null` without a mark, and on hover); `ElementEventArgs.OriginalSource` gives the pressed WPF element. The marked element must be hit-testable at click time: give it a `Background`, even `Transparent`.

```xml
<Border Background="Transparent" flow:FlowView.TargetData="{Binding Data.ColumnName}">
    <TextBlock Text="{Binding Data.ColumnName}" />
</Border>
```

On the command side, `CommandsReceiveEventArgs="True"` makes `NodeClickCommand`, `EdgeClickCommand`, `PaneClickCommand`, the double-click commands and the context-menu commands receive the `ElementEventArgs`, instead of their usual parameter.

## Methods

| Member | Description |
|---|---|
| `void FitView()` | Frames all measured nodes (`fitView`). |
| `void ZoomIn()` / `ZoomOut()` | Zoom ×1.2 / ÷1.2 around the centre. |
| `void ZoomBy(double factor)` | Relative zoom around the centre, bounded by `MinZoom`/`MaxZoom`. |
| `XYPosition ScreenToFlow(Point p)` | Converts a point in control coordinates to flow coordinates. |
| `void SelectNode(string id, bool additive = false)` | Selects a node. |
| `void SelectOnly(string? nodeId, string? edgeId)` | Selects only this node or only this edge; with two `null`s, deselects everything. |
| `void SelectAll()` / `ClearSelection()` | Selects all nodes and all edges / deselects everything. |
| `Task<bool> DeleteSelected()` | Deletes the selection like `DeleteElementsAsync`, even if the view is locked (the Delete key, however, respects `IsLocked`). The task is already completed without `BeforeDeleteAsync`. |
| `Point FlowToScreen(XYPosition position)` | Inverse of `ScreenToFlow`. |
| `void CenterOn(XYPosition position)` | Centres the view on a flow point, without changing the zoom. |
| `void ApplyNodeChanges(changes)` / `ApplyEdgeChanges(changes)` | Injects changes as a gesture would: `…ChangesRequested` is raised, then they are applied if `AutoApplyChanges`. Adds nothing to the history. |
| `IReadOnlyList<Node> GetIncomers(string nodeId)` / `GetOutgoers(string nodeId)` | Nodes linked by an incoming / outgoing edge. |
| `void SetLocked(bool locked)` | Changes `IsLocked` while keeping its binding. |
| `void Undo()` / `Redo()` | Undoes / redoes (in controlled mode, the view keeps no history and `CanUndo` stays false). |
| `string ExportSvg(double padding = 20, string? background = null)` | Simplified vector drawing (nodes as rectangles with their text, edges and arrows); for a faithful image, use `SaveImage`. |
| `void ClearHistory()` | Empties the view's history. |
| `string ToJson(Func<Node,object?>? data = null, bool indented = true)` | Exports the graph (React Flow format). With a source, `Node.Data` is only written via `data`. |
| `void LoadJson(string json)` | Loads a graph; with a source, goes through `NodeFactory` / `EdgeFactory`. |
| `IReadOnlyCollection<Node> AbsoluteNodes` | Nodes in absolute coordinates (to draw bound views). |
| `Brush ThemeBrush(string key)` | Brush of the current theme (`Flow.*`), falling back to the light theme. |

## Advanced interaction options

| Property | Type | Default | Description |
|---|---|---|---|
| `PanOnDrag` | `bool` | `true` | Dragging the background moves the view. |
| `SelectionOnDrag` | `bool` | `false` | Dragging the background draws a selection rectangle (the view then moves with Space or the middle button). |
| `SelectionMode` | `FlowSelectionMode` (`Partial` / `Full`) | `Partial` | The rectangle selects the nodes it touches or fully contains. |
| `SelectNodesOnDrag` | `bool` | `true` | Pressing a node selects it; false: it moves without being selected (selection on click). |
| `PanOnScroll` / `PanOnScrollSpeed` / `PanOnScrollMode` | `bool` / `double` / `Free`, `Vertical`, `Horizontal` | `false` / 0.5 / `Free` | The mouse wheel pans the view (Shift: horizontally); `ZoomActivationKey` forces zoom. |
| `ZoomOnScroll` / `ZoomOnPinch` / `ZoomOnDoubleClick` | `bool` | `true` | Zoom with the wheel, with pinch, on double click on the background (×2, animated). |
| `DeleteKeys` | `IReadOnlyList<Key>` | `Delete`, `Back` | Deletion keys; empty = no keyboard deletion. |
| `SelectionKey` / `MultiSelectionKey` / `ZoomActivationKey` | `ModifierKeys` | `Shift` / `Control` / `Control` | Modifiers: selection rectangle, adding to the selection, forced zoom. |
| `PanActivationKey` | `Key` | `Space` | While held, dragging pans the view even over a node. |
| `NodeDragThreshold` / `ConnectionDragThreshold` / `PaneDragThreshold` | `double` | 1 | Pixels to cross before the gesture starts. A press stays a click as long as the mouse does not move more than 4 px away, or the threshold if it is larger; a drag brought back to its start is not a click. If the view loses capture in the middle of a gesture (dialog box), the gesture ends and its end events are raised. |
| `NodeOrigin` | `XYPosition` | `(0, 0)` | Anchor point of the nodes (`(0.5, 0.5)` = centre); overridden by `Node.Origin`. |
| `ElevateNodesOnSelect` / `ElevateEdgesOnSelect` | `bool` | `true` / `false` | A selected element comes to the front. |
| `NodesFocusable` / `EdgesFocusable` | `bool` | `true` / `true` | Elements reached by Tab. |
| `DisableKeyboardA11y` | `bool` | `false` | Disables Tab and the arrow keys. |
| `AutoPanOnNodeFocus` | `bool` | `true` | The view scrolls to show the node reached with the keyboard. |
| `ConnectOnClick` | `bool` | `true` | Connects with two clicks (handle, then handle); Esc cancels. |
| `ConnectionLineType` | `Bezier`, `Straight`, `Step`, `SmoothStep`, `SimpleBezier` | `Bezier` | Shape of the connection preview. |
| `ConnectionLineTemplate` | `DataTemplate?` | `null` | Free rendering of the preview; `DataContext` = `ConnectionLineContext`. |
| `ReconnectRadius` | `double` | 24 | Reach (pixels) for grabbing the end of an edge, by dragging it; a simple click at that distance remains a click on the edge. If both ends are in reach, the nearest one is moved. |
| `DefaultEdgeOptions` | `Edge?` | `null` | Template of the edges created by connection (`Type`, `Animated`, markers, style…). |
| `DefaultMarkerColor` | `Brush?` | `null` | Colour of the markers; `null` = colour of the stroke. |
| `FitViewOptions` | `FitViewOptions?` | `null` | Options of `FitViewOnInit` and of the fit button. |
| `BackgroundSize` / `BackgroundLineWidth` / `BackgroundOffset` | `double?` / `double` / `double` | `null` / 1 / 0 | Background settings. |
| `AdditionalBackgrounds` | `IReadOnlyList<BackgroundLayer>?` | `null` | Patterns overlaid on the main background. |
| `NodeAriaLabelProvider` | `Func<Node, string?>?` | `null` | UI Automation name of nodes without an `AriaLabel` (`ariaLabelConfig`); to set before the nodes. |
| `ZIndexMode` | `FlowZIndexMode` | `Basic` | `Basic` (depth + selection brought to the front), `Auto` (a parent whose descendant is selected also comes to the front), `Manual` (only `Node.ZIndex` counts). |
| `PanOnDragButtons` | `FlowMouseButtons` | `Left`, `Middle` | Buttons that move the view; with `Right`, a drag does not reopen the context menu. |
| `PreventScrolling` | `bool` | `true` | The view consumes the wheel; false: it bubbles up to the parent (`ScrollViewer`). |
| `EdgeAriaLabelProvider` | `Func<Edge, string?>?` | `null` | UI Automation name of edges without an `AriaLabel`. |
| `AnnounceSelection` | `bool` | `true` | Announces the selection of a node or an edge to screen readers. |
| `Debug` | `bool` | `false` | Writes the applied changes to `System.Diagnostics.Trace`. |
| `BeforeDeleteAsync` | `Func<BeforeDeleteEventArgs, Task<bool>>?` | `null` | Asynchronous check before deletion; `false` cancels. Used by the delete key and by `DeleteElementsAsync`. |
| `NodesInitialized` | `bool` (read-only) | — | All visible nodes are measured. |
| `ConnectionState` | `FlowConnectionState?` (read-only) | `null` | Connection in progress: origin, position, target, validity (`useConnection`). |
| `AutoConnect` | `bool` | `true` | False: a connection raises `ConnectRequested` without creating an edge; the rest of the state is still applied by the view. |
| `HistoryEnabled` / `HistoryLimit` | `bool` / `int` | `true` / 100 | History of the view (no effect on `History`, the ViewModel's). |

Attached properties, to set on an element of a node template: `flow:FlowView.NoDrag="True"`, `NoPan`, `NoWheel` (equivalents of `nodrag`, `nopan`, `nowheel`), and `flow:NodeHost.DragHandle="grip"` for the area that moves the node when `Node.DragHandle = "grip"`.

## Additional events

| Event | Description |
|---|---|
| `FlowInitialized` | View ready (`onInit`). |
| `ErrorOccurred` (`FlowErrorEventArgs`: `Code`, `Message`) | Edge pointing to a missing node (`edge-node-missing`), once per edge (`onError`). |
| `BeforeDelete` (`BeforeDeleteEventArgs`: `Nodes`, `Edges`, `Cancel`) | Deletion imminent, cancellable (`onBeforeDelete`). |
| `NodeClick` / `EdgeClick` / `PaneClick` | Clicks (`ElementEventArgs`). |
| `NodeMouseEnter`/`Move`/`Leave`, `EdgeMouseEnter`/`Move`/`Leave`, `PaneMouseEnter`/`Move`/`Leave` | Hover (`ElementEventArgs`). |
| `NodeDragStarted` / `NodeDragging` / `NodeDragStopped` (`NodeDragEventArgs`) | Moving nodes, also for a multiple selection. |
| `SelectionStarted` / `SelectionEnded` (`SelectionBoxEventArgs`) | Selection rectangle. |
| `SelectionContextMenuRequested` | Right click on the selection (several nodes). |
| `EdgeReconnected` (`ReconnectEventArgs`) | End of an edge moved. |
| `ReconnectStarted` (`ElementEventArgs`) / `ReconnectEnded` (`ReconnectEndEventArgs`: `Edge`, `Connection`, `Succeeded`) | An end of an edge is grabbed / released, with or without a new connection. |
| `SelectionDragStarted` / `SelectionDragging` / `SelectionDragStopped` | Moving several nodes at once. |
| `NodeChangesRequested` / `EdgeChangesRequested` | Their argument has a **modifiable** `Changes`: a handler can filter or replace the changes before they are applied (middleware). |
| `ClickConnectStarted` / `ClickConnectEnded` | Connection with two clicks. |
| `ViewportMoveStarted` / `ViewportMoved` / `ViewportMoveEnded` | View movement (drag, wheel, transition; `ViewportMoved` also for programmatic changes). |
| `PaneScroll` | Wheel on the background. |
| `Deleted` (`DeletedEventArgs`: `Nodes`, `Edges`) | A deletion requested from the view took place (`onDelete`). |
| `SelectionChanged` (`FlowSelectionEventArgs`: `Nodes`, `Edges`) | The selection changed (`onSelectionChange`). |
| `NodesReady` | Once, when all nodes have been measured (`useNodesInitialized`). |

The same events exist in Blazor, and the same gestures raise them in the same order: a double click gives click, double click, click; a right click on a selection, the selection's menu then the node's.

## Instance methods (`useReactFlow`)

| Member | Description |
|---|---|
| `bool FitView(FitViewOptions? options)` | Frames the nodes: `Padding`, `MinZoom`, `MaxZoom`, `NodeIds`, `IncludeHiddenNodes`, `Duration` (transition). |
| `void SetViewport(Viewport v, TimeSpan duration = default)` | Moves the view, with a transition if `duration > 0`; a gesture interrupts it. `IsViewportAnimating`. |
| `void SetCenter(double x, double y, double? zoom = null, TimeSpan duration = default)` | Centres the view on a flow point. |
| `void FitBounds(Rect bounds, double? padding = null, TimeSpan duration = default, …)` | Frames a rectangle. |
| `void ZoomTo(double zoom, TimeSpan duration = default)` / `ZoomIn(TimeSpan)` / `ZoomOut(TimeSpan)` | Zoom, optionally animated. |
| `Node? GetNode(id)` / `Edge? GetEdge(id)` / `Node? GetInternalNode(id)` | Search by identifier (the last one in absolute coordinates). |
| `void AddNodes(...)` / `AddEdges(...)` | Addition (`AddEdges` avoids duplicates). |
| `void UpdateNode(id, Func<Node,Node>)` / `UpdateNodeData(id, data)` / `UpdateEdge(...)` / `UpdateEdgeData(...)` | Update by identifier. |
| `void UpdateNodeInternals(id)` | Measures a node and its handles again (`useUpdateNodeInternals`). Not needed when the node changes size or when a handle appears, disappears or changes property; necessary after only **moving** handles (margin changed by a style trigger, animation). |
| `bool DeleteElements(nodeIds, edgeIds)` / `Task<bool> DeleteElementsAsync(...)` | Deletion by identifiers (descendants and linked edges included; `Deletable`, `BeforeDelete` and, for the asynchronous version, `BeforeDeleteAsync` are respected). |
| `GetIntersectingNodes(Node` or `Rect, bool partially = true)` / `IsNodeIntersecting(...)` | Intersections, in absolute coordinates. |
| `GetNodeConnections(nodeId, HandleType? type, handleId)` | Edges of a node. |
| `Rect? GetNodesBounds(IEnumerable<string>? ids)` | Bounding rectangle. |
| `bool Copy()` / `Cut()` / `Paste()` / `CanPaste` | Graph clipboard (Ctrl+C / X / V, commands `FlowCommands.Copy`…). Copies receive new identifiers and are selected; each paste offsets by 20 px. |
| `RenderTargetBitmap? RenderToBitmap(padding, scale, background)` / `bool SaveImage(path, padding, scale)` | Export of the whole graph as an image (« Download Image » and « Printing » examples). |
| `Canvas? PortalLayer` / `AddToPortal(element, x, y)` / `RemoveFromPortal(element)` | Content positioned in flow coordinates, above the nodes (`ViewportPortal`). |

## Keyboard and mouse

See [Interactions](../learn/09-interactions.md#gestures).

## Node templates

A node of type `T` is rendered with the `DataTemplate` keyed `wpflow-node-T` found in the resources (searched from `FlowView`). Built-in types: `default`, `input`, `output`, `group`.

## Accessibility

`FlowView` creates a `FlowViewAutomationPeer` (`Pane`). See [Performance and accessibility](../learn/11-performance-accessibility.md#accessibility).

## React Flow correspondence

| React Flow | Wpflow |
|---|---|
| `nodes`, `edges`, `onNodesChange`, `onEdgesChange` | `Nodes`, `Edges`, `NodeChangesRequested`, `EdgeChangesRequested` |
| `onConnect` | `ConnectRequested` |
| `defaultViewport` / `useViewport` | `Viewport` |
| `fitView` (prop) / `fitView()` | `FitViewOnInit` / `FitView()` |
| `minZoom`, `maxZoom` | `MinZoom`, `MaxZoom` |
| `snapToGrid`, `snapGrid` | `SnapToGrid`, `SnapGrid` |
| `nodeExtent`, `translateExtent` | `NodeExtent`, `TranslateExtent` |
| `connectionMode`, `connectionRadius`, `isValidConnection` | `ConnectionMode`, `ConnectionRadius`, `IsValidConnection` |
| `onlyRenderVisibleElements` | `OnlyRenderVisibleElements` |
| `colorMode` | `ColorMode` |
| `nodesDraggable`, `nodesConnectable`, `elementsSelectable`, `edgesReconnectable` | `NodesDraggable`, `NodesConnectable`, `ElementsSelectable`, `EdgesReconnectable` (and `IsLocked` to lock everything) |
| `autoPanOnNodeDrag`, `autoPanOnConnect`, `autoPanOnSelection`, `autoPanSpeed` | `AutoPanOnNodeDrag`, `AutoPanOnConnect`, `AutoPanOnSelection`, `AutoPanSpeed` |
| `onConnectStart`, `onConnectEnd` | `ConnectStarted`, `ConnectEnded` (+ `ConnectStartCommand`, `ConnectEndCommand`) |
| `onNodeDoubleClick`, `onEdgeDoubleClick`, `on*ContextMenu` | `NodeDoubleClick`, `EdgeDoubleClick`, `*ContextMenuRequested` (+ `*Command` commands) |

## State, commands and MVVM

See the [MVVM](../learn/13-mvvm.md) guide for details; quick reference:

| Property | Type | Description |
|---|---|---|
| `Zoom` | `double` | Zoom, `TwoWay`; writing it zooms around the centre. |
| `IsConnecting` | `bool` (read-only) | Connection being drawn. |
| `CanUndo` / `CanRedo` | `bool` (read-only) | Availability of the history (the view's, or `History`). |
| `Controller` | `IFlowController?` | Handed to the ViewModel through a `OneWayToSource` binding: `FitView`, `ZoomTo`, `CenterOn`, selection, undo, coordinate conversions. |
| `History` | `IFlowHistory?` | The ViewModel's history (`Checkpoint`, `Undo`, `Redo`) instead of the view's. |
| `ConnectCommand` | `ICommand?` | Parameter `Connection`; replaces the creation of the edge, a `false` `CanExecute` refuses it. |
| `NodesDeleteCommand` / `EdgesDeleteCommand` | `ICommand?` | Parameter: list of elements; replaces the automatic deletion, a `false` `CanExecute` cancels it. |
| `DropCommand` | `ICommand?` | Parameter `FlowDropEventArgs`. |
| `NodeDragStopCommand` | `ICommand?` | Parameter: list of moved elements. |
| `NodeClickCommand` / `EdgeClickCommand` | `ICommand?` | Parameter: the clicked element. |
| `PaneClickCommand` | `ICommand?` | Parameter: flow `XYPosition`. |
| `CommandsReceiveEventArgs` | `bool` (false) | The click, double-click and context-menu commands receive the `ElementEventArgs` (including `TargetData`). |
| other `…Command` | `ICommand?` | One command per event ([list](../learn/13-mvvm.md#commands)); it receives the event's arguments. |

`FlowCommands` (static class of `RoutedUICommand`s): `FitView`, `ZoomIn`, `ZoomOut`, `Undo`, `Redo`, `SelectAll`, `ClearSelection`, `DeleteSelection`, `ToggleLock`.
