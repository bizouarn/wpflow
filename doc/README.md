# Documentation de Wpflow

Wpflow porte [xyflow / React Flow](https://reactflow.dev/) vers WPF. Cette documentation suit l'organisation du site de React Flow : **Learn** (guides), **API Reference**, **Examples**. Quand un concept existe dans React Flow, la page indique son équivalent.

## Learn

| Page | Équivalent React Flow |
|---|---|
| [1. Démarrage rapide](learn/01-quickstart.md) | Quick Start |
| [2. Termes et concepts](learn/02-concepts.md) | Terms and Concepts, Built-In Components |
| [3. Construire un flow](learn/03-building-a-flow.md) | Adding Interactivity, State Management |
| [4. Nodes personnalisés](learn/04-custom-nodes.md) | Custom Nodes, Handles, Node Resizer, Node Toolbar |
| [5. Edges](learn/05-edges.md) | Custom Edges, Edge Labels, Markers, Reconnecting |
| [6. Sous-flows et groupes](learn/06-sub-flows.md) | Sub Flows |
| [7. Disposition automatique](learn/07-layouting.md) | Layouting |
| [8. Thèmes et styles](learn/08-theming.md) | Theming, Dark mode |
| [9. Interactions et connexions](learn/09-interactions.md) | Interaction props, Connection validation, Drag and Drop |
| [10. Calculer avec un flow](learn/10-computing-flows.md) | Computing Flows (la démo de la page d'accueil) |
| [11. Performance et accessibilité](learn/11-performance-accessibility.md) | Performance, Accessibility |
| [12. Sauvegarde et JSON](learn/12-serialization.md) | Save and Restore, `toObject` |
| [13. MVVM](learn/13-mvvm.md) | State Management (ViewModels, binding, sélection) |
| [14. Personnaliser](learn/14-customization.md) | Custom Nodes, Custom Edges, Custom Markers (`nodeTypes`, `edgeTypes`) |

## API Reference

| Page | Contenu |
|---|---|
| [FlowView](reference/flowview.md) | Propriétés, événements et méthodes (`<ReactFlow>` + `useReactFlow`) |
| [Composants](reference/components.md) | `Handle`, `MiniMap`, `FlowControls`, `FlowPanel`, `NodeResizer`, `NodeToolbar` |
| [Types](reference/types.md) | `Node`, `Edge`, `Connection`, `Viewport`, `HandleInfo`… |
| [Changes](reference/changes.md) | `NodeChange`, `EdgeChange`, `Changes.Apply` |
| [Utilitaires](reference/utils.md) | `EdgePaths`, `ViewportMath`, `GraphUtils`, `NodeTree`, `Connections`, `NodeResize`, `GraphLayout`, `GraphSerializer` |
| [Clés de thème](reference/theme-keys.md) | Ressources `Flow.*` |

## Examples

[Liste des exemples](examples/README.md) : page d'accueil animée, types d'edges, groupes, toolbar et resizer, palette glisser-déposer, disposition automatique, options et JSON, personnalisation, MVVM, performance.

## Deux niveaux d'API

- **`Wpflow.Core`** (net8.0) : tout ce qui ne dépend pas de l'interface — modèle, mathématiques, chemins d'edges, JSON. Utilisable dans un service, un test ou un autre framework UI.
- **`Wpflow.Wpf`** (net8.0-windows) : les contrôles. Ils ne contiennent que de la présentation et de l'interaction ; la logique vient de `Wpflow.Core`.
