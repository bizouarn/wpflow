# 3. Building a flow

*React Flow equivalent: Adding Interactivity, State Management.*

## The model: immutable lists and changes

```csharp
public sealed record Node(string Id, XYPosition Position, string Type = "default", object? Data = null, …);
public sealed record Edge(string Id, string Source, string Target, …);
```

To modify a node, create a copy of it:

```csharp
Flow.Nodes = Flow.Nodes
    .Select(n => n.Id == "2" ? n with { Position = new XYPosition(300, 120) } : n)
    .ToList();
```

`FlowView` detects the nodes whose reference has changed and only updates those (and the edges concerned): a list of 1,000 nodes in which only one changes stays smooth.

## Uncontrolled mode (default)

`AutoApplyChanges="True"`: when the user moves a node, `FlowView` produces a `NodePositionChange`, applies it with `Changes.Apply` and reassigns `Nodes`. You have nothing to do; read `Flow.Nodes` whenever you need it. This is the equivalent of `useNodesState` / `useEdgesState`.

To be notified:

```csharp
Flow.NodeChangesRequested += (_, e) =>
{
    foreach (var change in e.Changes)
        if (change is NodeRemoveChange remove) Console.WriteLine($"Removed {remove.Id}");
};
Flow.ConnectRequested += (_, e) => Console.WriteLine($"{e.Connection.Source} -> {e.Connection.Target}");
```

## Controlled mode

For **your** code to own the state (centralised store, validation, action log):

```csharp
Flow.AutoApplyChanges = false;

Flow.NodeChangesRequested += (_, e) => _store.Nodes = Changes.Apply(e.Changes, _store.Nodes);
Flow.EdgeChangesRequested += (_, e) => _store.Edges = Changes.Apply(e.Changes, _store.Edges);
Flow.ConnectRequested     += (_, e) => _store.AddEdge(e.Connection);

// and in the XAML: Nodes="{Binding Nodes}" Edges="{Binding Edges}"
```

In this mode, `FlowView` does not change anything itself: connections do not add any edge — it is up to you to do so in `ConnectRequested` — and Ctrl+Z / Ctrl+Y are disabled (the history is yours).

## MVVM binding

`Nodes`, `Edges` and `Viewport` are dependency properties that can be bound in `TwoWay` mode:

```xml
<flow:FlowView Nodes="{Binding Nodes, Mode=TwoWay}"
               Edges="{Binding Edges, Mode=TwoWay}"
               Viewport="{Binding Viewport, Mode=TwoWay}" />
```

They hold `IReadOnlyList<T>`, not `ObservableCollection`s: replace the whole list when it changes.

## Available changes

| Change | Emitted when |
|---|---|
| `NodePositionChange` | a node is moved (mouse or arrow keys) |
| `NodeDimensionsChange` | a node has been measured or has changed size |
| `NodeResizeChange` | a node is resized with `NodeResizer` |
| `NodeSelectChange` / `EdgeSelectChange` | the selection changes |
| `NodeRemoveChange` / `EdgeRemoveChange` | deletion (Delete key, including descendants and linked edges) |
| `NodeAddChange` / `EdgeAddChange` | a connection is created |
| `NodeReplaceChange` / `EdgeReplaceChange` | an edge is reconnected |

Details in the [reference](../reference/changes.md).

## Undo / redo

`Undo()` and `Redo()` (or Ctrl+Z / Ctrl+Y) keep 100 levels: the state is saved before a move, a connection, a reconnection or a deletion. Since the lists are immutable, a snapshot costs almost nothing. `CanUndo` / `CanRedo` let you enable your buttons.

## Controlling the view

```csharp
Flow.ZoomIn();  Flow.ZoomOut();  Flow.ZoomBy(1.5);
Flow.FitView();
Flow.Viewport = new Viewport(0, 0, 1);
Flow.IsLocked = true;            // no moving, connecting or deleting (pan and zoom stay active)
```

Next: [Custom nodes](04-custom-nodes.md).
