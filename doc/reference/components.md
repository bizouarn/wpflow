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
| `ConnectionStatus` | `HandleConnectionStatus` | `None` | Piloté par la vue pendant un fil : `Origin`, `Valid`, `Invalid` ; le dessin par défaut change de couleur, un `Template` peut s'en servir. |
| `IsConnectable` | `bool` | `true` | Le handle accepte des connexions. |
| `IsConnectableStart` / `IsConnectableEnd` | `bool` | `true` | Une connexion peut partir de / aboutir à ce handle. |
| `IsValidConnection` | `Func<Connection,bool>?` | `null` | Validateur propre au handle, en plus de celui de la vue. |
| événement `Connected` (`Connection`) | | | Une connexion vient d'aboutir à ce handle ou d'en partir (`onConnect` d'un handle). |

Le handle fait 8 × 8 par défaut (`Width` / `Height` modifiables) et déborde de la moitié de sa taille sur le bord de son conteneur ; sa zone de clic est élargie de 6 px. Il se stylise comme un `Control` (`Background`, `BorderBrush`, `BorderThickness`, `Width`, `Height`) et un `Template` remplace son dessin : ciblez `flow:Handle` dans un `Style` (voir [Personnaliser](../learn/14-customization.md#handles)).

## MiniMap

Vue d'ensemble du graphe ; cliquer ou glisser recentre le viewport. À placer dans le `Content` de `FlowView`, ou ailleurs avec `Flow="{Binding ElementName=Flow}"`.

Valeurs par défaut : 200 × 150, en bas à droite, marge de 15. Redimensionnez avec `Width`/`Height`, repositionnez avec `HorizontalAlignment`/`VerticalAlignment`/`Margin`. Couleurs : `Flow.MiniMapBackground`, `Flow.MiniMapNode`, `Flow.MiniMapNodeSelected`, `Flow.MiniMapMask`.

| Propriété | Type | Défaut | Description |
|---|---|---|---|
| `NodeColor` / `NodeStrokeColor` | `Func<Node, Brush?>?` | `null` | Couleur et contour par node (`nodeColor`, `nodeStrokeColor`) ; résultat `null` = thème. |
| `NodeStrokeWidth` / `NodeBorderRadius` | `double` | 2 / 3 | Contour et arrondi. |
| `NodeRenderer` | `Action<DrawingContext, Node, Rect>?` | `null` | Dessin libre d'un node (`nodeComponent`). |
| `MaskColor` | `Brush?` | `null` | Voile hors de la vue ; `null` = `Flow.MiniMapMask`. |
| `Pannable` / `Zoomable` / `InversePan` / `ZoomStep` | `bool` / `bool` / `bool` / `double` | `true` / `false` / `false` / 10 | Glisser recentre la vue, molette zoome (`zoomable`), sens inversé, sensibilité. |
| événement `NodeClick` (`MiniMapNodeClickEventArgs`) | | | Clic sur un node de la minimap. |

## FlowControls

Boutons zoom +, zoom −, cadrage (`FitView`) et verrou (`IsLocked`). Placé en bas à gauche par défaut ; la vue pilotée est celle qui le contient, ou `Flow="{Binding ElementName=Flow}"`. Couleurs : `Flow.Controls*`. Le cadenas reflète `FlowView.IsLocked` (y compris quand il est modifié ailleurs).

| Propriété | Type | Défaut | Description |
|---|---|---|---|
| `ShowZoom` / `ShowFitView` / `ShowInteractive` | `bool` | `true` | Affiche les boutons de zoom, de cadrage, de verrou. |
| `Orientation` | `Orientation` | `Vertical` | Disposition des boutons. |
| `ExtraContent` | `object?` | `null` | Boutons supplémentaires (`ControlButton`), avant le verrou. |
| `AnimationDuration` | `TimeSpan` | 0 | Transition des zooms et du cadrage. |
| `FitViewOptions` | `FitViewOptions?` | `null` | Options du bouton de cadrage (sinon celles de la vue). |
| événements | | | `ZoomedIn`, `ZoomedOut`, `FitViewClicked`, `InteractiveChanged` (`bool` : vrai = déverrouillé). |

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
| `KeepAspectRatio` | `bool` | `false` | Conserve le rapport largeur/hauteur pendant le redimensionnement. |
| `ShowResizer` | `bool` | `true` | Faux : poignées masquées (`isVisible`). |
| `AutoScale` | `bool` | `false` | Poignées à taille constante à l'écran quel que soit le zoom (`autoScale`). |
| `Variant` | `ResizerVariant` | `Handle` | `Handle` (carrés aux coins) ou `Line` (traits sur les côtés). |
| `ShouldResize` | `Func<ResizeDirection, Rect, bool>?` | `null` | Refuse une taille proposée. |
| événements `ResizeStarted`, `Resizing`, `ResizeEnded` | `NodeResizeEventArgs` | | Poignée, rectangle de départ et courant. |
| `Accent` | `Brush` | bleu #3367D9 | Couleur des poignées de coin. |

`Thumb? GetThumb(ResizeDirection)` donne accès à chaque poignée (tests d'interface). Chaque geste émet un `NodeResizeChange` ; l'historique enregistre l'état au début du geste.

## NodeToolbar

`ContentControl` affiché à côté du node quand il est sélectionné. Équivalent de `<NodeToolbar>`.

| Propriété | Type | Défaut | Description |
|---|---|---|---|
| `ToolbarPosition` | `Position` | `Top` | Côté du node. |
| `Offset` | `double` | 10 | Distance au node. |
| `Align` | `ToolbarAlign` | `Center` | `Start`, `Center`, `End` le long du côté. |
| `IsToolbarVisible` | `bool?` | `null` | Force la visibilité ; `null` = visible quand le node est sélectionné. |

Style : fond `Flow.ControlsBackground`, coins arrondis, ombre.

## EdgeToolbar

Barre d'outils d'un edge (`<EdgeToolbar>`) : visible quand l'edge est sélectionné. À placer dans un template de label (`EdgeLabelTemplate`, `wpflow-edge-label-{type}`) ou d'edge ; le `DataContext` est l'`Edge` (ou un `EdgeRenderContext`). Propriétés : `ToolbarPosition` (`Top`), `Offset` (10), `IsToolbarVisible` (`bool?`).

## BackgroundLayer

Un motif de fond : `new BackgroundLayer(Variant, Gap, Color, Size, LineWidth, Offset)`. À superposer avec `FlowView.AdditionalBackgrounds` (plusieurs `<Background id="…">`).


## NodeHost

Conteneur interne d'un node (public pour l'automatisation et les tests). Propriétés utiles : `Node`, `IsSelected` (utilisable dans un `Trigger` du style), `ShowOutline`, `OutlineCornerRadius` (0 par défaut : contour à angles droits ; une valeur positive arrondit le contour).

## BackgroundVariant, ColorMode, PanelPosition

```csharp
enum BackgroundVariant { None, Dots, Lines, Cross }
enum ColorMode { Light, Dark }
enum PanelPosition { TopLeft, TopCenter, TopRight, CenterLeft, CenterRight, BottomLeft, BottomCenter, BottomRight }
```

## SelectionToolbar

Barre d'outils d'une sélection multiple : apparaît au-dessus du rectangle englobant les nodes sélectionnés quand au moins `MinSelected` (2) le sont. Avec `NodeIds` (liste d'identifiants), elle suit ces nodes et s'affiche dès que l'un d'eux est sélectionné ; `IsToolbarVisible` force la visibilité. À placer dans l'overlay de `FlowView` (ou `Flow="{Binding ElementName=Flow}"`) ; propriétés `MinSelected`, `Offset` (10).

## NodeRotator

Poignée de rotation (exemple « Rotatable Node ») : un rond au-dessus du node, visible quand il est sélectionné ; le glisser fait tourner le node autour de son centre en écrivant `Node.Rotation` (degrés), avec historique. Maj arrondit à `SnapAngle` (15°). À placer dans le `Grid` racine du template. Les handles et les edges restent attachés aux positions non tournées, comme dans React Flow.

## NodeResizeControl

Poignée de redimensionnement libre (`<NodeResizeControl>`) : un `Thumb` que vous habillez (`Template`, `Content`) et placez où vous voulez dans le template, avec la `Direction` qu'il pilote (`TopLeft`…`Bottom`). Mêmes réglages que `NodeResizer` : `NodeMinWidth`/`Height`, `NodeMaxWidth`/`Height`, `KeepAspectRatio`, `ShouldResize`, événements `ResizeStarted`, `Resizing`, `ResizeEnded`.
