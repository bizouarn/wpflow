# Écart avec xyflow (React Flow)

Comparaison de Wpflow avec l'API publique de `@xyflow/react` et `@xyflow/system` (relevé initial le 25/11/2026 : `index.ts`, `component-props.ts`, types des nodes/edges, `instance.ts`, composants additionnels ; mis à jour après chaque lot).

**Le relevé initial listait environ 120 écarts (P1, P2, P3) : tous sont traités, sauf ceux de la section « Écarts restants ».** Les tableaux de détail d'origine ont été supprimés ; la référence de ce qui existe est `doc/reference/` (`flowview.md`, `components.md`, `types.md`, `utils.md`, `changes.md`).

Légende : ➖ sans objet en WPF · 🟡 autre forme ou partiel.

## Ce que couvre Wpflow

- **Noyau** : état par « changes » (`NodeChange` / `EdgeChange`, y compris `NodeInteractionChange` pour `dragging` / `resizing`), `Changesets.Diff`, chemins d'edges (`default`, `straight`, `step`, `smoothstep`, `simplebezier`, options par edge) **comparés à la sortie réelle de `@xyflow/system` 1.0.1 sur 224 cas**, requêtes (`GetIncomers`, `GetOutgoers`, `GetConnectedEdges`, `GetNode`, `GetIntersectingNodes`, `GetNodeConnections`, `AddEdge`, `ReconnectEdge`), `GraphClipboard`, `GraphLayout` (4 directions, réduction des croisements), JSON compatible React Flow.
- **Nodes** : tous les champs de React Flow (`hidden`, `connectable`, `deletable`, `dragHandle`, `extent`, `expandParent` dans les 4 directions, `zIndex`, `ariaLabel`, `origin` — y compris celui d'un parent —, `sourcePosition` / `targetPosition`, `initialWidth/Height`, `handles` explicites, `dragging`, `resizing`) et, en plus, `Rotation`.
- **Edges** : `data`, `hidden`, `selectable`, `deletable`, `reconnectable`, `interactionWidth`, `zIndex`, `ariaLabel`, style, style du label, options de tracé, options de marqueur (couleur, taille, trait, `Orient`).
- **Surface** : toutes les options d'interaction (globales, touches, boutons de pan, molette, pincement, double-clic, sélection, seuils, auto-défilement, `ConnectOnClick`, `ZIndexMode`, `PreventScrolling`…), tous les événements de clic, double-clic, menu contextuel, survol, glisser de node et de sélection, connexion et reconnexion, mouvement du viewport, molette, `FlowInitialized`, `ErrorOccurred`, `BeforeDelete` et `BeforeDeleteAsync`, middleware sur les changes (`NodeChangesRequested` modifiable), `Debug`, `ConnectionState` (`useConnection`), `NodesInitialized`, transitions animées (`SetViewport`, `SetCenter`, `FitBounds`, `FitView(options)`, `ZoomTo`), API d'instance, copier-coller, export d'image, portail, mode MVVM complet.
- **Composants** : `Handle` (états visuels, `IsConnectable*`, validateur, `Connected`), `MiniMap`, `FlowControls`, fonds superposables, `NodeToolbar`, `EdgeToolbar`, `SelectionToolbar`, `NodeResizer`, `NodeResizeControl`, `NodeRotator`, `FlowPanel`, labels d'edges.
- **Accessibilité** : peers UI Automation, noms configurables (`NodeAriaLabelProvider`, `EdgeAriaLabelProvider`), annonce de sélection (`AnnounceSelection`), navigation clavier.

## Écarts restants

| Élément | Raison |
|---|---|
| `proOptions`, `width` / `height`, `attributionPosition`, `Attribution`, `ReactFlowProvider`, hooks de store (`useStore`, `useNodesState`…) | ➖ sans objet en WPF |
| `className`, `style` CSS libres | ➖ remplacés par les ressources, les styles et les templates WPF |
| Handles et edges d'un node tourné | 🟡 comme dans React Flow, ils restent attachés aux positions non tournées |
| Dagre / ELK | 🟡 `GraphLayout.Layered` réduit les croisements mais n'est pas un moteur complet ; brancher un moteur externe pour les graphes complexes |
| `isValidConnection` asynchrone | 🟡 synchrone ; `ConnectCommand.CanExecute` joue le rôle de validateur côté ViewModel |
| `ConnectStarted` / `ConnectEnded` en connexion par clics | 🟡 `ConnectStarted` à l'appui ; la fin passe par `ClickConnectEnded` |
| `RenderToBitmap` avec `OnlyRenderVisibleElements` | 🟡 ne rend que les éléments déjà créés |
| `EdgeMarkerOptions.Orient` | 🟡 angle fixe en degrés (pas de `auto-start-reverse`) ; `Width` / `Height` relatifs à 12,5 |
| Pincement tactile, lecteurs d'écran | non vérifiables ici (voir [ROADMAP.md](ROADMAP.md)) |

## Vérifications faites

- Chemins d'edges comparés à `@xyflow/system` : `tests/Wpflow.Core.Tests/XyflowReferenceTests.cs` et `xyflow-reference.json` (généré par un script Node : `npm i @xyflow/system`, puis `getBezierPath`, `getSmoothStepPath`, `getStraightPath`). `getSimpleBezierPath` n'existe pas dans cette version du paquet : `SimpleBezier` n'est vérifié que par calcul.
- Compatibilité MVVM avec les vrais paquets CommunityToolkit.Mvvm, Prism et ReactiveUI (`MvvmFrameworksTests`).
- Packages NuGet installés depuis une source locale dans un projet WPF tiers : build, thème, minimap et contrôles rendus.
- Tests unitaires (noyau et WPF) et auto-tests à la vraie souris (`WPFLOW_SELFTEST=1` pour la démo, `WPFLOW_EXAMPLES=selftest` pour les exemples : survol, double-clic, connexion par clics, sélection partielle / complète, copier-coller, `PanOnScroll`, auto-défilement, `DragHandle`, `NoDrag`, `PanOnDrag`, `SelectionOnDrag`, `DefaultEdgeOptions`).
