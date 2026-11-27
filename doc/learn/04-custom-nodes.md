# 4. Custom nodes

*React Flow equivalent: Custom Nodes, Handles, Node Resizer, Node Toolbar.*

## One node = one DataTemplate

A node of type `"card"` is rendered with the `DataTemplate` keyed `wpflow-node-card`. Declare it in any resource dictionary (window, `App.xaml`, `FlowView.Resources`). The `DataContext` is the `Node`; put your ViewModel in `Node.Data`.

```xml
<Window.Resources>
    <DataTemplate x:Key="wpflow-node-card">
        <Grid>
            <Border Background="{DynamicResource Flow.NodeBackground}"
                    BorderBrush="{DynamicResource Flow.NodeBorder}" BorderThickness="1"
                    CornerRadius="0" Padding="12">
                <StackPanel>
                    <TextBlock Text="{Binding Data.Title}" FontWeight="SemiBold"
                               Foreground="{DynamicResource Flow.NodeText}" />
                    <Slider Minimum="0" Maximum="100" Value="{Binding Data.Value, Mode=TwoWay}" />
                </StackPanel>
            </Border>

            <flow:Handle Type="Target" Position="Left" />
            <flow:Handle Type="Source" Position="Right" />
        </Grid>
    </DataTemplate>
</Window.Resources>
```

```csharp
public sealed class CardVm : INotifyPropertyChanged { /* Title, Value… */ }

Flow.Nodes = [ new Node("a", new XYPosition(0, 0), "card", new CardVm { Title = "Volume" }) ];
```

WPF controls (Slider, TextBox, ComboBox, buttons…) work normally inside a node: they mark their mouse events as handled, so interacting with them does not move the node. Clicking or dragging an « empty » area of the node moves it.

> **Updating data.** Make `Node.Data` an object that implements `INotifyPropertyChanged`: the `Data.Value` binding updates without recreating the node. The `DataContext` (the `Node`) is replaced when the node changes, and the bindings are re-evaluated.

## Handles

```xml
<flow:Handle Type="Source" Position="Right" HandleId="output" Offset="40" />
```

| Property | Role |
|---|---|
| `Type` | `Source` (start) or `Target` (end) |
| `Position` | Side of the node: `Left`, `Top`, `Right`, `Bottom` |
| `HandleId` | Identifier, to tell apart several handles on the same node (`Edge.SourceHandle` / `TargetHandle`) |
| `Offset` | Distance from the top (left/right sides) or from the left (top/bottom sides). Without a value: centred. |

**Place the handles as children of the template's root `Grid`, not inside a border with `Padding`**: a handle is positioned on the edge of its container. An edge without `SourceHandle` uses the first `Source` handle of the node.

The `default` node has a target handle at the top and a source handle at the bottom; `input` only has the source, `output` only the target.

## Resizing: `NodeResizer`

```xml
<Grid>
    <Border … />                                   <!-- the card: it must stretch, without fixed Width/Height -->
    <flow:NodeResizer NodeMinWidth="150" NodeMinHeight="70" />   <!-- after the card, before the handles -->
    <flow:Handle … />
</Grid>
```

```csharp
new Node("a", pos, "card", data, FixedWidth: 200, FixedHeight: 90)   // initial size
```

The 8 grips appear when the node is selected. Each resize emits a `NodeResizeChange` (size + position, because dragging the left or top edge moves the node).

## Toolbar: `NodeToolbar`

```xml
<flow:NodeToolbar ToolbarPosition="Top" Offset="10">
    <StackPanel Orientation="Horizontal">
        <Button Content="Duplicate" Command="{Binding Data.DuplicateCommand}" />
        <Button Content="Delete" Command="{Binding Data.DeleteCommand}" />
    </StackPanel>
</flow:NodeToolbar>
```

The toolbar is only visible when the node is selected and is placed outside the node, on the requested side.

## Selection outline

A node with a custom template automatically receives a rounded outline when it is selected (key `Flow.Outline`). For another style, bind to `Selected`:

```xml
<DataTrigger Binding="{Binding Selected}" Value="True"> … </DataTrigger>
```

## Default node

Without a template, a `default` node displays `Node.Data.ToString()` (or the `Id`) in a card 150 px wide, with its handles. To change this rendering globally, redefine the [theme keys](../reference/theme-keys.md).

Next: [Edges](05-edges.md).
