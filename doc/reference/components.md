# Components

All in `Wpflow.Wpf` (`xmlns:flow="clr-namespace:Wpflow.Wpf;assembly=Wpflow.Wpf"`).

## Handle

Connection point, to place in a node template (child of the root `Grid`). Equivalent of `<Handle>`.

| Property | Type | Default | Description |
|---|---|---|---|
| `Type` | `HandleType` | `Source` | `Source` (start) or `Target` (end). |
| `Position` | `Position` | `Top` | Side of the node: `Left`, `Top`, `Right`, `Bottom`. |
| `HandleId` | `string?` | `null` | Identifier (`Edge.SourceHandle` / `TargetHandle`). |
| `Offset` | `double` | `NaN` | Offset along the side from the top (left/right) or from the left (top/bottom); `NaN` = centred. |
| `ConnectionStatus` | `HandleConnectionStatus` | `None` | Driven by the view during a wire: `Origin`, `Valid`, `Invalid`; the default drawing changes colour, a `Template` can use it. |
| `IsConnectable` | `bool` | `true` | The handle accepts connections. |
| `IsConnectableStart` / `IsConnectableEnd` | `bool` | `true` | A connection can start from / end at this handle. |
| `IsValidConnection` | `Func<Connection,bool>?` | `null` | Validator specific to the handle, in addition to the view's. |
| `Connected` event (`Connection`) | | | A connection has just ended at this handle or started from it (`onConnect` of a handle). |

The handle is 8 × 8 by default (`Width` / `Height` can be changed) and overflows by half its size over the edge of its container; its click area is widened by 6 px. It is styled like a `Control` (`Background`, `BorderBrush`, `BorderThickness`, `Width`, `Height`) and a `Template` replaces its drawing: target `flow:Handle` in a `Style` (see [Customization](../learn/14-customization.md#handles)).

## MiniMap

Overview of the graph; clicking or dragging recentres the viewport. Place it in the `Content` of `FlowView`, or elsewhere with `Flow="{Binding ElementName=Flow}"`.

Default values: 200 × 150, bottom right, margin of 15. Resize with `Width`/`Height`, reposition with `HorizontalAlignment`/`VerticalAlignment`/`Margin`. Colours: `Flow.MiniMapBackground`, `Flow.MiniMapNode`, `Flow.MiniMapNodeSelected`, `Flow.MiniMapMask`.

| Property | Type | Default | Description |
|---|---|---|---|
| `NodeColor` / `NodeStrokeColor` | `Func<Node, Brush?>?` | `null` | Colour and outline per node (`nodeColor`, `nodeStrokeColor`); a `null` result = theme. |
| `NodeStrokeWidth` / `NodeBorderRadius` | `double` | 2 / 3 | Outline and corner radius. |
| `NodeRenderer` | `Action<DrawingContext, Node, Rect>?` | `null` | Free drawing of a node (`nodeComponent`). |
| `MaskColor` | `Brush?` | `null` | Veil outside the view; `null` = `Flow.MiniMapMask`. |
| `Pannable` / `Zoomable` / `InversePan` / `ZoomStep` | `bool` / `bool` / `bool` / `double` | `true` / `false` / `false` / 10 | Dragging recentres the view, the mouse wheel zooms (`zoomable`), inverted direction, sensitivity. |
| `NodeClick` event (`MiniMapNodeClickEventArgs`) | | | Click on a node of the minimap. |

## FlowControls

Buttons for zoom in, zoom out, fit (`FitView`) and lock (`IsLocked`). Placed at the bottom left by default; the view it drives is the one that contains it, or `Flow="{Binding ElementName=Flow}"`. Colours: `Flow.Controls*`. The padlock reflects `FlowView.IsLocked` (including when it is changed elsewhere).

| Property | Type | Default | Description |
|---|---|---|---|
| `ShowZoom` / `ShowFitView` / `ShowInteractive` | `bool` | `true` | Shows the zoom, fit and lock buttons. |
| `Orientation` | `Orientation` | `Vertical` | Layout of the buttons. |
| `ExtraContent` | `object?` | `null` | Additional buttons (`ControlButton`), before the lock. |
| `AnimationDuration` | `TimeSpan` | 0 | Transition of zooms and fit. |
| `FitViewOptions` | `FitViewOptions?` | `null` | Options of the fit button (otherwise those of the view). |
| events | | | `ZoomedIn`, `ZoomedOut`, `FitViewClicked`, `InteractiveChanged` (`bool`: true = unlocked). |

## FlowPanel

`ContentControl` that anchors its content to a position of the overlay. Equivalent of `<Panel position="…">`.

| Property | Values |
|---|---|
| `PanelPosition` | `TopLeft` (default), `TopCenter`, `TopRight`, `CenterLeft`, `CenterRight`, `BottomLeft`, `BottomCenter`, `BottomRight` |

Default margin: 15.

```xml
<flow:FlowPanel PanelPosition="TopRight"><Button Content="Save" /></flow:FlowPanel>
```

## NodeResizer

Resize handles of a node (4 corners, 4 sides), visible when the node is selected. Place it in the root `Grid` of the template, **after** the card and **before** the handles; the node's content must stretch.

| Property | Type | Default | Description |
|---|---|---|---|
| `NodeMinWidth` / `NodeMinHeight` | `double` | 60 / 40 | Minimum size. |
| `NodeMaxWidth` / `NodeMaxHeight` | `double` | ∞ | Maximum size. |
| `KeepAspectRatio` | `bool` | `false` | Keeps the width/height ratio while resizing. |
| `ShowResizer` | `bool` | `true` | False: handles hidden (`isVisible`). |
| `AutoScale` | `bool` | `false` | Handles of constant size on screen whatever the zoom (`autoScale`). |
| `Variant` | `ResizerVariant` | `Handle` | `Handle` (squares at the corners) or `Line` (lines on the sides). |
| `ShouldResize` | `Func<ResizeDirection, Rect, bool>?` | `null` | Refuses a proposed size. |
| events `ResizeStarted`, `Resizing`, `ResizeEnded` | `NodeResizeEventArgs` | | Handle, starting and current rectangle. |
| `Accent` | `Brush` | blue #3367D9 | Colour of the corner handles. |

`Thumb? GetThumb(ResizeDirection)` gives access to each handle (interface tests). Each gesture emits a `NodeResizeChange`; the history records the state at the start of the gesture.

## NodeToolbar

`ContentControl` displayed next to the node when it is selected. Equivalent of `<NodeToolbar>`.

| Property | Type | Default | Description |
|---|---|---|---|
| `ToolbarPosition` | `Position` | `Top` | Side of the node. |
| `Offset` | `double` | 10 | Distance from the node. |
| `Align` | `ToolbarAlign` | `Center` | `Start`, `Center`, `End` along the side. |
| `IsToolbarVisible` | `bool?` | `null` | Forces visibility; `null` = visible when the node is selected. |

Style: background `Flow.ControlsBackground`, rounded corners, shadow.

## EdgeToolbar

Toolbar of an edge (`<EdgeToolbar>`): visible when the edge is selected. Place it in a label template (`EdgeLabelTemplate`, `wpflow-edge-label-{type}`) or an edge template; the `DataContext` is the `Edge` (or an `EdgeRenderContext`). Properties: `ToolbarPosition` (`Top`), `Offset` (10), `IsToolbarVisible` (`bool?`).

## BackgroundLayer

A background pattern: `new BackgroundLayer(Variant, Gap, Color, Size, LineWidth, Offset)`. To overlay with `FlowView.AdditionalBackgrounds` (several `<Background id="…">`).


## NodeHost

Internal container of a node (public for automation and tests). Useful properties: `Node`, `IsSelected` (usable in a `Trigger` of the style), `ShowOutline`, `OutlineCornerRadius` (0 by default: outline with square corners; a positive value rounds the outline).

## BackgroundVariant, ColorMode, PanelPosition

```csharp
enum BackgroundVariant { None, Dots, Lines, Cross }
enum ColorMode { Light, Dark }
enum PanelPosition { TopLeft, TopCenter, TopRight, CenterLeft, CenterRight, BottomLeft, BottomCenter, BottomRight }
```

## SelectionToolbar

Toolbar of a multiple selection: appears above the bounding rectangle of the selected nodes when at least `MinSelected` (2) of them are selected. With `NodeIds` (list of identifiers), it follows these nodes and is displayed as soon as one of them is selected; `IsToolbarVisible` forces visibility. Place it in the overlay of `FlowView` (or `Flow="{Binding ElementName=Flow}"`); properties `MinSelected`, `Offset` (10).

## NodeRotator

Rotation handle (« Rotatable Node » example): a round grip above the node, visible when it is selected; dragging it rotates the node around its centre by writing `Node.Rotation` (degrees), with history. Shift rounds to `SnapAngle` (15°). Place it in the root `Grid` of the template. The handles and edges stay attached to the unrotated positions, as in React Flow.

## NodeResizeControl

Free resize handle (`<NodeResizeControl>`): a `Thumb` that you style (`Template`, `Content`) and place wherever you want in the template, with the `Direction` it drives (`TopLeft`…`Bottom`). Same settings as `NodeResizer`: `NodeMinWidth`/`Height`, `NodeMaxWidth`/`Height`, `KeepAspectRatio`, `ShouldResize`, events `ResizeStarted`, `Resizing`, `ResizeEnded`.
