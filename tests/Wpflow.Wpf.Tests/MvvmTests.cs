using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Wpflow.Core;
using Wpflow.Wpf;

namespace Wpflow.Wpf.Tests;

/// <summary>
///     Exécute du code WPF sur un thread STA unique qui héberge une <see cref="Application" /> : sans elle, les styles par
///     défaut (<c>Themes/Generic.xaml</c>) ne sont pas chargés et les contrôles n'ont pas de gabarit.
/// </summary>
internal static class Sta
{
    private static readonly System.Windows.Threading.Dispatcher Dispatcher = Start();

    private static System.Windows.Threading.Dispatcher Start()
    {
        System.Windows.Threading.Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            _ = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
            dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            ready.Set();
            System.Windows.Threading.Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return dispatcher!;
    }

    public static void Run(Action action) => Dispatcher.Invoke(action);
}

public class NodeVm : INotifyPropertyChanged
{
    private double _x, _y, _w = double.NaN;
    private bool _selected;
    private string _title = "";

    public string Id { get; set; } = "";
    public string Kind { get; set; } = "default";

    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    public double X
    {
        get => _x;
        set => Set(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => Set(ref _y, value);
    }

    public double Width
    {
        get => _w;
        set => Set(ref _w, value);
    }

    public bool IsSelected
    {
        get => _selected;
        set => Set(ref _selected, value);
    }

    public int WritesToX { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        if (name == nameof(X)) WritesToX++;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public class EdgeVm
{
    public string Id { get; set; } = "";
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public bool IsSelected { get; set; }
}

public class MvvmTests
{
    private static NodeVm Vm(string id, double x = 0, double y = 0) => new() { Id = id, X = x, Y = y, Title = "t" + id };

    private static FlowView Flow(ObservableCollection<NodeVm> nodes, NodeMapping? mapping = null,
        ObservableCollection<EdgeVm>? edges = null)
    {
        var flow = new FlowView
        {
            NodeMapping = mapping ?? new NodeMapping { SelectedPath = "IsSelected", WidthPath = "Width", TypePath = "Kind" },
            EdgeMapping = new EdgeMapping { SelectedPath = "IsSelected" }
        };
        flow.NodesSource = nodes;
        if (edges != null) flow.EdgesSource = edges;
        return flow;
    }

    [Fact]
    public void Source_is_projected_into_nodes_with_the_view_model_as_data()
    {
        Sta.Run(() =>
        {
            var a = Vm("a", 10, 20);
            var flow = Flow([a, Vm("b", 5, 6)]);

            Assert.Equal(2, flow.Nodes.Count);
            Assert.Equal(new XYPosition(10, 20), flow.Nodes[0].Position);
            Assert.Same(a, flow.Nodes[0].Data);
        });
    }

    [Fact]
    public void Property_change_replaces_only_that_record()
    {
        Sta.Run(() =>
        {
            var a = Vm("a");
            var flow = Flow([a, Vm("b")]);
            var before = flow.Nodes.ToList();

            a.X = 99;

            Assert.Equal(99, flow.Nodes[0].Position.X);
            Assert.Same(before[1], flow.Nodes[1]); // le node b n'a pas été reconstruit
        });
    }

    [Fact]
    public void Unmapped_property_change_does_not_touch_nodes()
    {
        Sta.Run(() =>
        {
            var a = Vm("a");
            var flow = Flow([a]);
            var before = flow.Nodes;

            a.Title = "autre";

            Assert.Same(before, flow.Nodes);
        });
    }

    [Fact]
    public void Collection_add_and_remove_keep_unchanged_records()
    {
        Sta.Run(() =>
        {
            var nodes = new ObservableCollection<NodeVm> { Vm("a"), Vm("b") };
            var flow = Flow(nodes);
            var a = flow.Nodes[0];

            nodes.Add(Vm("c", 1, 1));
            Assert.Equal(["a", "b", "c"], flow.Nodes.Select(n => n.Id));
            Assert.Same(a, flow.Nodes[0]);

            nodes.RemoveAt(1);
            Assert.Equal(["a", "c"], flow.Nodes.Select(n => n.Id));
        });
    }

    [Fact]
    public void Runtime_state_survives_a_rebuild()
    {
        Sta.Run(() =>
        {
            var nodes = new ObservableCollection<NodeVm> { Vm("a") };
            var flow = Flow(nodes);
            flow.ApplyNodeChanges([new NodeDimensionsChange("a", 120, 40)]); // mesure faite par la vue

            nodes.Add(Vm("b"));

            Assert.Equal(120, flow.Nodes[0].Width);
        });
    }

    [Fact]
    public void Drag_is_written_back_to_the_view_model()
    {
        Sta.Run(() =>
        {
            var a = Vm("a");
            var flow = Flow([a]);

            flow.ApplyNodeChanges([new NodePositionChange("a", new XYPosition(30, 40))]);

            Assert.Equal(30, a.X);
            Assert.Equal(40, a.Y);
            Assert.Equal(1, a.WritesToX); // une seule écriture, pas de boucle
            Assert.Equal(30, flow.Nodes[0].Position.X);
        });
    }

    [Fact]
    public void Resize_selection_and_removal_are_written_back()
    {
        Sta.Run(() =>
        {
            var nodes = new ObservableCollection<NodeVm> { Vm("a"), Vm("b") };
            var flow = Flow(nodes);

            flow.ApplyNodeChanges([new NodeResizeChange("a", 200, 80, new XYPosition(5, 6))]);
            Assert.Equal(200, nodes[0].Width);
            Assert.Equal(5, nodes[0].X);

            flow.ApplyNodeChanges([new NodeSelectChange("b", true)]);
            Assert.True(nodes[1].IsSelected);

            flow.ApplyNodeChanges([new NodeRemoveChange("a")]);
            Assert.Equal(["b"], nodes.Select(n => n.Id));
            Assert.Equal(["b"], flow.Nodes.Select(n => n.Id));
        });
    }

    [Fact]
    public void View_model_selection_flag_selects_the_node()
    {
        Sta.Run(() =>
        {
            var a = Vm("a");
            var flow = Flow([a]);

            a.IsSelected = true;

            Assert.True(flow.Nodes[0].Selected);
        });
    }

    [Fact]
    public void Edges_follow_their_source_and_are_written_back()
    {
        Sta.Run(() =>
        {
            var edges = new ObservableCollection<EdgeVm> { new() { Id = "e", Source = "a", Target = "b" } };
            var flow = Flow([Vm("a"), Vm("b")], edges: edges);
            Assert.Equal("a", flow.Edges[0].Source);

            flow.ApplyEdgeChanges([new EdgeReplaceChange("e", flow.Edges[0] with { Target = "c" })]);
            Assert.Equal("c", edges[0].Target);

            flow.ApplyEdgeChanges([new EdgeSelectChange("e", true)]);
            Assert.True(edges[0].IsSelected);

            flow.ApplyEdgeChanges([new EdgeRemoveChange("e")]);
            Assert.Empty(edges);
        });
    }

    [Fact]
    public void Factories_create_view_models_for_elements_added_by_the_view()
    {
        Sta.Run(() =>
        {
            var nodes = new ObservableCollection<NodeVm>();
            var edges = new ObservableCollection<EdgeVm>();
            var flow = Flow(nodes, edges: edges);
            flow.NodeFactory = n => new NodeVm { Id = n.Id, X = n.Position.X, Y = n.Position.Y };
            flow.EdgeFactory = e => new EdgeVm { Id = e.Id, Source = e.Source, Target = e.Target };

            flow.ApplyNodeChanges([new NodeAddChange(new Node("n", new XYPosition(7, 8)))]);
            flow.ApplyEdgeChanges([new EdgeAddChange(new Edge("e", "n", "n"))]);

            Assert.Equal("n", Assert.Single(nodes).Id);
            Assert.Equal(7, nodes[0].X);
            Assert.Equal("e", Assert.Single(edges).Id);
            Assert.Single(flow.Nodes); // pas de doublon
        });
    }

    [Fact]
    public void Undo_restores_view_models_including_removed_ones()
    {
        Sta.Run(() =>
        {
            var a = Vm("a", 1, 2);
            var b = Vm("b", 5, 5);
            var nodes = new ObservableCollection<NodeVm> { a, b };
            var flow = Flow(nodes);

            flow.PushHistory();
            flow.ApplyNodeChanges([new NodePositionChange("a", new XYPosition(50, 60))]);
            Assert.Equal(50, a.X);
            flow.Undo();
            Assert.Equal((1, 2), (a.X, a.Y));
            Assert.Equal(new XYPosition(1, 2), flow.Nodes[0].Position);

            flow.PushHistory();
            flow.ApplyNodeChanges([new NodeRemoveChange("b")]);
            Assert.Single(nodes);
            flow.Undo();
            Assert.Equal(2, nodes.Count);
            Assert.Same(b, nodes[1]); // le même ViewModel, pas une copie
            Assert.Equal(["a", "b"], flow.Nodes.Select(n => n.Id));

            flow.Redo();
            Assert.Single(nodes);
        });
    }

    [Fact]
    public void SelectedNodes_exposes_the_view_models()
    {
        Sta.Run(() =>
        {
            var a = Vm("a");
            var flow = Flow([a, Vm("b")]);

            flow.ApplyNodeChanges([new NodeSelectChange("a", true)]);

            Assert.Same(a, Assert.Single(flow.SelectedNodes.Cast<object>()));
            Assert.Same(a, flow.SelectedNode);

            flow.ApplyNodeChanges([new NodeSelectChange("a", false)]);
            Assert.Empty(flow.SelectedNodes);
            Assert.Null(flow.SelectedNode);
        });
    }

    [Fact]
    public void SelectedNodes_is_not_reallocated_while_selection_is_unchanged()
    {
        Sta.Run(() =>
        {
            var flow = Flow([Vm("a"), Vm("b")]);
            flow.ApplyNodeChanges([new NodeSelectChange("a", true)]);
            var list = flow.SelectedNodes;

            flow.ApplyNodeChanges([new NodePositionChange("a", new XYPosition(1, 1)), new NodePositionChange("b", new XYPosition(2, 2))]);

            Assert.Same(list, flow.SelectedNodes);
        });
    }

    [Fact]
    public void Assigning_SelectedNodes_selects()
    {
        Sta.Run(() =>
        {
            var a = Vm("a");
            var b = Vm("b");
            var flow = Flow([a, b]);

            flow.SelectedNodes = new List<object> { b };

            Assert.False(flow.Nodes[0].Selected);
            Assert.True(flow.Nodes[1].Selected);
            Assert.True(b.IsSelected);
        });
    }

    [Fact]
    public void An_observable_selection_collection_is_updated_in_place_and_observed()
    {
        Sta.Run(() =>
        {
            var a = Vm("a");
            var b = Vm("b");
            var flow = Flow([a, b]);
            var selection = new ObservableCollection<NodeVm>();
            flow.SelectedNodes = selection;

            flow.ApplyNodeChanges([new NodeSelectChange("a", true)]);
            Assert.Same(selection, flow.SelectedNodes);
            Assert.Same(a, Assert.Single(selection));

            selection.Add(b); // le ViewModel sélectionne
            Assert.True(flow.Nodes[1].Selected);

            selection.Clear();
            Assert.DoesNotContain(flow.Nodes, n => n.Selected);
        });
    }

    [Fact]
    public void Replacing_the_selection_collection_content_does_not_reenter_the_collection()
    {
        Sta.Run(() =>
        {
            var a = Vm("a");
            var b = Vm("b");
            var flow = Flow([a, b]);
            var selection = new ObservableCollection<NodeVm> { a };
            flow.SelectedNodes = selection;

            selection.Clear(); // Clear puis Add déclenchent chacun une sélection pendant l'événement de la collection
            selection.Add(b);

            Assert.Equal([false, true], flow.Nodes.Select(n => n.Selected));
            Assert.Same(b, Assert.Single(selection));
        });
    }

    [Fact]
    public void Records_mode_selection_exposes_node_records()
    {
        Sta.Run(() =>
        {
            var flow = new FlowView { Nodes = [new Node("a", XYPosition.Zero), new Node("b", XYPosition.Zero)] };

            flow.ApplyNodeChanges([new NodeSelectChange("b", true)]);

            Assert.Equal("b", Assert.IsType<Node>(Assert.Single(flow.SelectedNodes.Cast<object>())).Id);
        });
    }

    [Fact]
    public void Assigning_SelectedNode_selects_only_it()
    {
        Sta.Run(() =>
        {
            var a = Vm("a");
            var b = Vm("b");
            var flow = Flow([a, b]);
            flow.SelectedNode = a;
            flow.SelectedNode = b;

            Assert.Equal([false, true], flow.Nodes.Select(n => n.Selected));
        });
    }
}
