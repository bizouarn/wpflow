# 1. Démarrage rapide

*Équivalent React Flow : Quick Start.*

## Installation

Ajoutez une référence au projet (ou au package NuGet `Wpflow.Wpf`, qui dépend de `Wpflow.Core`) :

```xml
<ProjectReference Include="..\src\Wpflow.Wpf\Wpflow.Wpf.csproj" />
```

Prérequis : .NET 8 ou supérieur, Windows.

## Votre premier flow

**XAML**

```xml
<Window x:Class="MonApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:flow="clr-namespace:Wpflow.Wpf;assembly=Wpflow.Wpf"
        Title="Mon flow" Width="900" Height="600">

    <flow:FlowView x:Name="Flow" FitViewOnInit="True">
        <!-- Le contenu de FlowView est un overlay affiché au-dessus du graphe. -->
        <Grid>
            <flow:MiniMap />
            <flow:FlowControls />
        </Grid>
    </flow:FlowView>
</Window>
```

> `FlowView` n'accepte **qu'un seul** enfant (c'est un `ContentControl`). Pour plusieurs overlays, enveloppez-les dans un `Grid`, comme ci-dessus.

**C#**

```csharp
using Wpflow.Core;

public MainWindow()
{
    InitializeComponent();

    Flow.Nodes =
    [
        new Node("1", new XYPosition(0, 0),    "input",   "Début"),
        new Node("2", new XYPosition(250, 80), "default", "Étape"),
        new Node("3", new XYPosition(500, 0),  "output",  "Fin"),
    ];

    Flow.Edges =
    [
        new Edge("e1-2", "1", "2", Animated: true),
        new Edge("e2-3", "2", "3", Type: "smoothstep", Label: "ok", MarkerEnd: EdgeMarker.ArrowClosed),
    ];
}
```

Lancez l'application : vous pouvez déplacer les nodes, relier les petits cercles (handles), zoomer à la molette, déplacer la vue en glissant le fond, supprimer avec la touche Suppr, annuler avec Ctrl+Z. Aucun autre code n'est nécessaire.

## Ce qu'il faut retenir

- Un **node** a un `Id`, une `Position` (coordonnées « flow »), un `Type` et des `Data`.
- Un **edge** relie deux ids de nodes (`Source` → `Target`).
- `Nodes` et `Edges` sont des **listes immuables** : pour les modifier, affectez une nouvelle liste (voir [Construire un flow](03-building-a-flow.md)).
- Les types intégrés sont `default`, `input`, `output` et `group`.

Étape suivante : [Termes et concepts](02-concepts.md).
