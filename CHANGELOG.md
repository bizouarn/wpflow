# Changelog

## 1.2.1

### .NET 10 target

- `Wpflow.Core` also targets `net10.0`, `Wpflow.Wpf` also targets `net10.0-windows`, and `Wpflow.Blazor` also targets `net10.0` (its `Microsoft.AspNetCore.Components.Web` reference is 10.0 on that target). The `net8.0` and `net472` / `netstandard2.0` targets are unchanged.

The three packages (`Wpflow.Core`, `Wpflow.Wpf`, `Wpflow.Blazor`) share the same version number. At `dotnet pack`, package validation compares the API with the published version (`PackageValidationBaselineVersion` in `Directory.Build.props`): a removed API makes the build fail, unless the break is declared in `CompatibilitySuppressions.xml`.

## 1.2.0

### Fix (Blazor navigation)

- Blazor: when leaving a page that contains a graph (per-page interactivity, improved navigation), the JS bridge could call the view that had already been destroyed, which logged an error in the console (« There is no tracked object with id… »). A view removed from the page no longer sends anything.

### .NET Framework and .NET Standard targets

- `Wpflow.Core` also targets `net472` and `netstandard2.0` (so .NET Core 2.0 and later), with a dependency on `System.Text.Json` 8 on these two targets. The `net8.0` version is unchanged: no dependency, with the native APIs.
- `Wpflow.Wpf` also targets `net472`. Selection announcements to screen readers are not emitted there. WPF does not exist on .NET Core 2.x.
- An application on .NET Core 2.x must set `SuppressTfmSupportBuildWarnings` (see the [quick start](doc/learn/01-quickstart.md)).
- `Wpflow.Blazor` stays on `net8.0`.

### Additions

- The three packages have an icon (`PackageIcon`, source `assets/logo.svg`).
- `GraphUtils.GetHandleSide(node, position)`: the side a handle faces once its node is rotated.

### Fixes (rotated nodes)

- WPF and Blazor: edges did not follow a rotated node (`Node.Rotation`). Their end point, the direction they leave from, the wire of a connection in progress and the search for the targeted handle now take the rotation into account. `GraphUtils.GetHandlePosition` and `GetHandleCenter` return the point as it is displayed; the `HandleInfo`s remain expressed in the unrotated node.
- Blazor: the handles of a rotated node were measured from its bounding rectangle, so they were in the wrong place.

### Fixes (Blazor controlled mode)

- Blazor: with `AutoApplyChanges="false"`, a connection still requested the addition of the edge through `OnEdgesChange` after `OnConnect`; an application handling both created two overlapping edges. Only `OnConnect` is now raised, as in WPF. If your application relied on that addition request, create the edge in `OnConnect`.

### Fixes (Blazor groups)

- Blazor: the frame of a `group` node did not take the height given by `FixedHeight` (it stayed at 80 px), and the children seemed to leave the group. It now fills the node.

### Fixes (Blazor background and virtualisation)

- Blazor: the `BackgroundVariant.Cross` background only showed corners (┌), the pattern tile cutting off the crosses. The default half-length of the crosses goes from 6 to 3, as in WPF.
- Blazor: with `FitViewOnInit="false"`, `OnlyRenderVisibleElements` never turned on. Virtualisation now takes effect as soon as the size of the view is known; an edge between nodes never measured is discarded based on their assumed size, as in WPF.

### Performance (Blazor, large graphs)

Measured in WebAssembly in Chrome, 855 nodes and 908 edges all visible: a panning step goes from 280 to 520 ms per frame to about 17 ms.

- A parent that listens to `ViewportChanged` (`@bind-Viewport`), `OnMove`, `OnMoveStart`, `OnMoveEnd` or `OnStateChange` no longer rebuilds all nodes and edges at each step.
- `OnlyRenderVisibleElements`: the layer is only rebuilt when an element must appear or disappear, with the same keep margin as in WPF.
- Pressing and releasing the background, or holding Space, no longer rebuilds the layer.
- The minimap no longer redraws its rectangles at each panning step.

Same graph, with an edge template and a parent listening to dragging and hovering (development configuration):

| Gesture or step | Before | After |
|---|---|---|
| Loading (controlled mode) | 78 s | 9 s |
| Dragging a node, per step | 1,820 ms | 300 ms |
| Wire being drawn, per step | 990 ms | 140 ms |
| Selection rectangle, per step | 1,270 ms | 150 ms |
| Hovering the background, per step | 620 ms | 55 ms |

- Each edge and each label is memoised, like nodes: moving a node only redraws its edges.
- The measurements of a large graph, which the JS bridge splits into dozens of messages (`MeasuredNode.More`), are applied only once: a single change (`OnNodesChange`) instead of one per message.
- The minimap only redraws its rectangles when nodes change (not when virtualisation makes some appear), and only attaches a click handler to them if `OnNodeClick` is listened to. « Performance » page of the documentation site, 1,000 nodes with virtualisation (development configuration, no listening parent and no template): panning the view 119 → 83 ms per step, zoom 480 → 306 ms. Dragging a node there stays around 200 to 240 ms: the minimap accounts for about half of it.
- The wire of a connection is drawn outside the layer; hovering, the selection rectangle and parent renders that change nothing no longer rebuild it.

### Behaviour changes (Blazor)

- An edge template (`EdgeTemplates`) or label template (`EdgeLabelTemplate`) is only called again if its edge, its ends, one of its two nodes or a view setting changes, or if it is re-declared (a new delegate). A template that reads the state of the parent or of another node must receive that state through the data.
- The wire of a connection now passes above the nodes, as in WPF (it used to pass below). It is in its own `svg` (`wf-connection-layer`).
- During a view change, the parent's render no longer redraws edge and label templates that would read its state (node templates already behaved this way): pass that state through the data, or re-declare the template. The same rule applies to the minimap's `NodeColor`.
- With `OnlyRenderVisibleElements`, edges close to the view (300 px) are drawn in advance, as nodes are.
- `OnStateChange`, when only the view moves, is raised just before rendering instead of after.

### Fixes (hostile integration conditions)

- Blazor: under a parent with `transform: scale()`, clicks, dragging, panning, pinching, the minimap and fitting landed off target. The JS bridge now gives the layout size of the view and the scale of its parents (`OnRootScale`).
- Blazor: after content inserted above the graph (with no size change or scrolling), a drop or a hover landed off target. The position is now re-read when the pointer enters and during a drag-and-drop.
- Blazor: a global CSS rule on `svg` (`max-width: 100%`, icon size) erased edges or shrank the minimap; a `min-width` on `button` widened the controls.
- Blazor: after deleting the node that had focus, the keyboard (Ctrl+Z) could no longer reach the view.
- Blazor: on a freshly loaded page, or for a very brief gesture, the last movement before release was lost: node or view stopped short of the mouse, connection not made.
- Blazor Server: a button released during a circuit outage never arrived; the node stayed in a dragging state after reconnection. The JS bridge now re-sends the end of the gesture until the server responds (`OnGestureLost`).
- WPF: an exception thrown by a handler during a connection left the mouse captured by the view, which no longer responded to the next click.
- WPF: a closed view stayed in memory as long as its ViewModels (`NodesSource`, `EdgesSource`), its `SelectedNodes` / `SelectedEdges` collection or its `History` lived. These subscriptions are now weak.

### Behaviour changes

- `NodeSourceAdapter` and `EdgeSourceAdapter`: the source no longer holds the adapter. The views keep it themselves; code that creates an adapter directly must keep a reference to it for as long as it must track the source.

## 1.1.0

### Breaking changes

> This minor version contains an API break, chosen instead of a 2.0.0.

- `EdgeGeometry.Options` is removed, together with the corresponding fifth parameter of the constructor and of `Deconstruct`. A custom path provider (WPF or Blazor) that read it now reads `geometry.Edge?.PathOptions`.

### Behaviour changes

Both views now behave the same way, with WPF as the reference.

- Blazor: a double click on a node or an edge gives click, double click, click (previously click, click, double click). The last two events arrive in the same handler: a click handler that undoes what the double click did does it before any render.
- Blazor: a right click on a selection raises `OnSelectionContextMenu`, then `OnNodeContextMenu` for the targeted node (previously only the first).
- Blazor: `SelectAll()` and Ctrl+A now also select edges, and hidden nodes, as in WPF.
- Blazor: in controlled mode (`AutoApplyChanges="false"`), the view no longer keeps a history.
- WPF: in controlled mode, `CanUndo` stays false (`Undo` already did nothing there).
- WPF, MVVM: undo and redo now rewrite all mapped properties into the ViewModels, not only position, selection, size and ends. A change your code made after the gesture is not undone.
- Blazor: `NodeDragThreshold`, `ConnectionDragThreshold` and `PaneDragThreshold` default to 1 px, as in WPF (3 before), and `ReconnectRadius` to 24 px (10).
- WPF and Blazor: a press stays a click as long as the pointer does not move more than 4 px away, or beyond the drag threshold if it is larger. The greatest distance travelled counts: a drag brought back to its start is not a click.
- WPF and Blazor: reconnection only starts on the first movement. A click near the end of an edge is a click on the edge.
- WPF: pulling a handle from which no connection can start (locked view, non-connectable node or handle) no longer moves the node or the view.
- WPF: with `SelectNodesOnDrag` false, a dragged node stays unselected, as the documentation says (it used to be selected at the start of the drag).
- Blazor: a click on a node of a multiple selection keeps the selection; Ctrl + click on a selected edge no longer deselects it.
- Blazor: pressing an HTML control inside a node (`button`, `input`…) no longer selects the node.
- Blazor: a locked, non-selectable node, or one grabbed outside its `DragHandle` area, lets the drag move the view.
- Blazor: `Node.ExpandParent` now works, as in WPF (it was ignored). Dragging such a child beyond its parent enlarges the parent; to the left and at the top, the parent moves back and its other children stay in place on screen.
- Blazor: when both ends of an edge are within reach (short edge), pressing reconnects the nearer one, the target when they are equally close (it always reconnected the source).
- Blazor: the wheel on the minimap (`Zoomable`) zoomed twice as much per notch. With `ZoomStep` at 10, a notch zooms as on the surface, as in WPF. To get back the old sensitivity: `ZoomStep="5"`. Wheel scrolls expressed in lines are also converted to pixels, as on the surface.
- Blazor: with `ElementsSelectable` false, `SelectOnly` now only deselects, as in WPF.

### Additions

- **MVVM in Blazor**: `NodesSource`, `EdgesSource`, `NodeMapping`, `EdgeMapping`, `NodeFactory`, `EdgeFactory`, bound selection (`SelectedNodeItems`, `SelectedEdgeItems`, `SelectedNode`), `Controller` / `ControllerChanged`, `History`, `NodeModel` / `EdgeModel`, `ToJson(selector)`. `EventCallback`s take the place of commands.
- The MVVM logic is in the core (`NodeSourceAdapter`, `EdgeSourceAdapter`, `NodeMappingBase`, `EdgeMappingBase`); `NodeMapping` and `EdgeMapping` derive from them, with no change for existing code.
- MVVM: all properties of `Node` and `Edge` can be mapped (rotation, z-index, style, path and marker options, handle positions, extent, origin…).
- WPF: a command for each event that had none (hover, dragging, selection rectangle, reconnection, view movement, wheel, initialisation, errors, changes).
- WPF: `Deleted`, `SelectionChanged` and `NodesReady` events (with their commands); `AutoConnect`, `HistoryEnabled`, `HistoryLimit` settings.
- Blazor: `OnNodeMouseMove`, `OnEdgeMouseMove`, `OnPaneScroll`, `OnStateChange`; `AutoPanOnNodeFocus`, `EdgeHitWidth` settings; `NodesInitialized`, `CanPaste`, `IsViewportAnimating`, `IsConnecting`, `Zoom`, `AbsoluteNodes`.
- WPF: `ExportSvg()`, with the same drawing as in Blazor (`SvgExport` in the core).
- `EdgeGeometry.Edge`: path providers receive the edge being drawn (WPF and Blazor).
- Blazor: `NodeEventArgs.TargetData`, the `data-*` attributes of the clicked element inside a node.
- WPF: knowing which part of a node was clicked, double-clicked or targeted by the context menu. The attached property `FlowView.TargetData` marks an element of a template; `ElementEventArgs.TargetData` gives the nearest mark and `ElementEventArgs.OriginalSource` the pressed element.
- WPF: public methods that exist in Blazor are now on `FlowView`: `ApplyNodeChanges`, `ApplyEdgeChanges`, `ClearHistory`, `ClearSelection`, `SelectAll`, `SelectOnly`, `DeleteSelected`, `CenterOn(XYPosition)`, `FlowToScreen`, `GetIncomers`, `GetOutgoers`, `SetLocked`. They are synchronous, except `DeleteSelected`.
- WPF, MVVM: `EdgeMapping.DataPath`, `MarkerStartPath` and `MarkerEndPath`. The path and markers of an edge can depend on its ViewModel.
- WPF, MVVM: `FlowView.SelectionContextMenuCommand`, the command that was missing next to the `SelectionContextMenuRequested` event.
- WPF: `FlowView.UpdateNodeInternals(id)` re-measures a node whose handles were moved without a size change (the view does not detect this on its own; in Blazor it is automatic).
- WPF: `FlowView.CommandsReceiveEventArgs` (false by default) makes the click, double-click and context-menu commands receive the `ElementEventArgs`.

### Fixes

- Blazor: a double click on a node never arrived and zoomed the background.
- Blazor: the wheel on a `Zoomable` minimap did not stop the page from scrolling, and the browser logged an error in the console (`preventDefault` in a passive listener).
- Blazor Server: the SignalR circuit dropped at the first display as soon as a few dozen rich nodes (or a single 150-column table) were shown, the measurements going out in a single message of more than 32 KB. They are now sent in several messages.
- Blazor: edges did not follow handles that were moved, added or removed without a change of node size (reordered columns). The content of nodes is now observed and re-measured.
- Blazor: no `@onkeydown` set on a field inside a node ever fired (Enter, Escape…), because the JS bridge stopped key propagation.
- Blazor: a right click or a middle click cleared the selection and raised `OnPaneClick`, even on a node; `OnSelectionContextMenu` therefore was never raised after a real right click.
- Controlled mode: typing on an unselected node moved the old selection to its place until the new one was applied (`DragSelection.CollectStart`, WPF and Blazor; new overload with `additive`).
- WPF: `RenderToBitmap` / `SaveImage` captured the image with the current viewport instead of the whole graph. The image was offset, truncated or entirely white.
- WPF: when the view lost mouse capture in the middle of a gesture (dialog box, other application), `NodeDragStopped`, `ViewportMoveEnded`, `SelectionEnded`, `ConnectEnded` and `ReconnectEnded` were never raised, and nodes stayed marked `Dragging`. The gesture now ends as on a release in empty space.
- WPF: a double-click handler that opened a dialog box left the view capturing the mouse, and the node followed the mouse with the button released.
- WPF: a slightly shaky click was lost when a drag threshold exceeded 4 px.
- WPF: the second click of a two-click connection also raised `NodeClick`.
- WPF and Blazor: a simple click within `ReconnectRadius` of an edge end started a reconnection; in WPF, `EdgeClick` and the double click were swallowed.
- Blazor: `OnDrop` did not work in a browser (`dragover` was not cancelled).
- Blazor, controlled mode: `OnSelectionChange` was never raised.
- Blazor: a lost release (dialog box, other application) left the gesture following the mouse; an abandoned connection or reconnection did not raise `OnConnectEnd` / `OnReconnectEnd`.
- Blazor: reconnection raised neither `OnConnectStart` nor `OnConnectEnd` (`ConnectStartEventArgs.IsReconnection` tells them apart).
- Blazor: a click on the minimap, a toolbar or the second click of a connection also raised `OnPaneClick` and cleared the selection; with `OnNodeClick`, a click on a minimap node no longer moved the view.
- Blazor: a double click on a panel zoomed the graph, and on a button of a node raised `OnNodeDoubleClick`.
- Blazor: a right-button drag that moved the view then opened the context menu.
- Blazor: `OnPaneMouseEnter`, `OnPaneMouseMove` and `OnPaneMouseLeave` did not take hovered nodes and edges into account; as in WPF, the background is whatever is neither of them.
- Blazor: after pressing a toolbar, the next press could be ignored.
- WPF, MVVM: when a ViewModel adjusted or refused a value written by the view (a clamping setter, a locked element), the view kept its own. A ViewModel that raises `PropertyChanged` while the write happens is now read back.
- WPF, MVVM: `UpdateNode` and `UpdateNodeData` wrote nothing into the ViewModel; the view and the ViewModel diverged until the ViewModel's next change, which cancelled the update.
- WPF, MVVM: `UpdateEdge` and `UpdateEdgeData` only wrote the ends; the label, type, animation, selection and other mapped properties are now written too.
- WPF, MVVM: undoing the first resize of a node left its size in the ViewModel.
- WPF: `UpdateNode` silently did nothing until the view's template was applied (before the first display).
- Blazor: `ExportSvg` wrote an edge's own colour as is; it is now escaped.

### Performance

No change of API or behaviour. Measurements on 1,500 nodes (Blazor) and 2,000 nodes (WPF, core), Release.

- Blazor: a panning step allocates 2 KB instead of 284 KB (0.32 → 0.07 ms); moving a node allocates 2.2 MB instead of 4.2 MB (11 → 7 ms; with groups 13 → 8 ms).
- WPF: moving a node in a graph with groups allocates 0.7 MB instead of 2.1 MB (2.7 → 2.0 ms); only the absolute-coordinate copies of the nodes that moved are rebuilt. With a large selection, no string is rebuilt at each frame any more.
- WPF and Blazor: dragging a wire no longer builds the list of candidate handles at each movement (331 KB → 0.1 KB, 0.36 → 0.10 ms with 4,000 handles).

### Tests and publication

- Publication runs the Blazor end-to-end tests and the WPF real-mouse self-tests before `pack`.
- WPF self-tests return a failing exit code (an `ECHEC` line, an exception or a timeout); `tests/run-wpf-selftests.ps1` chains them.
- `tests/run-wpf-selftests-sandbox.ps1` chains the same self-tests in Windows Sandbox, off screen: the desktop stays usable.
- `tests/consumers/run.ps1` installs the packages into fresh applications, outside the solution.
- End-to-end tests: `WPFLOW_BROWSER_CHANNEL` chooses the browser (for example `msedge`); `WPFLOW_SERVER_DEMO_TFM=net10.0` allows running the Server demo on a machine without the .NET 8 runtime.

## 1.0.1

- First version of `Wpflow.Blazor` (Server and WebAssembly).

## 1.0.0

- First version of `Wpflow.Core` and `Wpflow.Wpf`.
