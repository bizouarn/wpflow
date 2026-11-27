# Wpflow.Blazor

The Blazor version of Wpflow: the same graph editor as the WPF version, rendered in HTML/SVG, on top of the `Wpflow.Core` core (immutable model, changes, edge paths, viewport, connections, JSON). Names follow React Flow and the WPF version; each page of this folder also applies here for the concepts (`Node`, `Edge`, changes, handles…).

## Installation

Reference the `Wpflow.Blazor` project (or the NuGet package of the same name), then add the stylesheet to the host page (`App.razor`, `index.html`…):

```html
<link rel="stylesheet" href="_content/Wpflow.Blazor/wpflow.css" />
```

The JS bridge script (`_content/Wpflow.Blazor/wpflow.js`) is loaded by the component itself.

## First graph

```razor
@using Wpflow.Blazor
@using Wpflow.Core

<div style="height: 500px">
    <FlowView @bind-Nodes="_nodes" @bind-Edges="_edges">
        <FlowMiniMap />
        <FlowControls />
    </FlowView>
</div>

@code {
    private IReadOnlyList<Node> _nodes =
    [
        new Node("1", new XYPosition(0, 0), "input", "Input"),
        new Node("2", new XYPosition(0, 120), "output", "Output")
    ];

    private IReadOnlyList<Edge> _edges = [new Edge("e1-2", "1", "2", Animated: true, MarkerEnd: EdgeMarker.ArrowClosed)];
}
```

The container must have a height (the view fills its parent). `@bind-Nodes` / `@bind-Edges` / `@bind-Viewport` publish the state after each interaction.

<!-- exemple-blazor: premier-graphe -->

## State: the changes

As in React Flow, an interaction never modifies the lists: it produces `NodeChange` / `EdgeChange` values, published by `OnNodesChange` / `OnEdgesChange` and then applied (`AutoApplyChanges`, true by default) with `Changes.Apply`. With `AutoApplyChanges="false"` (controlled mode), you apply the changes yourself. A connection then only raises `OnConnect`, as in WPF: create the edge there. `ApplyNodeChanges` / `ApplyEdgeChanges` allow you to inject some.

<!-- exemple-blazor: etat-controle -->

## Node content

Built-in types: `input`, `output`, `default`, `group` (`<FlowDefaultNode Node="n" />`). For custom content, one template per type (`NodeTemplates`) or a single template (`NodeTemplate`):

```razor
<FlowView @bind-Nodes="_nodes" @bind-Edges="_edges" NodeTemplates="_templates" />

@code {
    private Dictionary<string, RenderFragment<Node>> _templates = new()
    {
        ["card"] = n => @<div class="wf-node-default">
            <NodeResizer />
            <input class="nodrag" value="@n.Data" />
            <FlowHandle Type="HandleType.Target" Position="Position.Left" />
            <FlowHandle Type="HandleType.Source" Position="Position.Right" />
        </div>
    };
}
```

HTML controls (`input`, `button`, `select`…) and any element with the `nodrag` class do not drag the node; `nopan` prevents the view from panning, `nowheel` leaves the mouse wheel to the element. `Node.DragHandle` restricts dragging to a CSS selector (`.grip`).

<!-- exemple-blazor: nodes-personnalises -->

## FlowView

### Data and state

| Parameter | Default | Description |
|---|---|---|
| `Nodes`, `Edges`, `Viewport` (+ `…Changed`) | | Data and view, bindable. |
| `AutoApplyChanges` | true | False: controlled mode. |
| `IsLocked` (+ `IsLockedChanged`) | false | No dragging, connecting or selecting (padlock of the controls). |
| `HistoryEnabled`, `HistoryLimit` | true, 100 | Undo / redo (Ctrl+Z, Ctrl+Y). No effect in controlled mode: the history belongs to the application. |
| `NodesSource`, `EdgesSource`… | | See [MVVM](#mvvm). |
| `Debug` | false | Logs the changes to the console. |

### Viewport

`MinZoom` (0.5), `MaxZoom` (2), `FitViewOnInit`, `FitViewPadding`, `FitViewOptions`, `TranslateExtent`, `NodeExtent`, `NodeOrigin`, `SnapGrid` (snapping step, `null` = none), `OnlyRenderVisibleElements` (virtualisation: nodes and edges outside the view are not rendered; with `FitViewOnInit`, after the first fit, otherwise as soon as the size of the view is known).

### Interactions

`PanOnDrag`, `PanOnDragButtons` (`[0, 1]`), `PanOnScroll`, `PanOnScrollSpeed`, `PanOnScrollMode`, `ZoomOnScroll`, `ZoomOnPinch`, `ZoomOnDoubleClick`, `PreventScrolling`, `NodesDraggable`, `NodesConnectable`, `ElementsSelectable`, `EdgesReconnectable`, `SelectNodesOnDrag`, `SelectionOnDrag`, `SelectionMode` (`Partial` / `Full`), `SelectionKey` (`Shift`), `MultiSelectionKey` (`Control`), `PanActivationKey` (`Space`), `ZoomActivationKey` (`Control`), `DeleteKeys`, `DeleteKeyEnabled`, `NodeDragThreshold`, `ConnectionDragThreshold`, `PaneDragThreshold`, `AutoPanOnNodeDrag`, `AutoPanOnConnect`, `AutoPanOnSelection`, `AutoPanOnNodeFocus`, `AutoPanSpeed`, `EdgeHitWidth`, `ElevateNodesOnSelect`, `ElevateEdgesOnSelect`, `ZIndexMode`.

Keyboard: Delete / Backspace deletes, Ctrl+A selects everything, Ctrl+C / X / V clipboard (copies offset by 20 px), Ctrl+Z / Ctrl+Y history, arrow keys move the selection (Shift: ×4, `SnapGrid` otherwise 5 px), Escape clears the selection or cancels a click-based connection, Enter / Space selects the node that has focus, Space + drag scrolls the view. Touch: two-finger pinch.

### Connections

`ConnectionMode` (`Strict` / `Loose`), `IsValidConnection`, `ConnectionRadius` (20 px), `ReconnectRadius` (24 px), `ConnectOnClick` (click on a handle, then on another), `ConnectionLineType`, `ConnectionLineTemplate` (receives the connection state, with the wire path in `Path`), `AutoConnect` (adds the edge), `DefaultEdgeType`, `DefaultEdgeOptions` (`edge => edge with { Animated = true }`). Reconnection: drag the end of an edge onto another handle (releasing in empty space leaves it in place); a simple click near an end remains a click on the edge. As in WPF, it raises `OnConnectStart` (`IsReconnection`), `OnReconnectStart`, then `OnReconnect` if it succeeds, `OnReconnectEnd` and `OnConnectEnd`. `FlowHandle`: `Type`, `Position`, `HandleId`, `Offset`, `IsConnectable`, `IsConnectableStart`, `IsConnectableEnd`, `IsValidConnection`, `OnConnected`. `CurrentConnection` exposes the connection in progress (`useConnection`).

<!-- exemple-blazor: connexions -->

### Rendering

`ShowBackground`, `BackgroundVariant` (`Dots`, `Lines`, `Cross`), `BackgroundGap`, `BackgroundSize`, `BackgroundLineWidth`, `BackgroundColor`, `BackgroundOffset`, `AdditionalBackgrounds` (list of `BackgroundLayer`), `Dark` (or the `--wf-*` CSS variables), `DefaultMarkerColor`, `NodeTemplate(s)`, `EdgeTypes` (one `IEdgePathProvider` per type; it receives the edge being drawn in `EdgeGeometry.Edge`, for example so that a single ERD link type varies according to `Edge.Data`), `EdgeTemplates` (full drawing of an edge type), `EdgeLabelTemplate`, `MarkerTemplates` (custom SVG marker, `Edge.MarkerEndKey`).

### Accessibility

`NodesFocusable`, `EdgesFocusable`, `DisableKeyboardA11y`, `AnnounceSelection` (`aria-live` region), `NodeAriaLabelProvider`, `EdgeAriaLabelProvider`, roles and `aria-selected` on nodes and edges.

### Events

`OnNodesChange`, `OnEdgesChange`, `OnConnect`, `OnConnectStart`, `OnConnectEnd`, `OnClickConnectStart`, `OnClickConnectEnd`, `OnReconnectStart`, `OnReconnect`, `OnReconnectEnd`, `OnNodeClick`, `OnNodeDoubleClick`, `OnNodeContextMenu`, `OnNodeMouseEnter`, `OnNodeMouseLeave`, `OnEdgeClick`, `OnEdgeDoubleClick`, `OnEdgeContextMenu`, `OnEdgeMouseEnter`, `OnEdgeMouseLeave`, `OnPaneClick`, `OnPaneContextMenu`, `OnPaneMouseEnter`, `OnPaneMouseLeave`, `OnPaneMouseMove`, `OnNodeMouseMove`, `OnEdgeMouseMove`, `OnPaneScroll`, `OnStateChange`, `OnSelectionContextMenu`, `OnNodeDragStart`, `OnNodeDrag`, `OnNodeDragStop`, `OnSelectionDragStart`, `OnSelectionDrag`, `OnSelectionDragStop`, `OnSelectionStart`, `OnSelectionEnd`, `OnSelectionChange`, `OnMoveStart`, `OnMove`, `OnMoveEnd`, `OnInit`, `OnNodesInitialized`, `OnError`, `OnDrop` (drag and drop from a palette, position in flow coordinates), `OnBeforeDelete` (returning false cancels), `OnDelete`. Hover and context-menu listeners are only attached if a handler is provided.

As in WPF, a double click gives **click, double click, click**, and a right click on a selection raises `OnSelectionContextMenu` then `OnNodeContextMenu`. The second click is held until the browser's double click (at most 500 ms, if an element of your model stops it); both then arrive in the same handler.

#### Knowing which element of the node was clicked

For `OnNodeClick`, `OnNodeDoubleClick` and `OnNodeContextMenu`, `NodeEventArgs.TargetData` contains the `data-*` attributes of the pressed element and of its parents inside the node (the closest one wins), without the `data-` prefix:

```razor
@foreach (var column in table.Columns)
{
    <div data-column="@column.Name">@column.Name <small>@column.Type</small></div>
}
```

```csharp
void OnDoubleClick(NodeEventArgs e)
{
    if (e.TargetData.TryGetValue("column", out var column)) EditColumn(e.Node.Id, column);
}
```

The retained element is the one under the pointer at press time. The dictionary is empty on hover and if no element carries an attribute. The node itself is excluded. A click on an HTML control (`button`, `input`, `select`, `textarea`, `a`) does not raise `OnNodeClick`: use its own `@onclick`.

### Methods (`@ref`)

Viewport: `SetViewport(viewport, duration)`, `SetCenter`, `FitBounds`, `FitView(options)`, `ZoomTo`, `ZoomIn`, `ZoomOut`, `ZoomBy` (all animatable with a `duration`). Queries: `GetNode`, `GetEdge`, `GetInternalNode`, `GetIntersectingNodes`, `IsNodeIntersecting`, `GetNodeConnections`, `GetIncomers`, `GetOutgoers`, `GetNodesBounds`, `SelectedNodes`, `SelectedEdges`. Editing: `AddNodes`, `AddEdges`, `UpdateNode`, `UpdateNodeData`, `UpdateEdge`, `UpdateEdgeData`, `DeleteElements`, `DeleteSelected`, `SelectAll`, `ClearSelection`, `Copy`, `Cut`, `Paste`, `Undo`, `Redo`, `CanUndo`, `CanRedo`, `ClearHistory`, `SetLocked`. States: `NodesInitialized`, `CanPaste`, `IsViewportAnimating`, `IsConnecting`, `Zoom`, `AbsoluteNodes`. Conversion: `ScreenToFlow`, `FlowToScreen`, `ClientToFlow`. Saving: `ToJson()` / `LoadJson(json)` (React Flow format, see [Saving and JSON](learn/12-serialization.md)), `SaveImage("graph.png")` (downloads the real rendering as PNG or SVG: nodes with custom content, styles and theme; `vector: true` for the simplified drawing), `ExportSvg()` (the simplified drawing: rectangles and text).

## Components

| Component | Role |
|---|---|
| `FlowHandle` | Connection point, to place in a node template. |
| `FlowDefaultNode` | Built-in nodes (`input`, `output`, `default`, `group`). |
| `FlowControls` | Zoom +/−, fit, lock; `ShowZoom`, `ShowFitView`, `ShowInteractive`, `Horizontal`, `PanelPosition`, `ExtraContent`, `AnimationDuration`, `FitViewOptions`, `OnZoomIn/Out`, `OnFitView`, `OnInteractiveChange`. |
| `FlowMiniMap` | Overview; `Width`, `Height`, `PanelPosition`, `NodeColor`, `NodeStrokeColor`, `NodeStrokeWidth`, `NodeBorderRadius`, `MaskColor`, `Pannable`, `Zoomable`, `InversePan`, `ZoomStep`, `OnNodeClick`. |
| `FlowPanel` | Anchors its content to one of the 8 positions (`PanelPosition`). |
| `NodeResizer`, `NodeResizeControl` | Resize handles (4 corners, 4 sides) or a free handle; `NodeMinWidth/Height`, `NodeMaxWidth/Height`, `KeepAspectRatio`, `ShowResizer`, `AutoScale`, `Variant`, `ShouldResize`, `OnResizeStart`, `OnResize`, `OnResizeEnd`, `Accent`. |
| `NodeRotator` | Rotation handle (`Node.Rotation`, Shift = `SnapAngle` step). |
| `NodeToolbar` | Toolbar of the selected node; `ToolbarPosition`, `Offset`, `Align`, `IsToolbarVisible`. |
| `EdgeToolbar` | Toolbar of a selected edge, inside `EdgeLabelTemplate`. |
| `SelectionToolbar` | Toolbar of a multiple selection; `MinSelected`, `Offset`, `NodeIds`, `IsToolbarVisible`. |

## Theme

CSS variables on `.wf-flow`: `--wf-bg`, `--wf-dot`, `--wf-node-bg`, `--wf-node-border`, `--wf-node-text`, `--wf-node-selected`, `--wf-edge`, `--wf-edge-selected`, `--wf-handle`, `--wf-handle-bg`, `--wf-connect`, `--wf-panel-bg`, `--wf-panel-border`, `--wf-minimap-mask`, `--wf-resize-accent`. `Dark="true"` applies the dark theme.

<!-- exemple-blazor: theme -->

## MVVM

As in WPF ([MVVM](learn/13-mvvm.md)), the view can work on **your ViewModels** instead of lists of `record`s: same mappings, same rules for rewriting, refusing and undoing (the logic is shared, in the core).

```razor
<FlowView NodesSource="_vm.Tasks" EdgesSource="_vm.Links" NodeMapping="_mapping"
          EdgeFactory="@(e => new LinkVm(e.Id, e.Source, e.Target))"
          @bind-SelectedNodeItems="_selected" ControllerChanged="c => _vm.Flow = c"
          NodeTemplates="_templates" />

@code {
    // In a field: a mapping written in the markup would be recreated, and the source re-read, on every render.
    private readonly NodeMapping _mapping = new() { SelectedPath = nameof(TaskVm.IsSelected) };
    private IReadOnlyList<object> _selected = [];
}
```

Do not give `Nodes` / `Edges` at the same time as a source.

| Parameter | Description |
|---|---|
| `NodesSource`, `EdgesSource` | Collections of ViewModels (`ObservableCollection`, `INotifyPropertyChanged` items). |
| `NodeMapping`, `EdgeMapping` | Property names; see [the mappings](learn/13-mvvm.md#mappings). `Node.Data` is the ViewModel. |
| `NodeFactory`, `EdgeFactory` | Create the ViewModel of an element added by the view (connection, paste, `LoadJson`). |
| `SelectedNodeItems`, `SelectedEdgeItems`, `SelectedNode` (+ `…Changed`) | Selection as ViewModels. A **new** value selects; the same one, given again at each render without a binding, is ignored. `SelectedNodes` remains the list of `record`s. |
| `ControllerChanged` | Hands an `IFlowController` (asynchronous) to your ViewModel. |
| `History` | Your history (`Wpflow.Blazor.IFlowHistory`) instead of the view's own. |

`NodeModel(id)` and `EdgeModel(id)` give the ViewModel of an element from an event.

**No commands**: `EventCallback`s take their place. For what a WPF command *replaces*:

| WPF | Blazor |
|---|---|
| `ConnectCommand` (replaces creation, `CanExecute` validates) | `AutoConnect="false"`, `OnConnect` adds your link, `IsValidConnection` validates |
| `NodesDeleteCommand`, `EdgesDeleteCommand` | `OnBeforeDelete`: returning false cancels **all** of the deletion; then do your own (it does not enter the view's history) |
| `DropCommand` | `OnDrop` |

Two points specific to Blazor:

- **Rendering**: node templates are memoised. A `PropertyChanged`, even of an unmapped property (a title), redraws the node concerned. However, a collection **inside** a ViewModel (the columns of a table) must be signalled by the ViewModel: `Columns.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Columns));`.
- **Threads**: modify the ViewModels on the component's render context (`InvokeAsync` from a timer or a background task). Changes within the same burst produce only one render.

<!-- exemple-blazor: mvvm -->

## Differences from the WPF version

Both views have the same events and the same settings, which a test checks (`ViewParityTests`), and the same gestures raise them in the same order. The parity table is the [`GestureMatrix`](../tests/Scenarios.Shared/GestureMatrix.cs): for each gesture, the exact sequence of events, played with real input in both views. What remains are the platform differences:

| WPF | Blazor |
|---|---|
| Synchronous methods | Asynchronous (`await`) |
| Commands (`ICommand`) | `EventCallback` |
| Resources `wpflow-node-*`, `wpflow-edge-*`, `wpflow-marker-*` | `NodeTemplates`, `EdgeTypes`, `EdgeTemplates`, `MarkerTemplates` |
| `ColorMode`, `EdgeStroke`, `EdgeStrokeThickness`, `SelectionOutline*`, `ConnectionLineStyle` | `Dark` and CSS variables |
| `DefaultEdgeOptions` | also `DefaultEdgeType` |
| Empty `DeleteKeys` | also `DeleteKeyEnabled` |
| `SnapToGrid` + `SnapGrid` | `SnapGrid` (`null`: no snapping) |
| `SelectedNodes` (ViewModels) | `SelectedNodeItems` |
| `Zoom` bound in both directions | `Zoom` read-only, `ZoomTo` |
| `FitViewOnInit` false by default | true by default |
| `EdgeHitWidth`: 10 by default | 20 by default |
| `RenderToBitmap`, `SaveImage` (file) | `SaveImage` (download) |
| Double click on the background: `PaneClick`, start of zoom, `PaneClick` | `PaneClick`, `PaneClick`, then the zoom (the browser sends its `dblclick` after the second click) |
| `BeforeDelete` (event) | `OnBeforeDelete` (function that answers) |
| Dragging the background clears the selection on press | Dragging the background keeps it; a click clears it |
| Clipboard shared by all views of the application | Clipboard specific to each view |

- **Mouse editing**: same gestures; the JS bridge (`wpflow.js`) measures the nodes and handles and follows the pointer during a gesture.
- **Image export**: `SaveImage` captures the browser's rendering (the layer is cloned with its computed styles then rasterised); content that cannot be serialised to SVG (canvas, video, images from another origin) does not appear in it. `vector: true` or `ExportSvg()` give a simplified drawing (rectangle + `Node.Data`).
- **Blazor Server or WebAssembly**: the library works with both (it depends on `Microsoft.AspNetCore.Components.Web`). On Server, every pointer movement during a gesture is a network round trip; WebAssembly runs everything in the browser. The demo exists for both (`demo-blazor`, `demo-blazor-wasm`).
- **Virtualisation**: with `OnlyRenderVisibleElements`, nodes outside the view are not rendered. With `FitViewOnInit` (true by default), everything is first rendered and measured, for the duration of the initial fit. Without it, a node never approached is never measured, as in WPF: `FitView()` and the minimap ignore it, and `OnNodesInitialized` waits for it. With or without virtualisation, a panning step does not redo the nodes or the edges (layer memoised, even with `@bind-Viewport`) as long as the visible elements stay the same; during a view movement, an edge or label template that reads the parent's state is therefore not redrawn (same rule as for node templates below). If almost everything is visible at once, virtualisation brings nothing. A node is only rendered if it has changed and the path of an edge is cached: a graph of 1,500 nodes stays smooth.
- **Memoised edge templates**: an edge template (`EdgeTemplates`) or label template (`EdgeLabelTemplate`) is only called again if its edge, its ends, one of its two nodes or a setting of the view changes, or if it is re-declared (a new delegate redraws). What it reads otherwise (parent state, another node) must arrive through `Edge.Data`.
- **Memoised node templates**: a template (`NodeTemplate`) only renders when its node changes, like a memoised React component; a template that reads the parent's state must receive that state through the `Node` (`Node.Data`) or be re-declared (a new delegate redraws). A state specific to the display of a node (a field being renamed, a checked box) goes into a **child component** rendered by the template, which redraws itself; the handles it moves are measured again.
- **Fields inside a node**: keys typed in a field are not handled by the graph (Delete, arrows, Ctrl+A, Ctrl+Z stay with the field), and your `@onkeydown` receives them. Add `class="nodrag"` to the field.
- **Containers**: the view can be under a parent with `transform: scale()`, in a page or a scrolled container, or in a hidden tab that is then shown; a rotated or skewed parent (`rotate`, `skew`) is not taken into account.
- **Loading another document**: call `ClearHistory()` after replacing `Nodes` and `Edges`, otherwise « undo » brings back the previous document.
- **Blazor Server and large graphs**: measurements are split into messages of less than 32 KB, so SignalR's default limit does not need to be raised.
