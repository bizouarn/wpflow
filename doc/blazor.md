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

## État : les changes

Comme dans React Flow, une interaction ne modifie jamais les listes : elle produit des `NodeChange` / `EdgeChange`, publiés par `OnNodesChange` / `OnEdgesChange` puis appliqués (`AutoApplyChanges`, vrai par défaut) avec `Changes.Apply`. Avec `AutoApplyChanges="false"` (mode contrôlé), vous appliquez les changes vous-même. `ApplyNodeChanges` / `ApplyEdgeChanges` permettent d'en injecter.

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

## FlowView

### Données et état

| Paramètre | Défaut | Description |
|---|---|---|
| `Nodes`, `Edges`, `Viewport` (+ `…Changed`) | | Données et vue, liables. |
| `AutoApplyChanges` | vrai | Faux : mode contrôlé. |
| `IsLocked` (+ `IsLockedChanged`) | faux | Plus de glisser, connexion ni sélection (cadenas des contrôles). |
| `HistoryEnabled`, `HistoryLimit` | vrai, 100 | Annuler / rétablir (Ctrl+Z, Ctrl+Y). |
| `Debug` | faux | Journalise les changes dans la console. |

### Viewport

`MinZoom` (0,5), `MaxZoom` (2), `FitViewOnInit`, `FitViewPadding`, `FitViewOptions`, `TranslateExtent`, `NodeExtent`, `NodeOrigin`, `SnapGrid` (pas d'aimantation, `null` = aucune), `OnlyRenderVisibleElements` (virtualisation : nodes et edges hors de la vue ne sont pas rendus ; après le premier cadrage).

### Interactions

`PanOnDrag`, `PanOnDragButtons` (`[0, 1]`), `PanOnScroll`, `PanOnScrollSpeed`, `PanOnScrollMode`, `ZoomOnScroll`, `ZoomOnPinch`, `ZoomOnDoubleClick`, `PreventScrolling`, `NodesDraggable`, `NodesConnectable`, `ElementsSelectable`, `EdgesReconnectable`, `SelectNodesOnDrag`, `SelectionOnDrag`, `SelectionMode` (`Partial` / `Full`), `SelectionKey` (`Shift`), `MultiSelectionKey` (`Control`), `PanActivationKey` (`Space`), `ZoomActivationKey` (`Control`), `DeleteKeys`, `DeleteKeyEnabled`, `NodeDragThreshold`, `ConnectionDragThreshold`, `PaneDragThreshold`, `AutoPanOnNodeDrag`, `AutoPanOnConnect`, `AutoPanOnSelection`, `AutoPanSpeed`, `ElevateNodesOnSelect`, `ElevateEdgesOnSelect`, `ZIndexMode`.

Clavier : Suppr / Retour arrière supprime, Ctrl+A tout sélectionner, Ctrl+C / X / V presse-papier (copies décalées de 20 px), Ctrl+Z / Ctrl+Y historique, flèches déplacent la sélection (Maj : ×4, `SnapGrid` sinon 5 px), Échap efface la sélection ou annule une connexion par clics, Entrée / Espace sélectionne le node ayant le focus, Espace + glisser fait défiler la vue. Tactile : pincement à deux doigts (non vérifié sur matériel).

### Connexions

`ConnectionMode` (`Strict` / `Loose`), `IsValidConnection`, `ConnectionRadius` (20 px), `ReconnectRadius` (10 px), `ConnectOnClick` (clic sur un handle puis sur un autre), `ConnectionLineType`, `ConnectionLineTemplate` (reçoit l'état de la connexion, avec le tracé du fil dans `Path`), `AutoConnect` (ajoute l'edge), `DefaultEdgeType`, `DefaultEdgeOptions` (`edge => edge with { Animated = true }`). Reconnexion : tirez l'extrémité d'un edge vers un autre handle (relâcher dans le vide la laisse en place). `FlowHandle` : `Type`, `Position`, `HandleId`, `Offset`, `IsConnectable`, `IsConnectableStart`, `IsConnectableEnd`, `IsValidConnection`, `OnConnected`. `CurrentConnection` expose la connexion en cours (`useConnection`).

### Rendu

`ShowBackground`, `BackgroundVariant` (`Dots`, `Lines`, `Cross`), `BackgroundGap`, `BackgroundSize`, `BackgroundLineWidth`, `BackgroundColor`, `BackgroundOffset`, `AdditionalBackgrounds` (liste de `BackgroundLayer`), `Dark` (ou les variables CSS `--wf-*`), `DefaultMarkerColor`, `NodeTemplate(s)`, `EdgeTypes` (un `IEdgePathProvider` par type), `EdgeTemplates` (dessin complet d'un type d'edge), `EdgeLabelTemplate`, `MarkerTemplates` (marqueur SVG personnalisé, `Edge.MarkerEndKey`).

### Accessibilité

`NodesFocusable`, `EdgesFocusable`, `DisableKeyboardA11y`, `AnnounceSelection` (région `aria-live`), `NodeAriaLabelProvider`, `EdgeAriaLabelProvider`, rôles et `aria-selected` sur nodes et edges.

### Évènements

`OnNodesChange`, `OnEdgesChange`, `OnConnect`, `OnConnectStart`, `OnConnectEnd`, `OnClickConnectStart`, `OnClickConnectEnd`, `OnReconnectStart`, `OnReconnect`, `OnReconnectEnd`, `OnNodeClick`, `OnNodeDoubleClick`, `OnNodeContextMenu`, `OnNodeMouseEnter`, `OnNodeMouseLeave`, `OnEdgeClick`, `OnEdgeDoubleClick`, `OnEdgeContextMenu`, `OnEdgeMouseEnter`, `OnEdgeMouseLeave`, `OnPaneClick`, `OnPaneContextMenu`, `OnPaneMouseEnter`, `OnPaneMouseLeave`, `OnPaneMouseMove`, `OnSelectionContextMenu`, `OnNodeDragStart`, `OnNodeDrag`, `OnNodeDragStop`, `OnSelectionDragStart`, `OnSelectionDrag`, `OnSelectionDragStop`, `OnSelectionStart`, `OnSelectionEnd`, `OnSelectionChange`, `OnMoveStart`, `OnMove`, `OnMoveEnd`, `OnInit`, `OnNodesInitialized`, `OnError`, `OnDrop` (glisser-déposer depuis une palette, position en coordonnées flow), `OnBeforeDelete` (renvoyer faux annule), `OnDelete`. Les écouteurs de survol et de menu contextuel ne sont attachés que si un gestionnaire est fourni.

### Méthodes (`@ref`)

Viewport : `SetViewport(viewport, duration)`, `SetCenter`, `FitBounds`, `FitView(options)`, `ZoomTo`, `ZoomIn`, `ZoomOut`, `ZoomBy` (toutes animables avec une `duration`). Requêtes : `GetNode`, `GetEdge`, `GetInternalNode`, `GetIntersectingNodes`, `IsNodeIntersecting`, `GetNodeConnections`, `GetIncomers`, `GetOutgoers`, `GetNodesBounds`, `SelectedNodes`, `SelectedEdges`. Édition : `AddNodes`, `AddEdges`, `UpdateNode`, `UpdateNodeData`, `UpdateEdge`, `UpdateEdgeData`, `DeleteElements`, `DeleteSelected`, `SelectAll`, `ClearSelection`, `Copy`, `Cut`, `Paste`, `Undo`, `Redo`, `CanUndo`, `CanRedo`, `ClearHistory`, `SetLocked`. Conversion : `ScreenToFlow`, `FlowToScreen`, `ClientToFlow`. Sauvegarde : `ToJson()` / `LoadJson(json)` (format React Flow, voir [Sauvegarde et JSON](learn/12-serialization.md)), `SaveImage("graphe.png")` (télécharge le rendu réel en PNG ou SVG : nodes à contenu personnalisé, styles et thème ; `vector: true` pour le dessin simplifié), `ExportSvg()` (le dessin simplifié : rectangles et texte).

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

## Différences avec la version WPF

- **MVVM** : pas d'adaptateur `NodesSource` / `EdgesSource` ni de commandes : en Blazor, on lie `@bind-Nodes` / `@bind-Edges` et on réagit aux `EventCallback`. Les ViewModels se projettent vers les `record` dans votre code.
- **Édition à la souris** : mêmes gestes ; le pont JS (`wpflow.js`) mesure les nodes et les handles et suit le pointeur pendant un geste.
- **Export d'image** : `SaveImage` capture le rendu du navigateur (le calque est cloné avec ses styles calculés puis rastérisé) ; le contenu non sérialisable en SVG (canvas, vidéo, images d'une autre origine) n'y apparaît pas. `vector: true` ou `ExportSvg()` donnent un dessin simplifié (rectangle + `Node.Data`).
- **Blazor Server ou WebAssembly** : la bibliothèque fonctionne avec les deux (elle dépend de `Microsoft.AspNetCore.Components.Web`). En Server, chaque déplacement de pointeur pendant un geste est un aller-retour réseau ; WebAssembly exécute tout dans le navigateur. La démo existe pour les deux (`demo-blazor`, `demo-blazor-wasm`).
- **Virtualisation** : avec `OnlyRenderVisibleElements`, les nodes jamais mesurés et hors de la vue ne sont pas créés ; le cadrage initial se fait avant. Sans elle, un pas de pan ne refait ni les nodes ni les edges (couche mémorisée), un node ne se rend que s'il a changé et le tracé d'un edge est mis en cache : un graphe de 1 500 nodes reste fluide.
- **Modèles de nodes mémorisés** : un modèle (`NodeTemplate`) ne se rend que quand son node change, comme un composant React mémorisé ; un modèle qui lit l'état du parent doit recevoir cet état par le `Node` (`Node.Data`) ou être redéclaré (un délégué neuf redessine).
