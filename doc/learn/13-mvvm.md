# 13. MVVM

*Équivalent React Flow : State Management (le store est ici votre ViewModel).*

Wpflow s'utilise de deux façons pour l'état du graphe :

| Mode | Vous fournissez | La vue modifie | Pour qui |
|---|---|---|---|
| **Fonctionnel** (par défaut) | `Nodes` / `Edges` : listes de `record` immuables | `Nodes` / `Edges` (nouvelle liste), ou rien en mode contrôlé | Équivalent exact de React Flow, tests simples |
| **MVVM** | `NodesSource` / `EdgesSource` : collections de **vos ViewModels** | Les propriétés de vos ViewModels et vos collections | Applications WPF classiques, binding |

Ne mélangez pas les deux sur un même `FlowView` : avec une source, `Nodes` et `Edges` sont calculés pour vous.

Aucune dépendance n'est imposée : un ViewModel n'a besoin que de `INotifyPropertyChanged` et d'`ICommand`. C'est vérifié avec **CommunityToolkit.Mvvm** (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`) par les tests ; Prism et ReactiveUI reposent sur les mêmes interfaces mais ne sont pas testés ici.

## Un ViewModel de node

Il suffit d'exposer des propriétés lisibles par nom. Par défaut : `Id`, `X`, `Y`.

```csharp
public partial class TaskVm : ObservableObject     // INotifyPropertyChanged, par exemple CommunityToolkit.Mvvm
{
    public string Id { get; init; } = "";
    public string Kind => "task";                  // choisit le template wpflow-node-task
    [ObservableProperty] private double _x, _y;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private bool _isSelected;
}

public class MainViewModel
{
    public ObservableCollection<TaskVm> Tasks { get; } = new();
    public ObservableCollection<LinkVm> Links { get; } = new();
    public ObservableCollection<TaskVm> Selection { get; } = new();
}
```

```xml
<flow:FlowView NodesSource="{Binding Tasks}" EdgesSource="{Binding Links}"
               SelectedNodes="{Binding Selection, Mode=OneWay}">
    <flow:FlowView.NodeMapping>
        <flow:NodeMapping TypePath="Kind" SelectedPath="IsSelected" />
    </flow:FlowView.NodeMapping>
    <flow:FlowView.EdgeMapping>
        <flow:EdgeMapping />                       <!-- Id, Source, Target par défaut -->
    </flow:FlowView.EdgeMapping>
</flow:FlowView>
```

Le template du node reçoit le `Node` en `DataContext`, et `Node.Data` **est votre ViewModel** : les bindings sont vivants et bidirectionnels (`{Binding Data.Title}`).

## Mappages

Chaque propriété de `NodeMapping` / `EdgeMapping` est le **nom d'une propriété** du ViewModel ; `null` = non mappé (la valeur courante du node est conservée).

| `NodeMapping` | Défaut | Rôle |
|---|---|---|
| `IdPath` | `Id` | Identifiant (chaîne) |
| `XPath`, `YPath` | `X`, `Y` | Position (relative au parent si `ParentIdPath`) ; écrites par le glisser |
| `TypePath` | — | Type de node (`"default"` sinon) |
| `WidthPath`, `HeightPath` | — | Taille explicite (`FixedWidth/FixedHeight`) ; écrites par `NodeResizer`. `NaN` ou `null` = automatique |
| `SelectedPath` | — | Sélection, dans les deux sens |
| `DraggablePath`, `SelectablePath` | — | Lecture seule |
| `ParentIdPath` | — | Groupe parent |
| `DataPath` | — | Valeur donnée à `Node.Data` (le ViewModel lui-même si `null`) |

`EdgeMapping` : `IdPath` (`Id`), `SourcePath` (`Source`), `TargetPath` (`Target`), `SourceHandlePath`, `TargetHandlePath`, `TypePath`, `LabelPath`, `AnimatedPath`, `SelectedPath`, `DataPath`, `MarkerStartPath`, `MarkerEndPath`.

- `DataPath` donne la valeur de `Edge.Data`, celle que lisent un [fournisseur de tracé](05-edges.md) ou un modèle d'edge. Contrairement à `NodeMapping.DataPath`, `null` **ne met pas** le ViewModel dans `Data`.
- `MarkerStartPath` et `MarkerEndPath` désignent des propriétés de type `EdgeMarker`.

Quand le tracé ou les marqueurs dépendent d'une propriété calculée, levez `PropertyChanged` pour **cette** propriété (pas seulement pour celle dont elle dérive) :

```csharp
public string Cardinality
{
    get => _cardinality;
    set { SetProperty(ref _cardinality, value); OnPropertyChanged(nameof(Data)); OnPropertyChanged(nameof(MarkerEnd)); }
}
public Relation Data => new(Cardinality);                 // EdgeMapping.DataPath
public EdgeMarker MarkerEnd => Cardinality == "1-n" ? EdgeMarker.ArrowClosed : EdgeMarker.None;
```

Les types numériques sont convertis (un `int` ou un `float` fonctionne pour `X`). Un mappage se fixe avant ou après la source : la source est reconstruite quand il change, mais **modifier ses propriétés après coup n'est pas détecté**.

## Ce qui se passe, dans les deux sens

**ViewModel → vue.** `CollectionChanged` ajoute et retire des nodes ; un `PropertyChanged` d'une propriété mappée remplace le seul record concerné (les autres gardent leur référence, donc la vue ne retraite rien d'autre). Une propriété non mappée (un titre affiché par le template) ne coûte rien.

**Vue → ViewModel.** Les changes que la vue applique sont reportés dans vos objets :

| Geste | Écriture |
|---|---|
| Glisser, flèches | `XPath`, `YPath` |
| `NodeResizer` | `WidthPath`, `HeightPath` (et `XPath`/`YPath` si le bord gauche ou haut bouge) |
| Sélection | `SelectedPath` |
| Suppr | Le ViewModel est retiré de la collection (si elle est modifiable) ; les edges reliés aussi |
| Reconnexion d'un edge | `SourcePath`, `TargetPath`, `SourceHandlePath`, `TargetHandlePath` |
| Connexion à la souris, dépôt | **`EdgeFactory` / `NodeFactory`** créent le ViewModel, ajouté à la collection (ou une [commande](#commandes) s'en charge) |

```csharp
// en code ; ou liées à une propriété du ViewModel : EdgeFactory="{Binding LinkFactory}"
flow.EdgeFactory = edge => new LinkVm { Id = edge.Id, Source = edge.Source, Target = edge.Target };
flow.NodeFactory = node => new TaskVm { Id = node.Id, X = node.Position.X, Y = node.Position.Y, Title = "Nouveau" };
```

Sans fabrique ni commande, une connexion ou un dépôt n'ajoute rien à la source (les événements `ConnectRequested` / `DropRequested` restent disponibles).

Les écritures ne reviennent pas en boucle.

### Refuser ou ajuster un changement

Un ViewModel a le dernier mot. S'il lève `PropertyChanged` pendant que la vue lui écrit une valeur, la vue le **relit** après l'écriture et affiche ce qu'il contient : un setter qui borne une position, l'aligne sur une grille ou la refuse (élément verrouillé) est suivi. Pour refuser, ne changez pas le champ mais **levez quand même** `PropertyChanged` ; sans notification, la vue garde la valeur qu'elle a écrite.

```csharp
public double X
{
    get => _x;
    set { if (!IsLocked) _x = value; OnPropertyChanged(); }
}
```

Pour refuser une suppression, utilisez `NodesDeleteCommand` / `EdgesDeleteCommand` ; une connexion, le `CanExecute` de `ConnectCommand` ([commandes](#commandes)).

> **Pendant un glisser**, `X` et `Y` sont écrits à chaque mouvement de souris. Si leur setter est coûteux (sauvegarde, recalcul), ne le faites pas dans le setter.

## Sélection bindable

| Propriété | Contenu |
|---|---|
| `SelectedNodes` | Les ViewModels sélectionnés (des `Node` sans source) |
| `SelectedEdges` | Idem pour les edges |
| `SelectedNode` | Le premier node sélectionné, `null` sinon |

Lues avec un binding `OneWay`. Écrites (`TwoWay`, ou en code), elles **sélectionnent** ces éléments. Si vous liez une `ObservableCollection`, elle est **modifiée sur place** par la vue, et ajouter ou retirer des éléments depuis le ViewModel change la sélection à l'écran :

```csharp
Selection.Add(Tasks[3]);   // le node 4 se sélectionne
Selection.Clear();         // plus rien n'est sélectionné
```

Si vous liez une propriété de type `IList` ordinaire, la vue y met une **nouvelle** liste à chaque changement de sélection (jamais la même liste modifiée, sinon un binding ne serait pas notifié). La sélection n'est pas recréée tant qu'elle ne change pas, y compris pendant un glisser.

## État bindable

| Propriété | Type | Rôle |
|---|---|---|
| `Zoom` | `double` | Niveau de zoom, `TwoWay` : l'écrire zoome en gardant le centre de la vue (borné par `MinZoom` / `MaxZoom`) |
| `Viewport` | `Viewport` | Position et zoom, `TwoWay` |
| `IsConnecting` | `bool` (lecture seule) | Une connexion est en cours de tracé |
| `CanUndo`, `CanRedo` | `bool` (lecture seule) | Pour griser des boutons ; suivent l'historique de la vue ou celui du ViewModel |

## Commandes

Les actions de l'utilisateur sont exposées en `ICommand` (propriétés de dépendance, donc liables). Règle commune : **`CanExecute` est consulté avant l'action**, et une commande qui *remplace* l'action automatique la fait à la place de la vue.

| Propriété | Paramètre | Effet |
|---|---|---|
| `ConnectCommand` | `Connection` | **Remplace** la création de l'edge : le ViewModel ajoute son lien. `CanExecute` faux **refuse** la connexion — c'est le validateur côté ViewModel (une reconnexion d'edge n'est que soumise à `CanExecute`) |
| `NodesDeleteCommand` | liste des éléments à supprimer (descendants compris) | **Remplace** la suppression automatique des nodes ; `CanExecute` faux annule toute la suppression |
| `EdgesDeleteCommand` | liste des edges sélectionnés | Remplace la suppression automatique de ces edges |
| `DropCommand` | `FlowDropEventArgs` | Appelée à chaque dépôt (palette) |
| `NodeDragStopCommand` | liste des éléments déplacés | Notification en fin de déplacement (les positions sont déjà écrites) |
| `NodeClickCommand` | l'élément (ViewModel) | Clic sans déplacement sur un node |
| `EdgeClickCommand` | l'élément | Clic sur un edge |
| `PaneClickCommand` | `XYPosition` (coordonnées flow) | Clic sur le fond |
| `NodeDoubleClickCommand`, `EdgeDoubleClickCommand` | l'élément | Double-clic |
| `NodeContextMenuCommand`, `EdgeContextMenuCommand` | l'élément | Clic droit |
| `PaneContextMenuCommand` | `XYPosition` | Clic droit sur le fond |
| `SelectionContextMenuCommand` | liste des éléments sélectionnés | Clic droit sur une sélection de plusieurs nodes (la commande du node est appelée aussi) |

Avec `CommandsReceiveEventArgs="True"`, les commandes de clic, de double-clic et de menu contextuel reçoivent à la place un `ElementEventArgs` : élément, position et `TargetData`, la partie du modèle visée (voir [Interactions](09-interactions.md)).

```xml
<flow:FlowView ConnectCommand="{Binding ConnectCommand}" NodesDeleteCommand="{Binding DeleteCommand}"
               NodeClickCommand="{Binding SelectCommand}" PaneClickCommand="{Binding AddAtCommand}" />
```

```csharp
ConnectCommand = new RelayCommand<Connection>(c => Links.Add(new LinkVm(c)),
                                              c => c!.Target != "1");   // validateur : jamais vers la tâche 1
```

`IsValidConnection` (un `Func<Connection, bool>`, lui aussi liable) reste disponible pour une règle qui ne passe pas par une commande.

### Commandes routées : `FlowCommands`

Pour des boutons et des menus **sans code-behind** : `FitView`, `ZoomIn`, `ZoomOut`, `Undo`, `Redo`, `SelectAll`, `ClearSelection`, `DeleteSelection`, `ToggleLock`. Leur `CanExecute` suit l'état (Annuler grisé sans historique, Supprimer sans sélection…).

```xml
<Button Content="Annuler" Command="{x:Static flow:FlowCommands.Undo}" CommandTarget="{Binding ElementName=Flow}" />
```

Un bouton placé **dans** le `FlowView` (overlay) n'a pas besoin de `CommandTarget`. `FlowControls` et `MiniMap` se branchent aussi explicitement : `Flow="{Binding ElementName=Flow}"` (par défaut, la vue qui les contient).

## Piloter la vue depuis le ViewModel : `IFlowController`

Le ViewModel n'a pas à connaître `FlowView`. Le contrôle lui remet un `IFlowController` par une liaison `OneWayToSource` :

```xml
<flow:FlowView Controller="{Binding Flow, Mode=OneWayToSource}" />
```

```csharp
public IFlowController? Flow { get; set; }          // renseigné par la vue

Flow?.FitView();  Flow?.ZoomTo(1.5);  Flow?.CenterOn("node-42");
Flow?.SelectAll();  Flow?.ClearSelection();  Flow?.DeleteSelection();  Flow?.Undo();
var p = Flow?.FlowToScreen(new XYPosition(10, 20));   // et ScreenToFlow
```

Le contrôleur ne laisse voir que ces opérations, pas le contrôle.

## Undo / Redo

Avec une source, Ctrl+Z fonctionne pour les changements faits par la vue : l'historique garde des états de records et reporte la différence dans vos ViewModels (un node supprimé revient avec le **même** ViewModel). Les modifications faites directement par votre code dans un ViewModel ne sont pas dans cet historique.

Si l'application a **son propre** historique, implémentez `IFlowHistory` (`Checkpoint()`, `Undo()`, `Redo()`, `CanUndo`, `CanRedo`) et liez-le : `History="{Binding History}"`. La vue demande alors `Checkpoint()` avant chaque modification faite par un geste (déplacement, suppression, connexion) et délègue Ctrl+Z / Ctrl+Y ; `CanUndo` / `CanRedo` suivent l'objet (via `INotifyPropertyChanged` s'il l'implémente).

## JSON

`flow.ToJson()` écrit le graphe au format React Flow ; `flow.LoadJson(json)` le charge.

- **Sans source** : `Nodes` et `Edges` sont lus et remplacés.
- **Avec une source** : `Node.Data` **n'est pas écrit par défaut** (un ViewModel n'est pas fait pour être sérialisé tel quel). `ToJson(n => new { title = ((TaskVm)n.Data!).Title })` choisit ce qui est écrit. `LoadJson` vide la collection puis y ajoute les ViewModels créés par `NodeFactory` / `EdgeFactory` (obligatoires, sinon `InvalidOperationException`) dont les propriétés mappées sont renseignées.

`GraphSerializer.ToJson(nodes, edges, viewport, data: …)` accepte le même sélecteur de données.

## Limites

- Les modifications faites par votre code dans un ViewModel ne sont pas annulables par Ctrl+Z de la vue (sauf avec votre `IFlowHistory`).
- Une propriété mappée qui change des milliers de fois par seconde sur de nombreux nodes recopie la liste à chaque fois : regroupez vos mises à jour.
- Les dimensions mesurées par la vue (`Width`/`Height` du `Node`) ne sont pas écrites dans le ViewModel (seule la taille explicite l'est).
- Un `ConnectCommand` n'est pas consulté pendant le tracé (pas d'aperçu « connexion refusée »), seulement au relâchement.

## Exemples

L'onglet **MVVM** de la fenêtre d'exemples (`demo/wpf/MvvmExample.xaml`, `MvvmViewModel.cs`) n'a **aucun code-behind** : collections de `TaskVm` et de `LinkVm`, template de node avec `TextBox` et `CheckBox` liés, commandes de connexion (avec règle métier), de suppression, de clic et de fin de déplacement, boutons Annuler / Rétablir / Supprimer en commandes routées (grisés selon l'état), zoom lié à un curseur, `IFlowController` pour Cadrer et Centrer, et un indicateur `IsConnecting`.

La **page d'accueil** (les cubes) est aussi pilotée par un ViewModel (`demo/wpf/HomeViewModel.cs`) : nodes et liens sont des collections, le viewport est lié en `TwoWay`, et c'est le ViewModel qui décide que couleur, forme et zoom n'agissent que si leur lien existe.

Suivant : [Personnaliser](14-customization.md).
