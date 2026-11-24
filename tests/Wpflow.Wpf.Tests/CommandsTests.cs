using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wpflow.Core;
using Wpflow.Wpf;

namespace Wpflow.Wpf.Tests;

/// <summary>Commande de test qui enregistre ses appels.</summary>
internal sealed class SpyCommand(bool canExecute = true) : ICommand
{
    public List<object?> Executed { get; } = new();
    public bool Allowed { get; set; } = canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => Allowed;
    public void Execute(object? parameter) => Executed.Add(parameter);
}

internal sealed class FakeHistory : IFlowHistory, INotifyPropertyChanged
{
    private bool _canUndo;
    public int Checkpoints, Undos, Redos;

    public bool CanUndo
    {
        get => _canUndo;
        set
        {
            _canUndo = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanUndo)));
        }
    }

    public bool CanRedo => false;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Checkpoint()
    {
        Checkpoints++;
        CanUndo = true;
    }

    public void Undo() => Undos++;
    public void Redo() => Redos++;
}

public class ControllerHolder
{
    public IFlowController? Flow { get; set; }
}

/// <summary>ViewModel écrit avec CommunityToolkit.Mvvm : prouve que les bibliothèques MVVM courantes fonctionnent sans adaptation.</summary>
public partial class ToolkitNode : ObservableObject
{
    public string Id { get; init; } = "";

    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private bool _isSelected;

    [RelayCommand]
    private void Rename() => Title += "!";
}

public class CommandsTests
{
    private static FlowView Flow(IReadOnlyList<Node>? nodes = null, IReadOnlyList<Edge>? edges = null) =>
        CustomizationTests.Realize(nodes: nodes, edges: edges);

    private static IReadOnlyList<Node> Three() =>
    [
        new Node("a", new XYPosition(0, 0), Width: 100, Height: 40),
        new Node("b", new XYPosition(300, 0), Width: 100, Height: 40),
        new Node("c", new XYPosition(300, 100), Width: 100, Height: 40)
    ];

    // ---- B2 : état bindable ----

    [Fact]
    public void Zoom_is_two_way_with_the_viewport_and_clamped()
    {
        Sta.Run(() =>
        {
            var flow = Flow();
            flow.Zoom = 1.5;
            Assert.Equal(1.5, flow.Viewport.Zoom, 6);

            flow.Viewport = new Viewport(10, 20, 0.8);
            Assert.Equal(0.8, flow.Zoom, 6);

            flow.Zoom = 50; // au-delà de MaxZoom
            Assert.Equal(flow.MaxZoom, flow.Zoom, 6);
            Assert.Equal(flow.MaxZoom, flow.Viewport.Zoom, 6);
        });
    }

    [Fact]
    public void CanUndo_and_CanRedo_are_dependency_properties_following_the_history()
    {
        Sta.Run(() =>
        {
            var flow = Flow(Three());
            Assert.False(flow.CanUndo);
            flow.PushHistory();
            Assert.True(flow.CanUndo);
            Assert.False(flow.CanRedo);

            flow.Undo();
            Assert.False(flow.CanUndo);
            Assert.True(flow.CanRedo);
        });
    }

    [Fact]
    public void IsConnecting_and_history_state_are_read_only_properties()
    {
        Sta.Run(() =>
        {
            var flow = Flow();
            Assert.False(flow.IsConnecting);
            Assert.True(FlowView.IsConnectingProperty.ReadOnly);
            Assert.True(FlowView.CanUndoProperty.ReadOnly);
            Assert.True(FlowView.CanRedoProperty.ReadOnly);
        });
    }

    // ---- B3 : commandes ----

    [Fact]
    public void Routed_commands_drive_the_view_and_report_state()
    {
        Sta.Run(() =>
        {
            var flow = Flow(Three());
            var zoom = flow.Zoom;

            Assert.True(FlowCommands.ZoomIn.CanExecute(null, flow));
            FlowCommands.ZoomIn.Execute(null, flow);
            Assert.True(flow.Zoom > zoom);

            Assert.False(FlowCommands.Undo.CanExecute(null, flow));
            Assert.False(FlowCommands.DeleteSelection.CanExecute(null, flow));

            FlowCommands.SelectAll.Execute(null, flow);
            Assert.All(flow.Nodes, n => Assert.True(n.Selected));
            Assert.True(FlowCommands.DeleteSelection.CanExecute(null, flow));

            FlowCommands.ClearSelection.Execute(null, flow);
            Assert.DoesNotContain(flow.Nodes, n => n.Selected);

            FlowCommands.ToggleLock.Execute(null, flow);
            Assert.True(flow.IsLocked);
        });
    }

    [Fact]
    public void Nodes_delete_command_replaces_the_automatic_deletion()
    {
        Sta.Run(() =>
        {
            var nodes = new ObservableCollection<NodeVm> { new() { Id = "a" }, new() { Id = "b" } };
            var flow = new FlowView { NodesSource = nodes, NodeMapping = new NodeMapping { SelectedPath = "IsSelected" } };
            var command = new SpyCommand();
            flow.NodesDeleteCommand = command;
            flow.SelectNode("a", false);

            flow.DeleteSelection();

            var deleted = Assert.IsAssignableFrom<IReadOnlyList<object>>(Assert.Single(command.Executed));
            Assert.Same(nodes[0], Assert.Single(deleted)); // le ViewModel, pas le record
            Assert.Equal(2, nodes.Count); // c'est à la commande de supprimer
        });
    }

    [Fact]
    public void Delete_command_that_refuses_cancels_the_whole_deletion()
    {
        Sta.Run(() =>
        {
            var flow = new FlowView { Nodes = Three(), Edges = [new Edge("e", "a", "b")] };
            var command = new SpyCommand(canExecute: false);
            flow.NodesDeleteCommand = command;
            flow.ApplyNodeChanges([new NodeSelectChange("a", true)]);

            flow.DeleteSelection();

            Assert.Empty(command.Executed);
            Assert.Equal(3, flow.Nodes.Count);
            Assert.Single(flow.Edges);
        });
    }

    [Fact]
    public void A_command_only_replaces_its_own_part_of_the_deletion()
    {
        Sta.Run(() =>
        {
            var flow = new FlowView { Nodes = Three(), Edges = [new Edge("e1", "a", "b"), new Edge("e2", "b", "c")] };
            flow.ApplyEdgeChanges([new EdgeSelectChange("e1", true)]);
            var edgeCommand = new SpyCommand();
            flow.EdgesDeleteCommand = edgeCommand;

            flow.DeleteSelection();

            Assert.Single(edgeCommand.Executed);
            Assert.Equal(2, flow.Edges.Count); // le ViewModel n'a rien supprimé : la vue non plus

            flow.EdgesDeleteCommand = null;
            flow.DeleteSelection();
            Assert.Equal(["e2"], flow.Edges.Select(e => e.Id));
        });
    }

    // ---- B4 : contrôleur, historique ----

    [Fact]
    public void Controller_is_pushed_to_the_view_model_without_exposing_the_control()
    {
        Sta.Run(() =>
        {
            var holder = new ControllerHolder();
            var flow = Flow(Three());
            BindingOperations.SetBinding(flow, FlowView.ControllerProperty,
                new Binding(nameof(ControllerHolder.Flow)) { Source = holder, Mode = BindingMode.OneWayToSource });

            var controller = Assert.IsAssignableFrom<IFlowController>(holder.Flow);
            Assert.False(controller is FlowView);

            controller.ZoomTo(1.4);
            Assert.Equal(1.4, flow.Zoom, 6);

            Assert.True(controller.CenterOn("b"));
            var center = controller.FlowToScreen(new XYPosition(350, 20));
            Assert.Equal(flow.ActualWidth / 2, center.X, 3);
            Assert.Equal(flow.ActualHeight / 2, center.Y, 3);
            Assert.False(controller.CenterOn("inconnu"));

            controller.SelectAll();
            Assert.All(flow.Nodes, n => Assert.True(n.Selected));
            controller.ClearSelection();
            Assert.DoesNotContain(flow.Nodes, n => n.Selected);
        });
    }

    [Fact]
    public void View_model_history_replaces_the_built_in_one()
    {
        Sta.Run(() =>
        {
            var history = new FakeHistory();
            var flow = Flow(Three());
            flow.History = history;

            flow.PushHistory();
            Assert.Equal(1, history.Checkpoints);
            Assert.True(flow.CanUndo); // suit l'historique du ViewModel (INotifyPropertyChanged)

            flow.Undo();
            flow.Redo();
            Assert.Equal((1, 1), (history.Undos, history.Redos));
            Assert.False(FlowCommands.Redo.CanExecute(null, flow)); // CanRedo vient du ViewModel
        });
    }

    // ---- B5 : JSON ----

    [Fact]
    public void Json_roundtrip_in_records_mode()
    {
        Sta.Run(() =>
        {
            var flow = Flow(Three(), [new Edge("e", "a", "b")]);
            var json = flow.ToJson();

            var other = Flow([]);
            other.LoadJson(json);
            Assert.Equal(["a", "b", "c"], other.Nodes.Select(n => n.Id));
            Assert.Single(other.Edges);
        });
    }

    [Fact]
    public void Json_with_view_models_omits_data_by_default_and_loads_through_the_factories()
    {
        Sta.Run(() =>
        {
            var nodes = new ObservableCollection<NodeVm> { new() { Id = "a", X = 5, Y = 6, Title = "secret" }, new() { Id = "b", X = 50, Y = 60 } };
            var edges = new ObservableCollection<EdgeVm> { new() { Id = "e", Source = "a", Target = "b" } };
            var flow = new FlowView { NodesSource = nodes, EdgesSource = edges };

            var json = flow.ToJson();
            Assert.DoesNotContain("secret", json);
            Assert.Contains("\"id\": \"a\"", json);

            var target = new ObservableCollection<NodeVm> { new() { Id = "old" } };
            var targetEdges = new ObservableCollection<EdgeVm>();
            var loaded = new FlowView { NodesSource = target, EdgesSource = targetEdges };
            Assert.Throws<InvalidOperationException>(() => loaded.LoadJson(json)); // pas de fabrique

            loaded.NodeFactory = n => new NodeVm { Id = n.Id };
            loaded.EdgeFactory = e => new EdgeVm { Id = e.Id, Source = e.Source, Target = e.Target };
            loaded.LoadJson(json);

            Assert.Equal(["a", "b"], target.Select(n => n.Id));
            Assert.Equal((5, 6), (target[0].X, target[0].Y)); // les propriétés mappées sont renseignées
            Assert.Equal("a", Assert.Single(targetEdges).Source);
            Assert.Equal(["a", "b"], loaded.Nodes.Select(n => n.Id));
        });
    }

    [Fact]
    public void Data_selector_chooses_what_is_written()
    {
        Sta.Run(() =>
        {
            var nodes = new ObservableCollection<NodeVm> { new() { Id = "a", Title = "visible" } };
            var flow = new FlowView { NodesSource = nodes };

            var json = flow.ToJson(n => new { title = ((NodeVm)n.Data!).Title });

            Assert.Contains("visible", json);
        });
    }

    // ---- B4 : CommunityToolkit.Mvvm ----

    [Fact]
    public void CommunityToolkit_view_models_work_without_adaptation()
    {
        Sta.Run(() =>
        {
            var nodes = new ObservableCollection<ToolkitNode> { new() { Id = "a", X = 1, Y = 2, Title = "t" } };
            var flow = new FlowView { NodesSource = nodes, NodeMapping = new NodeMapping { SelectedPath = "IsSelected" } };

            nodes[0].X = 40; // ObservableProperty généré
            Assert.Equal(40, flow.Nodes[0].Position.X);

            flow.ApplyNodeChanges([new NodePositionChange("a", new XYPosition(7, 8)), new NodeSelectChange("a", true)]);
            Assert.Equal((7, 8), (nodes[0].X, nodes[0].Y));
            Assert.True(nodes[0].IsSelected);

            nodes[0].RenameCommand.Execute(null); // RelayCommand généré, utilisable dans un template
            Assert.Equal("t!", ((ToolkitNode)flow.Nodes[0].Data!).Title);
        });
    }

    [Fact]
    public void FlowControls_and_MiniMap_accept_an_explicit_flow()
    {
        Sta.Run(() =>
        {
            var flow = Flow(Three());
            var controls = new FlowControls { Flow = flow };
            var map = new MiniMap { Flow = flow };
            Assert.Same(flow, controls.Flow);
            Assert.Same(flow, map.Flow);
        });
    }
}
