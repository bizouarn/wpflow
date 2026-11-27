# Wpflow.Blazor

La version Blazor de Wpflow : le même éditeur de graphes que la version WPF, rendu en HTML/SVG, au-dessus du noyau `Wpflow.Core` (modèle immuable, changes, chemins d'edges, viewport, connexions, JSON). Les noms suivent React Flow et la version WPF ; chaque page de ce dossier s'applique aussi ici pour les concepts (`Node`, `Edge`, changes, handles…).

## Installation

Référencez le projet `Wpflow.Blazor` (ou le paquet NuGet du même nom) puis ajoutez la feuille de style dans la page hôte (`App.razor`, `index.html`…) :

```html
<link rel="stylesheet" href="_content/Wpflow.Blazor/wpflow.css" />
```

Le script du pont JS (`_content/Wpflow.Blazor/wpflow.js`) est chargé par le composant lui-même.

## Premier graphe

```razor
@using Wpflow.Blazor
@using Wpflow.Core

<div style="height: 500px">
    <FlowView @bind-Nodes="_nodes" @bind-Edges="_edges">
        <FlowMiniMap />
        <FlowControls />
    </FlowView>
</div>

@code {
    private IReadOnlyList<Node> _nodes =
    [
        new Node("1", new XYPosition(0, 0), "input", "Entrée"),
        new Node("2", new XYPosition(0, 120), "output", "Sortie")
    ];

    private IReadOnlyList<Edge> _edges = [new Edge("e1-2", "1", "2", Animated: true, MarkerEnd: EdgeMarker.ArrowClosed)];
}
```

Le conteneur doit avoir une hauteur (la vue remplit son parent). `@bind-Nodes` / `@bind-Edges` / `@bind-Viewport` publient l'état après chaque interaction.

<!-- exemple-blazor: premier-graphe -->

## État : les changes

Comme dans React Flow, une interaction ne modifie jamais les listes : elle produit des `NodeChange` / `EdgeChange`, publiés par `OnNodesChange` / `OnEdgesChange` puis appliqués (`AutoApplyChanges`, vrai par défaut) avec `Changes.Apply`. Avec `AutoApplyChanges="false"` (mode contrôlé), vous appliquez les changes vous-même. Une connexion lève alors seulement `OnConnect`, comme en WPF : créez-y l'edge. `ApplyNodeChanges` / `ApplyEdgeChanges` permettent d'en injecter.

<!-- exemple-blazor: etat-controle -->

## Contenu des nodes

Types intégrés : `input`, `output`, `default`, `group` (`<FlowDefaultNode Node="n" />`). Pour du contenu propre, un modèle par type (`NodeTemplates`) ou un modèle unique (`NodeTemplate`) :

```razor
<FlowView @bind-Nodes="_nodes" @bind-Edges="_edges" NodeTemplates="_templates" />

@code {
    private Dictionary<string, RenderFragment<Node>> _templates = new()
    {
        ["card"] = n => @<div class="wf-node-default">
            <NodeResizer />
            <input class="nodrag" value="@n.Data" />
            <FlowHandle Type="HandleType.Target" Position="Position.Left" />
            <FlowHandle Type="HandleType.Source" Position="Position.Right" />
        </div>
    };
}
```

Les contrôles HTML (`input`, `button`, `select`…) et tout élément de classe `nodrag` ne font pas glisser le node ; `nopan` empêche le défilement de la vue, `nowheel` laisse la molette à l'élément. `Node.DragHandle` restreint le glisser à un sélecteur CSS (`.poignee`).

<!-- exemple-blazor: nodes-personnalises -->

## FlowView

### Données et état

| Paramètre | Défaut | Description |
|---|---|---|
| `Nodes`, `Edges`, `Viewport` (+ `…Changed`) | | Données et vue, liables. |
| `AutoApplyChanges` | vrai | Faux : mode contrôlé. |
| `IsLocked` (+ `IsLockedChanged`) | faux | Plus de glisser, connexion ni sélection (cadenas des contrôles). |
| `HistoryEnabled`, `HistoryLimit` | vrai, 100 | Annuler / rétablir (Ctrl+Z, Ctrl+Y). Sans effet en mode contrôlé : l'historique appartient à l'application. |
| `NodesSource`, `EdgesSource`… | | Voir [MVVM](#mvvm). |
| `Debug` | faux | Journalise les changes dans la console. |

### Viewport

`MinZoom` (0,5), `MaxZoom` (2), `FitViewOnInit`, `FitViewPadding`, `FitViewOptions`, `TranslateExtent`, `NodeExtent`, `NodeOrigin`, `SnapGrid` (pas d'aimantation, `null` = aucune), `OnlyRenderVisibleElements` (virtualisation : nodes et edges hors de la vue ne sont pas rendus ; avec `FitViewOnInit`, après le premier cadrage, sinon dès que la taille de la vue est connue).

### Interactions

`PanOnDrag`, `PanOnDragButtons` (`[0, 1]`), `PanOnScroll`, `PanOnScrollSpeed`, `PanOnScrollMode`, `ZoomOnScroll`, `ZoomOnPinch`, `ZoomOnDoubleClick`, `PreventScrolling`, `NodesDraggable`, `NodesConnectable`, `ElementsSelectable`, `EdgesReconnectable`, `SelectNodesOnDrag`, `SelectionOnDrag`, `SelectionMode` (`Partial` / `Full`), `SelectionKey` (`Shift`), `MultiSelectionKey` (`Control`), `PanActivationKey` (`Space`), `ZoomActivationKey` (`Control`), `DeleteKeys`, `DeleteKeyEnabled`, `NodeDragThreshold`, `ConnectionDragThreshold`, `PaneDragThreshold`, `AutoPanOnNodeDrag`, `AutoPanOnConnect`, `AutoPanOnSelection`, `AutoPanOnNodeFocus`, `AutoPanSpeed`, `EdgeHitWidth`, `ElevateNodesOnSelect`, `ElevateEdgesOnSelect`, `ZIndexMode`.

Clavier : Suppr / Retour arrière supprime, Ctrl+A tout sélectionner, Ctrl+C / X / V presse-papier (copies décalées de 20 px), Ctrl+Z / Ctrl+Y historique, flèches déplacent la sélection (Maj : ×4, `SnapGrid` sinon 5 px), Échap efface la sélection ou annule une connexion par clics, Entrée / Espace sélectionne le node ayant le focus, Espace + glisser fait défiler la vue. Tactile : pincement à deux doigts (non vérifié sur matériel).

### Connexions

`ConnectionMode` (`Strict` / `Loose`), `IsValidConnection`, `ConnectionRadius` (20 px), `ReconnectRadius` (24 px), `ConnectOnClick` (clic sur un handle puis sur un autre), `ConnectionLineType`, `ConnectionLineTemplate` (reçoit l'état de la connexion, avec le tracé du fil dans `Path`), `AutoConnect` (ajoute l'edge), `DefaultEdgeType`, `DefaultEdgeOptions` (`edge => edge with { Animated = true }`). Reconnexion : tirez l'extrémité d'un edge vers un autre handle (relâcher dans le vide la laisse en place) ; un simple clic près d'une extrémité reste un clic sur l'edge. Comme en WPF, elle lève `OnConnectStart` (`IsReconnection`), `OnReconnectStart`, puis `OnReconnect` si elle aboutit, `OnReconnectEnd` et `OnConnectEnd`. `FlowHandle` : `Type`, `Position`, `HandleId`, `Offset`, `IsConnectable`, `IsConnectableStart`, `IsConnectableEnd`, `IsValidConnection`, `OnConnected`. `CurrentConnection` expose la connexion en cours (`useConnection`).

<!-- exemple-blazor: connexions -->

### Rendu

`ShowBackground`, `BackgroundVariant` (`Dots`, `Lines`, `Cross`), `BackgroundGap`, `BackgroundSize`, `BackgroundLineWidth`, `BackgroundColor`, `BackgroundOffset`, `AdditionalBackgrounds` (liste de `BackgroundLayer`), `Dark` (ou les variables CSS `--wf-*`), `DefaultMarkerColor`, `NodeTemplate(s)`, `EdgeTypes` (un `IEdgePathProvider` par type ; il reçoit l'edge tracé dans `EdgeGeometry.Edge`, par exemple pour qu'un seul type de lien ERD varie selon `Edge.Data`), `EdgeTemplates` (dessin complet d'un type d'edge), `EdgeLabelTemplate`, `MarkerTemplates` (marqueur SVG personnalisé, `Edge.MarkerEndKey`).

### Accessibilité

`NodesFocusable`, `EdgesFocusable`, `DisableKeyboardA11y`, `AnnounceSelection` (région `aria-live`), `NodeAriaLabelProvider`, `EdgeAriaLabelProvider`, rôles et `aria-selected` sur nodes et edges.

### Évènements

`OnNodesChange`, `OnEdgesChange`, `OnConnect`, `OnConnectStart`, `OnConnectEnd`, `OnClickConnectStart`, `OnClickConnectEnd`, `OnReconnectStart`, `OnReconnect`, `OnReconnectEnd`, `OnNodeClick`, `OnNodeDoubleClick`, `OnNodeContextMenu`, `OnNodeMouseEnter`, `OnNodeMouseLeave`, `OnEdgeClick`, `OnEdgeDoubleClick`, `OnEdgeContextMenu`, `OnEdgeMouseEnter`, `OnEdgeMouseLeave`, `OnPaneClick`, `OnPaneContextMenu`, `OnPaneMouseEnter`, `OnPaneMouseLeave`, `OnPaneMouseMove`, `OnNodeMouseMove`, `OnEdgeMouseMove`, `OnPaneScroll`, `OnStateChange`, `OnSelectionContextMenu`, `OnNodeDragStart`, `OnNodeDrag`, `OnNodeDragStop`, `OnSelectionDragStart`, `OnSelectionDrag`, `OnSelectionDragStop`, `OnSelectionStart`, `OnSelectionEnd`, `OnSelectionChange`, `OnMoveStart`, `OnMove`, `OnMoveEnd`, `OnInit`, `OnNodesInitialized`, `OnError`, `OnDrop` (glisser-déposer depuis une palette, position en coordonnées flow), `OnBeforeDelete` (renvoyer faux annule), `OnDelete`. Les écouteurs de survol et de menu contextuel ne sont attachés que si un gestionnaire est fourni.

Comme en WPF, un double-clic donne **clic, double-clic, clic**, et un clic droit sur une sélection lève `OnSelectionContextMenu` puis `OnNodeContextMenu`. Le second clic est retenu jusqu'au double-clic du navigateur (au plus 500 ms, si un élément de votre modèle l'arrête) ; les deux arrivent alors dans le même traitement.

#### Savoir quel élément du node a été cliqué

Pour `OnNodeClick`, `OnNodeDoubleClick` et `OnNodeContextMenu`, `NodeEventArgs.TargetData` contient les attributs `data-*` de l'élément pressé et de ses parents à l'intérieur du node (le plus proche l'emporte), sans le préfixe `data-` :

```razor
@foreach (var column in table.Columns)
{
    <div data-column="@column.Name">@column.Name <small>@column.Type</small></div>
}
```

```csharp
void OnDoubleClick(NodeEventArgs e)
{
    if (e.TargetData.TryGetValue("column", out var column)) EditColumn(e.Node.Id, column);
}
```

L'élément retenu est celui sous le pointeur à l'appui. Le dictionnaire est vide au survol et si aucun élément ne porte d'attribut. Le node lui-même est exclu. Un clic sur un contrôle HTML (`button`, `input`, `select`, `textarea`, `a`) ne lève pas `OnNodeClick` : utilisez son propre `@onclick`.

### Méthodes (`@ref`)

Viewport : `SetViewport(viewport, duration)`, `SetCenter`, `FitBounds`, `FitView(options)`, `ZoomTo`, `ZoomIn`, `ZoomOut`, `ZoomBy` (toutes animables avec une `duration`). Requêtes : `GetNode`, `GetEdge`, `GetInternalNode`, `GetIntersectingNodes`, `IsNodeIntersecting`, `GetNodeConnections`, `GetIncomers`, `GetOutgoers`, `GetNodesBounds`, `SelectedNodes`, `SelectedEdges`. Édition : `AddNodes`, `AddEdges`, `UpdateNode`, `UpdateNodeData`, `UpdateEdge`, `UpdateEdgeData`, `DeleteElements`, `DeleteSelected`, `SelectAll`, `ClearSelection`, `Copy`, `Cut`, `Paste`, `Undo`, `Redo`, `CanUndo`, `CanRedo`, `ClearHistory`, `SetLocked`. États : `NodesInitialized`, `CanPaste`, `IsViewportAnimating`, `IsConnecting`, `Zoom`, `AbsoluteNodes`. Conversion : `ScreenToFlow`, `FlowToScreen`, `ClientToFlow`. Sauvegarde : `ToJson()` / `LoadJson(json)` (format React Flow, voir [Sauvegarde et JSON](learn/12-serialization.md)), `SaveImage("graphe.png")` (télécharge le rendu réel en PNG ou SVG : nodes à contenu personnalisé, styles et thème ; `vector: true` pour le dessin simplifié), `ExportSvg()` (le dessin simplifié : rectangles et texte).

## Composants

| Composant | Rôle |
|---|---|
| `FlowHandle` | Point de connexion, à placer dans un modèle de node. |
| `FlowDefaultNode` | Nodes intégrés (`input`, `output`, `default`, `group`). |
| `FlowControls` | Zoom +/−, cadrage, verrou ; `ShowZoom`, `ShowFitView`, `ShowInteractive`, `Horizontal`, `PanelPosition`, `ExtraContent`, `AnimationDuration`, `FitViewOptions`, `OnZoomIn/Out`, `OnFitView`, `OnInteractiveChange`. |
| `FlowMiniMap` | Vue d'ensemble ; `Width`, `Height`, `PanelPosition`, `NodeColor`, `NodeStrokeColor`, `NodeStrokeWidth`, `NodeBorderRadius`, `MaskColor`, `Pannable`, `Zoomable`, `InversePan`, `ZoomStep`, `OnNodeClick`. |
| `FlowPanel` | Ancre son contenu à l'une des 8 positions (`PanelPosition`). |
| `NodeResizer`, `NodeResizeControl` | Poignées de redimensionnement (4 coins, 4 côtés) ou poignée libre ; `NodeMinWidth/Height`, `NodeMaxWidth/Height`, `KeepAspectRatio`, `ShowResizer`, `AutoScale`, `Variant`, `ShouldResize`, `OnResizeStart`, `OnResize`, `OnResizeEnd`, `Accent`. |
| `NodeRotator` | Poignée de rotation (`Node.Rotation`, Maj = pas de `SnapAngle`). |
| `NodeToolbar` | Barre d'outils du node sélectionné ; `ToolbarPosition`, `Offset`, `Align`, `IsToolbarVisible`. |
| `EdgeToolbar` | Barre d'outils d'un edge sélectionné, dans `EdgeLabelTemplate`. |
| `SelectionToolbar` | Barre d'outils d'une sélection multiple ; `MinSelected`, `Offset`, `NodeIds`, `IsToolbarVisible`. |

## Thème

Variables CSS sur `.wf-flow` : `--wf-bg`, `--wf-dot`, `--wf-node-bg`, `--wf-node-border`, `--wf-node-text`, `--wf-node-selected`, `--wf-edge`, `--wf-edge-selected`, `--wf-handle`, `--wf-handle-bg`, `--wf-connect`, `--wf-panel-bg`, `--wf-panel-border`, `--wf-minimap-mask`, `--wf-resize-accent`. `Dark="true"` applique le thème sombre.

<!-- exemple-blazor: theme -->

## MVVM

Comme en WPF ([MVVM](learn/13-mvvm.md)), la vue peut travailler sur **vos ViewModels** au lieu de listes de `record` : mêmes mappages, mêmes règles de réécriture, de refus et d'annulation (la logique est commune, dans le noyau).

```razor
<FlowView NodesSource="_vm.Tasks" EdgesSource="_vm.Links" NodeMapping="_mapping"
          EdgeFactory="@(e => new LinkVm(e.Id, e.Source, e.Target))"
          @bind-SelectedNodeItems="_selected" ControllerChanged="c => _vm.Flow = c"
          NodeTemplates="_templates" />

@code {
    // Dans un champ : un mappage écrit dans le balisage serait recréé, et la source relue, à chaque rendu.
    private readonly NodeMapping _mapping = new() { SelectedPath = nameof(TaskVm.IsSelected) };
    private IReadOnlyList<object> _selected = [];
}
```

Ne donnez pas `Nodes` / `Edges` en même temps qu'une source.

| Paramètre | Description |
|---|---|
| `NodesSource`, `EdgesSource` | Collections de ViewModels (`ObservableCollection`, éléments `INotifyPropertyChanged`). |
| `NodeMapping`, `EdgeMapping` | Noms des propriétés ; voir [les mappages](learn/13-mvvm.md#mappages). `Node.Data` est le ViewModel. |
| `NodeFactory`, `EdgeFactory` | Créent le ViewModel d'un élément ajouté par la vue (connexion, collage, `LoadJson`). |
| `SelectedNodeItems`, `SelectedEdgeItems`, `SelectedNode` (+ `…Changed`) | Sélection en ViewModels. Une **nouvelle** valeur sélectionne ; la même, redonnée à chaque rendu sans liaison, est ignorée. `SelectedNodes` reste la liste des `record`. |
| `ControllerChanged` | Remet un `IFlowController` (asynchrone) à votre ViewModel. |
| `History` | Votre historique (`Wpflow.Blazor.IFlowHistory`) à la place de celui de la vue. |

`NodeModel(id)` et `EdgeModel(id)` donnent le ViewModel d'un élément d'un évènement.

**Pas de commandes** : les `EventCallback` en tiennent lieu. Pour ce qu'une commande WPF *remplace* :

| WPF | Blazor |
|---|---|
| `ConnectCommand` (remplace la création, `CanExecute` valide) | `AutoConnect="false"`, `OnConnect` ajoute votre lien, `IsValidConnection` valide |
| `NodesDeleteCommand`, `EdgesDeleteCommand` | `OnBeforeDelete` : renvoyer faux annule **toute** la suppression ; faites alors la vôtre (elle n'entre pas dans l'historique de la vue) |
| `DropCommand` | `OnDrop` |

Deux points propres à Blazor :

- **Rendu** : les modèles de nodes sont mémorisés. Un `PropertyChanged`, même d'une propriété non mappée (un titre), redessine le node concerné. En revanche, une collection **à l'intérieur** d'un ViewModel (les colonnes d'une table) doit être signalée par le ViewModel : `Columns.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Columns));`.
- **Threads** : modifiez les ViewModels sur le contexte de rendu du composant (`InvokeAsync` depuis une minuterie ou une tâche de fond). Les changements d'une même rafale ne donnent qu'un rendu.

<!-- exemple-blazor: mvvm -->

## Différences avec la version WPF

Les deux vues ont les mêmes évènements et les mêmes réglages, ce qu'un test vérifie (`ViewParityTests`), et les mêmes gestes les lèvent dans le même ordre. Le tableau de parité est la matrice [`GestureMatrix`](../tests/Scenarios.Shared/GestureMatrix.cs) : pour chaque geste, la suite exacte des évènements, jouée avec de vraies entrées dans les deux vues. Restent les écarts liés à la plateforme :

| WPF | Blazor |
|---|---|
| Méthodes synchrones | Asynchrones (`await`) |
| Commandes (`ICommand`) | `EventCallback` |
| Ressources `wpflow-node-*`, `wpflow-edge-*`, `wpflow-marker-*` | `NodeTemplates`, `EdgeTypes`, `EdgeTemplates`, `MarkerTemplates` |
| `ColorMode`, `EdgeStroke`, `EdgeStrokeThickness`, `SelectionOutline*`, `ConnectionLineStyle` | `Dark` et variables CSS |
| `DefaultEdgeOptions` | aussi `DefaultEdgeType` |
| `DeleteKeys` vide | aussi `DeleteKeyEnabled` |
| `SnapToGrid` + `SnapGrid` | `SnapGrid` (`null` : sans aimantation) |
| `SelectedNodes` (ViewModels) | `SelectedNodeItems` |
| `Zoom` lié dans les deux sens | `Zoom` en lecture, `ZoomTo` |
| `FitViewOnInit` faux par défaut | vrai par défaut |
| `EdgeHitWidth` : 10 par défaut | 20 par défaut |
| `RenderToBitmap`, `SaveImage` (fichier) | `SaveImage` (téléchargement) |
| Double-clic sur le fond : `PaneClick`, début du zoom, `PaneClick` | `PaneClick`, `PaneClick`, puis le zoom (le navigateur envoie son `dblclick` après le second clic) |
| `BeforeDelete` (évènement) | `OnBeforeDelete` (fonction qui répond) |
| Glisser le fond efface la sélection à l'appui | Glisser le fond la garde ; un clic l'efface |
| Presse-papier commun à toutes les vues de l'application | Presse-papier propre à chaque vue |

- **Édition à la souris** : mêmes gestes ; le pont JS (`wpflow.js`) mesure les nodes et les handles et suit le pointeur pendant un geste.
- **Export d'image** : `SaveImage` capture le rendu du navigateur (le calque est cloné avec ses styles calculés puis rastérisé) ; le contenu non sérialisable en SVG (canvas, vidéo, images d'une autre origine) n'y apparaît pas. `vector: true` ou `ExportSvg()` donnent un dessin simplifié (rectangle + `Node.Data`).
- **Blazor Server ou WebAssembly** : la bibliothèque fonctionne avec les deux (elle dépend de `Microsoft.AspNetCore.Components.Web`). En Server, chaque déplacement de pointeur pendant un geste est un aller-retour réseau ; WebAssembly exécute tout dans le navigateur. La démo existe pour les deux (`demo-blazor`, `demo-blazor-wasm`).
- **Virtualisation** : avec `OnlyRenderVisibleElements`, les nodes hors de la vue ne sont pas rendus. Avec `FitViewOnInit` (vrai par défaut), tout est d'abord rendu et mesuré, le temps du cadrage initial. Sans lui, un node jamais approché n'est jamais mesuré, comme en WPF : `FitView()` et la minimap l'ignorent, et `OnNodesInitialized` attend qu'il le soit. Avec ou sans virtualisation, un pas de pan ne refait ni les nodes ni les edges (couche mémorisée, même avec `@bind-Viewport`) tant que les éléments visibles restent les mêmes ; pendant un mouvement de vue, un modèle d'edge ou de label qui lit l'état du parent n'est donc pas redessiné (même règle que pour les modèles de nodes ci-dessous). Si presque tout est visible à la fois, la virtualisation n'apporte rien. Un node ne se rend que s'il a changé et le tracé d'un edge est mis en cache : un graphe de 1 500 nodes reste fluide.
- **Modèles de nodes mémorisés** : un modèle (`NodeTemplate`) ne se rend que quand son node change, comme un composant React mémorisé ; un modèle qui lit l'état du parent doit recevoir cet état par le `Node` (`Node.Data`) ou être redéclaré (un délégué neuf redessine). Un état propre à l'affichage d'un node (champ en cours de renommage, case cochée) va dans un **composant enfant** rendu par le modèle, qui se redessine lui-même ; les handles qu'il déplace sont remesurés.
- **Champs dans un node** : les touches tapées dans un champ ne sont pas traitées par le graphe (Suppr, flèches, Ctrl+A, Ctrl+Z restent au champ), et vos `@onkeydown` les reçoivent. Ajoutez `class="nodrag"` au champ.
- **Conteneurs** : la vue peut être sous un parent en `transform: scale()`, dans une page ou un conteneur défilé, ou dans un onglet masqué puis affiché ; un parent tourné ou incliné (`rotate`, `skew`) n'est pas pris en compte.
- **Charger un autre document** : appelez `ClearHistory()` après avoir remplacé `Nodes` et `Edges`, sinon « annuler » ramène le document précédent.
- **Blazor Server et gros graphes** : les mesures sont découpées en messages de moins de 32 Ko, la limite par défaut de SignalR n'a pas à être augmentée.
