# 5. Edges

*Équivalent React Flow : Custom Edges, Edge Labels, Markers, Reconnecting Edges.*

## Types d'edges

| `Edge.Type` | Tracé | Chemin Core |
|---|---|---|
| `default` | Courbe de Bézier | `EdgePaths.Bezier` |
| `straight` | Ligne droite | `EdgePaths.Straight` |
| `step` | Angles droits | `EdgePaths.Step` |
| `smoothstep` | Angles droits arrondis (rayon 5) | `EdgePaths.SmoothStep` |

```csharp
new Edge("e1", "a", "b", Type: "smoothstep")
```

Le tracé tient compte du côté de chaque handle (`Left`, `Right`, `Top`, `Bottom`) : un edge qui part du bas d'un node et arrive à gauche d'un autre contourne correctement.

## Handles précis

Quand un node a plusieurs handles, désignez-les par leur `HandleId` :

```csharp
new Edge("e", "source", "cible", SourceHandle: "sortie-1", TargetHandle: "entrée-2")
```

Sans identifiant, c'est le premier handle du bon type du node qui est utilisé.

## Animation, marqueurs, label

```csharp
new Edge("e", "a", "b",
    Animated: true,                              // pointillés qui défilent
    MarkerEnd: EdgeMarker.ArrowClosed,           // flèche pleine côté cible
    MarkerStart: EdgeMarker.Arrow,               // flèche ouverte côté source
    Label: "valide")
```

La flèche pointe toujours vers le node, quel que soit le côté du handle.

## Contenu libre dans un label (équivalent d'`EdgeLabelRenderer`)

Définissez un `DataTemplate` dont le `DataContext` est l'`Edge` et affectez-le à `EdgeLabelTemplate` :

```xml
<DataTemplate x:Key="PillLabel">
    <Border x:Name="pill" Background="#FF0071" CornerRadius="9" Padding="8,2">
        <TextBlock Text="{Binding Label}" Foreground="White" FontSize="10" />
    </Border>
    <DataTemplate.Triggers>
        <DataTrigger Binding="{Binding Label}" Value="{x:Null}">
            <Setter TargetName="pill" Property="Visibility" Value="Collapsed" />
        </DataTrigger>
    </DataTemplate.Triggers>
</DataTemplate>

<flow:FlowView EdgeLabelTemplate="{StaticResource PillLabel}" … />
```

Le modèle est appliqué à **tous** les edges (masquez-le avec un `DataTrigger` quand il n'y a pas de label, comme ci-dessus). Le contenu est centré sur le milieu du tracé (`EdgePath.LabelX/LabelY`).

## Style des edges

| Propriété de `FlowView` | Effet |
|---|---|
| `EdgeStroke` | Couleur (sinon `Flow.Edge` du thème) |
| `EdgeStrokeThickness` | Épaisseur (1 par défaut) |

Un edge sélectionné utilise `Flow.EdgeSelected`.

## Reconnecter un edge

Attrapez un edge **près d'une de ses extrémités** (à moins de 24 px) et déposez-le sur un autre handle : l'edge garde son `Id`, seules ses extrémités changent (`EdgeReplaceChange`). La reconnexion respecte `ConnectionMode`, `IsValidConnection` et refuse de créer un doublon.

## Tracer vous-même

Les chemins sont des chaînes au format SVG, directement utilisables en WPF :

```csharp
EdgePath path = EdgePaths.SmoothStep(source, Position.Right, target, Position.Left, borderRadius: 10, offset: 30);
var geometry = Geometry.Parse(path.Path);
// path.LabelX, path.LabelY : centre du tracé ; path.OffsetX/OffsetY : distance depuis la source
```

Ces fonctions sont un port de `getBezierPath` / `getSmoothStepPath` de `@xyflow/system`, comparées au code source de la bibliothèque. Voir la [référence](../reference/utils.md#edgepaths).

## Limites actuelles

Les types d'edges sont ceux ci-dessus : il n'y a pas encore d'`edgeTypes` personnalisables (c'est-à-dire de tracé entièrement défini par l'utilisateur).

Suivant : [Sous-flows et groupes](06-sub-flows.md).
