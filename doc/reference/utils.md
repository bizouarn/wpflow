# Utilities

Static functions of `Wpflow.Core`. All are pure (no state, no UI) and covered by tests.

## EdgePaths

Edge paths, ported from `getBezierPath`, `getSmoothStepPath`, `getStraightPath`. The `source` and `target` parameters are the attachment points (see `GraphUtils.GetHandlePosition`).

```csharp
EdgePath Bezier(XYPosition source, Position sourcePosition, XYPosition target, Position targetPosition,
                double curvature = 0.25);
EdgePath Straight(XYPosition source, XYPosition target);
EdgePath Step(XYPosition source, Position sourcePosition, XYPosition target, Position targetPosition,
              double offset = 20, double stepPosition = 0.5);
EdgePath SmoothStep(XYPosition source, Position sourcePosition, XYPosition target, Position targetPosition,
                    double borderRadius = 5, double offset = 20, double stepPosition = 0.5,
                    double? centerX = null, double? centerY = null);

(double X, double Y, double OffsetX, double OffsetY) EdgeCenter(XYPosition source, XYPosition target);
(List<XYPosition> Points, double CenterX, double CenterY, double OffsetX, double OffsetY) StepPoints(…);
```

| Parameter | Role |
|---|---|
| `curvature` | Curvature of the Bézier when the target is « behind » the source. |
| `offset` | Distance (px) travelled in a straight line leaving the handle before the first bend. |
| `stepPosition` | Where the bend is placed, from 0 (source side) to 1 (target side). |
| `borderRadius` | Radius of the corners (`Step` = `SmoothStep` with 0). |
| `centerX`/`centerY` | Forces the position of the bend. |

`EdgePath.Path` is an SVG string (`M…C…`, `M…L…Q…`); `LabelX`/`LabelY` give the centre for a label.

Aligned points are removed from the « step » path, as in the recent version of xyflow.

## ViewportMath

```csharp
const double DefaultMinZoom = 0.5, DefaultMaxZoom = 2;

XYPosition ScreenToFlow(Viewport v, XYPosition screen, bool snapToGrid = false, double gridX = 15, double gridY = 15);
XYPosition FlowToScreen(Viewport v, XYPosition flow);
XYPosition SnapPosition(XYPosition p, double gridX, double gridY);
double     Clamp(double value, double min, double max);

Viewport PanBy(Viewport v, double dx, double dy);
Viewport ZoomAt(Viewport v, XYPosition screenPoint, double newZoom, double minZoom = 0.5, double maxZoom = 2);
double   WheelZoomFactor(double deltaY, bool ctrlKey = false);      // 2^(-deltaY × 0.002), ×10 if Ctrl

Viewport GetViewportForBounds(Rect bounds, double width, double height,
                              double minZoom = 0.5, double maxZoom = 2, double padding = 0.1);   // fitView
Rect     GetVisibleRect(Viewport v, double width, double height);                                // visible area in flow coordinates
Viewport ClampToExtent(Viewport v, double width, double height, Rect extent);                    // translateExtent
```

`ZoomAt` keeps the given screen point fixed: the point under the cursor does not move during the zoom.

## GraphUtils

```csharp
Rect? GetNodesBounds(IEnumerable<Node> nodes);      // measured nodes, in absolute coordinates; null if there are none
XYPosition GetHandlePosition(Node node, Position position, HandleInfo? handle = null);   // attachment point of an edge
XYPosition GetHandleCenter(Node node, HandleInfo handle);                                // centre of the handle (connection distance)
Position GetHandleSide(Node node, Position position);                                    // side a handle faces once the node is rotated
T? GetClosestHandle<T>(XYPosition point, double radius,
                       IEnumerable<(T Handle, XYPosition Position)> candidates) where T : class;
XYPosition ClampToExtent(XYPosition position, double width, double height, Rect extent); // nodeExtent
bool ConnectionExists(Connection c, IEnumerable<Edge> edges);   // null and "" designate the same handle
bool IsEdgeVisible(Rect sourceNode, Rect targetNode, Rect visible);
```

`GetHandlePosition` returns the **outer edge** of the handle on the indicated side (like `getHandlePosition` in xyflow): this is where an edge ends, which leaves an arrow visible.

On a rotated node (`Node.Rotation`), `GetHandlePosition` and `GetHandleCenter` return the point as it is displayed, the node rotating around its centre; the `HandleInfo`s remain expressed in the unrotated node. `GetHandleSide` gives the direction the edges then take, to the nearest quarter turn.

## NodeTree

```csharp
Dictionary<string, XYPosition> AbsolutePositions(IEnumerable<Node> nodes);
Dictionary<string, int> Depths(IEnumerable<Node> nodes);
HashSet<string> Descendants(string id, IEnumerable<Node> nodes);
HashSet<string> DescendantsOf(IEnumerable<string> ids, IEnumerable<Node> nodes);   // several roots, a single traversal
XYPosition ClampToParent(Node child, XYPosition relativePosition, Node? parent);   // extent: 'parent'
```

## Connections

```csharp
bool AreTypesCompatible(HandleType from, HandleType to, ConnectionMode mode);   // Strict: different types; Loose: always
Connection Create(string fromNode, string? fromHandle, HandleType fromType, string toNode, string? toHandle);
bool IsValid(Connection connection, Func<Connection, bool>? isValidConnection);
```

`Create` orients the connection according to the type of the **starting** handle.

## NodeResize

```csharp
Rect Resize(ResizeDirection direction, Rect start, double dx, double dy,
            double minWidth = 10, double minHeight = 10,
            double maxWidth = double.PositiveInfinity, double maxHeight = double.PositiveInfinity,
            bool keepAspectRatio = false);
```

`dx`/`dy` are the movement of the handle from `start`. The opposite edge stays fixed, even when a minimum or a maximum is reached.

## GraphLayout

```csharp
Dictionary<string, XYPosition> LayeredLeftToRight(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges,
    double horizontalGap = 80, double verticalGap = 30, double defaultWidth = 150, double defaultHeight = 40);
```

See [Automatic layout](../learn/07-layouting.md).

## GraphSerializer

```csharp
string ToJson(IEnumerable<Node> nodes, IEnumerable<Edge> edges, Viewport viewport, bool indented = true);
FlowGraph FromJson(string json);       // JsonException if an id or an end is missing
```

See [Saving and JSON](../learn/12-serialization.md).

## Changes

See [Changes](changes.md).

## GraphQueries

Queries on the graph, equivalents of `getIncomers`, `getOutgoers`, `getConnectedEdges` and `addEdge`.

| Method | Description |
|---|---|
| `GetIncomers(node, nodes, edges)` | Nodes that have an edge to `node`. |
| `GetOutgoers(node, nodes, edges)` | Nodes to which `node` has an edge. |
| `GetConnectedEdges(nodes, edges)` | Edges touching one of the `nodes`. |
| `AddEdge(edge, edges)` | Adds the edge unless an identical connection already exists; returns the list (unchanged in case of a duplicate). |
| `ConnectionId(source, sourceHandle, target, targetHandle)` | `xy-edge__…` identifier built as in React Flow. |
| `GetNode(id, nodes)` / `GetEdge(id, edges)` | Search by identifier. |
| `IsNodeIntersecting(node, area, partially = true)` | The (measured) node touches the area, or lies entirely within it with `partially = false`. |
| `GetIntersectingNodes(area or node, nodes, partially = true)` | Nodes that touch an area or another node; hidden nodes are ignored. |
| `GetNodeConnections(nodeId, edges, type?, handleId?)` | Connections of a node (`NodeConnection`: `Connection` and `EdgeId`), filterable by direction and by handle. |
| `ReconnectEdge(oldEdge, connection, edges, shouldReplaceId = true)` | Changes the ends of an edge; list unchanged if the connection already exists. |

## GraphClipboard

Copy / cut / paste of nodes and edges, without UI.

```csharp
ClipboardData? Copy(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges);   // selection + descendants + internal edges; null if nothing
ClipboardData Paste(ClipboardData data, IEnumerable<Node> existingNodes, IEnumerable<Edge> existingEdges, XYPosition offset);
```

`Paste` creates new ids (`a-copy`, `a-copy2`…), shifts the roots by `offset`, remaps parents and edge ends, and selects the pasted nodes. A node whose parent was not copied becomes a root at its absolute position.

## Other additions to the core

| Item | Description |
|---|---|
| `EdgePaths.SimpleBezier(...)` | `simplebezier` path (`getSimpleBezierPath`). |
| `EdgeGeometry.Edge` (`Edge?`) | The edge being drawn, passed to path providers (`null` without an edge). The `default`, `step` and `smoothstep` types respect its `PathOptions` (`Curvature`, `BorderRadius`, `Offset`, `StepPosition`). |
| `GraphLayout.Layered(nodes, edges, LayoutDirection, rankGap, nodeGap, …)` | Layered layout in the four directions (`LeftToRight`, `RightToLeft`, `TopToBottom`, `BottomToTop`). |
| `ViewportMath.GetViewportForCenter(x, y, zoom, w, h)` | Viewport centred on a point (`setCenter`). |
| `ViewportMath.Interpolate(from, to, t)` / `EaseInOut(t)` | Viewport transition (geometric zoom, linear translation). |
| `Rect.Contains(Rect)` | Inclusion of a rectangle. |

## Changesets

`Changesets.Diff(before, after)` (nodes or edges) returns the list of changes (`Add`, `Remove`, `Replace`) that transforms `before` into `after` (`getNodesChangeset`, `getEdgesChangeset`): `Changes.Apply(Changesets.Diff(a, b), a)` gives back `b`.

`GraphLayout.LayeredLeftToRight` and `Layered` reduce edge crossings: within each layer, the nodes are sorted by the barycentre of their neighbours (forward and backward sweeps, stable sort: without neighbours, the input order is kept).
