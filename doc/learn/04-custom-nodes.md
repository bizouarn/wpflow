# 4. Nodes personnalisés

*Équivalent React Flow : Custom Nodes, Handles, Node Resizer, Node Toolbar.*

## Un node = un DataTemplate

Un node de type `"carte"` est rendu avec le `DataTemplate` de clé `wpflow-node-carte`. Déclarez-le dans n'importe quelles ressources (fenêtre, `App.xaml`, `FlowView.Resources`). Le `DataContext` est le `Node` ; placez votre ViewModel dans `Node.Data`.

```xml
<Window.Resources>
    <DataTemplate x:Key="wpflow-node-carte">
        <Grid>
            <Border Background="{DynamicResource Flow.NodeBackground}"
                    BorderBrush="{DynamicResource Flow.NodeBorder}" BorderThickness="1"
                    CornerRadius="0" Padding="12">
                <StackPanel>
                    <TextBlock Text="{Binding Data.Titre}" FontWeight="SemiBold"
                               Foreground="{DynamicResource Flow.NodeText}" />
                    <Slider Minimum="0" Maximum="100" Value="{Binding Data.Valeur, Mode=TwoWay}" />
                </StackPanel>
            </Border>

            <flow:Handle Type="Target" Position="Left" />
            <flow:Handle Type="Source" Position="Right" />
        </Grid>
    </DataTemplate>
</Window.Resources>
```

```csharp
public sealed class CarteVm : INotifyPropertyChanged { /* Titre, Valeur… */ }

Flow.Nodes = [ new Node("a", new XYPosition(0, 0), "carte", new CarteVm { Titre = "Volume" }) ];
```

Les contrôles WPF (Slider, TextBox, ComboBox, boutons…) fonctionnent normalement dans un node : ils marquent leurs événements souris comme traités, donc interagir avec eux ne déplace pas le node. Cliquer ou glisser sur une zone « vide » du node le déplace.

> **Mettre à jour les données.** Faites de `Node.Data` un objet qui implémente `INotifyPropertyChanged` : le binding `Data.Valeur` se met à jour sans recréer le node. Le `DataContext` (le `Node`) est remplacé quand le node change, et les bindings se réévaluent.

## Handles

```xml
<flow:Handle Type="Source" Position="Right" HandleId="sortie" Offset="40" />
```

| Propriété | Rôle |
|---|---|
| `Type` | `Source` (départ) ou `Target` (arrivée) |
| `Position` | Côté du node : `Left`, `Top`, `Right`, `Bottom` |
| `HandleId` | Identifiant, pour distinguer plusieurs handles d'un même node (`Edge.SourceHandle` / `TargetHandle`) |
| `Offset` | Distance depuis le haut (côtés gauche/droit) ou la gauche (côtés haut/bas). Sans valeur : centré. |

**Placez les handles comme enfants du `Grid` racine du template, pas dans une bordure avec `Padding`** : un handle est positionné sur le bord de son conteneur. Un edge sans `SourceHandle` utilise le premier handle de type `Source` du node.

Le node `default` possède un handle cible en haut et un handle source en bas ; `input` n'a que la source, `output` que la cible.

## Redimensionner : `NodeResizer`

```xml
<Grid>
    <Border … />                                   <!-- la carte : elle doit s'étirer, sans Width/Height fixes -->
    <flow:NodeResizer NodeMinWidth="150" NodeMinHeight="70" />   <!-- après la carte, avant les handles -->
    <flow:Handle … />
</Grid>
```

```csharp
new Node("a", pos, "carte", data, FixedWidth: 200, FixedHeight: 90)   // taille initiale
```

Les 8 poignées apparaissent quand le node est sélectionné. Chaque redimensionnement émet un `NodeResizeChange` (taille + position, car tirer le bord gauche ou haut déplace le node).

## Barre d'outils : `NodeToolbar`

```xml
<flow:NodeToolbar ToolbarPosition="Top" Offset="10">
    <StackPanel Orientation="Horizontal">
        <Button Content="Dupliquer" Command="{Binding Data.DuplicateCommand}" />
        <Button Content="Supprimer" Command="{Binding Data.DeleteCommand}" />
    </StackPanel>
</flow:NodeToolbar>
```

La barre est visible uniquement quand le node est sélectionné et se place hors du node, du côté demandé.

## Contour de sélection

Un node à template personnalisé reçoit automatiquement un contour arrondi quand il est sélectionné (clé `Flow.Outline`). Pour un autre style, liez-vous à `Selected` :

```xml
<DataTrigger Binding="{Binding Selected}" Value="True"> … </DataTrigger>
```

## Node par défaut

Sans template, un node `default` affiche `Node.Data.ToString()` (ou l'`Id`) dans une carte de 150 px de large, avec ses handles. Pour changer ce rendu globalement, redéfinissez les [clés de thème](../reference/theme-keys.md).

Suivant : [Edges](05-edges.md).
