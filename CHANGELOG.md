# Changelog

Les trois paquets (`Wpflow.Core`, `Wpflow.Wpf`, `Wpflow.Blazor`) partagent le même numéro. Au `dotnet pack`, la validation de paquet compare l'API à la version publiée (`PackageValidationBaselineVersion` dans `Directory.Build.props`) : une API retirée fait échouer la construction, sauf si la rupture est déclarée dans `CompatibilitySuppressions.xml`.

## 1.1.0 — non publiée

### Ruptures

> Cette version mineure contient une rupture d'API, choisie à la place d'une 2.0.0.

- `EdgeGeometry.Options` est retiré, ainsi que le cinquième paramètre correspondant du constructeur et de `Deconstruct`. Un fournisseur de tracé personnalisé (WPF ou Blazor) qui le lisait lit désormais `geometry.Edge?.PathOptions`.

### Ajouts

- `EdgeGeometry.Edge` : les fournisseurs de tracé reçoivent l'edge tracé (WPF et Blazor).
- Blazor : `NodeEventArgs.TargetData`, les attributs `data-*` de l'élément cliqué dans un node.
- WPF : savoir quelle partie d'un node a été cliquée, double-cliquée ou visée par le menu contextuel. La propriété attachée `FlowView.TargetData` marque un élément d'un modèle ; `ElementEventArgs.TargetData` donne la marque la plus proche et `ElementEventArgs.OriginalSource` l'élément pressé.
- WPF : des méthodes publiques en Blazor le deviennent sur `FlowView` : `ApplyNodeChanges`, `ApplyEdgeChanges`, `ClearHistory`, `ClearSelection`, `SelectAll`, `SelectOnly`, `DeleteSelected`, `CenterOn(XYPosition)`, `FlowToScreen`, `GetIncomers`, `GetOutgoers`, `SetLocked`. Elles sont synchrones, sauf `DeleteSelected`. En WPF, `SelectAll` sélectionne aussi les edges (en Blazor, les nodes seuls).
- WPF, MVVM : `EdgeMapping.DataPath`, `MarkerStartPath` et `MarkerEndPath`. Le tracé et les marqueurs d'un edge peuvent dépendre de son ViewModel.
- WPF, MVVM : `FlowView.SelectionContextMenuCommand`, la commande qui manquait à côté de l'évènement `SelectionContextMenuRequested`.
- WPF : `FlowView.UpdateNodeInternals(id)` remesure un node dont les handles ont été déplacés sans changement de taille (la vue ne le détecte pas seule ; en Blazor c'est automatique).
- WPF : `FlowView.CommandsReceiveEventArgs` (faux par défaut) fait recevoir l'`ElementEventArgs` aux commandes de clic, de double-clic et de menu contextuel.

### Corrections

- Blazor : le double-clic sur un node n'arrivait jamais et zoomait le fond.
- Blazor Server : le circuit SignalR tombait au premier affichage dès quelques dizaines de nodes riches (ou une seule table de 150 colonnes), les mesures partant en un seul message de plus de 32 Ko. Elles sont envoyées en plusieurs messages.
- Blazor : les edges ne suivaient pas des handles déplacés, ajoutés ou retirés sans changement de taille du node (colonnes réordonnées). Le contenu des nodes est observé et remesuré.
- Blazor : aucun `@onkeydown` posé sur un champ dans un node ne se déclenchait (Entrée, Échap…), le pont JS arrêtant la propagation des touches.
- Blazor : un clic droit ou un clic du milieu vidait la sélection et levait `OnPaneClick`, même sur un node ; `OnSelectionContextMenu` n'était donc jamais levé après un clic droit réel.
- Mode contrôlé : saisir un node non sélectionné déplaçait à sa place l'ancienne sélection, tant que la nouvelle n'était pas appliquée (`DragSelection.CollectStart`, WPF et Blazor ; nouvelle surcharge avec `additive`).
- WPF : `RenderToBitmap` / `SaveImage` prenaient l'image avec le viewport courant au lieu du graphe entier. L'image était décalée, tronquée ou entièrement blanche.
- WPF, MVVM : quand un ViewModel ajustait ou refusait une valeur écrite par la vue (setter qui borne, élément verrouillé), la vue gardait la sienne. Un ViewModel qui lève `PropertyChanged` pendant l'écriture est désormais relu.
- WPF, MVVM : `UpdateNode` et `UpdateNodeData` n'écrivaient rien dans le ViewModel ; la vue et le ViewModel divergeaient jusqu'au changement suivant du ViewModel, qui annulait la mise à jour.
- WPF, MVVM : `UpdateEdge` et `UpdateEdgeData` n'écrivaient que les extrémités ; le label, le type, l'animation, la sélection et les autres propriétés mappées sont désormais écrits aussi.

### Tests et publication

- La publication lance les tests de bout en bout Blazor et les auto-tests WPF à la vraie souris avant le `pack`.
- Les auto-tests WPF rendent un code de sortie en échec (ligne `ECHEC`, exception ou délai dépassé) ; `tests/run-wpf-selftests.ps1` les enchaîne.
- `tests/consumers/run.ps1` installe les paquets dans des applications neuves, hors de la solution.
- Tests de bout en bout : `WPFLOW_BROWSER_CHANNEL` choisit le navigateur (par exemple `msedge`) ; `WPFLOW_SERVER_DEMO_TFM=net10.0` permet de lancer la démo Server sur un poste sans runtime .NET 8.

## 1.0.1

- Première version de `Wpflow.Blazor` (Server et WebAssembly).

## 1.0.0

- Première version de `Wpflow.Core` et `Wpflow.Wpf`.
