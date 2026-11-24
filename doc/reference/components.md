# Composants

Tous dans `Wpflow.Wpf` (`xmlns:flow="clr-namespace:Wpflow.Wpf;assembly=Wpflow.Wpf"`).

## Handle

Point de connexion, à placer dans le template d'un node (enfant du `Grid` racine). Équivalent de `<Handle>`.

| Propriété | Type | Défaut | Description |
|---|---|---|---|
| `Type` | `HandleType` | `Source` | `Source` (départ) ou `Target` (arrivée). |
| `Position` | `Position` | `Top` | Côté du node : `Left`, `Top`, `Right`, `Bottom`. |
| `HandleId` | `string?` | `null` | Identifiant (`Edge.SourceHandle` / `TargetHandle`). |
| `Offset` | `double` | `NaN` | Décalage le long du côté depuis le haut (gauche/droite) ou la gauche (haut/bas) ; `NaN` = centré. |

Le handle fait 8 × 8 et déborde de 4 px sur le bord de son conteneur ; sa zone de clic est élargie de 6 px. Il se stylise comme un `Control` (`Background`, `BorderBrush`, `BorderThickness`) : ciblez `flow:Handle` dans un `Style`.

## MiniMap

Vue d'ensemble du graphe ; cliquer ou glisser recentre le viewport. À placer dans le `Content` de `FlowView`.

Valeurs par défaut : 200 × 150, en bas à droite, marge de 15. Redimensionnez avec `Width`/`Height`, repositionnez avec `HorizontalAlignment`/`VerticalAlignment`/`Margin`. Couleurs : `Flow.MiniMapBackground`, `Flow.MiniMapNode`, `Flow.MiniMapNodeSelected`, `Flow.MiniMapMask`.

## FlowControls

Boutons zoom +, zoom −, cadrage (`FitView`) et verrou (`IsLocked`). Placé en bas à gauche par défaut. Couleurs : `Flow.Controls*`. Le cadenas reflète `FlowView.IsLocked` (y compris quand il est modifié ailleurs).

## FlowPanel

`ContentControl` qui ancre son contenu à une position de l'overlay. Équivalent de `<Panel position="…">`.

| Propriété | Valeurs |
|---|---|
| `PanelPosition` | `TopLeft` (défaut), `TopCenter`, `TopRight`, `CenterLeft`, `CenterRight`, `BottomLeft`, `BottomCenter`, `BottomRight` |

Marge par défaut : 15.

```xml
<flow:FlowPanel PanelPosition="TopRight"><Button Content="Enregistrer" /></flow:FlowPanel>
```

## NodeResizer

Poignées de redimensionnement d'un node (4 coins, 4 côtés), visibles quand le node est sélectionné. À placer dans le `Grid` racine du template, **après** la carte et **avant** les handles ; le contenu du node doit s'étirer.

| Propriété | Type | Défaut | Description |
|---|---|---|---|
| `NodeMinWidth` / `NodeMinHeight` | `double` | 60 / 40 | Taille minimale. |
| `NodeMaxWidth` / `NodeMaxHeight` | `double` | ∞ | Taille maximale. |
| `Accent` | `Brush` | bleu #3367D9 | Couleur des poignées de coin. |

`Thumb? GetThumb(ResizeDirection)` donne accès à chaque poignée (tests d'interface). Chaque geste émet un `NodeResizeChange` ; l'historique enregistre l'état au début du geste.

## NodeToolbar

`ContentControl` affiché à côté du node quand il est sélectionné. Équivalent de `<NodeToolbar>`.

| Propriété | Type | Défaut | Description |
|---|---|---|---|
| `ToolbarPosition` | `Position` | `Top` | Côté du node. |
| `Offset` | `double` | 10 | Distance au node. |

Style : fond `Flow.ControlsBackground`, coins arrondis, ombre.

## NodeHost

Conteneur interne d'un node (public pour l'automatisation et les tests). Propriétés utiles : `Node`, `IsSelected` (utilisable dans un `Trigger` du style), `ShowOutline`, `OutlineCornerRadius` (8 par défaut).

## BackgroundVariant, ColorMode, PanelPosition

```csharp
enum BackgroundVariant { None, Dots, Lines, Cross }
enum ColorMode { Light, Dark }
enum PanelPosition { TopLeft, TopCenter, TopRight, CenterLeft, CenterRight, BottomLeft, BottomCenter, BottomRight }
```
