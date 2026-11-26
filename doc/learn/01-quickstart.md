# 1. Démarrage rapide

*Équivalent React Flow : Quick Start.*

## Installation

Ajoutez une référence au projet (ou au package NuGet `Wpflow.Wpf`, qui dépend de `Wpflow.Core`) :

```xml
<ProjectReference Include="..\src\Wpflow.Wpf\Wpflow.Wpf.csproj" />
```

Prérequis : Windows, avec .NET 8 ou supérieur, ou .NET Framework 4.7.2 ou supérieur.

| Paquet | Cibles |
|---|---|
| `Wpflow.Core` | `net8.0`, `net472`, `netstandard2.0` (.NET Core 2.0 et suivants) |
| `Wpflow.Wpf` | `net8.0-windows`, `net472` |
| `Wpflow.Blazor` | `net8.0` |

WPF n'existe sur .NET Core qu'à partir de .NET Core 3.0 : en .NET Core 2.x, seul le noyau `Wpflow.Core` est utilisable.

Particularités des anciennes cibles :

- **.NET Framework 4.7.2** : un projet au format SDK a besoin de `<LangVersion>latest</LangVersion>` pour utiliser les records du noyau avec la syntaxe C# actuelle (`with`, `init`). Les annonces de sélection aux lecteurs d'écran ne sont pas émises (l'API UI Automation n'existe pas dans ce framework).
- **.NET Core 2.x** : la dépendance `System.Text.Json` 8 bloque la construction (« System.Runtime.CompilerServices.Unsafe doesn't support netcoreapp2.0 »), Microsoft ne la prenant pas en charge avant .NET Core 3.1. Pour passer outre, ajoutez `<SuppressTfmSupportBuildWarnings>true</SuppressTfmSupportBuildWarnings>` au projet de l'application ; ce réglage vaut pour toutes ses dépendances. .NET Core 2.x n'est plus maintenu.

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
