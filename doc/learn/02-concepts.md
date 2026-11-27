# 2. Terms and concepts

*React Flow equivalent: Terms and Concepts, Built-In Components.*

## Vocabulary

| Term | Meaning |
|---|---|
| **Flow** | The graph displayed by a `FlowView`. |
| **Node** | A draggable element of the graph (`Node`). Its content can be any WPF control. |
| **Edge** | A link between two nodes (`Edge`). |
| **Handle** | A connection point on the edge of a node (`Handle`). It is of type `Source` (start) or `Target` (end). |
| **Connection** | A link being created or completed: `Source`, `Target`, `SourceHandle`, `TargetHandle`. |
| **Viewport** | The displayed transformation: `Viewport(X, Y, Zoom)`. |
| **Flow coordinates** | The coordinate space of the nodes, independent of the zoom and of the view's position. |
| **Screen coordinates** | The pixels of the control. `screen = flow × Zoom + (X, Y)`. |
| **Change** | An atomic modification requested by the interface (`NodeChange`, `EdgeChange`). |

## The viewport

```csharp
Flow.Viewport = new Viewport(x: 100, y: 50, zoom: 1.5);

XYPosition flowPoint = Flow.ScreenToFlow(Mouse.GetPosition(Flow));   // screen -> flow
```

The zoom is bounded by `MinZoom` (0.5) and `MaxZoom` (2). `FitView()` frames all measured nodes; `FitViewOnInit="True"` does it once all nodes have been measured.

## The three layers

Inside `FlowView`, from bottom to top:

1. the **background** (dots, lines or crosses), which follows the viewport;
2. the **transformed surface**: the edges, then the nodes, then the connection line in progress;
3. the **overlay**: the `Content` of the `FlowView` (minimap, controls, panels, texts…), which does not move with the viewport.

## Built-in components

| React Flow | Wpflow | Role |
|---|---|---|
| `<Background>` | `BackgroundVariant`, `BackgroundGap`, `BackgroundColor` (properties of `FlowView`) | Background |
| `<Controls>` | `FlowControls` | Zoom +/−, fit, lock |
| `<MiniMap>` | `MiniMap` | Overview, clickable |
| `<Panel>` | `FlowPanel` | Anchor content to a corner |
| `<NodeResizer>` | `NodeResizer` | Resize handles |
| `<NodeToolbar>` | `NodeToolbar` | Floating toolbar |
| `<Handle>` | `Handle` | Connection point |

## Measuring nodes

React Flow measures nodes in the DOM; Wpflow measures them in the WPF visual tree. After layout, each `NodeHost` reports its size and the positions of its handles. Consequences:

- `Node.Width` / `Node.Height` are **measured** (do not set them to size a node);
- a fixed size is given with `FixedWidth` / `FixedHeight`;
- an edge is only drawn once both of its ends have been measured.

## Immutability and changes

Nodes and edges are immutable `record`s. The interface never modifies them: it emits *changes* and applies them to a **new** list. See [Building a flow](03-building-a-flow.md).
