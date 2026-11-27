# Theme keys

Wpflow's colours are named `Brush` resources `Flow.*`, defined in `Themes/Light.xaml` and `Themes/Dark.xaml` and merged into `FlowView.Resources` according to `ColorMode`. Your own entries in `FlowView.Resources` (or in a parent's resources) keep priority.

| Key | Used for | Light | Dark |
|---|---|---|---|
| `Flow.Background` | Background of the surface | `#FFFFFF` | `#141414` |
| `Flow.Dots` | Dots / lines / crosses of the background | `#91919A` | `#4A4A52` |
| `Flow.Edge` | Edges | `#B1B1B7` | `#6B6B75` |
| `Flow.EdgeSelected` | Selected edge | `#555555` | `#E5E5E5` |
| `Flow.NodeBackground` | Background of default nodes (and edge labels) | `#FFFFFF` | `#1E1E22` |
| `Flow.NodeBorder` | Border of default nodes | `#1A192B` | `#D9D9DD` |
| `Flow.NodeBorderSelected` | Border of a selected default node | `#555555` | `#FFFFFF` |
| `Flow.NodeText` | Text of default nodes | `#222222` | `#F8F8F8` |
| `Flow.GroupBackground` | Background of groups | `#14F0F0F0` | `#14FFFFFF` |
| `Flow.Outline` | Selection outline of nodes with a template | `#555555` | `#E5E5E5` |
| `Flow.MiniMapBackground` | Background of the minimap | `#FFFFFF` | `#1E1E22` |
| `Flow.MiniMapNode` | Nodes in the minimap | `#E2E2E2` | `#3A3A42` |
| `Flow.MiniMapNodeSelected` | Selected node in the minimap | `#B1B1B7` | `#7A7A85` |
| `Flow.MiniMapMask` | Veil outside the visible area | `#99F0F0F0` | `#99141414` |
| `Flow.ControlsBackground` | Background of the controls and of `NodeToolbar` | `#FFFFFF` | `#1E1E22` |
| `Flow.ControlsForeground` | Icons of the controls | `#222222` | `#F2F2F2` |
| `Flow.ControlsBorder` | Separators of the controls | `#EEEEEE` | `#34343A` |
| `Flow.ControlsHover` | Hover state of a control button | `#F4F4F4` | `#2C2C32` |

## Using the keys

In a template (follows the theme and its changes):

```xml
<Border Background="{DynamicResource Flow.NodeBackground}" />
```

In code (falls back to the light theme if the key is missing):

```csharp
Brush brush = Flow.ThemeBrush("Flow.Edge");
```

## Overriding

```xml
<flow:FlowView>
    <flow:FlowView.Resources>
        <SolidColorBrush x:Key="Flow.Edge" Color="#1B86D6" />
    </flow:FlowView.Resources>
</flow:FlowView>
```

No other key is required: the ones you do not redefine come from the theme.

## Content replacement keys

In addition to the `Flow.*` colours, these keys replace **elements** (see [Customization](../learn/14-customization.md)):

| Key | Resource |
|---|---|
| `wpflow-node-{type}` | `DataTemplate` of the node |
| `wpflow-edge-{type}` | `IEdgePathProvider` (path) |
| `wpflow-edge-template-{type}` | `DataTemplate` for the full rendering of the edge |
| `wpflow-edge-label-{type}` | `DataTemplate` of the label |
| `wpflow-marker-{key}` | `Geometry` of the marker |
