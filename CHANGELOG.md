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
- WPF : `FlowView.CommandsReceiveEventArgs` (faux par défaut) fait recevoir l'`ElementEventArgs` aux commandes de clic, de double-clic et de menu contextuel.

### Corrections

- Blazor : le double-clic sur un node n'arrivait jamais et zoomait le fond.
- WPF : `RenderToBitmap` / `SaveImage` prenaient l'image avec le viewport courant au lieu du graphe entier. L'image était décalée, tronquée ou entièrement blanche.
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
