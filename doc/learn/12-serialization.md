# 12. Sauvegarde et JSON

*Équivalent React Flow : Save and Restore (`toObject`).*

`GraphSerializer` lit et écrit un document de la même forme que `toObject()` de React Flow : `{ nodes, edges, viewport }`. Il permet de sauvegarder un graphe, de le restaurer, ou d'échanger avec une application React Flow.

```csharp
// Sauvegarder
string json = GraphSerializer.ToJson(Flow.Nodes, Flow.Edges, Flow.Viewport);
File.WriteAllText("flow.json", json);

// Restaurer
FlowGraph graph = GraphSerializer.FromJson(File.ReadAllText("flow.json"));
Flow.Nodes = graph.Nodes;
Flow.Edges = graph.Edges;
Flow.Viewport = graph.Viewport;
```

`ToJson(nodes, edges, viewport, indented: true)` écrit un JSON indenté ; passez `indented: false` pour une ligne.

## Format

```json
{
  "nodes": [
    { "id": "1", "type": "input", "position": { "x": 250, "y": 5 }, "data": { "label": "Input" } },
    { "id": "g", "type": "group", "position": { "x": 0, "y": 0 }, "width": 300, "height": 200 },
    { "id": "2", "position": { "x": 20, "y": 30 }, "data": "Enfant", "parentId": "g", "extent": "parent" }
  ],
  "edges": [
    { "id": "e1-2", "source": "1", "target": "2", "animated": true, "type": "smoothstep",
      "label": "ok", "markerEnd": { "type": "arrowclosed" } }
  ],
  "viewport": { "x": 0, "y": 0, "zoom": 1 }
}
```

### Correspondance des champs

| JSON | Wpflow |
|---|---|
| `id`, `type`, `position` | `Node.Id`, `Type` (défaut `default`), `Position` |
| `data` | `Node.Data` |
| `width`, `height` | `Node.FixedWidth`, `FixedHeight` |
| `selected`, `draggable`, `selectable` | `Selected`, `Draggable`, `Selectable` |
| `parentId`, `extent: "parent"` | `ParentId`, `ExtentParent` |
| `source`, `target`, `sourceHandle`, `targetHandle` | idem sur `Edge` |
| `animated`, `label`, `selected`, `type` | idem |
| `markerEnd`, `markerStart` | `MarkerEnd`, `MarkerStart` — chaîne (`"arrowclosed"`) ou objet `{ "type": "arrowclosed" }`, valeurs `arrow` et `arrowclosed` |
| `viewport.x/y/zoom` | `Viewport` |

Les champs absents prennent leur valeur par défaut. Un node ou un edge sans `id` (ou un edge sans `source` / `target`) lève une `JsonException`.

## À propos de `Data`

- **À l'écriture**, `Node.Data` est sérialisé avec `System.Text.Json` : types simples, dictionnaires, objets publics. Un ViewModel qui n'est pas sérialisable (contrôles, commandes…) doit être converti avant.
- **À la lecture**, `Data` revient en `System.Text.Json.JsonElement`. Convertissez-le vers votre type :

```csharp
Flow.Nodes = graph.Nodes
    .Select(n => n.Data is JsonElement j ? n with { Data = j.Deserialize<MesDonnees>() } : n)
    .ToList();
```

Le node par défaut affiche `Data.ToString()` : pour un `JsonElement` de type chaîne, extrayez le texte (`j.GetString()`) avant d'afficher.

## Exemple complet

La fenêtre d'exemples (onglet « Options et JSON ») exporte et réimporte le graphe affiché ; l'auto-test vérifie que positions et identifiants sont identiques après l'aller-retour.

Suivant : [MVVM](13-mvvm.md).
