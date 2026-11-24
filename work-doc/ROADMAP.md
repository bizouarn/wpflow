# Roadmap — portage de xyflow vers WPF

Objectif : fournir en WPF l'équivalent de [xyflow](https://github.com/xyflow/xyflow) (React Flow / Svelte Flow) : un éditeur de graphes node/edge interactif, avec viewport pan/zoom, handles, connexions, sélection, etc.

> **Statut du code existant : provisoire, remise en cause assumée.** Voir [ARCHITECTURE-DECISIONS.md](ARCHITECTURE-DECISIONS.md). Aucune phase ne doit être contrainte par le prototype actuel (`Wpflow : Canvas`, `Nodes`, `Edge` stub).

## Démo cible

**La démo (`demo/`) doit ressembler à celle de la page d'accueil de [reactflow.dev](https://reactflow.dev/)** : fond en points, nodes à l'apparence React Flow (bordure fine, coins arrondis, handles aux bords), edges bézier avec flèches et edges animés, minimap, boutons de contrôle (zoom +/−, fit view, verrouillage), pan/zoom fluides, drag des nodes, création de connexions à la souris, sélection. Elle sert de test de réception de chaque phase : une fonctionnalité n'est « finie » que quand elle est visible dans la démo.

## Ce qu'on porte (et ce qu'on ne porte pas)

xyflow est découpé en trois couches ; ce découpage guide le portage :

| xyflow | Rôle | Équivalent WPF visé |
|---|---|---|
| `@xyflow/system` | Logique pure, sans framework (maths de viewport, chemins d'edges, drag, connexions, handles) | Projet C# **sans dépendance UI** (`Wpflow.Core`) : testable unitairement |
| `@xyflow/react` | Rendu + état (store), composants | Contrôles WPF (`Wpflow.Wpf`) + ViewModels/modèle |
| `@xyflow/svelte` | Idem, autre framework | Hors périmètre |

Hors périmètre initial : SSR, serialisation `toObject` identique au JSON de React Flow (à reconsidérer plus tard pour l'interop), thème Tailwind.

## Phase 0 — Cadrage et décisions (avant d'écrire du code)

- [x] Lire `@xyflow/system` et lister ce qui est réutilisable en C#. *(lus et comparés ligne à ligne : `smoothstep-edge`, `bezier-edge`, `edges/general`, `xyhandle/utils` ; non relus en détail : `XYDrag`, `XYPanZoom`, `XYResizer`, `XYMinimap`, dont le comportement a été reproduit à partir de la connaissance de la lib et validé par les auto-tests.)*
- [x] Trancher les questions ouvertes de [ARCHITECTURE-DECISIONS.md](ARCHITECTURE-DECISIONS.md) *(journal des décisions §3)*.
- [x] Définir le périmètre MVP et la parité d'API visée : parité fonctionnelle, API idiomatique WPF (voir tableau de correspondance dans `README.md`).
- [x] Décider du sort du prototype : jeter, ou extraire ce qui sert (le drag de `Nodes` est le seul morceau fonctionnel).
- [x] Structure de solution, tests, CI (`.github/workflows/ci.yml`, non exécutée ici : aucun dépôt distant configuré), conventions (commentaires en français).

**Critère de sortie :** décisions écrites, prototype réévalué, structure de solution validée.

## Phase 1 — Noyau sans UI (`Wpflow.Core`)

Port de la logique de `@xyflow/system`, avec tests unitaires :

> **Démarrée** — projet `src/Wpflow.Core` (net8.0, sans WPF) et `tests/Wpflow.Core.Tests` (xUnit, net10.0 car seul le runtime 10 est installé sur la machine de dev).

- [x] Types de base : `Node`, `Edge`, `HandleInfo`, `Viewport` (x, y, zoom), `Rect`, `XYPosition`, `Position` (Top/Right/Bottom/Left).
- [x] Transformations viewport ↔ coordonnées flow (`ScreenToFlow`, `FlowToScreen`), zoom centré sur le curseur, bornes `minZoom`/`maxZoom`, `WheelZoomFactor`, `GetVisibleRect`.
- [x] `translateExtent` (limites de pan) : `ViewportMath.ClampToExtent` (algorithme de d3-zoom) + `FlowView.TranslateExtent`.
- [x] Génération des chemins d'edges : `Straight`, `Bezier`, `Step`, `SmoothStep` (+ centre du label). Sortie en syntaxe SVG, directement exploitable par `Geometry.Parse` en WPF.
- [x] Calcul de la position des handles (`GetHandlePosition`).
- [x] Calcul de bounds (`GetNodesBounds`), `GetViewportForBounds` (fitView), `snapToGrid`.
- [x] `nodeExtent` (bornes de déplacement des nodes) : `GraphUtils.ClampToExtent` + `FlowView.NodeExtent`.
- [x] Détection du handle le plus proche (`GetClosestHandle`), détection de doublon (`ConnectionExists`).
- [x] Validation de connexion : `ConnectionMode` strict/loose, `Connections.Create/IsValid`, `FlowView.IsValidConnection`.
- [x] Mise à jour d'état par « changes » (`NodeChange` / `EdgeChange` : position, dimensions, select, remove, add, replace) — `Changes.Apply`.
- [x] `stepPosition` et `centerX/centerY` pour `SmoothStep`. *(« handles en `position: absolute » : sans objet en WPF, les handles sont mesurés par leur transformation réelle.)*

**Critère de sortie :** couverture de tests sur chaque algorithme, comparée à des valeurs de référence produites par xyflow.
*Reste à faire : les chemins `SmoothStep` mixtes et les valeurs de référence sont écrits de mémoire du code xyflow et vérifiés par calcul manuel ; les comparer à la sortie réelle de `@xyflow/system` (même version épinglée).*

## Phase 2 — Surface de rendu et viewport (`Wpflow.Wpf`)

- [x] Contrôle racine `Flow` (équivalent `<ReactFlow>`), avec `ItemsSource` nodes/edges.
- [x] Viewport : pan (clic milieu / espace + drag), zoom molette et pinch, transformation appliquée à une couche unique (`MatrixTransform`), pas aux enfants un à un. *(fait : pan au glisser sur le fond, zoom molette centré curseur ; clic milieu, Espace + glisser et pincement tactile)*
- [x] Rendu des nodes via `DataTemplate` (équivalent `nodeTypes`) ; mesure de la taille réelle (`ActualWidth/Height`) remontée au modèle.
- [x] Rendu des edges (`Path` / `StreamGeometry`) à partir des chemins de la Phase 1, avec `edgeTypes` personnalisables.
- [x] Drag de nodes dans le référentiel du viewport (zoom pris en compte), `snapToGrid`, `nodeExtent`. *(fait, y compris `nodeExtent`)*
- [x] Couches séparées : fond, edges, nodes, overlay (connexion en cours, sélection).

**Critère de sortie :** démo avec ~10 nodes/edges, pan/zoom/drag fluides.

## Phase 3 — MVP interactif

- [x] Handles (source/target) positionnables sur les 4 côtés ; plusieurs handles par node.
- [x] Création de connexions par drag handle → handle, ligne de connexion en cours, validation, `onConnect`.
- [x] Sélection : clic, multi-sélection (Ctrl/Shift), sélection rectangulaire, suppression au clavier. *(fait : clic, Ctrl/Maj, rectangle avec Maj, Suppr)*
- [x] Reconnexion d'edges existants (on attrape l'edge près d'une extrémité et on la dépose sur un autre handle).
- [x] Marqueurs de flèche (`markerEnd` / `markerStart`), labels d'edges. *(`markerStart` et `markerEnd`, flèche ouverte ou fermée)*
- [x] `fitView`, API de commandes (`zoomIn`, `zoomOut`, `setViewport`, `fitView`) — équivalent `useReactFlow`.
- [x] Undo/redo (Ctrl+Z, Ctrl+Y ; API `Undo()`/`Redo()` ; 100 niveaux, mode non contrôlé uniquement).

**Critère de sortie :** on peut construire et éditer un graphe complet à la souris et au clavier.

## Phase 4 — Composants annexes

Équivalents des composants de `@xyflow/react` :

- [x] `Background` (points / lignes / croix).
- [x] `Controls` (zoom +/−, fit, verrouillage).
- [x] `MiniMap` (rendu simplifié, pan/zoom depuis la minimap). *(fait : clic/glisser recentre le viewport)*
- [x] `NodeResizer` (8 poignées, visibles quand le node est sélectionné ; tailles min/max ; `NodeResizeChange`, logique pure testée dans `NodeResize`).
- [x] Contour de sélection des nodes à template personnalisé (`NodeHost.IsSelected` / `ShowOutline`).
- [x] `NodeToolbar` et `EdgeLabelRenderer` (`FlowView.EdgeLabelTemplate`).
- [x] `Panel` : `FlowPanel` (8 positions).
- [x] Nodes imbriqués / groupes (`ParentId`, `ExtentParent`, type `group`) : `NodeTree`, déplacement/suppression en cascade, empilement par profondeur.

## Phase 5 — Finitions, performance, distribution

- [x] Virtualisation (`OnlyRenderVisibleElements`) : 3 000 nodes -> ~260 rendus ; `GraphUtils.IsEdgeVisible` porté de xyflow.
- [x] Accessibilité : navigation clavier complète (Tab, flèches, Ctrl+A, Échap) et `AutomationPeer` pour `FlowView` et `NodeHost` (nom + pattern SelectionItem).
- [x] Thèmes clair/sombre (`ColorMode`, `Themes/Light.xaml` / `Dark.xaml`, clés `Flow.*`).
- [x] Support tactile : pincement (`ManipulationDelta`) et glisser via les événements souris promus. **Non testé sur matériel tactile.**
- [x] Sérialisation JSON compatible React Flow : `GraphSerializer.ToJson/FromJson`.
- [x] Packages NuGet (`Wpflow.Core`, `Wpflow.Wpf` 0.1.0, vérifiés avec `dotnet pack`, non publiés ; licence MIT), `README.md`, fenêtre d'exemples (types d'edges, groupes, toolbar/resizer, palette glisser-déposer, disposition automatique, options/JSON, performance) et `GraphLayout`.

## Risques

- **Performance WPF** avec beaucoup d'éléments (arbre visuel lourd) — à mesurer dès la Phase 2, pas à la fin.
- **Mesure des nodes** : xyflow dépend de dimensions mesurées avant de calculer les handles/edges ; en WPF le cycle measure/arrange impose un traitement asynchrone équivalent.
- **Divergence de modèle** : copier l'API React (hooks, props) tel quel est contre-productif en WPF ; viser la parité fonctionnelle, pas syntaxique.
- **Cible** : WPF est Windows-only ; si le multiplateforme compte, voir Avalonia (cf. décisions).

## État d'avancement

Toutes les cases ci-dessus sont cochées (24/11/2026). « Terminé » signifie : implémenté, vérifié par les tests du noyau (71 tests) et par les auto-tests pilotant la vraie souris/le vrai clavier (`WPFLOW_SELFTEST=1` : 13 mesures journalisées ; `WPFLOW_EXAMPLES=selftest` : 28 vérifications). Ce qui n'a **pas** pu être vérifié ou reste hors périmètre, honnêtement :

- Pincement tactile : implémenté, jamais testé sur un écran tactile.
- Lecteurs d'écran : les `AutomationPeer` sont vérifiés par code, pas avec Narrateur/NVDA.
- CI GitHub Actions : écrite, jamais exécutée (pas de dépôt distant configuré).
- NuGet : packages construits (`dotnet pack`) mais ni installés dans un projet tiers ni publiés . Licence MIT ajoutée.
- Hors périmètre initial, non faits : edges personnalisés par type (seuls `default`/`straight`/`step`/`smoothstep`), marqueurs personnalisés, sélection d'edges au rectangle, `NodeResizer` à ratio d'aspect, `isValidConnection` asynchrone, comparaison automatisée des chemins d'edges avec la sortie réelle de `@xyflow/system` (la comparaison a été faite à la lecture du code source).

## Suite

Voir [ROADMAP-2-EXTENSIBILITE-MVVM.md](ROADMAP-2-EXTENSIBILITE-MVVM.md) : edges/handles/marqueurs personnalisables et compatibilité MVVM (source observable, sélection bindable, commandes).
