# 9. Interactions and connections

*React Flow equivalent: Interaction props, Connection validation, Drag and Drop, Keyboard.*

## Gestures

| Action | Gesture |
|---|---|
| Zoom | Mouse wheel (centred on the cursor); touch pinch; `FlowControls` |
| Pan the view | Drag the background; middle click; hold **Space** + drag (even over a node) |
| Move nodes | Drag a node (all selected nodes follow) |
| Select | Click; Ctrl/Shift + click to add or remove; **Shift + drag** on the background for a rectangle |
| Select all / deselect all | Ctrl+A / Esc |
| Delete | Delete or Backspace |
| Undo / redo | Ctrl+Z / Ctrl+Y (or Ctrl+Shift+Z) |
| Connect | Drag from one handle to another |
| Reconnect | Grab an edge near its end and drop it on another handle |

## Keyboard

| Key | Effect |
|---|---|
| Tab / Shift+Tab | Selects the next / previous node |
| ← ↑ → ↓ | Moves the selection by 5 px (20 with Shift) |
| Esc | Deselects |

The keyboard acts when `FlowView` has focus (a click on the surface gives it focus). If focus is inside a control of a node (a `TextBox`…), Tab and the arrow keys stay with that control.

## Locking

`IsLocked="True"` disables moving nodes, connecting, selecting and deleting; panning and zooming remain possible. The padlock button of `FlowControls` acts on this property.

## Snapping and limits

```xml
<flow:FlowView SnapToGrid="True" SnapGrid="20" />
```

```csharp
Flow.NodeExtent      = new Rect(0, 0, 2000, 1200);    // nodes: they cannot leave this area (flow coordinates)
Flow.TranslateExtent = new Rect(-100, -100, 800, 500); // view: never shows more than this area
Flow.MinZoom = 0.2; Flow.MaxZoom = 4;
```

(`Rect` here means `Wpflow.Core.Rect`.) If the area is smaller than the view, it is centred.

## Connections

```csharp
Flow.ConnectionMode = ConnectionMode.Strict;   // default: a source handle only connects to a target handle
Flow.ConnectionMode = ConnectionMode.Loose;    // any handle to any handle
Flow.ConnectionRadius = 20;                    // snap distance around the cursor (flow units)
```

While dragging, a line follows the cursor. On release, the nearest compatible handle **within the radius** is chosen (for equal distances, the one of the opposite type). In `Loose` mode, the connection is oriented according to the type of the **starting** handle: starting from a target reverses the connection.

### Validating a connection

```csharp
Flow.IsValidConnection = c => c.Source != c.Target                      // no loops
                           && !Flow.Edges.Any(e => e.Target == c.Target); // a single input per node
```

The predicate receives the `Connection`, already oriented. Returning `false` cancels the gesture. Exact duplicates are always refused.

### Reacting to a connection

```csharp
Flow.ConnectRequested += (_, e) => Log($"{e.Connection.Source} -> {e.Connection.Target}");
```

In uncontrolled mode the edge is added afterwards, with the identifier `xy-edge__{source}{sourceHandle}-{target}{targetHandle}` (same format as React Flow).

## Drag and drop from a palette

`FlowView` accepts drops (`AllowDrop`). Start the drag from any element with `DragDrop.DoDragDrop`, then create the node at the drop position:

```csharp
// Palette: an element that starts the drag
private void OnPaletteMove(object sender, MouseEventArgs e)
{
    if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement { Tag: string type } item)
        DragDrop.DoDragDrop(item, new DataObject("wpflow-node-type", type), DragDropEffects.Copy);
}

// Surface: receiving
Flow.DropRequested += (_, e) =>
{
    if (e.Data.GetData("wpflow-node-type") is not string type) return;
    var node = new Node(Guid.NewGuid().ToString(), e.Position - new XYPosition(75, 20), type, $"{type} node");
    Flow.Nodes = Flow.Nodes.Append(node).ToList();
};
```

`e.Position` is already in flow coordinates (zoom and offset taken into account).

## Programmatic selection

```csharp
Flow.SelectNode("2");                    // alone
Flow.SelectNode("3", additive: true);    // in addition
Flow.Nodes = Flow.Nodes.Select(n => n with { Selected = n.Id == "2" }).ToList();
```

## Surface events

| Event | When |
|---|---|
| `NodeChangesRequested` / `EdgeChangesRequested` | before a batch of changes is applied |
| `ConnectRequested` | valid connection created with the mouse |
| `DropRequested` | object dropped |
| `StateChanged` | nodes, edges, viewport or lock changed (to draw your own bound views) |

## Additional events

`ConnectStarted` / `ConnectEnded` (release position and whether a connection was made, to « create a node when the wire is dropped in empty space »), `NodeDoubleClick`, `NodeContextMenuRequested` (and `Edge…`, `Pane…`, `SelectionContextMenuRequested`), `NodeMouseEnter`/`Move`/`Leave`, `NodeDragStarted`/`NodeDragging`/`NodeDragStopped`, `ViewportMoveStarted`/`Ended`, `FlowInitialized`, `BeforeDelete` (cancellable), `ErrorOccurred`… The full list is in the [`FlowView` reference](../reference/flowview.md#additional-events). To know which column or area of a node is targeted, mark it with `flow:FlowView.TargetData` ([details](../reference/flowview.md#knowing-which-part-of-a-node-is-targeted)).

## Interaction settings

Everything React Flow sets through props is a property of `FlowView`: `NodesDraggable`, `NodesConnectable`, `ElementsSelectable`, `PanOnDrag`, `PanOnScroll`, `ZoomOnScroll`, `ZoomOnDoubleClick`, `SelectionOnDrag`, `SelectionMode`, `DeleteKeys`, `SelectionKey`, `MultiSelectionKey`, `PanActivationKey`, `NodeOrigin`, `ConnectOnClick`, `ConnectionLineType`, drag thresholds, auto-panning… ([table](../reference/flowview.md#advanced-interaction-options)). On an element of a node template, `flow:FlowView.NoDrag="True"` / `NoPan` / `NoWheel` prevent the node from being moved, the view from being panned or zooming, and `flow:NodeHost.DragHandle="grip"` (with `Node.DragHandle = "grip"`) limits dragging to one area.

## Copy and paste, transitions, image

- Ctrl+C / Ctrl+X / Ctrl+V (or `Flow.Copy()`, `Cut()`, `Paste()`, `FlowCommands.Copy`…): the selected nodes, their descendants and the edges that link them are duplicated with new ids.
- `Flow.SetCenter(x, y, zoom, TimeSpan.FromMilliseconds(400))`, `FitView(new FitViewOptions(Duration: …, NodeIds: …))`, `FitBounds`, `SetViewport`: the viewport moves with a transition; a user gesture interrupts it.
- `Flow.SaveImage("graph.png")` or `Flow.RenderToBitmap()` exports the whole graph (to print, pass the bitmap to `PrintDialog`).
- **Contextual zoom**: bind to `Flow.Viewport` (or `Zoom`) and change how a node is displayed according to the zoom level in your ViewModel.
- **Collisions, guide lines, proximity connection**: listen to `NodeDragging` and use `Flow.GetIntersectingNodes(node)` or `GraphQueries`.

Next: [Computing with a flow](10-computing-flows.md).
