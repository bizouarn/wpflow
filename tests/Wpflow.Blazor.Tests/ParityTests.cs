using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Wpflow.Blazor;
using Wpflow.Core;

namespace Wpflow.Blazor.Tests;

/// <summary>Les comportements alignés sur la vue WPF, qui fait référence.</summary>
public sealed class ParityTests : TestContext
{
    private static readonly Node A = new("a", new XYPosition(0, 0), Selected: true);
    private static readonly Node B = new("b", new XYPosition(300, 100), Selected: true);
    private static readonly Edge Link = new("e", "a", "b");

    public ParityTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/Wpflow.Blazor/wpflow.js").Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<FlowView> Render(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges,
        Action<ComponentParameterCollectionBuilder<FlowView>>? configure = null) => RenderComponent<FlowView>(p =>
    {
        p.Add(f => f.Nodes, nodes);
        p.Add(f => f.Edges, edges);
        configure?.Invoke(p);
    });

    [Fact]
    public async Task Select_all_takes_the_edges_too()
    {
        var cut = Render([A with { Selected = false }, B with { Selected = false }], [Link, Link with { Id = "fixed", Selectable = false }]);

        await cut.InvokeAsync(() => cut.Instance.SelectAll());

        Assert.Equal(["a", "b"], cut.Instance.SelectedNodes.Select(n => n.Id));
        Assert.Equal(["e"], cut.Instance.SelectedEdges.Select(e => e.Id));
    }

    [Fact]
    public async Task In_controlled_mode_the_view_keeps_no_history()
    {
        var changes = new List<NodeChange>();
        var cut = Render([A, B], [Link], p =>
        {
            p.Add(f => f.AutoApplyChanges, false);
            p.Add(f => f.OnNodesChange, EventCallback.Factory.Create<IReadOnlyList<NodeChange>>(this, changes.AddRange));
        });

        await cut.InvokeAsync(() => cut.Instance.DeleteSelected());
        changes.Clear();

        Assert.False(cut.Instance.CanUndo);
        Assert.False(await cut.InvokeAsync(() => cut.Instance.Undo()));
        Assert.Empty(changes);
    }

    [Fact]
    public async Task Moving_over_a_node_and_turning_the_wheel_are_notified()
    {
        var raised = new List<string>();
        var cut = Render([A, B], [Link], p =>
        {
            p.Add(f => f.OnNodeMouseMove, EventCallback.Factory.Create<NodeEventArgs>(this, e => raised.Add("node " + e.Node.Id)));
            p.Add(f => f.OnEdgeMouseMove, EventCallback.Factory.Create<EdgeEventArgs>(this, e => raised.Add("edge " + e.Edge.Id)));
            p.Add(f => f.OnPaneScroll, EventCallback.Factory.Create<PaneScrollEventArgs>(this, e => raised.Add($"molette {e.DeltaY}")));
        });
        await Measure(cut);

        await cut.Find("[data-node-id='a']").TriggerEventAsync("onpointermove", new PointerEventArgs { ClientX = 5, ClientY = 5 });
        await cut.Find(".wf-edge-hit").TriggerEventAsync("onpointermove", new PointerEventArgs { ClientX = 200, ClientY = 70 });
        await cut.InvokeAsync(() => cut.Instance.OnWheel(10, 10, 0, 3, false, false, false, 1));

        Assert.Equal(["node a", "edge e", "molette 60"], raised);
    }

    [Fact]
    public async Task State_changes_are_notified_and_read_back()
    {
        var count = 0;
        var cut = Render([A, B], [Link], p => p.Add(f => f.OnStateChange, EventCallback.Factory.Create(this, () => count++)));
        await Measure(cut);
        Assert.True(cut.Instance.NodesInitialized);
        var before = count;

        await cut.InvokeAsync(() => cut.Instance.SetViewport(new Viewport(10, 20, 2)));

        Assert.True(count > before);
        Assert.Equal(2, cut.Instance.Zoom);
        Assert.False(cut.Instance.IsConnecting);
        Assert.False(cut.Instance.IsViewportAnimating);
        Assert.False(cut.Instance.CanPaste);
        await cut.InvokeAsync(() => cut.Instance.Copy());
        Assert.True(cut.Instance.CanPaste);
        Assert.Equal(["a", "b"], cut.Instance.AbsoluteNodes.Select(n => n.Id).Order());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_node_reached_with_Tab_is_brought_into_view_but_not_one_focused_by_a_click(bool tabbed)
    {
        var far = new Node("far", new XYPosition(2000, 0));
        var cut = Render([A, far], []);
        await Measure(cut);
        var before = cut.Instance.Viewport;

        if (tabbed) await cut.Find(".wf-flow").KeyDownAsync(new KeyboardEventArgs { Key = "Tab" });
        await cut.Find("[data-node-id='far']").TriggerEventAsync("onfocus", new FocusEventArgs());

        Assert.Equal(tabbed, cut.Instance.Viewport != before);
    }

    [Fact]
    public async Task The_clickable_width_of_edges_can_be_set()
    {
        var cut = Render([A, B], [Link], p => p.Add(f => f.EdgeHitWidth, 30));
        await Measure(cut); // les edges ne sont tracés qu'une fois les nodes mesurés

        Assert.Contains("stroke-width:30px", cut.Find(".wf-edge-hit").GetAttribute("style"));
    }

    private static async Task Measure(IRenderedComponent<FlowView> cut)
    {
        await cut.InvokeAsync(() => cut.Instance.OnRootRect(0, 0, 800, 600));
        var measured = cut.Instance.Nodes.Select(n => new MeasuredNode
        {
            Id = n.Id, Width = 100, Height = 40,
            Handles =
            [
                new MeasuredHandle { Type = "target", Position = "top", X = 46, Y = -4, Width = 8, Height = 8 },
                new MeasuredHandle { Type = "source", Position = "bottom", X = 46, Y = 36, Width = 8, Height = 8 }
            ]
        }).ToList();
        await cut.InvokeAsync(() => cut.Instance.OnMeasured(measured));
    }

    [Fact]
    public async Task A_right_click_on_a_selection_raises_the_selection_menu_then_the_node_menu()
    {
        var raised = new List<string>();
        var cut = Render([A, B], [Link], p =>
        {
            p.Add(f => f.OnSelectionContextMenu, EventCallback.Factory.Create<PaneEventArgs>(this, _ => raised.Add("sélection")));
            p.Add(f => f.OnNodeContextMenu, EventCallback.Factory.Create<NodeEventArgs>(this, e => raised.Add("node " + e.Node.Id)));
        });

        await cut.Find("[data-node-id='b']").TriggerEventAsync("oncontextmenu", new MouseEventArgs { ClientX = 350, ClientY = 120 });

        Assert.Equal(["sélection", "node b"], raised);
    }
}
