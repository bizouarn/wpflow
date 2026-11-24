# 3. Construire un flow

*Équivalent React Flow : Adding Interactivity, State Management.*

## Le modèle : des listes immuables et des changes

```csharp
public sealed record Node(string Id, XYPosition Position, string Type = "default", object? Data = null, …);
public sealed record Edge(string Id, string Source, string Target, …);
```

Modifier un node, c'est en créer une copie :

```csharp
Flow.Nodes = Flow.Nodes
    .Select(n => n.Id == "2" ? n with { Position = new XYPosition(300, 120) } : n)
    .ToList();
```

`FlowView` détecte les nodes dont la référence a changé et ne met à jour que ceux-là (et les edges concernés) : une liste de 1 000 nodes dont un seul change reste fluide.

## Mode non contrôlé (par défaut)

`AutoApplyChanges="True"` : quand l'utilisateur déplace un node, `FlowView` produit un `NodePositionChange`, l'applique avec `Changes.Apply` et réaffecte `Nodes`. Vous n'avez rien à faire ; relisez `Flow.Nodes` quand vous en avez besoin. C'est l'équivalent de `useNodesState` / `useEdgesState`.

Pour être prévenu :

```csharp
Flow.NodeChangesRequested += (_, e) =>
{
    foreach (var change in e.Changes)
        if (change is NodeRemoveChange remove) Console.WriteLine($"Suppression de {remove.Id}");
};
Flow.ConnectRequested += (_, e) => Console.WriteLine($"{e.Connection.Source} -> {e.Connection.Target}");
```

## Mode contrôlé

Pour que **votre** code possède l'état (store centralisé, validation, journal d'actions) :

```csharp
Flow.AutoApplyChanges = false;

Flow.NodeChangesRequested += (_, e) => _store.Nodes = Changes.Apply(e.Changes, _store.Nodes);
Flow.EdgeChangesRequested += (_, e) => _store.Edges = Changes.Apply(e.Changes, _store.Edges);
Flow.ConnectRequested     += (_, e) => _store.AddEdge(e.Connection);

// et dans le XAML : Nodes="{Binding Nodes}" Edges="{Binding Edges}"
```

Dans ce mode, `FlowView` ne modifie rien lui-même : les connexions n'ajoutent aucun edge : c'est à vous de le faire dans `ConnectRequested`, et Ctrl+Z / Ctrl+Y sont désactivés (l'historique est à vous).

## Binding MVVM

`Nodes`, `Edges` et `Viewport` sont des propriétés de dépendance liables en mode `TwoWay` :

```xml
<flow:FlowView Nodes="{Binding Nodes, Mode=TwoWay}"
               Edges="{Binding Edges, Mode=TwoWay}"
               Viewport="{Binding Viewport, Mode=TwoWay}" />
```

Elles portent des `IReadOnlyList<T>`, pas des `ObservableCollection` : remplacez la liste entière quand elle change.

## Changes disponibles

| Change | Émis quand |
|---|---|
| `NodePositionChange` | un node est déplacé (souris ou flèches) |
| `NodeDimensionsChange` | un node a été mesuré ou a changé de taille |
| `NodeResizeChange` | un node est redimensionné avec `NodeResizer` |
| `NodeSelectChange` / `EdgeSelectChange` | la sélection change |
| `NodeRemoveChange` / `EdgeRemoveChange` | suppression (touche Suppr, y compris les descendants et les edges liés) |
| `NodeAddChange` / `EdgeAddChange` | une connexion est créée |
| `NodeReplaceChange` / `EdgeReplaceChange` | un edge est reconnecté |

Détail dans la [référence](../reference/changes.md).

## Annuler / rétablir

`Undo()` et `Redo()` (ou Ctrl+Z / Ctrl+Y) gardent 100 niveaux : l'état est enregistré avant un déplacement, une connexion, une reconnexion ou une suppression. Les listes étant immuables, un instantané ne coûte presque rien. `CanUndo` / `CanRedo` permettent d'activer vos boutons.

## Contrôler la vue

```csharp
Flow.ZoomIn();  Flow.ZoomOut();  Flow.ZoomBy(1.5);
Flow.FitView();
Flow.Viewport = new Viewport(0, 0, 1);
Flow.IsLocked = true;            // plus de déplacement, connexion ni suppression (pan et zoom restent actifs)
```

Suivant : [Nodes personnalisés](04-custom-nodes.md).
