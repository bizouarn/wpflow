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
| `NodesSource` / `EdgesSource` | `IEnumerable?` | `null` | Collections de ViewModels projetées dans `Nodes`/`Edges` (mode [MVVM](../learn/13-mvvm.md)). Undo/Redo restent actifs. |
| `NodeMapping` / `EdgeMapping` | `NodeMapping?` / `EdgeMapping?` | `Id`, `X`, `Y` / `Id`, `Source`, `Target` | Noms des propriétés des ViewModels ; toutes les propriétés de `Node` et d'`Edge` se mappent. |
| `NodeFactory` / `EdgeFactory` | `Func<Node, object?>?` / `Func<Edge, object?>?` | `null` | Créent le ViewModel d'un node ou d'un edge ajouté par la vue (dépôt, connexion). Propriétés de dépendance : liables à une propriété du ViewModel. |
| `SelectedNodes` / `SelectedEdges` | `IList` | vide | Éléments sélectionnés (ViewModels avec une source, sinon `Node`/`Edge`). Écrire = sélectionner ; une `ObservableCollection` liée est mise à jour sur place. |
| `SelectedNode` | `object?` | `null` | Premier node sélectionné ; l'écrire le sélectionne seul. |

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
| `NodesDraggable` | `bool` | `true` | Les nodes peuvent être déplacés (en plus de `Node.Draggable`). |
| `NodesConnectable` | `bool` | `true` | On peut démarrer ou terminer une connexion sur les handles (en plus de `Node.Connectable`, `Handle.IsConnectable*`). |
| `ElementsSelectable` | `bool` | `true` | Nodes et edges sélectionnables (en plus de `Node.Selectable`) ; faux : on ne peut que désélectionner. |
| `EdgesReconnectable` | `bool` | `true` | Les extrémités d'un edge peuvent être rattrapées pour le reconnecter. |
| `AutoPanOnNodeDrag` / `AutoPanOnConnect` / `AutoPanOnSelection` | `bool` | `true` | La vue défile quand la souris approche d'un bord (40 px) pendant un déplacement, une connexion ou une boîte de sélection. |
| `AutoPanSpeed` | `double` | 15 | Défilement maximal, en pixels par image (~16 ms). |
| `SnapToGrid` / `SnapGrid` | `bool` / `double` | `false` / 15 | Magnétisme au déplacement. |
| `NodeExtent` | `Rect?` | `null` | Zone où les nodes doivent rester (coordonnées flow). |
| `ConnectionMode` | `ConnectionMode` | `Strict` | `Strict` ou `Loose`. |
| `ConnectionRadius` | `double` | 20 | Distance d'accroche d'une connexion. |
| `IsValidConnection` | `Func<Connection,bool>?` | `null` | Validation personnalisée (propriété de dépendance, liable). |

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
| `EdgeHitWidth` | `double` | 10 | Largeur de la zone cliquable d'un edge. |
| `SelectionOutlineGap` / `SelectionOutlineThickness` | `double` | 3 / 1,5 | Contour de sélection des nodes à template. |
| `ConnectionLineStyle` | `Style` | gris, 1,5 | Style (`TargetType="Path"`) de l'aperçu de connexion. |
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
| `ConnectStarted` | `ConnectStartEventArgs` (`NodeId`, `HandleId`, `HandleType`, `IsReconnection`) | Un fil est tiré depuis un handle (ou une extrémité d'edge rattrapée). |
| `ConnectEnded` | `ConnectEndEventArgs` (`Position`, `FromNodeId`, `ToNodeId`, `Connection`, `IsValid`) | Fil relâché, réussi ou non ; `Position` (coordonnées flow) permet de créer un node en lâchant dans le vide. |
| `NodeDoubleClick` / `EdgeDoubleClick` | `ElementEventArgs` (`Kind`, `Id`, `Element`, `Position`, `OriginalSource`, `TargetData`) | Double-clic. |
| `NodeContextMenuRequested` / `EdgeContextMenuRequested` / `PaneContextMenuRequested` | `ElementEventArgs` | Clic droit (l'événement n'est pas marqué : un `ContextMenu` WPF s'ouvre aussi). |
| `StateChanged` | `EventArgs` | Nodes, edges, viewport, verrouillage ou thème modifiés. |

Les deux premiers sont déclenchés **avant** l'application, qu'elle ait lieu ou non (mode contrôlé).

### Savoir quelle partie d'un node est visée

Marquez des éléments d'un modèle avec la propriété attachée `FlowView.TargetData` (n'importe quel objet). Au clic, au double-clic et au menu contextuel, `ElementEventArgs.TargetData` donne la marque la plus proche de l'élément pressé (`null` sans marque, et au survol) ; `ElementEventArgs.OriginalSource` donne l'élément WPF pressé. L'élément marqué doit être visible au clic : donnez-lui un `Background`, même `Transparent`.

```xml
<Border Background="Transparent" flow:FlowView.TargetData="{Binding Data.ColumnName}">
    <TextBlock Text="{Binding Data.ColumnName}" />
</Border>
```

Côté commandes, `CommandsReceiveEventArgs="True"` fait recevoir l'`ElementEventArgs` à `NodeClickCommand`, `EdgeClickCommand`, `PaneClickCommand`, aux commandes de double-clic et à celles de menu contextuel, à la place de leur paramètre habituel.

## Méthodes

| Membre | Description |
|---|---|
| `void FitView()` | Cadre tous les nodes mesurés (`fitView`). |
| `void ZoomIn()` / `ZoomOut()` | Zoom ×1,2 / ÷1,2 autour du centre. |
| `void ZoomBy(double factor)` | Zoom relatif autour du centre, borné par `MinZoom`/`MaxZoom`. |
| `XYPosition ScreenToFlow(Point p)` | Convertit un point en coordonnées du contrôle vers les coordonnées flow. |
| `void SelectNode(string id, bool additive = false)` | Sélectionne un node. |
| `void SelectOnly(string? nodeId, string? edgeId)` | Sélectionne ce seul node ou ce seul edge ; avec deux `null`, désélectionne tout. |
| `void SelectAll()` / `ClearSelection()` | Sélectionne tous les nodes et tous les edges / désélectionne tout. |
| `Task<bool> DeleteSelected()` | Supprime la sélection comme `DeleteElementsAsync`, même si la vue est verrouillée (la touche Suppr, elle, respecte `IsLocked`). La tâche est déjà terminée sans `BeforeDeleteAsync`. |
| `Point FlowToScreen(XYPosition position)` | Inverse de `ScreenToFlow`. |
| `void CenterOn(XYPosition position)` | Centre la vue sur un point flow, sans changer le zoom. |
| `void ApplyNodeChanges(changes)` / `ApplyEdgeChanges(changes)` | Injecte des changes comme le ferait un geste : `…ChangesRequested` est levé, puis ils sont appliqués si `AutoApplyChanges`. N'ajoute rien à l'historique. |
| `IReadOnlyList<Node> GetIncomers(string nodeId)` / `GetOutgoers(string nodeId)` | Nodes reliés par un edge entrant / sortant. |
| `void SetLocked(bool locked)` | Change `IsLocked` en gardant sa liaison. |
| `void Undo()` / `Redo()` | Annule / rétablit (en mode contrôlé, la vue ne tient pas d'historique et `CanUndo` reste faux). |
| `string ExportSvg(double padding = 20, string? background = null)` | Dessin vectoriel simplifié (nodes en rectangles avec leur texte, edges et flèches) ; pour une image fidèle, `SaveImage`. |
| `void ClearHistory()` | Vide l'historique de la vue. |
| `string ToJson(Func<Node,object?>? data = null, bool indented = true)` | Exporte le graphe (format React Flow). Avec une source, `Node.Data` n'est écrit que via `data`. |
| `void LoadJson(string json)` | Charge un graphe ; avec une source, passe par `NodeFactory` / `EdgeFactory`. |
| `IReadOnlyCollection<Node> AbsoluteNodes` | Nodes en coordonnées absolues (pour dessiner des vues liées). |
| `Brush ThemeBrush(string key)` | Brosse du thème courant (`Flow.*`), repli sur le thème clair. |

## Options d'interaction avancées

| Propriété | Type | Défaut | Description |
|---|---|---|---|
| `PanOnDrag` | `bool` | `true` | Glisser sur le fond déplace la vue. |
| `SelectionOnDrag` | `bool` | `false` | Glisser sur le fond trace un rectangle de sélection (la vue se déplace alors avec Espace ou le bouton du milieu). |
| `SelectionMode` | `FlowSelectionMode` (`Partial` / `Full`) | `Partial` | Le rectangle sélectionne les nodes qu'il touche ou qu'il contient entièrement. |
| `SelectNodesOnDrag` | `bool` | `true` | Appuyer sur un node le sélectionne ; faux : il se déplace sans l'être (sélection au clic). |
| `PanOnScroll` / `PanOnScrollSpeed` / `PanOnScrollMode` | `bool` / `double` / `Free`, `Vertical`, `Horizontal` | `false` / 0,5 / `Free` | La molette déplace la vue (Maj : horizontalement) ; `ZoomActivationKey` force le zoom. |
| `ZoomOnScroll` / `ZoomOnPinch` / `ZoomOnDoubleClick` | `bool` | `true` | Zoom à la molette, au pincement, au double-clic sur le fond (×2, animé). |
| `DeleteKeys` | `IReadOnlyList<Key>` | `Delete`, `Back` | Touches de suppression ; vide = pas de suppression au clavier. |
| `SelectionKey` / `MultiSelectionKey` / `ZoomActivationKey` | `ModifierKeys` | `Shift` / `Control` / `Control` | Modificateurs : rectangle de sélection, ajout à la sélection, zoom forcé. |
| `PanActivationKey` | `Key` | `Space` | Maintenue, le glisser déplace la vue même au-dessus d'un node. |
| `NodeDragThreshold` / `ConnectionDragThreshold` / `PaneDragThreshold` | `double` | 1 | Pixels à franchir avant que le geste démarre. |
| `NodeOrigin` | `XYPosition` | `(0, 0)` | Point d'ancrage des nodes (`(0,5 ; 0,5)` = centre) ; surchargé par `Node.Origin`. |
| `ElevateNodesOnSelect` / `ElevateEdgesOnSelect` | `bool` | `true` / `false` | Un élément sélectionné passe devant. |
| `NodesFocusable` / `EdgesFocusable` | `bool` | `true` / `true` | Éléments atteints par Tab. |
| `DisableKeyboardA11y` | `bool` | `false` | Désactive Tab et les flèches. |
| `AutoPanOnNodeFocus` | `bool` | `true` | La vue défile pour montrer le node atteint au clavier. |
| `ConnectOnClick` | `bool` | `true` | Connecte par deux clics (handle, puis handle) ; Échap annule. |
| `ConnectionLineType` | `Bezier`, `Straight`, `Step`, `SmoothStep`, `SimpleBezier` | `Bezier` | Forme de l'aperçu de connexion. |
| `ConnectionLineTemplate` | `DataTemplate?` | `null` | Rendu libre de l'aperçu ; `DataContext` = `ConnectionLineContext`. |
| `ReconnectRadius` | `double` | 24 | Portée (pixels) pour rattraper l'extrémité d'un edge. |
| `DefaultEdgeOptions` | `Edge?` | `null` | Modèle des edges créés par connexion (`Type`, `Animated`, marqueurs, style…). |
| `DefaultMarkerColor` | `Brush?` | `null` | Couleur des marqueurs ; `null` = couleur du trait. |
| `FitViewOptions` | `FitViewOptions?` | `null` | Options de `FitViewOnInit` et du bouton de cadrage. |
| `BackgroundSize` / `BackgroundLineWidth` / `BackgroundOffset` | `double?` / `double` / `double` | `null` / 1 / 0 | Réglages du fond. |
| `AdditionalBackgrounds` | `IReadOnlyList<BackgroundLayer>?` | `null` | Motifs superposés au fond principal. |
| `NodeAriaLabelProvider` | `Func<Node, string?>?` | `null` | Nom UI Automation des nodes sans `AriaLabel` (`ariaLabelConfig`) ; à définir avant les nodes. |
| `ZIndexMode` | `FlowZIndexMode` | `Basic` | `Basic` (profondeur + sélection au premier plan), `Auto` (un parent dont un descendant est sélectionné passe aussi devant), `Manual` (seul `Node.ZIndex` compte). |
| `PanOnDragButtons` | `FlowMouseButtons` | `Left`, `Middle` | Boutons qui déplacent la vue ; avec `Right`, un glisser ne rouvre pas le menu contextuel. |
| `PreventScrolling` | `bool` | `true` | La vue consomme la molette ; faux : elle remonte au parent (`ScrollViewer`). |
| `EdgeAriaLabelProvider` | `Func<Edge, string?>?` | `null` | Nom UI Automation des edges sans `AriaLabel`. |
| `AnnounceSelection` | `bool` | `true` | Annonce la sélection d'un node ou d'un edge aux lecteurs d'écran. |
| `Debug` | `bool` | `false` | Écrit les changes appliqués dans `System.Diagnostics.Trace`. |
| `BeforeDeleteAsync` | `Func<BeforeDeleteEventArgs, Task<bool>>?` | `null` | Contrôle asynchrone avant suppression ; `false` annule. Utilisé par la touche de suppression et par `DeleteElementsAsync`. |
| `NodesInitialized` | `bool` (lecture seule) | — | Tous les nodes visibles sont mesurés. |
| `ConnectionState` | `FlowConnectionState?` (lecture seule) | `null` | Connexion en cours : origine, position, cible, validité (`useConnection`). |
| `AutoConnect` | `bool` | `true` | Faux : une connexion lève `ConnectRequested` sans créer d'edge ; le reste de l'état reste appliqué par la vue. |
| `HistoryEnabled` / `HistoryLimit` | `bool` / `int` | `true` / 100 | Historique de la vue (sans effet sur `History`, celui du ViewModel). |

Attachées, à poser sur un élément d'un template de node : `flow:FlowView.NoDrag="True"`, `NoPan`, `NoWheel` (équivalents de `nodrag`, `nopan`, `nowheel`), et `flow:NodeHost.DragHandle="grip"` pour la zone qui déplace le node quand `Node.DragHandle = "grip"`.

## Événements supplémentaires

| Événement | Description |
|---|---|
| `FlowInitialized` | Vue prête (`onInit`). |
| `ErrorOccurred` (`FlowErrorEventArgs` : `Code`, `Message`) | Edge vers un node absent (`edge-node-missing`), une fois par edge (`onError`). |
| `BeforeDelete` (`BeforeDeleteEventArgs` : `Nodes`, `Edges`, `Cancel`) | Suppression imminente, annulable (`onBeforeDelete`). |
| `NodeClick` / `EdgeClick` / `PaneClick` | Clics (`ElementEventArgs`). |
| `NodeMouseEnter`/`Move`/`Leave`, `EdgeMouseEnter`/`Move`/`Leave`, `PaneMouseEnter`/`Move`/`Leave` | Survol (`ElementEventArgs`). |
| `NodeDragStarted` / `NodeDragging` / `NodeDragStopped` (`NodeDragEventArgs`) | Déplacement de nodes, aussi pour une sélection multiple. |
| `SelectionStarted` / `SelectionEnded` (`SelectionBoxEventArgs`) | Rectangle de sélection. |
| `SelectionContextMenuRequested` | Clic droit sur la sélection (plusieurs nodes). |
| `EdgeReconnected` (`ReconnectEventArgs`) | Extrémité d'edge déplacée. |
| `ReconnectStarted` (`ElementEventArgs`) / `ReconnectEnded` (`ReconnectEndEventArgs` : `Edge`, `Connection`, `Succeeded`) | On rattrape / relâche l'extrémité d'un edge, avec ou sans nouvelle connexion. |
| `SelectionDragStarted` / `SelectionDragging` / `SelectionDragStopped` | Déplacement de plusieurs nodes à la fois. |
| `NodeChangesRequested` / `EdgeChangesRequested` | Leur argument a un `Changes` **modifiable** : un gestionnaire peut filtrer ou remplacer les changes avant leur application (middleware). |
| `ClickConnectStarted` / `ClickConnectEnded` | Connexion par deux clics. |
| `ViewportMoveStarted` / `ViewportMoved` / `ViewportMoveEnded` | Mouvement de la vue (glisser, molette, transition ; `ViewportMoved` aussi pour les changements programmatiques). |
| `PaneScroll` | Molette sur le fond. |
| `Deleted` (`DeletedEventArgs` : `Nodes`, `Edges`) | Une suppression demandée à la vue a eu lieu (`onDelete`). |
| `SelectionChanged` (`FlowSelectionEventArgs` : `Nodes`, `Edges`) | La sélection a changé (`onSelectionChange`). |
| `NodesReady` | Une fois, quand tous les nodes ont été mesurés (`useNodesInitialized`). |

Les mêmes évènements existent en Blazor, et les mêmes gestes les lèvent dans le même ordre : un double-clic donne clic, double-clic, clic ; un clic droit sur une sélection, le menu de la sélection puis celui du node.

## Méthodes d'instance (`useReactFlow`)

| Membre | Description |
|---|---|
| `bool FitView(FitViewOptions? options)` | Cadre les nodes : `Padding`, `MinZoom`, `MaxZoom`, `NodeIds`, `IncludeHiddenNodes`, `Duration` (transition). |
| `void SetViewport(Viewport v, TimeSpan duration = default)` | Déplace la vue, avec transition si `duration > 0` ; un geste l'interrompt. `IsViewportAnimating`. |
| `void SetCenter(double x, double y, double? zoom = null, TimeSpan duration = default)` | Centre la vue sur un point flow. |
| `void FitBounds(Rect bounds, double? padding = null, TimeSpan duration = default, …)` | Cadre un rectangle. |
| `void ZoomTo(double zoom, TimeSpan duration = default)` / `ZoomIn(TimeSpan)` / `ZoomOut(TimeSpan)` | Zoom, éventuellement animé. |
| `Node? GetNode(id)` / `Edge? GetEdge(id)` / `Node? GetInternalNode(id)` | Recherche par identifiant (le dernier en coordonnées absolues). |
| `void AddNodes(...)` / `AddEdges(...)` | Ajout (`AddEdges` évite les doublons). |
| `void UpdateNode(id, Func<Node,Node>)` / `UpdateNodeData(id, data)` / `UpdateEdge(...)` / `UpdateEdgeData(...)` | Mise à jour par identifiant. |
| `void UpdateNodeInternals(id)` | Remesure un node et ses handles (`useUpdateNodeInternals`). Inutile quand le node change de taille ou qu'un handle apparaît, disparaît ou change de propriété ; nécessaire après avoir seulement **déplacé** des handles (marge changée par un déclencheur de style, animation). |
| `bool DeleteElements(nodeIds, edgeIds)` / `Task<bool> DeleteElementsAsync(...)` | Suppression par identifiants (descendants et edges reliés compris, `Deletable`, `BeforeDelete` et, pour la version asynchrone, `BeforeDeleteAsync` respectés). |
| `GetIntersectingNodes(Node` ou `Rect, bool partially = true)` / `IsNodeIntersecting(...)` | Intersections, en coordonnées absolues. |
| `GetNodeConnections(nodeId, HandleType? type, handleId)` | Edges d'un node. |
| `Rect? GetNodesBounds(IEnumerable<string>? ids)` | Rectangle englobant. |
| `bool Copy()` / `Cut()` / `Paste()` / `CanPaste` | Presse-papiers de graphe (Ctrl+C / X / V, commandes `FlowCommands.Copy`…). Les copies reçoivent des ids neufs et sont sélectionnées ; chaque collage décale de 20 px. |
| `RenderTargetBitmap? RenderToBitmap(padding, scale, background)` / `bool SaveImage(path, padding, scale)` | Export du graphe entier en image (exemples « Download Image » et « Printing »). |
| `Canvas? PortalLayer` / `AddToPortal(element, x, y)` / `RemoveFromPortal(element)` | Contenu positionné en coordonnées flow, au-dessus des nodes (`ViewportPortal`). |

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
| `nodesDraggable`, `nodesConnectable`, `elementsSelectable`, `edgesReconnectable` | `NodesDraggable`, `NodesConnectable`, `ElementsSelectable`, `EdgesReconnectable` (et `IsLocked` pour tout verrouiller) |
| `autoPanOnNodeDrag`, `autoPanOnConnect`, `autoPanOnSelection`, `autoPanSpeed` | `AutoPanOnNodeDrag`, `AutoPanOnConnect`, `AutoPanOnSelection`, `AutoPanSpeed` |
| `onConnectStart`, `onConnectEnd` | `ConnectStarted`, `ConnectEnded` (+ `ConnectStartCommand`, `ConnectEndCommand`) |
| `onNodeDoubleClick`, `onEdgeDoubleClick`, `on*ContextMenu` | `NodeDoubleClick`, `EdgeDoubleClick`, `*ContextMenuRequested` (+ commandes `*Command`) |

## État, commandes et MVVM

Voir le guide [MVVM](../learn/13-mvvm.md) pour le détail ; aide-mémoire :

| Propriété | Type | Description |
|---|---|---|
| `Zoom` | `double` | Zoom, `TwoWay` ; l'écrire zoome autour du centre. |
| `IsConnecting` | `bool` (lecture seule) | Connexion en cours de tracé. |
| `CanUndo` / `CanRedo` | `bool` (lecture seule) | Disponibilité de l'historique (celui de la vue, ou `History`). |
| `Controller` | `IFlowController?` | Remis au ViewModel par liaison `OneWayToSource` : `FitView`, `ZoomTo`, `CenterOn`, sélection, annulation, conversions de coordonnées. |
| `History` | `IFlowHistory?` | Historique du ViewModel (`Checkpoint`, `Undo`, `Redo`) à la place de celui de la vue. |
| `ConnectCommand` | `ICommand?` | Paramètre `Connection` ; remplace la création de l'edge, `CanExecute` faux la refuse. |
| `NodesDeleteCommand` / `EdgesDeleteCommand` | `ICommand?` | Paramètre : liste d'éléments ; remplace la suppression automatique, `CanExecute` faux l'annule. |
| `DropCommand` | `ICommand?` | Paramètre `FlowDropEventArgs`. |
| `NodeDragStopCommand` | `ICommand?` | Paramètre : liste d'éléments déplacés. |
| `NodeClickCommand` / `EdgeClickCommand` | `ICommand?` | Paramètre : l'élément cliqué. |
| `PaneClickCommand` | `ICommand?` | Paramètre : `XYPosition` flow. |
| `CommandsReceiveEventArgs` | `bool` (faux) | Les commandes de clic, de double-clic et de menu contextuel reçoivent l'`ElementEventArgs` (dont `TargetData`). |
| autres `…Command` | `ICommand?` | Une commande par évènement ([liste](../learn/13-mvvm.md#commandes)) ; elle reçoit les arguments de l'évènement. |

`FlowCommands` (classe statique de `RoutedUICommand`) : `FitView`, `ZoomIn`, `ZoomOut`, `Undo`, `Redo`, `SelectAll`, `ClearSelection`, `DeleteSelection`, `ToggleLock`.
