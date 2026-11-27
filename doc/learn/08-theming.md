# 8. Theming and styles

*React Flow equivalent: Theming, Dark Mode.*

## Light / dark mode

```xml
<flow:FlowView ColorMode="Dark" />
```

```csharp
Flow.ColorMode = ColorMode.Dark;   // switches on the fly: background, dots, edges, default nodes, minimap, controls
```

`ColorMode` merges `Themes/Light.xaml` or `Themes/Dark.xaml` into `FlowView.Resources`.

## Customizing colours

All colours are `Flow.*` resources ([full list](../reference/theme-keys.md)). Redefine them in `FlowView.Resources`: they replace the theme's values for this flow.

```xml
<flow:FlowView>
    <flow:FlowView.Resources>
        <SolidColorBrush x:Key="Flow.Edge" Color="#1B86D6" />
        <SolidColorBrush x:Key="Flow.NodeBackground" Color="#FFF7FA" />
    </flow:FlowView.Resources>
</flow:FlowView>
```

> The theme is merged (`MergedDictionaries`) below your own entries: your `Flow.*` keys keep priority, even after a change of `ColorMode`. For different colours in light and dark mode, change them yourself when you switch mode.

## In your templates

Use `DynamicResource` to follow the theme:

```xml
<Border Background="{DynamicResource Flow.NodeBackground}"
        BorderBrush="{DynamicResource Flow.NodeBorder}">
    <TextBlock Foreground="{DynamicResource Flow.NodeText}" … />
</Border>
```

`StaticResource` would freeze the colour at load time.

## Background

| Property | Values |
|---|---|
| `BackgroundVariant` | `Dots` (default), `Lines`, `Cross`, `None` |
| `BackgroundGap` | Spacing in flow units (20 by default); the pattern follows the zoom |
| `BackgroundColor` | Colour of the pattern; `null` = `Flow.Dots` |
| `Background` (WPF property) | Background colour; `null` = `Flow.Background` |

## Edges, handles, controls

- Edges: `EdgeStroke`, `EdgeStrokeThickness`, or the `Flow.Edge` / `Flow.EdgeSelected` keys.
- Handles: standard WPF style for the `flow:Handle` type (`Background`, `BorderBrush`, `BorderThickness`). Define a `Style` targeting `flow:Handle` in your resources.
- Minimap and controls: `Flow.MiniMap*` and `Flow.Controls*` keys.

## Drawing with the theme in code

If you write a control that draws itself (like `MiniMap`), read the colours with `flow.ThemeBrush("Flow.…")` — which falls back to the light theme if the key is missing — and redraw yourself on the `StateChanged` event.

Next: [Interactions and connections](09-interactions.md).
