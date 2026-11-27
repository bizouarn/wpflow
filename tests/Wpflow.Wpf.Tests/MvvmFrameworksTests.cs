using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;
using ReactiveUI;
using Wpflow.Core;
using Wpflow.Wpf;

namespace Wpflow.Wpf.Tests;

/// <summary>Prism : <c>BindableBase</c> et <c>DelegateCommand</c>.</summary>
public class PrismNode : BindableBase
{
    private double _x, _y;
    private bool _selected;
    private string _title = "";

    public string Id { get; init; } = "";

    public double X { get => _x; set => SetProperty(ref _x, value); }
    public double Y { get => _y; set => SetProperty(ref _y, value); }
    public bool IsSelected { get => _selected; set => SetProperty(ref _selected, value); }
    public string Title { get => _title; set => SetProperty(ref _title, value); }
}

/// <summary>ReactiveUI : <c>ReactiveObject</c> et <c>ReactiveCommand</c>.</summary>
public class ReactiveNode : ReactiveObject
{
    private double _x, _y;
    private bool _selected;

    public string Id { get; init; } = "";

    public double X { get => _x; set => this.RaiseAndSetIfChanged(ref _x, value); }
    public double Y { get => _y; set => this.RaiseAndSetIfChanged(ref _y, value); }
    public bool IsSelected { get => _selected; set => this.RaiseAndSetIfChanged(ref _selected, value); }
}

/// <summary>Vérifie, avec les vrais paquets, que Prism et ReactiveUI fonctionnent sans adaptation.</summary>
public class MvvmFrameworksTests
{
    [Fact]
    public void Prism_view_models_and_delegate_commands_work()
    {
        Sta.Run(() =>
        {
            var nodes = new ObservableCollection<PrismNode> { new() { Id = "a", X = 1, Y = 2 } };
            var flow = new FlowView { NodesSource = nodes, NodeMapping = new NodeMapping { SelectedPath = "IsSelected" } };

            nodes[0].X = 30; // SetProperty de BindableBase
            Assert.Equal(30, flow.Nodes[0].Position.X);

            flow.ApplyNodeChanges([new NodePositionChange("a", new XYPosition(5, 6)), new NodeSelectChange("a", true)]);
            Assert.Equal((5, 6), (nodes[0].X, nodes[0].Y));
            Assert.True(nodes[0].IsSelected);

            // Un DelegateCommand de Prism branché sur ConnectCommand : CanExecute filtre, Execute reçoit la Connection.
            Connection? received = null;
            var command = new DelegateCommand<Connection>(c => received = c, c => c.Target != "forbidden");
            flow.ConnectCommand = command;
            Assert.True(flow.ConnectCommand.CanExecute(new Connection("a", "b", null, null)));
            Assert.False(flow.ConnectCommand.CanExecute(new Connection("a", "forbidden", null, null)));
            flow.ConnectCommand.Execute(new Connection("a", "b", null, null));
            Assert.Equal("b", received!.Target);
        });
    }

    [Fact]
    public void ReactiveUI_view_models_and_reactive_commands_work()
    {
        Sta.Run(() =>
        {
            var nodes = new ObservableCollection<ReactiveNode> { new() { Id = "a", X = 1, Y = 2 } };
            var flow = new FlowView { NodesSource = nodes, NodeMapping = new NodeMapping { SelectedPath = "IsSelected" } };

            nodes[0].X = 77; // RaiseAndSetIfChanged
            Assert.Equal(77, flow.Nodes[0].Position.X);

            flow.ApplyNodeChanges([new NodePositionChange("a", new XYPosition(9, 8)), new NodeSelectChange("a", true)]);
            Assert.Equal((9, 8), (nodes[0].X, nodes[0].Y));
            Assert.True(nodes[0].IsSelected);

            // ReactiveCommand est un ICommand : il se lie aux commandes de FlowView.
            var received = new List<Connection>();
            var command = ReactiveCommand.Create<Connection>(received.Add, outputScheduler: ReactiveUI.Primitives.Concurrency.ImmediateSequencer.Instance);
            flow.ConnectCommand = command;
            flow.ConnectCommand.Execute(new Connection("a", "b", null, null));
            Assert.Single(received);
        });
    }
}
