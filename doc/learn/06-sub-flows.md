# 6. Sous-flows et groupes

*Équivalent React Flow : Sub Flows.*

Un node peut en contenir d'autres : c'est un **groupe** (type `group`) et ses **enfants**.

```csharp
Flow.Nodes =
[
    new Node("g1", new XYPosition(0, 0), "group", "Groupe A", FixedWidth: 360, FixedHeight: 230),

    // Les positions des enfants sont RELATIVES au parent.
    new Node("c1", new XYPosition(20, 40),  "default", "Enfant 1", ParentId: "g1", ExtentParent: true),
    new Node("c2", new XYPosition(180, 130), "default", "Enfant 2", ParentId: "g1", ExtentParent: true),

    new Node("libre", new XYPosition(520, 70), "output", "Hors groupe"),
];
Flow.Edges = [ new Edge("e1", "c1", "c2"), new Edge("e2", "c2", "libre") ];
```

| Propriété | Effet |
|---|---|
| `ParentId` | Fait de ce node l'enfant du node portant cet identifiant. `Position` devient relative au coin du parent. |
| `ExtentParent` | L'enfant ne peut pas sortir du parent (équivalent de `extent: 'parent'`). |
| `FixedWidth` / `FixedHeight` | Taille du groupe. |

## Comportements

- **Déplacer** un parent déplace tous ses descendants ; si parent et enfant sont sélectionnés ensemble, l'enfant n'est pas déplacé deux fois.
- **Supprimer** un parent supprime ses descendants (et leurs edges).
- **Empilement** : un parent est affiché sous ses enfants (le z-index suit la profondeur). Un groupe sélectionné ne passe pas devant ses enfants.
- Les edges relient n'importe quels nodes, dans ou hors d'un groupe ; ils sont dessinés dans le référentiel absolu.
- Les imbrications profondes (petits-enfants…) sont supportées. Un `ParentId` introuvable ou un cycle fait traiter le node comme une racine.

## Apparence

Le type `group` intégré est une zone translucide à bord fin (clés `Flow.GroupBackground` et `Flow.NodeBorder`), avec `Data` affiché comme titre en haut à gauche. Pour un groupe personnalisé, définissez un `DataTemplate` de clé `wpflow-node-group`, ou utilisez un autre type : n'importe quel node peut avoir des enfants dès lors qu'il a une taille.

## Côté Core

`NodeTree` expose les calculs, utilisables sans interface :

```csharp
var abs = NodeTree.AbsolutePositions(nodes);           // Id -> position absolue
var depths = NodeTree.Depths(nodes);                   // Id -> profondeur
var all = NodeTree.Descendants("g1", nodes);           // tous les descendants
var clamped = NodeTree.ClampToParent(child, wanted, parent);
```

`GraphUtils.GetNodesBounds` travaille aussi en coordonnées absolues.

Suivant : [Disposition automatique](07-layouting.md).
