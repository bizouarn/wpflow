# Décisions d'architecture et remise en cause du prototype

Document de travail : chaque question ouverte doit être tranchée en Phase 0 de la [ROADMAP](ROADMAP.md), puis la réponse consignée ici.

## 1. Bilan du prototype actuel

| Élément | État | Problème | Verdict provisoire |
|---|---|---|---|
| `Wpflow : Canvas` | Coquille vide | Hériter de `Canvas` empêche d'appliquer un viewport (pan/zoom) proprement et d'avoir des couches (fond, edges, nodes, overlay) ; un `Canvas` ne mesure pas ses enfants. | **À remplacer** par un contrôle composé (`Control` + template, ou `FrameworkElement` custom). |
| `Nodes` (`ContentControl`) | Drag fonctionnel | Calcule la position en coordonnées du `Canvas` parent : cassera dès qu'il y a un zoom/pan. Contraint à rester dans les bornes du canvas (xyflow n'a pas cette limite, il utilise `nodeExtent`). `Parent as Canvas` : couplage fort, exception si le parent change. Nom pluriel (`Nodes`) pour un seul node. | **À réécrire**, garder l'idée (capture souris + delta). |
| `Edge` | Stub | Ajoute une `Line` au canvas parent depuis un contrôle qui n'affiche rien lui-même ; extrémités en dur ; bug `Y1`; ne suit pas les nodes ; pas de courbes. | **À jeter.** |
| `Group` | Vide | — | À reconcevoir avec les nodes imbriqués (`parentId`). |
| Absence de modèle | — | Le « modèle » est l'arbre visuel. xyflow repose sur un état (nodes/edges/viewport) dont l'UI n'est qu'une projection. | Point de rupture principal. |
| Pas de tests | — | La logique (maths) est inséparable de l'UI. | Extraire un noyau testable. |

**Conclusion :** à part l'idée de drag par capture de souris, rien n'est à conserver. Repartir d'une page blanche est moins coûteux que de faire évoluer le prototype. Le dépôt peut garder l'historique ; le code actuel pourra être supprimé dès la Phase 2.

## 2. Questions ouvertes

### 2.1 Surface de rendu
- **A. Contrôle composé avec `ItemsControl`/`DataTemplate` + `Canvas` interne transformé** — idiomatique WPF, templating gratuit, binding naturel ; coût de l'arbre visuel avec beaucoup d'éléments.
- **B. `FrameworkElement` custom avec `DrawingContext` (OnRender)** — très performant pour les edges/fond, mais pas de templating pour les nodes.
- **C. Hybride (recommandé à valider par un spike)** : nodes en `ItemsControl` templatés ; edges, fond et overlay dessinés en `OnRender` / `StreamGeometry`.

*Action :* spike de performance (1 000 / 5 000 nodes) avant de trancher.

### 2.2 Modèle de données
- Classes de modèle (`Node`, `Edge`) observables (`INotifyPropertyChanged`) ou immuables + « changes » comme xyflow ?
- Recommandation : **immuables + changes** dans le noyau (fidèle à xyflow, testable), avec un adaptateur observable pour le binding WPF.
- Données utilisateur du node : `object Data` + `DataTemplate` par type, ou génériques ?

### 2.3 MVVM
- Le contrôle doit fonctionner **sans** ViewModel imposé (binding sur `ItemsSource` de nodes/edges) mais être MVVM-friendly (commandes, events routés).
- Éviter de dépendre d'un framework MVVM tiers dans la lib.

### 2.4 Découpage en projets
- `Wpflow.Core` : `net8.0` (ou `netstandard2.0` si réutilisation visée), **sans** référence WPF, avec projet de tests.
- `Wpflow.Wpf` (actuel `src/`) : `net8.0-windows`, `UseWPF`.
- `demo/` : galerie d'exemples calquée sur les exemples xyflow.
- Renommer le projet `wpflow` / namespace `wpflow` (casse, convention .NET `Wpflow`) : à décider tant qu'il n'y a pas de consommateurs.

### 2.5 Cible technologique
- WPF reste le choix demandé. À noter qu'avec un noyau sans UI, un portage Avalonia ultérieur devient possible ; ne pas s'en servir pour retarder le MVP.

### 2.6 Parité avec xyflow
- Parité **fonctionnelle** (mêmes capacités, mêmes noms de concepts : nodes, edges, handles, viewport, connection) ; API idiomatique WPF (propriétés de dépendance, événements routés, commandes) plutôt que props/hooks React.
- Suivre une version précise de xyflow comme référence (à épingler) et conserver des valeurs de référence pour les tests des chemins d'edges.

### 2.7 Conventions
- Langue des commentaires/docs : actuellement français (générés par le template Visual Studio). À fixer avant d'écrire la doc publique.
- Licence : MIT (voir `LICENSE`) ; xyflow étant MIT, son avis de copyright est conservé dans `THIRD-PARTY-NOTICES.md` (le code est un portage).

## 3. Journal des décisions

| Date | Décision | Raison |
|---|---|---|
| 2026-11-24 | Prototype (`Wpflow : Canvas`, `Nodes`, `Edge`, `Group`) **supprimé** ; projet WPF recréé sous `src/Wpflow.Wpf`. | Voir §1 : le prototype ne pouvait pas porter un viewport ni un modèle d'état. |
| 2026-11-24 | Noyau sans UI `Wpflow.Core` (net8.0) + modèle **immuable** + « changes » (`Changes.Apply`). | Fidèle à xyflow, testable sans WPF (35 tests xUnit). |
| 2026-11-24 | `FlowView` en mode **non contrôlé par défaut** (`AutoApplyChanges=true`), contrôlé si `false`. | Équivalent de `useNodesState` vs props contrôlées de React Flow. |
| 2026-11-24 | Nodes = éléments WPF (`NodeHost`) templatés par clé `wpflow-node-{type}` ; edges = vrais `Path` (pas `OnRender`). | Contrôles WPF natifs dans les nodes (Slider, TextBox…) ; hit-testing et animation gratuits. **Spike de perf fait** (`WPFLOW_PERF=N`, nodes par défaut, N−1 edges bézier) : 1 000 nodes → 0,8 s d'affichage, 3 ms/image en déplacement de node, 6 ms/image en pan ; 5 000 nodes → 5,4 s, 52 ms/image (déplacement), 34 ms/image (pan). Première mesure avant correction : 7,5 s et 171 s au chargement (coût quadratique : une mise à jour complète par node mesuré). Corrigé par le regroupement des mesures et la mise à jour incrémentale (seuls les nodes modifiés et leurs edges). Au-delà de ~2 000 nodes, prévoir la virtualisation (Phase 5). |
| 2026-11-24 | Historique undo/redo par instantanés des listes immuables (nodes + edges), poussé au début d'un déplacement, d'une connexion, d'une reconnexion et d'une suppression. | Les records immuables rendent l'instantané gratuit (partage structurel). Désactivé en mode contrôlé. |
| 2026-11-24 | Toute l'interaction souris est centralisée dans `FlowView`. | Les contrôles enfants qui marquent l'événement `Handled` (Slider…) continuent de fonctionner sans conflit de drag. |
| 2026-11-24 | `demo` cible net10.0-windows, bibliothèques net8.0. | Seul le runtime .NET 10 est installé sur la machine de dev. |
| 2026-11-24 | `Node.FixedWidth/FixedHeight` (taille explicite) distincts de `Width/Height` (taille mesurée) ; `NodeResizeChange` positionne les deux. `NodeResizer` est un `Grid` de `Thumb` placé dans le template du node, calcul dans `Core.NodeResize`. | Même séparation que `width`/`measured.width` de xyflow. Le delta est recalculé depuis la souris dans le référentiel flow (indépendant du zoom) et non depuis `DragDelta`, car la poignée bouge avec le node. |
| 2026-11-24 | Contour de sélection fourni par le style de `NodeHost` (pas par les templates), uniquement pour les templates personnalisés. | Le node par défaut gère déjà sa propre bordure ; les templates n'ont rien à faire. |
| 2026-11-24 | Mise à jour des chemins `SmoothStep` d'après les sources de `@xyflow/system` (téléchargées et lues) : `stepPosition`, `centerX/Y`, suppression des points alignés. Les valeurs de test écrites de mémoire (version ancienne) ont été corrigées. | La comparaison a montré que mon port, écrit de mémoire, était en retard d'une version ; le bézier était identique. |
| 2026-11-24 | Nodes imbriqués : `Node.Position` relatif au parent, index interne en coordonnées **absolues** (`_nodeIndex`) pour edges/handles/minimap, `_rawIndex` pour les contraintes de déplacement ; z-index = profondeur. | Un seul `Canvas` plat (pas de conteneurs imbriqués) : les enfants suivent le parent par simple recalcul de position absolue et les edges restent dans un seul référentiel. |
| 2026-11-24 | Thèmes par dictionnaires de ressources `Flow.*` fusionnés dans `FlowView.Resources` + `ThemeBrush(key)` pour le code qui dessine (edges, minimap, nodes par défaut). | Personnalisable sans recompiler ; les templates utilisateur utilisent `DynamicResource`. |
| 2026-11-24 | Virtualisation : masquage (`Visibility.Collapsed`) des nodes/edges hors vue, marge de 300 px ; un node jamais mesuré hors vue reste non mesuré. | Mesuré : 5 000 nodes passent de ~200 ms/image à 8–13 ms. Limite assumée : bounds, fitView et minimap ne voient que les nodes mesurés (documenté ; `FixedWidth/Height` recommandés). |
| 2026-11-24 | Bug trouvé par l'auto-test : les handles du node par défaut étaient enfants de la bordure (donc décalés de 10 px par le padding) ; ils sont maintenant frères de la bordure. | Un handle doit être sur le bord du node pour que les edges et la distance de connexion soient corrects. |
| 2026-11-24 | Perf mesurée après l'ajout du style de `NodeHost` et des handles frères : rendu complet de 5 000 nodes ~200 ms/image (contre 34 ms avant). Non optimisé : la virtualisation est la réponse retenue. | Le coût vient du nombre d'éléments visuels par node ; alléger les templates de `Handle`/`NodeHost` est possible mais n'a pas été fait. |
| 2026-11-24 | Audit : `Changes.Apply` indexe les ids pour les gros lots (≥ 8 changes) et renvoie la liste d'entrée telle quelle s'il n'y a aucun change ; `NodeTree.DescendantsOf` construit l'index parent → enfants une seule fois. | Micro-mesure (Release) de N `NodeDimensionsChange` : 5 000 nodes 125 → 11 ms, 20 000 nodes 2 073 → 17 ms. Le chargement émet un change par node mesuré ; l'ancien coût était quadratique. Les mesures de bout en bout (démo) sont trop bruitées d'une exécution à l'autre (×2) pour chiffrer le gain global. |
| 2026-11-24 | Audit : les brosses des edges sont résolues une fois par rafraîchissement (`EdgeStyle`) et un edge dont tracé, extrémités et style n'ont pas changé n'est pas re-parsé ; la minimap lit ses brosses une fois par image ; le hit-test souris fait un seul parcours de l'arbre. | Une recherche de ressource (`TryFindResource`) par edge ou par node de la minimap et par image coûtait plus cher que le dessin. |
| 2026-11-24 | Audit, corrections : `Undo`/`Redo` sont sans effet en mode contrôlé (même par appel direct) ; un nouveau gabarit de `FlowView` vide les hôtes de l'ancien canvas ; l'abonnement de `CubeField` à son état ne vit que tant que le contrôle est dans l'arbre. | Éviter un état incohérent et une fuite (le node supprimé restait référencé par l'état partagé). |
| 2026-11-24 | Audit, simplification : suppression du code mort (`Node.Handles`, `NodeTree.ToAbsolute`, brosses statiques de la minimap, `Frozen` de `NodeHost`) et des helpers d'auto-test dupliqués (`demo/Sim.cs`). | Aucun appelant ; trois copies de la saisie Win32 à maintenir. |
| 2026-11-24 | Arbre visuel allégé : `Handle` sans gabarit (dessiné dans `OnRender`, zone de clic élargie par un rectangle transparent), contour de sélection de `NodeHost` dessiné dans `OnRender` (plus de gabarit), `EdgeVisual` = un seul `FrameworkElement` qui dessine tracé, zone de clic, flèches et pointillés animés (`DashStyle.Offset`), labels d'edges créés à la demande dans la couche (`EdgeLabel`). Un handle passe de 4 éléments à 1, un node de ~5 à 2 (hors contenu), un edge de 6 à 1. | Mesure (3 000 nodes, rendu complet, 3 exécutions) : premier affichage 5,0–6,2 s → 3,5–3,8 s ; 1 000 nodes 1,2–1,4 s → 0,75 s. Le déplacement et le pan à 3 000 nodes ne bougent presque pas (66–96 → 64–97 ms) : le coût y vient du rendu WPF de milliers de primitives, que seule la virtualisation réduit (5 000 nodes : 7–11 ms/image). Hit-test, reconnexion, contour, flèches, labels et animation vérifiés par les auto-tests. |
