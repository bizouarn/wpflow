# Roadmap 2 — Extensibilité et compatibilité MVVM

Suite de [ROADMAP.md](ROADMAP.md) (toutes les phases y sont terminées). Deux objectifs :

1. **Tout ce qui s'affiche doit pouvoir être remplacé depuis l'extérieur de la lib** (templates, styles, ressources), sans toucher à `Wpflow.Wpf`.
2. **Utilisation naturelle en MVVM** : binding, `INotifyPropertyChanged`, `ICommand`, aucun code-behind obligatoire.

> **Principe directeur.** Le noyau (`Wpflow.Core`) reste immuable et sans UI : c'est lui qui rend les tests simples. Le MVVM s'ajoute **au-dessus** (couche d'adaptation dans `Wpflow.Wpf`, ou nouveau projet `Wpflow.Mvvm`) et ne remet pas en cause le modèle « changes » : un ViewModel observable est *projeté* vers des `Node`/`Edge` immuables, et les `NodeChange` sont *rétro-projetés* vers le ViewModel.

## État des lieux

| Besoin | Aujourd'hui | Écart |
|---|---|---|
| Template de node | `DataTemplate` `wpflow-node-{type}` | OK. À vérifier : redéfinir `default`/`input`/`output`/`group` |
| Template de label d'edge | `EdgeLabelTemplate` (global) | Pas par type d'edge |
| Types d'edges | 4 intégrés | **Pas d'edge personnalisé** |
| Marqueurs | `Arrow`, `ArrowClosed` | **Pas de marqueur personnalisé** |
| Handle | Contrôle auto-dessiné, couleur par thème | **Pas de `ControlTemplate`**, forme fixe |
| Contour de sélection / thème | Clés `Flow.*` | OK pour les couleurs, pas pour la forme |
| Binding `Nodes`/`Edges` | DP sur listes de `record` | Liste *remplacée* à chaque changement : pas de `ObservableCollection`, pas de ViewModel de node |
| Sélection | Champ `Selected` des records | Pas de `SelectedNodes` bindable |
| Actions | Méthodes (`FitView`, `Undo`…) et événements | **Aucune `ICommand`**, pas de commande de connexion/suppression |
| Contrôles annexes | `MiniMap`, `FlowControls` | Pas de `Flow="{Binding …}"` implicite, pas de commandes |

## Phase A — Extensibilité visuelle

Chaque point : API publique, doc dans `doc/`, exemple dans la démo, test (auto-test visuel ou test du noyau).

- [x] **A1. Vérifier et documenter la redéfinition des nodes intégrés** (`wpflow-node-default|input|output|group`) ; ajouter un test.
- [x] **A2. `Handle` templatable** : `Handle` reste auto-dessiné par défaut ; propriété `HandleTemplate` (`DataTemplate`/`ControlTemplate`, clé `wpflow-handle`) optionnelle, utilisée seulement si définie (aucun coût sinon). La zone de clic et la mesure restent celles du `Handle`.
- [x] **A3. Types d'edges personnalisés** (équivalent de `edgeTypes`) :
  - [x] noyau : interface `IEdgePathProvider` (`EdgePath GetPath(EdgeGeometry)`) avec les 4 types actuels comme implémentations, + registre `EdgePathRegistry` ;
  - [x] WPF : propriété `FlowView.EdgeTypes` (dictionnaire `string → IEdgePathProvider`) **et** clé de ressource `wpflow-edge-{type}` pour fournir le *rendu* (un `DataTemplate` dessiné dans `EdgeVisual`, DataContext = `EdgeRenderContext` avec chemin, extrémités, état sélectionné/animé) ;
  - [x] `EdgeVisual` garde le chemin rapide (rendu direct) pour les types intégrés.
- [x] **A4. Marqueurs personnalisés** : `EdgeMarker` devient une valeur (`Kind` + `Key` optionnelle) ; géométrie fournie par `FlowView.Markers` ou clé `wpflow-marker-{key}`.
- [x] **A5. Label d'edge par type** (`EdgeLabelTemplates` par clé) en plus du template global.
- [x] **A6. Styles exposés** : `NodeStyle` pour le contour de sélection (épaisseur, rayon), `HandleSize`, `EdgeHitWidth`, `SelectionBoxBrush` en propriétés ou clés `Flow.*` ; compléter `doc/reference/theme-keys.md`.
- [x] **A7. `ConnectionLine` personnalisable** (aperçu de connexion) : propriété `ConnectionLineTemplate`.
- [x] **A8. Docs + démo** : onglet « Personnalisation » (node, handle, edge, marqueur) et page `doc/learn/` dédiée.

**Critère de sortie :** un projet tiers n'ayant que la référence NuGet peut définir un node, un handle, un edge et un marqueur entièrement personnalisés, sans réflexion ni sous-classe.

## Phase B — Compatibilité MVVM

### B1. Source de données observable

- [x] **`NodesSource` / `EdgesSource`** (`IEnumerable`, `INotifyCollectionChanged` pris en charge) en complément de `Nodes`/`Edges` : chaque élément est un ViewModel arbitraire. *(fait : `FlowView.NodesSource`, `Mvvm/SourceAdapter.cs`.)*
- [x] **Adaptateur par mappage** : `NodeMapping<TVm>` (ou propriétés `NodeIdPath`, `NodeTypePath`, `NodeXPath`, `NodeYPath`… façon `DisplayMemberPath`) projetant le ViewModel vers un `Node` immuable. Les modifications `INotifyPropertyChanged` du ViewModel déclenchent un `NodeChange` ciblé (pas de reconstruction de toute la liste). *(fait, sous la forme de `NodeMapping` / `EdgeMapping` : noms de propriétés, valeurs par défaut `Id`, `X`, `Y`.)*
- [x] **Rétro-projection** : un `NodePositionChange` / `NodeResizeChange` / `NodeDimensionsChange` écrit dans les propriétés du ViewModel (`X`, `Y`, `Width`, `Height`) via binding bidirectionnel ou délégué (`OnNodeChanged`). *(fait pour position, taille explicite, sélection, reconnexion ; les dimensions mesurées ne sont volontairement pas écrites.)*
- [x] **Suppression et ajout** : `CollectionChanged` du modèle → `NodeAddChange`/`NodeRemoveChange` ; suppression à l'écran (touche Suppr) → retire l'élément de la collection source. *(fait ; `NodeFactory`/`EdgeFactory` créent les ViewModels des éléments ajoutés par la vue en attendant `ConnectCommand`.)*
- [x] Le mode actuel (listes de `record`, `AutoApplyChanges`) reste supporté et documenté comme « mode fonctionnel » (équivalent React).

### B2. Sélection et état bindables

- [x] `SelectedNodes` / `SelectedEdges` (`IList` bindable, `OneWay`/`TwoWay`), `SelectedNode` (premier), `SelectionChanged` (RoutedEvent). *(fait, sans `SelectionChanged` : la propriété liée suffit.)*
- [x] `Viewport` déjà bindable (`TwoWay` par défaut à vérifier) ; ajouter `Zoom` en lecture/écriture.
- [x] `IsConnecting`, `CanUndo`, `CanRedo` en propriétés de dépendance en lecture seule (pour désactiver des boutons).

### B3. Commandes (`ICommand`)

- [x] Propriétés de commande sur `FlowView` : `ConnectCommand` (paramètre `Connection`), `NodeDragStopCommand`, `NodesDeleteCommand`, `EdgesDeleteCommand`, `DropCommand` (paramètre `FlowDropEventArgs`), `NodeClickCommand`, `EdgeClickCommand`, `PaneClickCommand`. `CanExecute` consulté avant l'action (équivalent de `isValidConnection` côté ViewModel).
- [x] Commandes **statiques** réutilisables en XAML (`FlowCommands.FitView`, `ZoomIn`, `ZoomOut`, `Undo`, `Redo`, `SelectAll`, `DeleteSelection`), `CommandBinding` branché sur `FlowView` : `FlowControls`, menus et boutons les utilisent sans code-behind.
- [x] `FlowControls` et `MiniMap` trouvent leur `FlowView` par `ElementName`/`RelativeSource` ou `Flow="{Binding …}"` (aujourd'hui : parent visuel implicite).

### B4. Découplage ViewModel ↔ vue

- [x] **Interface `IFlowController`** (FitView, ZoomTo, ScreenToFlow, Undo/Redo, centrer sur un node) exposée via `FlowView.Controller` (liaison `OneWayToSource` vers le ViewModel) : le ViewModel pilote la vue sans référence à `FlowView`.
- [x] Templates de nodes **sans code-behind** : exemples avec `Node.Data` = ViewModel (`ObservableObject` / `INotifyPropertyChanged`) et commandes liées (toolbar du node).
- [x] **Compatibilité avec les bibliothèques MVVM courantes** (CommunityToolkit.Mvvm, Prism, ReactiveUI) : aucune dépendance imposée dans `Wpflow.Wpf` ; projet d'exemples séparé utilisant `CommunityToolkit.Mvvm`.
- [x] **Undo/redo** : option pour déléguer à l'historique du ViewModel (`IUndoRedoService` optionnel) plutôt qu'aux snapshots de `FlowView`.

### B5. Validation et sérialisation côté ViewModel

- [x] `IsValidConnection` complété par `ValidateConnectionCommand`/`IConnectionValidator` injectable.
- [x] `GraphSerializer` : surcharges pour sérialiser/désérialiser les ViewModels via le mappage de B1.

### B6. Tests et démo MVVM

- [x] Projet de test `Wpflow.Wpf.Tests` (STA) pour l'adaptateur (ajout/suppression/propriété modifiée → bon `Change`, rétro-projection) : aujourd'hui seuls le noyau et les auto-tests visuels sont couverts.
- [x] Démo **« MVVM »** (nouvel onglet) : tout en XAML + ViewModel, zéro code-behind : collection observable de ViewModels, commande de connexion, sélection bindable, boutons liés aux commandes statiques, node à ViewModel.
- [x] Doc `doc/learn/13-mvvm.md` + section dans `doc/reference/flowview.md`.

**Critère de sortie :** la démo MVVM est entièrement pilotée par un ViewModel (aucune ligne de code-behind au-delà du `InitializeComponent`) et ajouter/supprimer/déplacer/connecter fonctionne dans les deux sens (vue ↔ ViewModel).

## Ordre proposé

1. **B1 + B2** (source observable, sélection) : c'est le plus structurant, décide du modèle ; commencer par un prototype d'adaptateur dans `Wpflow.Wpf` et valider le coût (le test de perf à 3 000 nodes doit rester dans les mêmes ordres de grandeur).
2. **B3** (commandes) : peu risqué, grosse valeur.
3. **A3 + A4** (edges et marqueurs personnalisés) : changent `EdgeVisual`/`EdgeLayer` ; à mesurer (le rendu direct des types intégrés ne doit pas régresser).
4. **A1, A2, A5–A7** : petits, indépendants.
5. **B4–B6** et docs/démo en continu.

## Décisions à trancher avant de coder

| Question | Options | Pente actuelle |
|---|---|---|
| Où vit le MVVM ? | dans `Wpflow.Wpf` / nouveau `Wpflow.Mvvm` | `Wpflow.Wpf` (pas de dépendance externe, donc pas besoin d'un paquet de plus) |
| Source de vérité | ViewModel observable / `record` immuables | Les deux, le mode choisi par `NodesSource` vs `Nodes` ; ne jamais les mélanger |
| Modifications en cours de drag | écrire à chaque mouvement / à la fin du drag | Écrire dans le ViewModel à chaque mouvement avec limitation de fréquence (option `CommitMode`), sinon coût et undo pollué |
| Identité d'un node | `Id` string imposé / clé quelconque | `Id` string issu d'un chemin de propriété (comme React Flow) |
| Edge personnalisé : dessin | géométrie fournie / `DataTemplate` complet | Les deux : provider de chemin (rapide) + template (flexible) |

## Risques

- **Performance** : projeter des ViewModels vers des records ajoute des allocations ; réutiliser les records tant que le ViewModel n'a pas changé, et garder le chemin de rendu actuel. Mesurer avec `WPFLOW_PERF` avant/après.
- **Double source de vérité** : ViewModel et records qui divergent. Règle : la vue ne modifie jamais le ViewModel directement, elle émet des changes que l'adaptateur applique.
- **Templates d'edges** : un `DataTemplate` par edge coûte beaucoup plus qu'un `OnRender` ; ne l'utiliser que pour les types personnalisés qui le demandent.
- **API qui enfle** : les nouvelles propriétés doivent rester optionnelles et sans effet quand elles ne sont pas utilisées.

## Avancement (24/11/2026)

**B1 et B2 (sélection) faits** : voir [doc/learn/13-mvvm.md](../doc/learn/13-mvvm.md). 18 tests STA (`tests/Wpflow.Wpf.Tests`) et 10 vérifications d'auto-test avec la vraie souris (onglet « MVVM »). Performances inchangées en mode fonctionnel (3 000 nodes : 3,4 s, 86 ms/image ; 5 000 virtualisés : 0,4 s, 9 ms/image). Décisions prises : MVVM dans `Wpflow.Wpf` (pas de paquet de plus), un mappage par **noms de propriétés** (réflexion mise en cache), `Id` chaîne, écriture dans le ViewModel à chaque mouvement de souris, Undo/Redo désactivé avec une source.

**Phase A faite (25/11/2026)** : voir [doc/learn/14-customization.md](../doc/learn/14-customization.md). Écarts assumés par rapport au plan, tous dans le sens d'une API plus petite et cohérente avec `wpflow-node-{type}` :

- **A2** : pas de propriété `HandleTemplate` ; un style implicite `flow:Handle` avec `Template` (et `Width`/`Height`). Sans `Template`, le dessin direct reste (coût nul).
- **A3** : pas de dictionnaire `FlowView.EdgeTypes` ; deux ressources par type, `wpflow-edge-{type}` (`IEdgePathProvider`, chemin rapide, rendu de la bibliothèque) et `wpflow-edge-template-{type}` (`DataTemplate` avec `EdgeRenderContext`). Les types intégrés restent en rendu direct ; on peut surcharger leur tracé.
- **A4** : `EdgeMarker` reste une énumération ; `Edge.MarkerEndKey` / `MarkerStartKey` désignent une `Geometry` (`wpflow-marker-{clé}`), sérialisée dans le JSON.
- **A5** : `wpflow-edge-label-{type}`.
- **A6** : propriétés `EdgeHitWidth`, `SelectionOutlineGap`, `SelectionOutlineThickness` plutôt qu'un objet `NodeStyle` (la taille des handles passe par leur style).
- **A7** : `ConnectionLineStyle` (un `Style` de `Path`) plutôt qu'un template : couleur, épaisseur et pointillés suffisent.
- **Hors plan, ajouté** : `NodeFactory`/`EdgeFactory` deviennent des propriétés de dépendance (liables), et **Undo/Redo fonctionne avec une source** (restauration par différence, le ViewModel supprimé revient à l'identique) ; la page d'accueil (cubes) est convertie en MVVM (`HomeViewModel`) et son auto-test passe par le ViewModel.
- **Vérifié** : 12 tests STA de personnalisation, 78 tests du noyau, 48 vérifications d'auto-test avec la vraie souris (clic sur un edge personnalisé et sur un edge à template, connexion avec des handles personnalisés).

**Phase B terminée (25/11/2026)** — guide : [doc/learn/13-mvvm.md](../doc/learn/13-mvvm.md). Compléments et écarts par rapport au plan :

- **B2** : `Zoom` (`TwoWay`), `IsConnecting`, `CanUndo`, `CanRedo` (ces trois en lecture seule). `Viewport` était déjà `TwoWay`.
- **B3** : huit `ICommand` en propriétés de dépendance. Règle unique : `CanExecute` est consulté avant l'action ; `ConnectCommand`, `NodesDeleteCommand` et `EdgesDeleteCommand` **remplacent** l'action automatique de la vue (le ViewModel la fait), les autres notifient. `FlowCommands` : neuf `RoutedUICommand` dont l'état suit la vue (Annuler grisé sans historique…). `FlowControls` / `MiniMap` : propriété `Flow` pour les placer hors de la vue.
- **B4** : `IFlowController` remis par liaison `OneWayToSource` (le ViewModel ne voit pas le contrôle) ; `IFlowHistory` pour déléguer l'historique (au lieu d'un `IUndoRedoService`) ; compatibilité vérifiée **par des tests** avec CommunityToolkit.Mvvm (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`) plutôt que par un projet d'exemples séparé. Prism et ReactiveUI : pas testés (mêmes interfaces `INotifyPropertyChanged` / `ICommand`).
- **B5** : pas de `ValidateConnectionCommand` ni d'`IConnectionValidator` : le `CanExecute` de `ConnectCommand` est le validateur côté ViewModel, et `IsValidConnection` devient liable. JSON : `FlowView.ToJson` / `LoadJson` et paramètre `data` de `GraphSerializer.ToJson` ; avec une source, `Node.Data` n'est pas écrit par défaut.
- **B6** : l'onglet MVVM n'a plus aucun code-behind et exerce commandes, contrôleur, zoom lié, `IsConnecting` et commandes routées. Un défaut de la démo (texte d'état qui faisait passer la barre d'outils sur deux lignes et décalait la vue pendant un glisser) a été trouvé par l'auto-test.
- **Vérifié** : 42 tests STA, 78 tests du noyau, 60 vérifications d'auto-test à la vraie souris, auto-test de l'accueil, performance inchangée (3 000 nodes : 3,4 s).
