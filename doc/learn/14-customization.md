# 14. Customizing without touching the library

*React Flow equivalent: Custom Nodes, Custom Edges, Custom Markers, `nodeTypes` / `edgeTypes`.*

Everything that is displayed can be replaced with **WPF resources** (in `FlowView.Resources`, the window or the application), without subclassing or reflection. The keys all follow the same convention:

| What you replace | Resource key | Resource type |
|---|---|---|
| A node type (even the built-in ones) | `wpflow-node-{type}` | `DataTemplate` (DataContext = `Node`) |
| The **path** of an edge type | `wpflow-edge-{type}` | `IEdgePathProvider` |
| The **full rendering** of an edge type | `wpflow-edge-template-{type}` | `DataTemplate` (DataContext = `EdgeRenderContext`) |
| The label of an edge type | `wpflow-edge-label-{type}` | `DataTemplate` (DataContext = `Edge`) |
| A marker | `wpflow-marker-{key}` | `Geometry` |
| The appearance of handles | implicit style for `flow:Handle` | `Style` (size, `Template`) |

The demonstrator for this page is the **Customization** tab of the examples (`demo/wpf/CustomizationExample.xaml`, `CustomEdges.cs`).

## Redefining a built-in node

`default`, `input`, `output` and `group` are types like any other: a `wpflow-node-default` resource replaces the default node for the whole flow.

```xml
<flow:FlowView>
    <flow:FlowView.Resources>
        <DataTemplate x:Key="wpflow-node-default">
            <Grid>
                <Border Background="{DynamicResource Flow.NodeBackground}" CornerRadius="0" Padding="12">
                    <TextBlock Text="{Binding Data}" />
                </Border>
                <flow:Handle Type="Target" Position="Left" />
                <flow:Handle Type="Source" Position="Right" />
            </Grid>
        </DataTemplate>
    </flow:FlowView.Resources>
</flow:FlowView>
```

A template replaces the whole node, **handles included**: put your own there.

## Handles

A `Handle` draws itself (8 × 8, a disc). To change it, an implicit style is enough; the size is set with `Width` / `Height`, the shape with a `Template`:

```xml
<Style TargetType="flow:Handle">
    <Setter Property="Width" Value="14" />
    <Setter Property="Height" Value="14" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="flow:Handle">
                <Rectangle Fill="#1B86D6" Stroke="White" StrokeThickness="1.5" RenderTransformOrigin="0.5,0.5">
                    <Rectangle.RenderTransform><RotateTransform Angle="45" /></Rectangle.RenderTransform>
                </Rectangle>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

The click area remains that of the handle, widened by 6 px, and the position on the edge of the node follows the chosen size. Without a `Template`, the handle keeps its direct drawing (no extra visual element).

## Edge types

Two levels, depending on what needs to change.

### A different path: `IEdgePathProvider`

The provider only computes the path (an SVG string and the position of the label); the library draws the line, the arrows, the animation, the selection and the click area, exactly as for a built-in type. This is the **fast** path.

```csharp
public sealed class WaveEdge : IEdgePathProvider
{
    public EdgePath GetPath(EdgeGeometry g)      // g.Source, g.SourcePosition, g.Target, g.TargetPosition
    {
        // ... build the SVG path ...
        return new EdgePath(path, labelX, labelY, offsetX, offsetY);
    }
}
```

```xml
<demo:WaveEdge x:Key="wpflow-edge-wave" />
```

```csharp
new Edge("e1", "a", "b", Type: "wave")
```

`g.Edge` gives the edge being drawn: the path can depend on its data (`Data`, handles, selection), so a single type is enough where one per variant would otherwise be needed. It is `null` when there is no edge, for example if you call the provider yourself without specifying one.

```csharp
new DelegateEdgePathProvider(g => g.Edge?.Data is Relation { IsSelfReference: true }
    ? LoopPath(g)                                  // your loop path
    : EdgePathProviders.ForType("smoothstep").GetPath(g));
```

`DelegateEdgePathProvider(g => …)` avoids creating a class. `EdgePaths.Bezier`, `Step`, `SmoothStep`, `Straight` are building blocks you can use in your provider, and `EdgePathProviders.ForType("step")` gives the built-in providers (you can also **override** a built-in type: `wpflow-edge-straight`).

### A different rendering: `wpflow-edge-template-{type}`

To draw the edge differently than with a single line (halo, several strokes, shapes…), a `DataTemplate` receives an `EdgeRenderContext`:

| Property | Content |
|---|---|
| `Edge`, `IsSelected`, `IsAnimated` | The edge and its state |
| `Geometry`, `PathData` | The computed path (by the provider of the same type, otherwise the bézier) |
| `Start`, `End`, `StartSide`, `EndSide`, `LabelPosition` | Ends and sides of the handles |
| `Stroke`, `StrokeThickness` | Brush (already the selection brush if selected) and thickness from the theme |

```xml
<DataTemplate x:Key="wpflow-edge-template-pulse">
    <Canvas>
        <Path Data="{Binding Geometry}" Stroke="{Binding Stroke}" StrokeThickness="9" Opacity="0.22" />
        <Path Data="{Binding Geometry}" Stroke="{Binding Stroke}" StrokeThickness="{Binding StrokeThickness}" />
    </Canvas>
</DataTemplate>
```

Clicking, selecting and reconnecting work as for the other edges, on the elements drawn by your template. A template costs a real visual tree per edge: keep it for the types that need it (types without a template remain drawn directly).

### Labels per type

`wpflow-edge-label-{type}` defines the label of a single type; otherwise, the global `FlowView.EdgeLabelTemplate` applies, and otherwise the text of `Edge.Label`.

## Markers

`Edge.MarkerEnd` / `MarkerStart`: `Arrow`, `ArrowClosed`. For another marker, `Edge.MarkerEndKey` / `MarkerStartKey` designate a **geometry** held in a resource:

```xml
<Geometry x:Key="wpflow-marker-dot">M-8,0 A4,4 0 1 1 0,0 A4,4 0 1 1 -8,0 Z</Geometry>
```

```csharp
new Edge("e", "a", "b", MarkerEndKey: "dot")
```

The **tip is at (0, 0) and the shape faces +x** (like the built-in arrows, which are rotated according to the side of the handle). A shape whose figures are all closed is filled with the edge's colour; an open shape is a simple line. An unknown key falls back to `MarkerEnd` / `MarkerStart`. Keys survive JSON export (`markerEnd: { type: "dot" }`).

## Appearance settings

| `FlowView` property | Default | Role |
|---|---|---|
| `EdgeHitWidth` | 10 | Width of the clickable area around an edge |
| `SelectionOutlineGap` | 3 | Gap of the selection outline (nodes with a template) |
| `SelectionOutlineThickness` | 1.5 | Thickness of this outline |
| `ConnectionLineStyle` | grey, 1.5 | `Style` (`TargetType="Path"`) of the preview of the connection in progress |
| `EdgeStroke`, `EdgeStrokeThickness` | theme, 1 | Edge stroke |

Colours come from the `Flow.*` keys (see [Theming](08-theming.md)).

Back to the [index](../README.md).
