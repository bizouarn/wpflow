# 5. Edges

*React Flow equivalent: Custom Edges, Edge Labels, Markers, Reconnecting Edges.*

## Edge types

| `Edge.Type` | Path | Core path |
|---|---|---|
| `default` | Bézier curve | `EdgePaths.Bezier` |
| `straight` | Straight line | `EdgePaths.Straight` |
| `step` | Right angles | `EdgePaths.Step` |
| `smoothstep` | Rounded right angles (radius 5) | `EdgePaths.SmoothStep` |

```csharp
new Edge("e1", "a", "b", Type: "smoothstep")
```

The path takes into account the side of each handle (`Left`, `Right`, `Top`, `Bottom`): an edge that leaves the bottom of one node and arrives at the left of another is routed correctly.

## Precise handles

When a node has several handles, refer to them by their `HandleId`:

```csharp
new Edge("e", "source", "target", SourceHandle: "output-1", TargetHandle: "input-2")
```

Without an identifier, the first handle of the right type on the node is used.

## Animation, markers, label

```csharp
new Edge("e", "a", "b",
    Animated: true,                              // dashes that move along the path
    MarkerEnd: EdgeMarker.ArrowClosed,           // filled arrow on the target side
    MarkerStart: EdgeMarker.Arrow,               // open arrow on the source side
    Label: "valid")
```

The arrow always points towards the node, whatever the side of the handle.

## Free content in a label (equivalent of `EdgeLabelRenderer`)

Define a `DataTemplate` whose `DataContext` is the `Edge` and assign it to `EdgeLabelTemplate`:

```xml
<DataTemplate x:Key="PillLabel">
    <Border x:Name="pill" Background="#1B86D6" CornerRadius="9" Padding="8,2">
        <TextBlock Text="{Binding Label}" Foreground="White" FontSize="10" />
    </Border>
    <DataTemplate.Triggers>
        <DataTrigger Binding="{Binding Label}" Value="{x:Null}">
            <Setter TargetName="pill" Property="Visibility" Value="Collapsed" />
        </DataTrigger>
    </DataTemplate.Triggers>
</DataTemplate>

<flow:FlowView EdgeLabelTemplate="{StaticResource PillLabel}" … />
```

The template is applied to **all** edges (hide it with a `DataTrigger` when there is no label, as above). The content is centred on the middle of the path (`EdgePath.LabelX/LabelY`).

## Edge style

| `FlowView` property | Effect |
|---|---|
| `EdgeStroke` | Colour (otherwise `Flow.Edge` from the theme) |
| `EdgeStrokeThickness` | Thickness (1 by default) |

A selected edge uses `Flow.EdgeSelected`.

## Reconnecting an edge

Grab an edge **near one of its ends** (within 24 px) and drop it on another handle: the edge keeps its `Id`, only its ends change (`EdgeReplaceChange`). Reconnection respects `ConnectionMode` and `IsValidConnection`, and refuses to create a duplicate.

## Drawing it yourself

Paths are strings in SVG format, directly usable in WPF:

```csharp
EdgePath path = EdgePaths.SmoothStep(source, Position.Right, target, Position.Left, borderRadius: 10, offset: 30);
var geometry = Geometry.Parse(path.Path);
// path.LabelX, path.LabelY: centre of the path; path.OffsetX/OffsetY: distance from the source
```

These functions are a port of `getBezierPath` / `getSmoothStepPath` from `@xyflow/system`, checked against the library's source code. See the [reference](../reference/utils.md#edgepaths).

## Current limitations

For a custom edge type of your own (path, rendering, markers), see [Customization](14-customization.md).

Next: [Sub-flows and groups](06-sub-flows.md).
