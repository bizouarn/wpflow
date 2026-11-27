# 11. Performance and accessibility

*React Flow equivalent: Performance, Accessibility.*

## Performance

### Measurements

Taken in the demo (`WPFLOW_PERF=N`), default nodes, one edge per node, on the development machine. These are orders of magnitude, not guarantees:

| | 3,000 nodes | 5,000 nodes, `OnlyRenderVisibleElements` | 20,000 nodes, `OnlyRenderVisibleElements` |
|---|---|---|---|
| First display + measurement | 3.5 to 3.8 s | 0.4 s | 0.4 s |
| Moving a node (per frame) | 65 to 95 ms | 9 to 12 ms | 24 ms |
| Panning the view (per frame) | 65 to 90 ms | 9 to 13 ms | 26 ms |
| Managed memory added | +32 MB (≈ 11 KB/node) | +6.8 MB | +8.4 MB |
| Working set added | +84 MB | +13 to 18 MB | +21 MB |

### Enabling virtualisation

```xml
<flow:FlowView OnlyRenderVisibleElements="True" />
```

Only the nodes and edges near the visible area have a visual element: those in the view plus 300 px are displayed, those up to 900 px are kept hidden (so they are not recreated on every back-and-forth pan), the others **do not exist** — neither in visual memory, nor in layout, nor in rendering. Selected nodes always stay displayed. So it is not only a speed gain: memory no longer depends on the total number of nodes (see the measurements below). Enable it from about 1,000 nodes.

**Known limitation**: a node is only measured once it has been displayed. Nodes never seen remain without dimensions, so `FitViewOnInit`, `FitView()` and the minimap only know the measured nodes. For a large virtualised graph, give an explicit size:

```csharp
new Node(id, position, "default", data, FixedWidth: 150, FixedHeight: 40)
```

### What Wpflow already does for you

- Node measurements are **batched**: N measured nodes = a single update (without this, loading was quadratic).
- An update only recalculates the nodes whose reference or absolute position has changed, and the edges connected to them.
- An edge whose path, ends and style have not changed is neither re-parsed nor redrawn; each edge is a single element that draws itself.

### Tips

- Keep node templates light: the cost per node mainly comes from the number of visual elements (a handle, an edge and a selection outline are each drawn by a single element).
- Avoid `Effect`s (drop shadows) on hundreds of nodes; one effect per card remains reasonable.
- Do not rebuild the whole list when a single node changes: `Flow.Nodes.Select(n => n.Id == x ? n with { … } : n)` keeps the reference of the others.

## Accessibility

### Keyboard

Everything can be done without a mouse: Tab / Shift+Tab to go through the nodes, arrow keys to move them, Delete, Ctrl+A, Esc, Ctrl+Z / Ctrl+Y. See [Interactions](09-interactions.md#keyboard).

### UI Automation

| Control | Peer | Exposes |
|---|---|---|
| `FlowView` | `FlowViewAutomationPeer` | A `Pane` named « Flow diagram » (or `AutomationProperties.Name`) |
| `NodeHost` | `NodeHostAutomationPeer` | A `ListItem` named after `Data` (text) or the `Id`, with the **SelectionItem** pattern (`Select`, `AddToSelection`, `RemoveFromSelection`) |

Name your flow:

```xml
<flow:FlowView AutomationProperties.Name="Validation process" … />
```

For a node with a custom template to be announced clearly, set `AutomationProperties.Name` on the root of its template.

> The peers are checked by code tests; they have not been tested with a real screen reader (Narrator, NVDA).

### Contrast and themes

The dark theme (`ColorMode="Dark"`) is provided; override the `Flow.*` keys for a high-contrast theme. Colours never carry information on their own (animated and selected edges also change style).

### Touch

Two-finger pinch zooms (`ManipulationDelta`); single-finger dragging moves nodes or the view through the mouse events promoted by WPF.

Next: [Saving and JSON](12-serialization.md).
