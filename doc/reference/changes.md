# Changes

Une *change* est une modification atomique demandée par l'interface. Les changes sont des `record`, regroupés en deux familles abstraites : `NodeChange` et `EdgeChange`.

## NodeChange

| Type | Champs | Effet de `Changes.Apply` |
|---|---|---|
| `NodePositionChange` | `Id`, `Position` | Remplace la position (relative au parent). |
| `NodeDimensionsChange` | `Id`, `Width`, `Height` | Remplace la taille **mesurée**. |
| `NodeResizeChange` | `Id`, `Width`, `Height`, `Position?` | Fixe `FixedWidth/Height` **et** `Width/Height` ; change la position si elle est fournie. |
| `NodeSelectChange` | `Id`, `Selected` | Change la sélection. |
| `NodeInteractionChange` | `Id`, `Dragging?`, `Resizing?` | Un déplacement ou redimensionnement commence ou finit ; `null` laisse la valeur. Émis par la vue. |
| `NodeRemoveChange` | `Id` | Retire le node. |
| `NodeAddChange` | `Item` | Ajoute le node en fin de liste. |
| `NodeReplaceChange` | `Id`, `Item` | Remplace le node. |

## EdgeChange

| Type | Champs | Effet |
|---|---|---|
| `EdgeSelectChange` | `Id`, `Selected` | Change la sélection. |
| `EdgeRemoveChange` | `Id` | Retire l'edge. |
| `EdgeAddChange` | `Item` | Ajoute l'edge. |
| `EdgeReplaceChange` | `Id`, `Item` | Remplace l'edge (reconnexion). |

## Changes.Apply

```csharp
public static IReadOnlyList<Node> Apply(IEnumerable<NodeChange> changes, IReadOnlyList<Node> nodes);
public static IReadOnlyList<Edge> Apply(IEnumerable<EdgeChange> changes, IReadOnlyList<Edge> edges);
```

- Renvoie une **nouvelle** liste ; l'entrée n'est jamais modifiée.
- Les changes sont appliqués dans l'ordre.
- Un `Id` inconnu est ignoré.
- Les nodes non concernés conservent la même référence (ce qui permet à `FlowView` de ne mettre à jour que le nécessaire).

```csharp
var nodes = Changes.Apply([ new NodePositionChange("a", new XYPosition(10, 20)),
                            new NodeSelectChange("a", true) ], Flow.Nodes);
```

## Quand l'interface les émet

Voir le tableau de [Construire un flow](../learn/03-building-a-flow.md#changes-disponibles).
