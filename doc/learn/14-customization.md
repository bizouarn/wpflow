# 14. Personnaliser sans toucher à la bibliothèque

*Équivalent React Flow : Custom Nodes, Custom Edges, Custom Markers, `nodeTypes` / `edgeTypes`.*

Tout ce qui s'affiche se remplace avec des **ressources WPF** (dans `FlowView.Resources`, la fenêtre ou l'application), sans sous-classe ni réflexion. Les clés suivent toutes la même convention :

| Ce qu'on remplace | Clé de ressource | Type de la ressource |
|---|---|---|
| Un type de node (même les intégrés) | `wpflow-node-{type}` | `DataTemplate` (DataContext = `Node`) |
| Le **tracé** d'un type d'edge | `wpflow-edge-{type}` | `IEdgePathProvider` |
| Le **rendu complet** d'un type d'edge | `wpflow-edge-template-{type}` | `DataTemplate` (DataContext = `EdgeRenderContext`) |
| Le label d'un type d'edge | `wpflow-edge-label-{type}` | `DataTemplate` (DataContext = `Edge`) |
| Un marqueur | `wpflow-marker-{clé}` | `Geometry` |
| L'apparence des handles | style implicite `flow:Handle` | `Style` (taille, `Template`) |

Le démonstrateur de cette page est l'onglet **Personnalisation** des exemples (`demo/wpf/CustomizationExample.xaml`, `CustomEdges.cs`).

## Redéfinir un node intégré

`default`, `input`, `output` et `group` sont des types comme les autres : une ressource `wpflow-node-default` remplace le node par défaut pour tout le flow.

```xml
<flow:FlowView>
    <flow:FlowView.Resources>
        <DataTemplate x:Key="wpflow-node-default">
            <Grid>
                <Border Background="{DynamicResource Flow.NodeBackground}" CornerRadius="0" Padding="12">
                    <TextBlock Text="{Binding Data}" />
                </Border>
                <flow:Handle Type="Target" Position="Left" />
                <flow:Handle Type="Source" Position="Right" />
            </Grid>
        </DataTemplate>
    </flow:FlowView.Resources>
</flow:FlowView>
```

Un template remplace tout le node, **handles compris** : placez-y les vôtres.

## Handles

Un `Handle` se dessine lui-même (8 × 8, disque). Pour le changer, un style implicite suffit ; la taille se règle par `Width` / `Height`, la forme par un `Template` :

```xml
<Style TargetType="flow:Handle">
    <Setter Property="Width" Value="14" />
    <Setter Property="Height" Value="14" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="flow:Handle">
                <Rectangle Fill="#FF0071" Stroke="White" StrokeThickness="1.5" RenderTransformOrigin="0.5,0.5">
                    <Rectangle.RenderTransform><RotateTransform Angle="45" /></Rectangle.RenderTransform>
                </Rectangle>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

La zone de clic reste celle du handle, élargie de 6 px, et la position sur le bord du node suit la taille choisie. Sans `Template`, le handle garde son dessin direct (pas d'élément visuel en plus).

## Types d'edges

Deux niveaux, selon ce qu'il faut changer.

### Un autre tracé : `IEdgePathProvider`

Le fournisseur ne calcule que le chemin (une chaîne SVG et la position du label) ; la bibliothèque dessine le trait, les flèches, l'animation, la sélection et la zone de clic, exactement comme pour un type intégré. C'est le chemin **rapide**.

```csharp
public sealed class WaveEdge : IEdgePathProvider
{
    public EdgePath GetPath(EdgeGeometry g)      // g.Source, g.SourcePosition, g.Target, g.TargetPosition
    {
        // ... construire le chemin SVG ...
        return new EdgePath(path, labelX, labelY, offsetX, offsetY);
    }
}
```

```xml
<demo:WaveEdge x:Key="wpflow-edge-wave" />
```

```csharp
new Edge("e1", "a", "b", Type: "wave")
```

`g.Edge` donne l'edge tracé : le tracé peut dépendre de ses données (`Data`, handles, sélection), et un seul type suffit alors là où il en faudrait un par variante. Il vaut `null` quand il n'y a pas d'edge, par exemple si vous appelez le fournisseur vous-même sans le préciser.

```csharp
new DelegateEdgePathProvider(g => g.Edge?.Data is Relation { IsSelfReference: true }
    ? LoopPath(g)                                  // votre tracé en boucle
    : EdgePathProviders.ForType("smoothstep").GetPath(g));
```

`DelegateEdgePathProvider(g => …)` évite de créer une classe. `EdgePaths.Bezier`, `Step`, `SmoothStep`, `Straight` sont des briques utilisables dans votre fournisseur, et `EdgePathProviders.ForType("step")` donne les fournisseurs intégrés (on peut aussi **surcharger** un type intégré : `wpflow-edge-straight`).

### Un autre rendu : `wpflow-edge-template-{type}`

Pour dessiner l'edge autrement qu'avec un seul trait (halo, plusieurs traits, formes…), un `DataTemplate` reçoit un `EdgeRenderContext` :

| Propriété | Contenu |
|---|---|
| `Edge`, `IsSelected`, `IsAnimated` | L'edge et son état |
| `Geometry`, `PathData` | Le chemin calculé (par le fournisseur du même type, sinon le bézier) |
| `Start`, `End`, `StartSide`, `EndSide`, `LabelPosition` | Extrémités et côtés des handles |
| `Stroke`, `StrokeThickness` | Brosse (déjà celle de la sélection si sélectionné) et épaisseur du thème |

```xml
<DataTemplate x:Key="wpflow-edge-template-pulse">
    <Canvas>
        <Path Data="{Binding Geometry}" Stroke="{Binding Stroke}" StrokeThickness="9" Opacity="0.22" />
        <Path Data="{Binding Geometry}" Stroke="{Binding Stroke}" StrokeThickness="{Binding StrokeThickness}" />
    </Canvas>
</DataTemplate>
```

Le clic, la sélection et la reconnexion fonctionnent comme pour les autres edges, sur les éléments dessinés par votre template. Un template coûte un vrai arbre visuel par edge : gardez-le pour les types qui en ont besoin (les types sans template restent dessinés directement).

### Labels par type

`wpflow-edge-label-{type}` définit le label d'un seul type ; à défaut, `FlowView.EdgeLabelTemplate` (global) s'applique, et à défaut le texte de `Edge.Label`.

## Marqueurs

`Edge.MarkerEnd` / `MarkerStart` : `Arrow`, `ArrowClosed`. Pour un autre marqueur, `Edge.MarkerEndKey` / `MarkerStartKey` désignent une **géométrie** en ressource :

```xml
<Geometry x:Key="wpflow-marker-dot">M-8,0 A4,4 0 1 1 0,0 A4,4 0 1 1 -8,0 Z</Geometry>
```

```csharp
new Edge("e", "a", "b", MarkerEndKey: "dot")
```

La **pointe est en (0, 0) et la forme regarde vers +x** (comme les flèches intégrées, qui sont tournées selon le côté du handle). Une forme dont toutes les figures sont fermées est remplie avec la couleur de l'edge ; une forme ouverte est un simple trait. Une clé introuvable retombe sur `MarkerEnd` / `MarkerStart`. Les clés survivent à l'export JSON (`markerEnd: { type: "dot" }`).

## Réglages d'apparence

| Propriété de `FlowView` | Défaut | Rôle |
|---|---|---|
| `EdgeHitWidth` | 10 | Largeur de la zone cliquable autour d'un edge |
| `SelectionOutlineGap` | 3 | Écart du contour de sélection (nodes à template) |
| `SelectionOutlineThickness` | 1,5 | Épaisseur de ce contour |
| `ConnectionLineStyle` | gris, 1,5 | `Style` (`TargetType="Path"`) de l'aperçu de la connexion en cours |
| `EdgeStroke`, `EdgeStrokeThickness` | thème, 1 | Trait des edges |

Les couleurs viennent des clés `Flow.*` (voir [Thèmes](08-theming.md)).

Retour à l'[index](../README.md).
