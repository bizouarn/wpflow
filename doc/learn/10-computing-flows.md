# 10. Computing with a flow

*React Flow equivalent: Computing Flows.*

A flow can be used to **compute**: some nodes produce values, others consume them along the edges. The Wpflow demo (`dotnet run --project demo/wpf`) shows it:

- three settings nodes — **colour** (a real colour picker), **shape** (cubes or pyramids), **zoom** (a slider);
- a **render** node that displays a ring of animated 3D solids;
- three edges, one per setting, to three distinct handles of the render node.

The key point: **the render node only uses a value if the corresponding edge exists.** Delete the zoom edge (select it, Delete): the zoom falls back to its default value; reconnect it: the slider takes over again. (In the code, this node has the type `output`.)

## The principle

1. **A shared state**: the input nodes write into one same object (`Node.Data`) that implements `INotifyPropertyChanged`.
2. **A ViewModel that knows what is connected**: it exposes *effective* values.
3. **A synchronisation with the edges**: each time `Edges` changes, it recalculates which inputs are connected.

### 1. The state

```csharp
public sealed class DemoState : INotifyPropertyChanged
{
    public Color Color { get; private set; }     // chosen with the colour picker
    public bool IsCube { get; set; }
    public double Zoom { get; set; }             // chosen with the slider

    // Connected? (updated from the edges)
    public bool UseColor { get; set; } = true;
    public bool UseShape { get; set; } = true;
    public bool UseZoom  { get; set; } = true;

    // Effective values: the chosen value if the input is connected, otherwise the default value.
    public Color  EffectiveColor  => UseColor ? Color : DefaultColor;
    public bool   EffectiveIsCube => !UseShape || IsCube;
    public double EffectiveZoom   => UseZoom ? Zoom : DefaultZoom;
}
```

The same object is put in `Data` of **all** the nodes; each template binds its controls to the property that concerns it.

### 2. The input nodes

Each one is a `DataTemplate` (`wpflow-node-color`, `-shape`, `-zoom`) with its WPF controls — a colour `Popup`, `RadioButton`s, a `Slider` — and a source handle on the right. Example of the slider:

```xml
<DataTemplate x:Key="wpflow-node-zoom">
    <Grid>
        <Border Style="{StaticResource Card}" Width="150">
            <StackPanel>
                <TextBlock Text="zoom level" />
                <Slider Minimum="0" Maximum="1" Value="{Binding Data.Zoom, Mode=TwoWay}" />
            </StackPanel>
        </Border>
        <flow:Handle Type="Source" Position="Right" />
    </Grid>
</DataTemplate>
```

### 3. The output node and its three inputs

The output exposes three distinct **target** handles, identified by `HandleId`, and draws the scene:

```xml
<DataTemplate x:Key="wpflow-node-output">
    <Grid>
        <Border Style="{StaticResource Card}">
            <demo:CubeField State="{Binding Data}" />
        </Border>
        <flow:NodeResizer NodeMinWidth="180" NodeMinHeight="140" />
        <flow:Handle Type="Target" Position="Left" HandleId="color" Offset="48" />
        <flow:Handle Type="Target" Position="Left" HandleId="shape" Offset="62" />
        <flow:Handle Type="Target" Position="Left" HandleId="zoom"  Offset="76" />
    </Grid>
</DataTemplate>
```

### 4. Connecting: following the edges

```csharp
Flow.Edges = [ new Edge("e-color", "color", "output", TargetHandle: "color", Animated: true),
               new Edge("e-shape", "shape", "output", TargetHandle: "shape", Animated: true),
               new Edge("e-zoom",  "zoom",  "output", TargetHandle: "zoom",  Animated: true) ];

void SyncConnections()
{
    bool Linked(string handle) => Flow.Edges.Any(e => e.Target == "output" && e.TargetHandle == handle);
    state.UseColor = Linked("color");
    state.UseShape = Linked("shape");
    state.UseZoom  = Linked("zoom");
}

DependencyPropertyDescriptor.FromProperty(FlowView.EdgesProperty, typeof(FlowView))
    .AddValueChanged(Flow, (_, _) => SyncConnections());
SyncConnections();
```

As `Edges` changes on every connection, reconnection, deletion or undo (Ctrl+Z), the scene follows without any other code. In controlled mode, call `SyncConnections()` after applying your changes.

## The animated scene

`CubeField` is a `FrameworkElement` that draws in 3D in `OnRender`:

- **Animation**: on every frame (`CompositionTarget.Rendering`) the cloud rotates on itself; each solid turns according to its own phase and floats slightly.
- **Rendering**: orthographic projection, faces sorted from back to front, faces turned away from the camera removed, shading according to a fixed light.
- **Zoom**: the scale of the « camera » follows `EffectiveZoom` with some inertia (the displayed zoom catches up with the target exponentially, independently of the frame rate).
- **Colour and shape**: `EffectiveColor` and `EffectiveIsCube`.

The full code is in `demo/wpf/CubeField.cs`, `demo/wpf/DemoState.cs` and `demo/wpf/MainWindow.xaml`.

Next: [Performance and accessibility](11-performance-accessibility.md).
