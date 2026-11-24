# 8. Thèmes et styles

*Équivalent React Flow : Theming, Dark Mode.*

## Mode clair / sombre

```xml
<flow:FlowView ColorMode="Dark" />
```

```csharp
Flow.ColorMode = ColorMode.Dark;   // bascule à chaud : fond, points, edges, nodes par défaut, minimap, contrôles
```

`ColorMode` fusionne `Themes/Light.xaml` ou `Themes/Dark.xaml` dans `FlowView.Resources`.

## Personnaliser les couleurs

Toutes les couleurs sont des ressources `Flow.*` ([liste complète](../reference/theme-keys.md)). Redéfinissez-en dans `FlowView.Resources` : elles remplacent celles du thème pour ce flow.

```xml
<flow:FlowView>
    <flow:FlowView.Resources>
        <SolidColorBrush x:Key="Flow.Edge" Color="#FF0071" />
        <SolidColorBrush x:Key="Flow.NodeBackground" Color="#FFF7FA" />
    </flow:FlowView.Resources>
</flow:FlowView>
```

> Le thème est fusionné (`MergedDictionaries`) sous vos propres entrées : vos clés `Flow.*` restent prioritaires, même après un changement de `ColorMode`. Pour des couleurs différentes en clair et en sombre, changez-les vous-même au moment de basculer le mode.

## Dans vos templates

Utilisez `DynamicResource` pour suivre le thème :

```xml
<Border Background="{DynamicResource Flow.NodeBackground}"
        BorderBrush="{DynamicResource Flow.NodeBorder}">
    <TextBlock Foreground="{DynamicResource Flow.NodeText}" … />
</Border>
```

`StaticResource` figerait la couleur au moment du chargement.

## Fond

| Propriété | Valeurs |
|---|---|
| `BackgroundVariant` | `Dots` (défaut), `Lines`, `Cross`, `None` |
| `BackgroundGap` | Espacement en unités flow (20 par défaut) ; le motif suit le zoom |
| `BackgroundColor` | Couleur du motif ; `null` = `Flow.Dots` |
| `Background` (propriété WPF) | Couleur de fond ; `null` = `Flow.Background` |

## Edges, handles, contrôles

- Edges : `EdgeStroke`, `EdgeStrokeThickness`, ou les clés `Flow.Edge` / `Flow.EdgeSelected`.
- Handles : style WPF standard du type `flow:Handle` (`Background`, `BorderBrush`, `BorderThickness`). Définissez un `Style` ciblant `flow:Handle` dans vos ressources.
- Minimap et contrôles : clés `Flow.MiniMap*` et `Flow.Controls*`.

## Dessiner avec le thème en code

Si vous écrivez un contrôle qui dessine lui-même (comme `MiniMap`), lisez les couleurs avec `flow.ThemeBrush("Flow.…")` — qui retombe sur le thème clair si la clé est absente — et redessinez-vous sur l'événement `StateChanged`.

Suivant : [Interactions et connexions](09-interactions.md).
