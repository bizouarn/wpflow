# FlowView

`Wpflow.Wpf.FlowView` : `ContentControl`. La surface du graphe (équivalent de `<ReactFlow>` et de `useReactFlow`).

Le `Content` est un **overlay** affiché au-dessus du graphe ; il n'accepte qu'un seul élément (utilisez un `Grid` pour en mettre plusieurs).

```xml
<flow:FlowView Nodes="{Binding Nodes}" Edges="{Binding Edges}" FitViewOnInit="True" />
```

## Données

| Propriété | Type | Défaut | Description |
|---|---|---|---|
| `Nodes` | `IReadOnlyList<Node>` | vide | Les nodes. Liable en `TwoWay`. |
| `Edges` | `IReadOnlyList<Edge>` | vide | Les edges. Liable en `TwoWay`. |
| `Viewport` | `Viewport` | `(0, 0, 1)` | Position et zoom. Liable en `TwoWay`. Contraint par `TranslateExtent`. |
| `AutoApplyChanges` | `bool` | `true` | Applique elle-même les changes à `Nodes`/`Edges`. `false` = mode contrôlé. |

## Vue

| Propriété | Type | Défaut | Description |
|---|---|---|---|
| `MinZoom` / `MaxZoom` | `double` | 0,5 / 2 | Bornes du zoom. |
| `TranslateExtent` | `Rect?` | `null` | Zone (coordonnées flow) hors de laquelle la vue ne peut pas aller. Centrée si plus petite que la vue. |
| `FitViewOnInit` | `bool` | `false` | Cadre le graphe une fois tous les nodes mesurés. |
| `FitViewPadding` | `double` | 0,1 | Marge relative de `FitView`. |

## Interaction

| Propriété | Type | Défaut | Description |
|---|---|---|---|
| `IsLocked` | `bool` | `false` | Interdit déplacement, connexion, sélection (souris) et suppression ; pan et zoom restent actifs. |
| `SnapToGrid` / `SnapGrid` | `bool` / `double` | `false` / 15 | Magnétisme au déplacement. |
| `NodeExtent` | `Rect?` | `null` | Zone où les nodes doivent rester (coordonnées flow). |
| `ConnectionMode` | `ConnectionMode` | `Strict` | `Strict` ou `Loose`. |
| `ConnectionRadius` | `double` | 20 | Distance d'accroche d'une connexion. |
| `IsValidConnection` | `Func<Connection,bool>?` | `null` | Validation personnalisée (non liable : propriété CLR). |

## Apparence

| Propriété | Type | Défaut | Description |
|---|---|---|---|
| `ColorMode` | `ColorMode` | `Light` | `Light` ou `Dark`. |
| `BackgroundVariant` | `BackgroundVariant` | `Dots` | `None`, `Dots`, `Lines`, `Cross`. |
| `BackgroundGap` | `double` | 20 | Espacement du motif (unités flow). |
| `BackgroundColor` | `Brush?` | `null` | Couleur du motif ; `null` = `Flow.Dots`. |
| `EdgeStroke` | `Brush?` | `null` | Couleur des edges ; `null` = `Flow.Edge`. |
| `EdgeStrokeThickness` | `double` | 1 | Épaisseur des edges. |
| `EdgeLabelTemplate` | `DataTemplate?` | `null` | Contenu des labels d'edges (DataContext = `Edge`). |
| `Background` | `Brush` | `Flow.Background` | Fond de la surface (propriété WPF). |

## Performance

| Propriété | Type | Défaut | Description |
|---|---|---|---|
| `OnlyRenderVisibleElements` | `bool` | `false` | Masque nodes et edges hors de la zone visible. |

## Événements

| Événement | Arguments | Description |
|---|---|---|
| `NodeChangesRequested` | `NodeChangesEventArgs` (`Changes`) | Un lot de `NodeChange` va être appliqué. |
| `EdgeChangesRequested` | `EdgeChangesEventArgs` (`Changes`) | Un lot d'`EdgeChange` va être appliqué. |
| `ConnectRequested` | `ConnectEventArgs` (`Connection`) | Connexion valide créée à la souris. |
| `DropRequested` | `FlowDropEventArgs` (`Position`, `Data`) | Objet déposé ; `Position` en coordonnées flow. |
| `StateChanged` | `EventArgs` | Nodes, edges, viewport, verrouillage ou thème modifiés. |

Les deux premiers sont déclenchés **avant** l'application, qu'elle ait lieu ou non (mode contrôlé).

## Méthodes

| Membre | Description |
|---|---|
| `void FitView()` | Cadre tous les nodes mesurés (`fitView`). |
| `void ZoomIn()` / `ZoomOut()` | Zoom ×1,2 / ÷1,2 autour du centre. |
| `void ZoomBy(double factor)` | Zoom relatif autour du centre, borné par `MinZoom`/`MaxZoom`. |
| `XYPosition ScreenToFlow(Point p)` | Convertit un point en coordonnées du contrôle vers les coordonnées flow. |
| `void SelectNode(string id, bool additive = false)` | Sélectionne un node. |
| `void Undo()` / `Redo()` | Annule / rétablit (mode non contrôlé). |
| `bool CanUndo` / `CanRedo` | Disponibilité de l'historique. |
| `IReadOnlyCollection<Node> AbsoluteNodes` | Nodes en coordonnées absolues (pour dessiner des vues liées). |
| `Brush ThemeBrush(string key)` | Brosse du thème courant (`Flow.*`), repli sur le thème clair. |

## Clavier et souris

Voir [Interactions](../learn/09-interactions.md#gestes).

## Modèles de nodes

Un node de type `T` est rendu avec le `DataTemplate` de clé `wpflow-node-T` trouvé dans les ressources (recherche à partir de `FlowView`). Types intégrés : `default`, `input`, `output`, `group`.

## Accessibilité

`FlowView` crée un `FlowViewAutomationPeer` (`Pane`). Voir [Performance et accessibilité](../learn/11-performance-accessibility.md#accessibilité).

## Correspondance React Flow

| React Flow | Wpflow |
|---|---|
| `nodes`, `edges`, `onNodesChange`, `onEdgesChange` | `Nodes`, `Edges`, `NodeChangesRequested`, `EdgeChangesRequested` |
| `onConnect` | `ConnectRequested` |
| `defaultViewport` / `useViewport` | `Viewport` |
| `fitView` (prop) / `fitView()` | `FitViewOnInit` / `FitView()` |
| `minZoom`, `maxZoom` | `MinZoom`, `MaxZoom` |
| `snapToGrid`, `snapGrid` | `SnapToGrid`, `SnapGrid` |
| `nodeExtent`, `translateExtent` | `NodeExtent`, `TranslateExtent` |
| `connectionMode`, `connectionRadius`, `isValidConnection` | `ConnectionMode`, `ConnectionRadius`, `IsValidConnection` |
| `onlyRenderVisibleElements` | `OnlyRenderVisibleElements` |
| `colorMode` | `ColorMode` |
| `nodesDraggable`/`nodesConnectable`/`elementsSelectable` | `IsLocked` (réglage unique) |
