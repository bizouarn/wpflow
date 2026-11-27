# 12. Saving and JSON

*React Flow equivalent: Save and Restore (`toObject`).*

`GraphSerializer` reads and writes a document of the same shape as React Flow's `toObject()`: `{ nodes, edges, viewport }`. It lets you save a graph, restore it, or exchange data with a React Flow application.

```csharp
// Save
string json = GraphSerializer.ToJson(Flow.Nodes, Flow.Edges, Flow.Viewport);
File.WriteAllText("flow.json", json);

// Restore
FlowGraph graph = GraphSerializer.FromJson(File.ReadAllText("flow.json"));
Flow.Nodes = graph.Nodes;
Flow.Edges = graph.Edges;
Flow.Viewport = graph.Viewport;
```

`ToJson(nodes, edges, viewport, indented: true)` writes indented JSON; pass `indented: false` for a single line.

## Format

```json
{
  "nodes": [
    { "id": "1", "type": "input", "position": { "x": 250, "y": 5 }, "data": { "label": "Input" } },
    { "id": "g", "type": "group", "position": { "x": 0, "y": 0 }, "width": 300, "height": 200 },
    { "id": "2", "position": { "x": 20, "y": 30 }, "data": "Child", "parentId": "g", "extent": "parent" }
  ],
  "edges": [
    { "id": "e1-2", "source": "1", "target": "2", "animated": true, "type": "smoothstep",
      "label": "ok", "markerEnd": { "type": "arrowclosed" } }
  ],
  "viewport": { "x": 0, "y": 0, "zoom": 1 }
}
```

### Field mapping

| JSON | Wpflow |
|---|---|
| `id`, `type`, `position` | `Node.Id`, `Type` (default `default`), `Position` |
| `data` | `Node.Data` |
| `width`, `height` | `Node.FixedWidth`, `FixedHeight` |
| `selected`, `draggable`, `selectable` | `Selected`, `Draggable`, `Selectable` |
| `parentId`, `extent: "parent"` | `ParentId`, `ExtentParent` |
| `source`, `target`, `sourceHandle`, `targetHandle` | same names on `Edge` |
| `animated`, `label`, `selected`, `type` | same names |
| `markerEnd`, `markerStart` | `MarkerEnd`, `MarkerStart` — string (`"arrowclosed"`) or object `{ "type": "arrowclosed" }`, values `arrow` and `arrowclosed` |
| `viewport.x/y/zoom` | `Viewport` |

Missing fields take their default value. A node or an edge without an `id` (or an edge without `source` / `target`) throws a `JsonException`.

## About `Data`

- **When writing**, `Node.Data` is serialised with `System.Text.Json`: simple types, dictionaries, public objects. A ViewModel that is not serialisable (controls, commands…) must be converted beforehand.
- **When reading**, `Data` comes back as a `System.Text.Json.JsonElement`. Convert it to your type:

```csharp
Flow.Nodes = graph.Nodes
    .Select(n => n.Data is JsonElement j ? n with { Data = j.Deserialize<MyData>() } : n)
    .ToList();
```

The default node displays `Data.ToString()`: for a `JsonElement` holding a string, extract the text (`j.GetString()`) before displaying it.

## Complete example

The examples window (« Options and JSON » tab) exports and re-imports the displayed graph; the self-test checks that positions and identifiers are identical after the round trip.

Next: [MVVM](13-mvvm.md).
