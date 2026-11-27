# 13. MVVM

*React Flow equivalent: State Management (here, your ViewModel is the store).*

Wpflow can be used in two ways for the state of the graph:

| Mode | You provide | The view modifies | For whom |
|---|---|---|---|
| **Functional** (default) | `Nodes` / `Edges`: lists of immutable `record`s | `Nodes` / `Edges` (new list), or nothing in controlled mode | An exact equivalent of React Flow, simple tests |
| **MVVM** | `NodesSource` / `EdgesSource`: collections of **your ViewModels** | The properties of your ViewModels and your collections | Classic WPF applications, binding |

Do not mix both on the same `FlowView`: with a source, `Nodes` and `Edges` are computed for you.

No dependency is imposed: a ViewModel only needs `INotifyPropertyChanged` and `ICommand`. This is verified with **CommunityToolkit.Mvvm** (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`) by the tests; Prism and ReactiveUI rely on the same interfaces but are not tested here.

## A node ViewModel

It is enough to expose properties readable by name. By default: `Id`, `X`, `Y`.

```csharp
public partial class TaskVm : ObservableObject     // INotifyPropertyChanged, for example from CommunityToolkit.Mvvm
{
    public string Id { get; init; } = "";
    public string Kind => "task";                  // selects the wpflow-node-task template
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
        <flow:EdgeMapping />                       <!-- Id, Source, Target by default -->
    </flow:FlowView.EdgeMapping>
</flow:FlowView>
```

The node template receives the `Node` as its `DataContext`, and `Node.Data` **is your ViewModel**: the bindings are live and two-way (`{Binding Data.Title}`).

## Mappings

Each property of `NodeMapping` / `EdgeMapping` is the **name of a property** of the ViewModel; `null` = not mapped (the node's current value is kept).

| `NodeMapping` | Default | Role |
|---|---|---|
| `IdPath` | `Id` | Identifier (string) |
| `XPath`, `YPath` | `X`, `Y` | Position (relative to the parent if `ParentIdPath`); written by dragging |
| `TypePath` | — | Node type (`"default"` otherwise) |
| `WidthPath`, `HeightPath` | — | Explicit size (`FixedWidth/FixedHeight`); written by `NodeResizer`. `NaN` or `null` = automatic |
| `SelectedPath` | — | Selection, in both directions |
| `DraggablePath`, `SelectablePath` | — | Read-only |
| `ParentIdPath` | — | Parent group |
| `DataPath` | — | Value given to `Node.Data` (the ViewModel itself if `null`) |

The other properties of a node can also be mapped, with a property of the indicated type: `HiddenPath`, `ConnectablePath`, `DeletablePath`, `ExtentParentPath`, `ExpandParentPath` (`bool`); `ExtentPath` (`Rect?`); `SourcePositionPath`, `TargetPositionPath` (`Position?`); `OriginPath` (`XYPosition?`); `RotationPath`, `InitialWidthPath`, `InitialHeightPath` (numbers); `ZIndexPath` (`int?`); `DragHandlePath`, `AriaLabelPath` (strings); `HandlesPath` (`IReadOnlyList<HandleInfo>`).

`EdgeMapping`: `IdPath` (`Id`), `SourcePath` (`Source`), `TargetPath` (`Target`), `SourceHandlePath`, `TargetHandlePath`, `TypePath`, `LabelPath`, `AnimatedPath`, `SelectedPath`, `HiddenPath`, `SelectablePath`, `DeletablePath`, `ReconnectablePath`, `DataPath`, `MarkerStartPath`, `MarkerEndPath`, `MarkerStartKeyPath`, `MarkerEndKeyPath`, `InteractionWidthPath`, `ZIndexPath`, `AriaLabelPath`, `StylePath`, `LabelStylePath`, `PathOptionsPath`, `MarkerStartOptionsPath`, `MarkerEndOptionsPath`.

In Blazor, this whole chapter also applies, with the differences described in [Wpflow.Blazor](../blazor.md#mvvm).

- `DataPath` gives the value of `Edge.Data`, the one read by a [path provider](05-edges.md) or an edge template. Unlike `NodeMapping.DataPath`, `null` **does not** put the ViewModel in `Data`.
- `MarkerStartPath` and `MarkerEndPath` designate properties of type `EdgeMarker`.

When the path or the markers depend on a computed property, raise `PropertyChanged` for **that** property (not only for the one it derives from):

```csharp
public string Cardinality
{
    get => _cardinality;
    set { SetProperty(ref _cardinality, value); OnPropertyChanged(nameof(Data)); OnPropertyChanged(nameof(MarkerEnd)); }
}
public Relation Data => new(Cardinality);                 // EdgeMapping.DataPath
public EdgeMarker MarkerEnd => Cardinality == "1-n" ? EdgeMarker.ArrowClosed : EdgeMarker.None;
```

Numeric types are converted (an `int` or a `float` works for `X`). A mapping is set before or after the source: the source is rebuilt when it changes, but **changing its properties afterwards is not detected**.

## What happens, in both directions

**ViewModel → view.** `CollectionChanged` adds and removes nodes; a `PropertyChanged` of a mapped property replaces only the record concerned (the others keep their reference, so the view does not reprocess anything else). An unmapped property (a title displayed by the template) costs nothing.

**View → ViewModel.** The changes applied by the view are written back to your objects:

| Gesture | Write |
|---|---|
| Dragging, arrow keys | `XPath`, `YPath` |
| `NodeResizer` | `WidthPath`, `HeightPath` (and `XPath`/`YPath` if the left or top edge moves) |
| Selection | `SelectedPath` |
| Delete | The ViewModel is removed from the collection (if it is modifiable); the connected edges too |
| Reconnecting an edge | `SourcePath`, `TargetPath`, `SourceHandlePath`, `TargetHandlePath` |
| Mouse connection, drop | **`EdgeFactory` / `NodeFactory`** create the ViewModel, which is added to the collection (or a [command](#commands) does it) |

```csharp
// in code; or bound to a property of the ViewModel: EdgeFactory="{Binding LinkFactory}"
flow.EdgeFactory = edge => new LinkVm { Id = edge.Id, Source = edge.Source, Target = edge.Target };
flow.NodeFactory = node => new TaskVm { Id = node.Id, X = node.Position.X, Y = node.Position.Y, Title = "New" };
```

Without a factory or a command, a connection or a drop adds nothing to the source (the `ConnectRequested` / `DropRequested` events remain available).

Writes do not loop back.

### Refusing or adjusting a change

A ViewModel has the last word. If it raises `PropertyChanged` while the view is writing a value to it, the view **reads it back** after the write and displays what it contains: a setter that clamps a position, snaps it to a grid or refuses it (locked element) is followed. To refuse, do not change the field but **still raise** `PropertyChanged`; without a notification, the view keeps the value it wrote.

```csharp
public double X
{
    get => _x;
    set { if (!IsLocked) _x = value; OnPropertyChanged(); }
}
```

To refuse a deletion, use `NodesDeleteCommand` / `EdgesDeleteCommand`; for a connection, the `CanExecute` of `ConnectCommand` ([commands](#commands)).

> **During a drag**, `X` and `Y` are written on every mouse movement. If their setter is expensive (saving, recalculation), do not do it in the setter.

## Bindable selection

| Property | Content |
|---|---|
| `SelectedNodes` | The selected ViewModels (`Node`s without a source) |
| `SelectedEdges` | Same for edges |
| `SelectedNode` | The first selected node, `null` otherwise |

Read with a `OneWay` binding. Written (`TwoWay`, or in code), they **select** these elements. If you bind an `ObservableCollection`, it is **modified in place** by the view, and adding or removing elements from the ViewModel changes the selection on screen:

```csharp
Selection.Add(Tasks[3]);   // node 4 gets selected
Selection.Clear();         // nothing is selected any more
```

If you bind an ordinary `IList` property, the view puts a **new** list in it at each selection change (never the same modified list, otherwise a binding would not be notified). The selection is not recreated as long as it does not change, including during a drag.

## Bindable state

| Property | Type | Role |
|---|---|---|
| `Zoom` | `double` | Zoom level, `TwoWay`: writing it zooms while keeping the centre of the view (bounded by `MinZoom` / `MaxZoom`) |
| `Viewport` | `Viewport` | Position and zoom, `TwoWay` |
| `IsConnecting` | `bool` (read-only) | A connection is being drawn |
| `CanUndo`, `CanRedo` | `bool` (read-only) | To grey out buttons; they follow the view's history or the ViewModel's |

## Commands

The user's actions are exposed as `ICommand`s (dependency properties, so bindable). Common rule: **`CanExecute` is consulted before the action**, and a command that *replaces* the automatic action does it instead of the view.

| Property | Parameter | Effect |
|---|---|---|
| `ConnectCommand` | `Connection` | **Replaces** the creation of the edge: the ViewModel adds its link. A `false` `CanExecute` **refuses** the connection — this is the validator on the ViewModel side (an edge reconnection is only subject to `CanExecute`) |
| `NodesDeleteCommand` | list of the elements to delete (descendants included) | **Replaces** the automatic deletion of nodes; a `false` `CanExecute` cancels the whole deletion |
| `EdgesDeleteCommand` | list of the selected edges | Replaces the automatic deletion of these edges |
| `DropCommand` | `FlowDropEventArgs` | Called on every drop (palette) |
| `NodeDragStopCommand` | list of the moved elements | Notification at the end of a move (the positions are already written) |
| `NodeClickCommand` | the element (ViewModel) | Click without a move on a node |
| `EdgeClickCommand` | the element | Click on an edge |
| `PaneClickCommand` | `XYPosition` (flow coordinates) | Click on the background |
| `NodeDoubleClickCommand`, `EdgeDoubleClickCommand` | the element | Double click |
| `NodeContextMenuCommand`, `EdgeContextMenuCommand` | the element | Right click |
| `PaneContextMenuCommand` | `XYPosition` | Right click on the background |
| `SelectionContextMenuCommand` | list of the selected elements | Right click on a selection of several nodes (the node's command is also called) |

**All the other events also have their command**, which receives the event's arguments and replaces no action:

| Area | Commands |
|---|---|
| Lifecycle | `InitCommand`, `NodesReadyCommand`, `StateChangedCommand`, `ErrorCommand` |
| Changes | `NodeChangesCommand`, `EdgeChangesCommand`, `SelectionChangedCommand`, `DeletedCommand` |
| Hover | `NodeMouseEnterCommand` / `MoveCommand` / `LeaveCommand`, and likewise for `EdgeMouse…` and `PaneMouse…`; `PaneScrollCommand` |
| Dragging | `NodeDragStartCommand`, `NodeDragCommand`, `SelectionDragStartCommand`, `SelectionDragCommand`, `SelectionDragStopCommand`, `SelectionStartCommand`, `SelectionEndCommand` |
| Connections | `ConnectStartCommand`, `ConnectEndCommand`, `ClickConnectStartCommand`, `ClickConnectEndCommand`, `ReconnectStartCommand`, `ReconnectEndCommand`, `EdgeReconnectCommand` |
| View | `ViewportMoveStartCommand`, `ViewportMoveCommand`, `ViewportMoveEndCommand` |

A notification command costs nothing as long as it is not set. Deletion has one exception without a command: `BeforeDelete`, which must **answer**; bind `BeforeDeleteAsync`.

With `CommandsReceiveEventArgs="True"`, the click, double-click and context-menu commands receive an `ElementEventArgs` instead: element, position and `TargetData`, the part of the model that was targeted (see [Interactions](09-interactions.md)).

```xml
<flow:FlowView ConnectCommand="{Binding ConnectCommand}" NodesDeleteCommand="{Binding DeleteCommand}"
               NodeClickCommand="{Binding SelectCommand}" PaneClickCommand="{Binding AddAtCommand}" />
```

```csharp
ConnectCommand = new RelayCommand<Connection>(c => Links.Add(new LinkVm(c)),
                                              c => c!.Target != "1");   // validator: never towards task 1
```

`IsValidConnection` (a `Func<Connection, bool>`, also bindable) remains available for a rule that does not go through a command.

### Routed commands: `FlowCommands`

For buttons and menus **without code-behind**: `FitView`, `ZoomIn`, `ZoomOut`, `Undo`, `Redo`, `SelectAll`, `ClearSelection`, `DeleteSelection`, `ToggleLock`. Their `CanExecute` follows the state (Undo greyed out without history, Delete without a selection…).

```xml
<Button Content="Undo" Command="{x:Static flow:FlowCommands.Undo}" CommandTarget="{Binding ElementName=Flow}" />
```

A button placed **inside** the `FlowView` (overlay) does not need `CommandTarget`. `FlowControls` and `MiniMap` are also wired explicitly: `Flow="{Binding ElementName=Flow}"` (by default, the view that contains them).

## Driving the view from the ViewModel: `IFlowController`

The ViewModel does not need to know `FlowView`. The control hands it an `IFlowController` through a `OneWayToSource` binding:

```xml
<flow:FlowView Controller="{Binding Flow, Mode=OneWayToSource}" />
```

```csharp
public IFlowController? Flow { get; set; }          // set by the view

Flow?.FitView();  Flow?.ZoomTo(1.5);  Flow?.CenterOn("node-42");
Flow?.SelectAll();  Flow?.ClearSelection();  Flow?.DeleteSelection();  Flow?.Undo();
var p = Flow?.FlowToScreen(new XYPosition(10, 20));   // and ScreenToFlow
```

The controller only exposes these operations, not the control.

## Undo / Redo

With a source, Ctrl+Z works for the changes made by the view: the history keeps record states and writes the difference back to your ViewModels, for **all mapped properties** (a deleted node comes back with the **same** ViewModel).

Changes made directly by your code are not in this history, and undoing a gesture does not revert them: a property changed by a background task keeps its new value. The **geometry** is an exception (position, size, selection, rotation, ends of an edge): it is restored even if your code adjusted it after the gesture, for example by aligning a node at the end of a drag.

If the application has **its own** history, implement `IFlowHistory` (`Checkpoint()`, `Undo()`, `Redo()`, `CanUndo`, `CanRedo`) and bind it: `History="{Binding History}"`. The view then asks for `Checkpoint()` before each change made by a gesture (move, delete, connection) and delegates Ctrl+Z / Ctrl+Y; `CanUndo` / `CanRedo` follow the object (via `INotifyPropertyChanged` if it implements it).

## JSON

`flow.ToJson()` writes the graph in React Flow format; `flow.LoadJson(json)` loads it.

- **Without a source**: `Nodes` and `Edges` are read and replaced.
- **With a source**: `Node.Data` **is not written by default** (a ViewModel is not meant to be serialised as is). `ToJson(n => new { title = ((TaskVm)n.Data!).Title })` chooses what is written. `LoadJson` empties the collection then adds to it the ViewModels created by `NodeFactory` / `EdgeFactory` (mandatory, otherwise `InvalidOperationException`) whose mapped properties are filled in.

`GraphSerializer.ToJson(nodes, edges, viewport, data: …)` accepts the same data selector.

## Limits

- Changes made by your code in a ViewModel cannot be undone with the view's Ctrl+Z (unless with your `IFlowHistory`); see [Undo / Redo](#undo--redo).
- A mapped property that changes thousands of times per second on many nodes copies the list every time: batch your updates.
- The dimensions measured by the view (`Width`/`Height` of the `Node`) are not written to the ViewModel (only the explicit size is).
- A `ConnectCommand` is not consulted while drawing (no « connection refused » preview), only on release.

## Examples

The **MVVM** tab of the examples window (`demo/wpf/MvvmExample.xaml`, `MvvmViewModel.cs`) has **no code-behind**: collections of `TaskVm` and `LinkVm`, a node template with bound `TextBox` and `CheckBox`, connection (with a business rule), deletion, click and end-of-move commands, Undo / Redo / Delete buttons as routed commands (greyed out depending on the state), zoom bound to a slider, `IFlowController` for Fit and Center, and an `IsConnecting` indicator.

The **home page** (the ring of cubes) is also driven by a ViewModel (`demo/wpf/HomeViewModel.cs`): nodes and links are collections, the viewport is bound in `TwoWay`, and it is the ViewModel that decides that colour, shape and zoom only act if their link exists.

Next: [Customization](14-customization.md).
