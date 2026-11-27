# 7. Automatic layout

*React Flow equivalent: Layouting.*

React Flow does not provide a layout engine: its examples use dagre, d3-hierarchy or ELK. Wpflow provides a simple layout, with no dependency, in `Wpflow.Core`:

```csharp
Dictionary<string, XYPosition> positions =
    GraphLayout.LayeredLeftToRight(Flow.Nodes, Flow.Edges, horizontalGap: 90, verticalGap: 30);

Flow.Nodes = Flow.Nodes.Select(n => n with { Position = positions[n.Id] }).ToList();
Flow.Dispatcher.BeginInvoke(() => Flow.FitView(), DispatcherPriority.Loaded);   // after the layout
```

## Principle

- The **layer** of a node is the length of the longest path leading to it (cycles are cut); the roots are in layer 0.
- The layers are placed from left to right; the width of a layer is that of its widest node.
- Within a layer, the nodes are stacked with `verticalGap` and centred vertically around 0.
- Nodes that have not been measured use `defaultWidth` / `defaultHeight` (150 × 40).

Call the layout **after** the nodes have been measured if you want their real sizes (non-null `Node.Width` / `Height`).

## Tips

- `GraphLayout.Layered(nodes, edges, LayoutDirection.TopToBottom)` (or `RightToLeft`, `BottomToTop`) lays out in the requested direction; then give the handles the matching positions (`Node.SourcePosition` / `TargetPosition` for the default nodes).
- `smoothstep` or `step` edges suit layered layouts better than Bézier curves.
- For complex graphs (crossing reduction, groups), plug in an external engine: it only has to produce a `Dictionary<string, XYPosition>` and apply it as above.

See the « Automatic layout » example in the [examples window](../examples/README.md#automatic-layout).

Next: [Theming and styles](08-theming.md).
