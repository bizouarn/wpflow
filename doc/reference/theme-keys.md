# Clés de thème

Les couleurs de Wpflow sont des ressources `Brush` nommées `Flow.*`, définies dans `Themes/Light.xaml` et `Themes/Dark.xaml` et fusionnées dans `FlowView.Resources` selon `ColorMode`. Vos propres entrées dans `FlowView.Resources` (ou dans les ressources d'un parent) restent prioritaires.

| Clé | Utilisation | Clair | Sombre |
|---|---|---|---|
| `Flow.Background` | Fond de la surface | `#FFFFFF` | `#141414` |
| `Flow.Dots` | Points / lignes / croix du fond | `#91919A` | `#4A4A52` |
| `Flow.Edge` | Edges | `#B1B1B7` | `#6B6B75` |
| `Flow.EdgeSelected` | Edge sélectionné | `#555555` | `#E5E5E5` |
| `Flow.NodeBackground` | Fond des nodes par défaut (et labels d'edges) | `#FFFFFF` | `#1E1E22` |
| `Flow.NodeBorder` | Bordure des nodes par défaut | `#1A192B` | `#D9D9DD` |
| `Flow.NodeBorderSelected` | Bordure d'un node par défaut sélectionné | `#555555` | `#FFFFFF` |
| `Flow.NodeText` | Texte des nodes par défaut | `#222222` | `#F8F8F8` |
| `Flow.GroupBackground` | Fond des groupes | `#14F0F0F0` | `#14FFFFFF` |
| `Flow.Outline` | Contour de sélection des nodes à template | `#555555` | `#E5E5E5` |
| `Flow.MiniMapBackground` | Fond de la minimap | `#FFFFFF` | `#1E1E22` |
| `Flow.MiniMapNode` | Nodes dans la minimap | `#E2E2E2` | `#3A3A42` |
| `Flow.MiniMapNodeSelected` | Node sélectionné dans la minimap | `#B1B1B7` | `#7A7A85` |
| `Flow.MiniMapMask` | Voile hors de la zone visible | `#99F0F0F0` | `#99141414` |
| `Flow.ControlsBackground` | Fond des contrôles et de `NodeToolbar` | `#FFFFFF` | `#1E1E22` |
| `Flow.ControlsForeground` | Icônes des contrôles | `#222222` | `#F2F2F2` |
| `Flow.ControlsBorder` | Séparateurs des contrôles | `#EEEEEE` | `#34343A` |
| `Flow.ControlsHover` | Survol d'un bouton de contrôle | `#F4F4F4` | `#2C2C32` |

## Utiliser les clés

Dans un template (suit le thème et ses changements) :

```xml
<Border Background="{DynamicResource Flow.NodeBackground}" />
```

En code (repli sur le thème clair si la clé est absente) :

```csharp
Brush brush = Flow.ThemeBrush("Flow.Edge");
```

## Surcharger

```xml
<flow:FlowView>
    <flow:FlowView.Resources>
        <SolidColorBrush x:Key="Flow.Edge" Color="#1B86D6" />
    </flow:FlowView.Resources>
</flow:FlowView>
```

Aucune autre clé n'est requise : celles que vous ne redéfinissez pas viennent du thème.

## Clés de remplacement de contenu

En plus des couleurs `Flow.*`, ces clés remplacent des **éléments** (voir [Personnaliser](../learn/14-customization.md)) :

| Clé | Ressource |
|---|---|
| `wpflow-node-{type}` | `DataTemplate` du node |
| `wpflow-edge-{type}` | `IEdgePathProvider` (tracé) |
| `wpflow-edge-template-{type}` | `DataTemplate` du rendu complet de l'edge |
| `wpflow-edge-label-{type}` | `DataTemplate` du label |
| `wpflow-marker-{clé}` | `Geometry` du marqueur |
