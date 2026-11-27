# Wpflow documentation

Wpflow is a node-based graph editor for WPF and Blazor. This documentation follows the organisation of the React Flow site: **Learn** (guides), **API Reference**, **Examples**. When a concept exists in React Flow, the page names its equivalent.

This documentation is also [online, with interactive Blazor examples](https://bizouarn.github.io/wpflow/) (the site is built from these files, see [`demo/docs-site`](../demo/docs-site/)).

## Learn

| Page | React Flow equivalent |
|---|---|
| [1. Quick start](learn/01-quickstart.md) | Quick Start |
| [2. Terms and concepts](learn/02-concepts.md) | Terms and Concepts, Built-In Components |
| [3. Building a flow](learn/03-building-a-flow.md) | Adding Interactivity, State Management |
| [4. Custom nodes](learn/04-custom-nodes.md) | Custom Nodes, Handles, Node Resizer, Node Toolbar |
| [5. Edges](learn/05-edges.md) | Custom Edges, Edge Labels, Markers, Reconnecting |
| [6. Sub-flows and groups](learn/06-sub-flows.md) | Sub Flows |
| [7. Automatic layout](learn/07-layouting.md) | Layouting |
| [8. Theming and styles](learn/08-theming.md) | Theming, Dark mode |
| [9. Interactions and connections](learn/09-interactions.md) | Interaction props, Connection validation, Drag and Drop |
| [10. Computing with a flow](learn/10-computing-flows.md) | Computing Flows (the home page demo) |
| [11. Performance and accessibility](learn/11-performance-accessibility.md) | Performance, Accessibility |
| [12. Saving and JSON](learn/12-serialization.md) | Save and Restore, `toObject` |
| [13. MVVM](learn/13-mvvm.md) | State Management (ViewModels, binding, selection) |
| [14. Customization](learn/14-customization.md) | Custom Nodes, Custom Edges, Custom Markers (`nodeTypes`, `edgeTypes`) |

## Blazor

| Page | Content |
|---|---|
| [Wpflow.Blazor](blazor.md) | The Blazor version: installation, first graph, parameters, events, methods, components, theme, differences from WPF |

## API Reference

| Page | Content |
|---|---|
| [FlowView](reference/flowview.md) | Properties, events and methods (`<ReactFlow>` + `useReactFlow`) |
| [Components](reference/components.md) | `Handle`, `MiniMap`, `FlowControls`, `FlowPanel`, `NodeResizer`, `NodeToolbar` |
| [Types](reference/types.md) | `Node`, `Edge`, `Connection`, `Viewport`, `HandleInfo`… |
| [Changes](reference/changes.md) | `NodeChange`, `EdgeChange`, `Changes.Apply` |
| [Utilities](reference/utils.md) | `EdgePaths`, `ViewportMath`, `GraphUtils`, `NodeTree`, `Connections`, `NodeResize`, `GraphLayout`, `GraphSerializer` |
| [Theme keys](reference/theme-keys.md) | `Flow.*` resources |

## Examples

[List of examples](examples/README.md): animated home page, edge types, groups, toolbar and resizer, drag-and-drop palette, automatic layout, options and JSON, customization, MVVM, performance.

## Two levels of API

- **`Wpflow.Core`** (net8.0, net472, netstandard2.0): everything that does not depend on the UI: model, maths, edge paths, JSON. Usable in a service, a test or another UI framework.
- **`Wpflow.Wpf`** (net8.0-windows, net472): the controls. They only contain presentation and interaction; the logic comes from `Wpflow.Core`.
