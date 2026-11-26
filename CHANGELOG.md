# Changelog

Les trois paquets (`Wpflow.Core`, `Wpflow.Wpf`, `Wpflow.Blazor`) partagent le même numéro. Au `dotnet pack`, la validation de paquet compare l'API à la version publiée (`PackageValidationBaselineVersion` dans `Directory.Build.props`) : une API retirée fait échouer la construction, sauf si la rupture est déclarée dans `CompatibilitySuppressions.xml`.

## Non publié

### Ajouts

- Les trois paquets ont une icône (`PackageIcon`, source `assets/logo.svg`).
- `GraphUtils.GetHandleSide(node, position)` : le côté vers lequel regarde un handle une fois le node tourné.

### Correctifs (nodes tournés)

- WPF et Blazor : les edges ne suivaient pas un node tourné (`Node.Rotation`). Leur extrémité, la direction d'où ils partent, le fil d'une connexion en cours et la recherche du handle visé tiennent désormais compte de la rotation. `GraphUtils.GetHandlePosition` et `GetHandleCenter` rendent le point tel qu'il est affiché ; les `HandleInfo` restent exprimés dans le node non tourné.
- Blazor : les handles d'un node tourné étaient mesurés d'après son rectangle englobant, donc à une place fausse.

### Correctifs (conditions d'intégration hostiles)

- Blazor : sous un parent en `transform: scale()`, les clics, le glisser, le pan, le pincement, la minimap et le cadrage visaient à côté. Le pont JS donne désormais la taille de mise en page de la vue et l'échelle de ses parents (`OnRootScale`).
- Blazor : après un contenu inséré au-dessus du graphe (sans changement de taille ni défilement), un dépôt ou un survol tombait à côté. La position est relue à l'entrée du pointeur et pendant un glisser-déposer.
- Blazor : un CSS global sur `svg` (`max-width: 100%`, taille d'icône) effaçait les edges ou réduisait la minimap ; un `min-width` sur `button` élargissait les contrôles.
- Blazor : après la suppression du node qui avait le focus, le clavier (Ctrl+Z) ne parvenait plus à la vue.
- Blazor : sur une page chargée, ou pour un geste très bref, le dernier mouvement avant le relâchement était perdu : node ou vue arrêtés en retrait de la souris, connexion non faite.
- Blazor Server : un bouton relâché pendant une coupure du circuit n'arrivait jamais ; le node restait en cours de glisser après la reprise. Le pont JS redemande la fin du geste jusqu'à la réponse du serveur (`OnGestureLost`).
- WPF : une exception levée par un gestionnaire pendant une connexion laissait la souris capturée par la vue, qui ne répondait plus au clic suivant.
- WPF : une vue fermée restait en mémoire tant que vivaient ses ViewModels (`NodesSource`, `EdgesSource`), sa collection `SelectedNodes` / `SelectedEdges` ou son `History`. Ces abonnements sont désormais faibles.

### Changements de comportement

- `NodeSourceAdapter` et `EdgeSourceAdapter` : la source ne retient plus l'adaptateur. Les vues le gardent elles-mêmes ; un code qui crée un adaptateur directement doit en garder une référence tant qu'il doit suivre la source.

## 1.1.0

### Ruptures

> Cette version mineure contient une rupture d'API, choisie à la place d'une 2.0.0.

- `EdgeGeometry.Options` est retiré, ainsi que le cinquième paramètre correspondant du constructeur et de `Deconstruct`. Un fournisseur de tracé personnalisé (WPF ou Blazor) qui le lisait lit désormais `geometry.Edge?.PathOptions`.

### Changements de comportement

Les deux vues se comportent désormais de la même façon, WPF servant de référence.

- Blazor : un double-clic sur un node ou un edge donne clic, double-clic, clic (auparavant clic, clic, double-clic). Les deux derniers évènements arrivent dans le même traitement : un gestionnaire de clic qui efface ce qu'a fait le double-clic le fait avant tout rendu.
- Blazor : un clic droit sur une sélection lève `OnSelectionContextMenu`, puis `OnNodeContextMenu` pour le node visé (auparavant le premier seulement).
- Blazor : `SelectAll()` et Ctrl+A sélectionnent aussi les edges, et les nodes masqués comme en WPF.
- Blazor : en mode contrôlé (`AutoApplyChanges="false"`), la vue ne tient plus d'historique.
- WPF : en mode contrôlé, `CanUndo` reste faux (`Undo` n'y faisait déjà rien).
- WPF, MVVM : annuler et rétablir réécrivent dans les ViewModels toutes les propriétés mappées, plus seulement la position, la sélection, la taille et les extrémités. Un changement que votre code a fait depuis le geste n'est pas défait.
- Blazor : `NodeDragThreshold`, `ConnectionDragThreshold` et `PaneDragThreshold` valent 1 px par défaut, comme en WPF (3 auparavant), et `ReconnectRadius` 24 px (10).
- WPF et Blazor : un appui reste un clic tant que le pointeur ne s'éloigne pas de 4 px, ou du seuil de glisser s'il est plus grand. La plus grande distance parcourue compte : un glisser revenu à son départ n'est pas un clic.
- WPF et Blazor : la reconnexion ne commence qu'au premier mouvement. Un clic près de l'extrémité d'un edge est un clic sur l'edge.
- WPF : tirer un handle d'où aucune connexion ne peut partir (vue verrouillée, node ou handle non connectable) ne déplace plus ni le node ni la vue.
- WPF : avec `SelectNodesOnDrag` faux, un node glissé reste non sélectionné, comme le dit la documentation (il était sélectionné au début du glisser).
- Blazor : un clic sur un node d'une sélection multiple la garde ; Ctrl + clic sur un edge sélectionné ne le désélectionne plus.
- Blazor : un appui sur un contrôle HTML d'un node (`button`, `input`…) ne sélectionne plus le node.
- Blazor : un node verrouillé, non sélectionnable ou saisi hors de sa zone `DragHandle` laisse le glisser déplacer la vue.
- Blazor : `Node.ExpandParent` agit, comme en WPF (il était ignoré). Glisser un tel enfant au-delà de son parent agrandit celui-ci ; à gauche et en haut, le parent recule et ses autres enfants restent en place à l'écran.
- Blazor : quand les deux extrémités d'un edge sont à portée (edge court), l'appui reconnecte la plus proche, la cible à égale distance (il reconnectait toujours la source).
- Blazor : la molette sur la minimap (`Zoomable`) zoome deux fois plus à chaque cran. Avec `ZoomStep` à 10, un cran zoome comme sur la surface, comme en WPF. Pour retrouver l'ancienne sensibilité : `ZoomStep="5"`. Les défilements exprimés en lignes sont aussi convertis en pixels, comme sur la surface.
- Blazor : avec `ElementsSelectable` faux, `SelectOnly` ne fait plus que désélectionner, comme en WPF.

### Ajouts

- **MVVM en Blazor** : `NodesSource`, `EdgesSource`, `NodeMapping`, `EdgeMapping`, `NodeFactory`, `EdgeFactory`, sélection liée (`SelectedNodeItems`, `SelectedEdgeItems`, `SelectedNode`), `Controller` / `ControllerChanged`, `History`, `NodeModel` / `EdgeModel`, `ToJson(sélecteur)`. Les `EventCallback` tiennent lieu de commandes.
- La logique MVVM est dans le noyau (`NodeSourceAdapter`, `EdgeSourceAdapter`, `NodeMappingBase`, `EdgeMappingBase`) ; `NodeMapping` et `EdgeMapping` en dérivent, sans changement pour le code existant.
- MVVM : toutes les propriétés de `Node` et d'`Edge` se mappent (rotation, z-index, style, options de tracé et de marqueurs, positions des handles, étendue, origine…).
- WPF : une commande pour chaque évènement qui n'en avait pas (survol, glisser, rectangle de sélection, reconnexion, déplacement de la vue, molette, initialisation, erreurs, changes).
- WPF : évènements `Deleted`, `SelectionChanged` et `NodesReady` (avec leurs commandes) ; réglages `AutoConnect`, `HistoryEnabled`, `HistoryLimit`.
- Blazor : `OnNodeMouseMove`, `OnEdgeMouseMove`, `OnPaneScroll`, `OnStateChange` ; réglages `AutoPanOnNodeFocus`, `EdgeHitWidth` ; `NodesInitialized`, `CanPaste`, `IsViewportAnimating`, `IsConnecting`, `Zoom`, `AbsoluteNodes`.
- WPF : `ExportSvg()`, avec le même dessin qu'en Blazor (`SvgExport` dans le noyau).
- `EdgeGeometry.Edge` : les fournisseurs de tracé reçoivent l'edge tracé (WPF et Blazor).
- Blazor : `NodeEventArgs.TargetData`, les attributs `data-*` de l'élément cliqué dans un node.
- WPF : savoir quelle partie d'un node a été cliquée, double-cliquée ou visée par le menu contextuel. La propriété attachée `FlowView.TargetData` marque un élément d'un modèle ; `ElementEventArgs.TargetData` donne la marque la plus proche et `ElementEventArgs.OriginalSource` l'élément pressé.
- WPF : des méthodes publiques en Blazor le deviennent sur `FlowView` : `ApplyNodeChanges`, `ApplyEdgeChanges`, `ClearHistory`, `ClearSelection`, `SelectAll`, `SelectOnly`, `DeleteSelected`, `CenterOn(XYPosition)`, `FlowToScreen`, `GetIncomers`, `GetOutgoers`, `SetLocked`. Elles sont synchrones, sauf `DeleteSelected`.
- WPF, MVVM : `EdgeMapping.DataPath`, `MarkerStartPath` et `MarkerEndPath`. Le tracé et les marqueurs d'un edge peuvent dépendre de son ViewModel.
- WPF, MVVM : `FlowView.SelectionContextMenuCommand`, la commande qui manquait à côté de l'évènement `SelectionContextMenuRequested`.
- WPF : `FlowView.UpdateNodeInternals(id)` remesure un node dont les handles ont été déplacés sans changement de taille (la vue ne le détecte pas seule ; en Blazor c'est automatique).
- WPF : `FlowView.CommandsReceiveEventArgs` (faux par défaut) fait recevoir l'`ElementEventArgs` aux commandes de clic, de double-clic et de menu contextuel.

### Corrections

- Blazor : le double-clic sur un node n'arrivait jamais et zoomait le fond.
- Blazor : la molette sur une minimap `Zoomable` n'empêchait pas la page de défiler, et le navigateur écrivait une erreur dans la console (`preventDefault` dans un écouteur passif).
- Blazor Server : le circuit SignalR tombait au premier affichage dès quelques dizaines de nodes riches (ou une seule table de 150 colonnes), les mesures partant en un seul message de plus de 32 Ko. Elles sont envoyées en plusieurs messages.
- Blazor : les edges ne suivaient pas des handles déplacés, ajoutés ou retirés sans changement de taille du node (colonnes réordonnées). Le contenu des nodes est observé et remesuré.
- Blazor : aucun `@onkeydown` posé sur un champ dans un node ne se déclenchait (Entrée, Échap…), le pont JS arrêtant la propagation des touches.
- Blazor : un clic droit ou un clic du milieu vidait la sélection et levait `OnPaneClick`, même sur un node ; `OnSelectionContextMenu` n'était donc jamais levé après un clic droit réel.
- Mode contrôlé : saisir un node non sélectionné déplaçait à sa place l'ancienne sélection, tant que la nouvelle n'était pas appliquée (`DragSelection.CollectStart`, WPF et Blazor ; nouvelle surcharge avec `additive`).
- WPF : `RenderToBitmap` / `SaveImage` prenaient l'image avec le viewport courant au lieu du graphe entier. L'image était décalée, tronquée ou entièrement blanche.
- WPF : quand la vue perdait la capture de la souris en plein geste (boîte de dialogue, autre application), `NodeDragStopped`, `ViewportMoveEnded`, `SelectionEnded`, `ConnectEnded` et `ReconnectEnded` n'étaient jamais levés, et les nodes restaient marqués `Dragging`. Le geste se termine désormais comme à un relâchement dans le vide.
- WPF : un gestionnaire de double-clic qui ouvrait une boîte de dialogue laissait la vue capturer la souris, et le node la suivait bouton relâché.
- WPF : un clic légèrement tremblé était perdu quand un seuil de glisser dépassait 4 px.
- WPF : le second clic d'une connexion par deux clics levait aussi `NodeClick`.
- WPF et Blazor : un simple clic à moins de `ReconnectRadius` d'une extrémité démarrait une reconnexion ; en WPF, `EdgeClick` et le double-clic étaient avalés.
- Blazor : `OnDrop` ne fonctionnait pas dans un navigateur (`dragover` n'était pas annulé).
- Blazor, mode contrôlé : `OnSelectionChange` n'était jamais levé.
- Blazor : un relâchement perdu (boîte de dialogue, autre application) laissait le geste suivre la souris ; une connexion ou une reconnexion abandonnée ne levait pas `OnConnectEnd` / `OnReconnectEnd`.
- Blazor : la reconnexion ne levait ni `OnConnectStart` ni `OnConnectEnd` (`ConnectStartEventArgs.IsReconnection` la distingue).
- Blazor : un clic sur la minimap, une barre d'outils ou le second clic d'une connexion levait aussi `OnPaneClick` et vidait la sélection ; avec `OnNodeClick`, un clic sur un node de la minimap ne déplaçait plus la vue.
- Blazor : un double-clic sur un panneau zoomait le graphe, et sur un bouton d'un node levait `OnNodeDoubleClick`.
- Blazor : un glisser au bouton droit qui déplaçait la vue ouvrait ensuite le menu contextuel.
- Blazor : `OnPaneMouseEnter`, `OnPaneMouseMove` et `OnPaneMouseLeave` ne tenaient pas compte des nodes et des edges survolés ; comme en WPF, le fond est ce qui n'est ni l'un ni l'autre.
- Blazor : après un appui sur une barre d'outils, l'appui suivant pouvait être ignoré.
- WPF, MVVM : quand un ViewModel ajustait ou refusait une valeur écrite par la vue (setter qui borne, élément verrouillé), la vue gardait la sienne. Un ViewModel qui lève `PropertyChanged` pendant l'écriture est désormais relu.
- WPF, MVVM : `UpdateNode` et `UpdateNodeData` n'écrivaient rien dans le ViewModel ; la vue et le ViewModel divergeaient jusqu'au changement suivant du ViewModel, qui annulait la mise à jour.
- WPF, MVVM : `UpdateEdge` et `UpdateEdgeData` n'écrivaient que les extrémités ; le label, le type, l'animation, la sélection et les autres propriétés mappées sont désormais écrits aussi.
- WPF, MVVM : annuler le premier redimensionnement d'un node laissait sa taille dans le ViewModel.
- WPF : `UpdateNode` ne faisait rien, sans le signaler, tant que le gabarit de la vue n'était pas appliqué (avant le premier affichage).
- Blazor : `ExportSvg` écrivait telle quelle la couleur propre d'un edge ; elle est échappée.

### Performances

Sans changement d'API ni de comportement. Mesures sur 1 500 nodes (Blazor) et 2 000 nodes (WPF, noyau), Release.

- Blazor : un pas de pan alloue 2 Ko au lieu de 284 Ko (0,32 → 0,07 ms) ; déplacer un node alloue 2,2 Mo au lieu de 4,2 Mo (11 → 7 ms ; avec des groupes 13 → 8 ms).
- WPF : déplacer un node dans un graphe avec des groupes alloue 0,7 Mo au lieu de 2,1 Mo (2,7 → 2,0 ms) ; seules les copies en coordonnées absolues des nodes qui ont bougé sont refaites. Avec une grosse sélection, plus de chaîne reconstruite à chaque image.
- WPF et Blazor : tirer un fil ne construit plus la liste des handles candidats à chaque mouvement (331 Ko → 0,1 Ko, 0,36 → 0,10 ms avec 4 000 handles).

### Tests et publication

- La publication lance les tests de bout en bout Blazor et les auto-tests WPF à la vraie souris avant le `pack`.
- Les auto-tests WPF rendent un code de sortie en échec (ligne `ECHEC`, exception ou délai dépassé) ; `tests/run-wpf-selftests.ps1` les enchaîne.
- `tests/run-wpf-selftests-sandbox.ps1` enchaîne les mêmes auto-tests dans le Bac à sable Windows, hors écran : le poste reste utilisable.
- `tests/consumers/run.ps1` installe les paquets dans des applications neuves, hors de la solution.
- Tests de bout en bout : `WPFLOW_BROWSER_CHANNEL` choisit le navigateur (par exemple `msedge`) ; `WPFLOW_SERVER_DEMO_TFM=net10.0` permet de lancer la démo Server sur un poste sans runtime .NET 8.

## 1.0.1

- Première version de `Wpflow.Blazor` (Server et WebAssembly).

## 1.0.0

- Première version de `Wpflow.Core` et `Wpflow.Wpf`.
