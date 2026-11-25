# 10. Calculer avec un flow

*Équivalent React Flow : Computing Flows — c'est la démo de la page d'accueil de reactflow.dev.*

Un flow peut servir à **calculer** : des nodes produisent des valeurs, d'autres les consomment le long des edges. La démo de Wpflow (`dotnet run --project demo/wpf`) reproduit celle de React Flow :

- trois nodes d'entrée — **shape color** (un vrai sélecteur de couleur), **shape type** (cube ou pyramide), **zoom level** (un curseur) ;
- un node **output** qui affiche un nuage de solides 3D animés ;
- trois edges, un par entrée, vers trois handles distincts de l'output.

Le point clé : **le node output n'utilise une valeur que si l'edge correspondant existe.** Supprimez l'edge du zoom (sélectionnez-le, Suppr) : le zoom retombe à sa valeur par défaut ; reconnectez-le : le curseur reprend la main.

## Le principe

1. **Un état partagé** : les nodes d'entrée écrivent dans un même objet (`Node.Data`) qui implémente `INotifyPropertyChanged`.
2. **Un ViewModel qui sait ce qui est relié** : il expose des valeurs *effectives*.
3. **Une synchronisation avec les edges** : à chaque changement de `Edges`, on recalcule quelles entrées sont reliées.

### 1. L'état

```csharp
public sealed class DemoState : INotifyPropertyChanged
{
    public Color Color { get; private set; }     // choisi par le sélecteur
    public bool IsCube { get; set; }
    public double Zoom { get; set; }             // choisi par le curseur

    // Reliées ? (mises à jour depuis les edges)
    public bool UseColor { get; set; } = true;
    public bool UseShape { get; set; } = true;
    public bool UseZoom  { get; set; } = true;

    // Valeurs effectives : la valeur choisie si l'entrée est reliée, sinon la valeur par défaut.
    public Color  EffectiveColor  => UseColor ? Color : DefaultColor;
    public bool   EffectiveIsCube => !UseShape || IsCube;
    public double EffectiveZoom   => UseZoom ? Zoom : DefaultZoom;
}
```

Le même objet est mis dans `Data` de **tous** les nodes ; chaque template lie ses contrôles à la propriété qui le concerne.

### 2. Les nodes d'entrée

Chacun est un `DataTemplate` (`wpflow-node-color`, `-shape`, `-zoom`) avec ses contrôles WPF — un `Popup` de couleurs, des `RadioButton`, un `Slider` — et un handle source à droite. Exemple du curseur :

```xml
<DataTemplate x:Key="wpflow-node-zoom">
    <Grid>
        <Border Style="{StaticResource Card}" Width="150">
            <StackPanel>
                <TextBlock Text="zoom level" />
                <Slider Minimum="0" Maximum="1" Value="{Binding Data.Zoom, Mode=TwoWay}" />
            </StackPanel>
        </Border>
        <flow:Handle Type="Source" Position="Right" />
    </Grid>
</DataTemplate>
```

### 3. Le node output et ses trois entrées

L'output expose trois handles **cible** distincts, identifiés par `HandleId`, et dessine la scène :

```xml
<DataTemplate x:Key="wpflow-node-output">
    <Grid>
        <Border Style="{StaticResource Card}">
            <demo:CubeField State="{Binding Data}" />
        </Border>
        <flow:NodeResizer NodeMinWidth="180" NodeMinHeight="140" />
        <flow:Handle Type="Target" Position="Left" HandleId="color" Offset="48" />
        <flow:Handle Type="Target" Position="Left" HandleId="shape" Offset="62" />
        <flow:Handle Type="Target" Position="Left" HandleId="zoom"  Offset="76" />
    </Grid>
</DataTemplate>
```

### 4. Relier : suivre les edges

```csharp
Flow.Edges = [ new Edge("e-color", "color", "output", TargetHandle: "color", Animated: true),
               new Edge("e-shape", "shape", "output", TargetHandle: "shape", Animated: true),
               new Edge("e-zoom",  "zoom",  "output", TargetHandle: "zoom",  Animated: true) ];

void SyncConnections()
{
    bool Linked(string handle) => Flow.Edges.Any(e => e.Target == "output" && e.TargetHandle == handle);
    state.UseColor = Linked("color");
    state.UseShape = Linked("shape");
    state.UseZoom  = Linked("zoom");
}

DependencyPropertyDescriptor.FromProperty(FlowView.EdgesProperty, typeof(FlowView))
    .AddValueChanged(Flow, (_, _) => SyncConnections());
SyncConnections();
```

Comme `Edges` change à chaque connexion, reconnexion, suppression ou annulation (Ctrl+Z), la scène suit sans autre code. En mode contrôlé, appelez `SyncConnections()` après avoir appliqué vos changes.

## La scène animée

`CubeField` est un `FrameworkElement` qui dessine en 3D dans `OnRender` :

- **Animation** : à chaque image (`CompositionTarget.Rendering`) le nuage tourne sur lui-même, chaque solide pivote selon sa propre phase et flotte légèrement.
- **Rendu** : projection orthographique, faces triées de l'arrière vers l'avant, faces dos à la caméra éliminées, ombrage selon une lumière fixe.
- **Zoom** : l'échelle de la « caméra » suit `EffectiveZoom` avec une inertie (le zoom affiché rattrape la cible de façon exponentielle, indépendamment de la cadence).
- **Couleur et forme** : `EffectiveColor` et `EffectiveIsCube`.

Le code complet est dans `demo/wpf/CubeField.cs`, `demo/wpf/DemoState.cs` et `demo/wpf/MainWindow.xaml`.

Suivant : [Performance et accessibilité](11-performance-accessibility.md).
