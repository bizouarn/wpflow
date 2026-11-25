# 7. Disposition automatique

*Équivalent React Flow : Layouting.*

React Flow ne fournit pas de moteur de disposition : ses exemples utilisent dagre, d3-hierarchy ou ELK. Wpflow fournit une disposition simple, sans dépendance, dans `Wpflow.Core` :

```csharp
Dictionary<string, XYPosition> positions =
    GraphLayout.LayeredLeftToRight(Flow.Nodes, Flow.Edges, horizontalGap: 90, verticalGap: 30);

Flow.Nodes = Flow.Nodes.Select(n => n with { Position = positions[n.Id] }).ToList();
Flow.Dispatcher.BeginInvoke(() => Flow.FitView(), DispatcherPriority.Loaded);   // après la mise en page
```

## Principe

- La **couche** d'un node est la longueur du plus long chemin qui y mène (les cycles sont coupés) ; les racines sont en couche 0.
- Les couches sont placées de gauche à droite ; la largeur d'une couche est celle de son node le plus large.
- Dans une couche, les nodes sont empilés avec `verticalGap` et centrés verticalement autour de 0.
- Les nodes non mesurés utilisent `defaultWidth` / `defaultHeight` (150 × 40).

Appelez la disposition **après** que les nodes ont été mesurés si vous voulez leurs vraies tailles (`Node.Width` / `Height` non nuls).

## Conseils

- `GraphLayout.Layered(nodes, edges, LayoutDirection.TopToBottom)` (ou `RightToLeft`, `BottomToTop`) dispose dans la direction voulue ; donnez alors aux handles les positions correspondantes (`Node.SourcePosition` / `TargetPosition` pour les nodes par défaut).
- Les edges `smoothstep` ou `step` se prêtent mieux aux dispositions en couches que les courbes de Bézier.
- Pour des graphes complexes (réduction de croisements, groupes), branchez un moteur externe : il suffit de produire un `Dictionary<string, XYPosition>` et de l'appliquer comme ci-dessus.

Voir l'exemple « Disposition auto » de la [fenêtre d'exemples](../examples/README.md#disposition-automatique).

Suivant : [Thèmes et styles](08-theming.md).
