# Changes

A *change* is an atomic modification requested by the interface. Changes are `record`s, grouped into two abstract families: `NodeChange` and `EdgeChange`.

## NodeChange

| Type | Fields | Effect of `Changes.Apply` |
|---|---|---|
| `NodePositionChange` | `Id`, `Position` | Replaces the position (relative to the parent). |
| `NodeDimensionsChange` | `Id`, `Width`, `Height` | Replaces the **measured** size. |
| `NodeResizeChange` | `Id`, `Width`, `Height`, `Position?` | Sets `FixedWidth/Height` **and** `Width/Height`; changes the position if one is provided. |
| `NodeSelectChange` | `Id`, `Selected` | Changes the selection. |
| `NodeInteractionChange` | `Id`, `Dragging?`, `Resizing?` | A move or resize starts or ends; `null` keeps the value. Emitted by the view. |
| `NodeRemoveChange` | `Id` | Removes the node. |
| `NodeAddChange` | `Item` | Adds the node at the end of the list. |
| `NodeReplaceChange` | `Id`, `Item` | Replaces the node. |

## EdgeChange

| Type | Fields | Effect |
|---|---|---|
| `EdgeSelectChange` | `Id`, `Selected` | Changes the selection. |
| `EdgeRemoveChange` | `Id` | Removes the edge. |
| `EdgeAddChange` | `Item` | Adds the edge. |
| `EdgeReplaceChange` | `Id`, `Item` | Replaces the edge (reconnection). |

## Changes.Apply

```csharp
public static IReadOnlyList<Node> Apply(IEnumerable<NodeChange> changes, IReadOnlyList<Node> nodes);
public static IReadOnlyList<Edge> Apply(IEnumerable<EdgeChange> changes, IReadOnlyList<Edge> edges);
```

- Returns a **new** list; the input is never modified.
- Changes are applied in order.
- An unknown `Id` is ignored.
- Nodes that are not concerned keep the same reference (which lets `FlowView` update only what is necessary).

```csharp
var nodes = Changes.Apply([ new NodePositionChange("a", new XYPosition(10, 20)),
                            new NodeSelectChange("a", true) ], Flow.Nodes);
```

## When the interface emits them

See the table in [Building a flow](../learn/03-building-a-flow.md#available-changes).
