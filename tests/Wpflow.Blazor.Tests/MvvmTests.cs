using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Wpflow.Blazor;
using Wpflow.Core;

namespace Wpflow.Blazor.Tests;

/// <summary><c>NodesSource</c> / <c>EdgesSource</c> : les mêmes garanties qu'en WPF.</summary>
public sealed class MvvmTests : FlowViewTestContext
{
    private abstract class Vm : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public int Subscribers => PropertyChanged?.GetInvocationList().Length ?? 0;

        protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            Raise(name);
        }
    }

    private sealed class TaskVm(string id, double x = 0, double y = 0) : Vm
    {
        private double _x = x, _y = y;
        private bool _selected;
        private string _title = "titre " + id;

        public string Id => id;
        public bool Locked { get; init; }

        public double X
        {
            get => _x;
            set
            {
                if (!Locked) _x = value;
                Raise();
            }
        }

        public double Y
        {
            get => _y;
            set
            {
                if (!Locked) _y = value;
                Raise();
            }
        }

        public bool IsSelected { get => _selected; set => Set(ref _selected, value); }

        /// <summary>Affiché par le modèle, jamais mappé.</summary>
        public string Title { get => _title; set => Set(ref _title, value); }
    }

    private sealed class LinkVm(string id, string source, string target) : Vm
    {
        public string Id => id;
        public string Source => source;
        public string Target => target;
    }

    private static readonly NodeMapping Mapping = new() { SelectedPath = "IsSelected" };

    private static RenderFragment<Node> Template => node => builder =>
        builder.AddMarkupContent(0, $"<b class='title'>{((TaskVm)node.Data!).Title}</b>");

    private readonly ObservableCollection<TaskVm> _tasks = [new("a"), new("b", 300, 100)];
    private readonly ObservableCollection<LinkVm> _links = [new("e", "a", "b")];

    private IRenderedComponent<FlowView> Render(Action<ComponentParameterCollectionBuilder<FlowView>>? configure = null) =>
        Render<FlowView>(p =>
        {
            p.Add(f => f.NodesSource, _tasks);
            p.Add(f => f.EdgesSource, _links);
            p.Add(f => f.NodeMapping, Mapping);
            p.Add(f => f.NodeTemplate, Template);
            p.Add(f => f.FitViewOnInit, false);
            configure?.Invoke(p);
        });

    private static async Task Drag(IRenderedComponent<FlowView> cut, string node, double dx, double dy)
    {
        await cut.Find($"[data-node-id='{node}']").TriggerEventAsync("onpointerdown", new PointerEventArgs { ClientX = 10, ClientY = 10 });
        await cut.InvokeAsync(() => cut.Instance.OnPointerMove(10 + dx, 10 + dy, false, false));
        await cut.InvokeAsync(() => cut.Instance.OnPointerUp(10 + dx, 10 + dy, false, false));
    }

    [Fact]
    public async Task The_collections_are_projected_and_followed()
    {
        var cut = Render();
        await Measure(cut);
        Assert.Equal(["a", "b"], cut.Instance.Nodes.Select(n => n.Id));
        Assert.Single(cut.Instance.Edges);
        var unchanged = cut.Instance.Nodes[1];

        await cut.InvokeAsync(() => _tasks[0].X = 40);
        await cut.InvokeAsync(() => _tasks.Add(new TaskVm("c", 5, 5)));

        Assert.Equal(40, cut.Instance.Nodes[0].Position.X);
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".wf-node").Count));
        Assert.Same(unchanged.Data, cut.Instance.Nodes[1].Data);
    }

    [Fact]
    public async Task A_burst_of_changes_is_drawn_once()
    {
        var cut = Render();
        await Measure(cut);
        var before = cut.RenderCount;

        await cut.InvokeAsync(() =>
        {
            for (var i = 0; i < 100; i++) _tasks.Add(new TaskVm("n" + i, i * 10, 0));
        });

        cut.WaitForAssertion(() => Assert.Equal(102, cut.FindAll(".wf-node").Count));
        Assert.InRange(cut.RenderCount - before, 1, 5);
    }

    [Fact]
    public async Task A_drag_writes_into_the_view_model_and_undo_brings_it_back()
    {
        var cut = Render();
        await Measure(cut);

        await Drag(cut, "a", 50, 30);

        Assert.Equal((50, 30), (_tasks[0].X, _tasks[0].Y));
        Assert.True(_tasks[0].IsSelected);
        await cut.InvokeAsync(() => cut.Instance.Undo());
        Assert.Equal((0, 0), (_tasks[0].X, _tasks[0].Y));
    }

    [Fact]
    public async Task A_view_model_that_refuses_a_move_is_followed_by_the_view()
    {
        _tasks.Add(new TaskVm("locked", 500, 0) { Locked = true });
        var cut = Render();
        await Measure(cut);

        await Drag(cut, "locked", 60, 20);

        Assert.Equal(500, _tasks[2].X);
        Assert.Equal(new XYPosition(500, 0), cut.Instance.Nodes[2].Position);
    }

    [Fact]
    public async Task An_unmapped_property_shown_by_the_template_is_redrawn()
    {
        var cut = Render();
        await Measure(cut);

        await cut.InvokeAsync(() => _tasks[0].Title = "renommé");

        cut.WaitForAssertion(() => Assert.Equal(["renommé", "titre b"], cut.FindAll(".title").Select(e => e.TextContent)));
    }

    [Fact]
    public async Task A_deletion_removes_the_view_models_and_undo_puts_the_same_ones_back()
    {
        var cut = Render();
        await Measure(cut);
        var first = _tasks[0];

        await cut.InvokeAsync(() => cut.Instance.DeleteElements(["a"]));

        Assert.Equal(["b"], _tasks.Select(t => t.Id));
        Assert.Empty(_links);
        await cut.InvokeAsync(() => cut.Instance.Undo());
        Assert.Contains(first, _tasks);
        Assert.Single(_links);
    }

    [Fact]
    public async Task A_connection_is_created_by_the_factory()
    {
        _links.Clear();
        var cut = Render(p => p.Add(f => f.EdgeFactory, edge => new LinkVm(edge.Id, edge.Source, edge.Target)));
        await Measure(cut);

        await cut.InvokeAsync(() => cut.Instance.AddEdges([new Edge("new", "a", "b")]));

        Assert.Equal("new", Assert.Single(_links).Id);
    }

    [Fact]
    public async Task The_bound_selection_works_both_ways()
    {
        IReadOnlyList<object> reported = [];
        object? single = null;
        var cut = Render(p =>
        {
            p.Add(f => f.SelectedNodeItemsChanged, EventCallback.Factory.Create<IReadOnlyList<object>>(this, v => reported = v));
            p.Add(f => f.SelectedNodeChanged, EventCallback.Factory.Create<object?>(this, v => single = v));
        });
        await Measure(cut);

        await cut.InvokeAsync(() => cut.Instance.SelectOnly("b", null));

        Assert.Same(_tasks[1], Assert.Single(reported));
        Assert.Same(_tasks[1], single);
        cut.Render(p => p.Add(f => f.SelectedNodeItems, new object[] { _tasks[0] }));
        Assert.True(_tasks[0].IsSelected);
        Assert.False(_tasks[1].IsSelected);
    }

    [Fact]
    public async Task A_selection_given_once_is_not_imposed_again_at_each_render()
    {
        object[] initial = [_tasks[0]];
        var cut = Render(p => p.Add(f => f.SelectedNodeItems, initial));
        await Measure(cut);
        Assert.True(_tasks[0].IsSelected);

        await cut.InvokeAsync(() => cut.Instance.SelectOnly("b", null));
        cut.Render(p => p.Add(f => f.SelectedNodeItems, initial)); // le parent se redessine

        Assert.True(_tasks[1].IsSelected);
        Assert.False(_tasks[0].IsSelected);
    }

    private sealed class OwnHistory : IFlowHistory
    {
        public List<string> Calls { get; } = [];
        public bool CanUndo => true;
        public bool CanRedo => false;
        public void Checkpoint() => Calls.Add("point");
        public void Undo() => Calls.Add("undo");
        public void Redo() => Calls.Add("redo");
    }

    [Fact]
    public async Task The_application_history_replaces_the_built_in_one()
    {
        var history = new OwnHistory();
        var cut = Render(p => p.Add(f => f.History, history));
        await Measure(cut);

        await Drag(cut, "a", 50, 30);
        await cut.InvokeAsync(() => cut.Instance.Undo());

        Assert.Equal(["point", "undo"], history.Calls);
        Assert.Equal(50, _tasks[0].X); // la vue n'a rien défait elle-même
    }

    [Fact]
    public async Task The_controller_drives_the_view_without_exposing_it()
    {
        IFlowController? controller = null;
        var cut = Render(p => p.Add(f => f.ControllerChanged, EventCallback.Factory.Create<IFlowController>(this, c => controller = c)));
        await Measure(cut);

        Assert.IsNotType<FlowView>(controller);
        await controller!.ZoomTo(2);
        await controller.SelectAll();

        Assert.Equal(2, cut.Instance.Viewport.Zoom);
        Assert.All(_tasks, t => Assert.True(t.IsSelected));
    }

    [Fact]
    public async Task Json_writes_what_the_selector_gives_and_loads_through_the_factories()
    {
        var cut = Render(p =>
        {
            p.Add(f => f.NodeFactory, node => new TaskVm(node.Id));
            p.Add(f => f.EdgeFactory, edge => new LinkVm(edge.Id, edge.Source, edge.Target));
        });
        await Measure(cut);
        await cut.InvokeAsync(() => _tasks[1].X = 123);

        Assert.DoesNotContain("titre a", cut.Instance.ToJson());
        var json = cut.Instance.ToJson(n => new { title = ((TaskVm)n.Data!).Title });
        Assert.Contains("titre a", json);

        await cut.InvokeAsync(() => cut.Instance.LoadJson(json));

        Assert.Equal(123, _tasks[1].X);
        Assert.Single(_links);
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact]
    public async Task Disposing_the_view_unsubscribes_from_the_view_models()
    {
        var cut = Render();
        await Measure(cut);
        Assert.Equal(1, _tasks[0].Subscribers);

        await cut.InvokeAsync(async () => await cut.Instance.DisposeAsync());

        Assert.Equal(0, _tasks[0].Subscribers);
    }
}
