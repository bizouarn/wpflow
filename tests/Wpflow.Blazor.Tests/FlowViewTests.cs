using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Wpflow.Blazor;
using Wpflow.Core;

namespace Wpflow.Blazor.Tests;

/// <summary>
///     Tests de <see cref="FlowView" /> sans navigateur : le pont JS est simulé (mode souple) et les callbacks que JS
///     appellerait (<c>OnMeasured</c>, <c>OnPointerMove</c>…) sont invoqués directement.
/// </summary>
public sealed class FlowViewTests : FlowViewTestContext
{
    private Harness Render(IReadOnlyList<Node>? nodes = null, IReadOnlyList<Edge>? edges = null,
        Action<ComponentParameterCollectionBuilder<FlowView>>? configure = null, bool fit = false) =>
        RenderHarness(nodes, edges, (h, p) =>
        {
            p.Add(f => f.OnNodesChange, EventCallback.Factory.Create<IReadOnlyList<NodeChange>>(this, c => h.NodeChanges.Add(c)));
            p.Add(f => f.OnConnect, EventCallback.Factory.Create<Connection>(this, c => h.Connections.Add(c)));
            configure?.Invoke(p);
        }, fit);

    [Fact]
    public void Renders_a_div_per_node_with_default_handles()
    {
        var h = Render();
        Assert.Equal(2, h.Cut.FindAll(".wf-node").Count);
        Assert.Contains("A", h.Cut.Find("[data-node-id='a']").TextContent);
        // default : handle cible en haut et source en bas
        Assert.Single(h.Cut.FindAll("[data-node-id='a'] .wf-handle-top"));
        Assert.Single(h.Cut.FindAll("[data-node-id='a'] .wf-handle-bottom"));
    }

    [Fact]
    public void Input_and_output_nodes_have_a_single_handle()
    {
        var h = Render([new Node("i", XYPosition.Zero, "input"), new Node("o", new XYPosition(0, 100), "output")], []);
        Assert.Single(h.Cut.FindAll("[data-node-id='i'] .wf-handle"));
        Assert.Contains("wf-handle-bottom", h.Cut.Find("[data-node-id='i'] .wf-handle").ClassList);
        Assert.Contains("wf-handle-top", h.Cut.Find("[data-node-id='o'] .wf-handle").ClassList);
    }

    [Fact]
    public async Task Edges_are_drawn_only_once_endpoints_are_measured()
    {
        var h = Render();
        Assert.Empty(h.Cut.FindAll(".wf-edge-path"));

        await Measure(h);
        var path = h.Cut.Find(".wf-edge-path");
        Assert.StartsWith("M", path.GetAttribute("d"));
        // du bord du handle source de a (50,44) au bord du handle cible de b (350,96)
        Assert.StartsWith("M50,44", path.GetAttribute("d"));
        Assert.EndsWith("350,96", path.GetAttribute("d"));
    }

    [Fact]
    public async Task Measuring_emits_dimension_changes_and_updates_nodes()
    {
        var h = Render();
        await Measure(h);

        Assert.All(h.Nodes, n =>
        {
            Assert.Equal(100, n.Width);
            Assert.Equal(40, n.Height);
        });
        Assert.Contains(h.NodeChanges.SelectMany(c => c), c => c is NodeDimensionsChange { Id: "a", Width: 100 });
    }

    [Fact]
    public async Task Dragging_a_node_moves_it_by_the_pointer_delta()
    {
        var h = Render();
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Move(h, 80, 50);
        await Up(h, 80, 50);

        var a = h.Nodes.Single(n => n.Id == "a");
        Assert.Equal(new XYPosition(30, 30), a.Position);
        Assert.False(a.Dragging);
        Assert.True(a.Selected); // glisser sélectionne
        Assert.Equal(new XYPosition(300, 100), h.Nodes.Single(n => n.Id == "b").Position);
    }

    [Fact]
    public async Task Dragging_divides_the_delta_by_the_zoom()
    {
        var h = Render(configure: p => p.Add(f => f.Viewport, new Viewport(0, 0, 2)));
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Move(h, 90, 60);
        await Up(h, 90, 60);

        Assert.Equal(new XYPosition(20, 20), h.Nodes.Single(n => n.Id == "a").Position);
    }

    [Fact]
    public async Task Movement_below_the_threshold_is_a_click_not_a_drag()
    {
        Node? clicked = null;
        var h = Render(configure: p => p.Add(f => f.NodeDragThreshold, 3)
            .Add(f => f.OnNodeClick, EventCallback.Factory.Create<NodeEventArgs>(this, n => clicked = n.Node)));
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Move(h, 51, 21);
        await Up(h, 51, 21);

        Assert.Equal(new XYPosition(0, 0), h.Nodes.Single(n => n.Id == "a").Position);
        Assert.Equal("a", clicked?.Id);
    }

    [Fact]
    public async Task Clicking_selects_one_node_and_clicking_the_pane_clears_the_selection()
    {
        var h = Render();
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Up(h, 50, 20);
        Assert.Equal(["a"], h.Nodes.Where(n => n.Selected).Select(n => n.Id));
        Assert.Contains("wf-selected", h.Cut.Find("[data-node-id='a']").ClassList);

        await Down(h.Cut.Find("[data-node-id='b']"), 350, 120);
        await Up(h, 350, 120);
        Assert.Equal(["b"], h.Nodes.Where(n => n.Selected).Select(n => n.Id));

        await Down(h.Cut.Find(".wf-flow"), 600, 500);
        await Up(h, 600, 500);
        Assert.DoesNotContain(h.Nodes, n => n.Selected);
    }

    [Fact]
    public async Task Shift_click_adds_to_the_selection()
    {
        var h = Render();
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Up(h, 50, 20);
        await Down(h.Cut.Find("[data-node-id='b']"), 350, 120, shift: true);
        await Up(h, 350, 120);

        Assert.Equal(2, h.Nodes.Count(n => n.Selected));
    }

    [Fact]
    public async Task Dragging_one_of_several_selected_nodes_moves_them_all()
    {
        var h = Render(configure: p => { });
        await Measure(h);
        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Up(h, 50, 20);
        await Down(h.Cut.Find("[data-node-id='b']"), 350, 120, shift: true);
        await Up(h, 350, 120);

        await Down(h.Cut.Find("[data-node-id='b']"), 350, 120);
        await Move(h, 360, 130);
        await Up(h, 360, 130);

        Assert.Equal(new XYPosition(10, 10), h.Nodes.Single(n => n.Id == "a").Position);
        Assert.Equal(new XYPosition(310, 110), h.Nodes.Single(n => n.Id == "b").Position);
    }

    [Fact]
    public async Task SnapGrid_aligns_the_dragged_position()
    {
        var h = Render(configure: p => p.Add(f => f.SnapGrid, 20.0));
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Move(h, 77, 53);
        await Up(h, 77, 53);

        Assert.Equal(new XYPosition(20, 40), h.Nodes.Single(n => n.Id == "a").Position);
    }

    [Fact]
    public async Task NodesDraggable_false_keeps_nodes_in_place()
    {
        var h = Render(configure: p => p.Add(f => f.NodesDraggable, false));
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Move(h, 150, 120);
        await Up(h, 150, 120);

        Assert.Equal(new XYPosition(0, 0), h.Nodes.Single(n => n.Id == "a").Position);
    }

    [Fact]
    public async Task Dragging_the_pane_pans_the_viewport()
    {
        var h = Render();
        await Measure(h);

        await Down(h.Cut.Find(".wf-flow"), 500, 400);
        await Move(h, 540, 430);
        await Up(h, 540, 430);

        Assert.Equal(new Viewport(40, 30, 1), h.Flow.Viewport);
        Assert.Contains("translate(40px,30px)", h.Cut.Find(".wf-viewport").GetAttribute("style"));
    }

    [Fact]
    public async Task Wheel_zooms_around_the_cursor_within_bounds()
    {
        var h = Render();
        await Measure(h);

        await h.Cut.InvokeAsync(() => h.Flow.OnWheel(400, 300, 0, -100, false, false, false, 0));
        var zoomed = h.Flow.Viewport;
        Assert.True(zoomed.Zoom > 1);
        // le point sous le curseur reste fixe
        Assert.Equal(400 - 400 * zoomed.Zoom, zoomed.X, 6);
        Assert.Equal(300 - 300 * zoomed.Zoom, zoomed.Y, 6);

        for (var i = 0; i < 40; i++) await h.Cut.InvokeAsync(() => h.Flow.OnWheel(400, 300, 0, -400, false, false, false, 0));
        Assert.Equal(2, h.Flow.Viewport.Zoom, 6); // MaxZoom
    }

    [Fact]
    public async Task ZoomOnScroll_false_ignores_the_wheel()
    {
        var h = Render(configure: p => p.Add(f => f.ZoomOnScroll, false));
        await Measure(h);

        await h.Cut.InvokeAsync(() => h.Flow.OnWheel(400, 300, 10, 20, false, false, false, 0));

        Assert.Equal(Viewport.Identity, h.Flow.Viewport); // comme en WPF : sans zoom ni défilement à la molette, la vue ne bouge pas
    }

    [Fact]
    public async Task FitView_frames_all_measured_nodes()
    {
        var h = Render();
        await Measure(h);

        await h.Cut.InvokeAsync(() => h.Flow.FitView());

        var v = h.Flow.Viewport;
        // les deux nodes (0..400 × 0..140) tiennent dans 800×600
        foreach (var n in h.Nodes)
        {
            var left = n.Position.X * v.Zoom + v.X;
            var right = (n.Position.X + 100) * v.Zoom + v.X;
            var top = n.Position.Y * v.Zoom + v.Y;
            var bottom = (n.Position.Y + 40) * v.Zoom + v.Y;
            Assert.InRange(left, 0, 800);
            Assert.InRange(right, 0, 800);
            Assert.InRange(top, 0, 600);
            Assert.InRange(bottom, 0, 600);
        }
    }

    [Fact]
    public async Task FitViewOnInit_runs_once_everything_is_measured()
    {
        var h = Render(fit: true);
        Assert.Equal(Viewport.Identity, h.Flow.Viewport);

        await Measure(h);

        Assert.NotEqual(Viewport.Identity, h.Flow.Viewport);
    }

    [Fact]
    public async Task Dragging_from_a_handle_to_another_node_creates_a_connection_and_an_edge()
    {
        var h = Render(edges: []);
        await Measure(h);

        // handle source de a (centre 50,40) vers le handle cible de b (centre 350,100)
        await Down(h.Cut.Find("[data-node-id='a'] .wf-handle-bottom"), 50, 40);
        await Move(h, 300, 90);
        await Move(h, 352, 102);
        Assert.NotEmpty(h.Cut.FindAll(".wf-connection-line"));
        Assert.Contains("wf-valid", h.Cut.Find("[data-node-id='b'] .wf-handle-top").ClassList);
        await Up(h, 352, 102);

        var connection = Assert.Single(h.Connections);
        Assert.Equal(new Connection("a", "b", null, null), connection);
        var edge = Assert.Single(h.Edges);
        Assert.Equal(("a", "b"), (edge.Source, edge.Target));
        Assert.Empty(h.Cut.FindAll(".wf-connection-line"));
        Assert.Equal(new XYPosition(0, 0), h.Nodes.Single(n => n.Id == "a").Position); // le node n'a pas bougé
    }

    [Fact]
    public async Task Releasing_a_connection_away_from_handles_creates_nothing()
    {
        var h = Render(edges: []);
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a'] .wf-handle-bottom"), 50, 40);
        await Move(h, 600, 500);
        await Up(h, 600, 500);

        Assert.Empty(h.Connections);
        Assert.Empty(h.Edges);
    }

    [Fact]
    public async Task A_target_handle_cannot_start_a_connection_to_another_target_in_strict_mode()
    {
        var h = Render(edges: []);
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a'] .wf-handle-top"), 50, 0);
        await Move(h, 350, 100); // handle cible de b
        await Up(h, 350, 100);

        Assert.Empty(h.Connections);
    }

    [Fact]
    public async Task Loose_mode_accepts_target_to_target()
    {
        var h = Render(edges: [], configure: p => p.Add(f => f.ConnectionMode, ConnectionMode.Loose));
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a'] .wf-handle-top"), 50, 0);
        await Move(h, 350, 100);
        await Up(h, 350, 100);

        Assert.Single(h.Connections);
    }

    [Fact]
    public async Task IsValidConnection_can_refuse_a_connection()
    {
        var h = Render(edges: [], configure: p => p.Add(f => f.IsValidConnection, _ => false));
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a'] .wf-handle-bottom"), 50, 40);
        await Move(h, 350, 100);
        await Up(h, 350, 100);

        Assert.Empty(h.Connections);
        Assert.Empty(h.Edges);
    }

    [Fact]
    public async Task An_existing_connection_is_not_added_twice()
    {
        var h = Render();
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a'] .wf-handle-bottom"), 50, 40);
        await Move(h, 350, 100);
        await Up(h, 350, 100);

        Assert.Empty(h.Connections);
        Assert.Single(h.Edges);
    }

    [Fact]
    public async Task AutoConnect_false_only_raises_OnConnect()
    {
        var h = Render(edges: [], configure: p => p.Add(f => f.AutoConnect, false));
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a'] .wf-handle-bottom"), 50, 40);
        await Move(h, 350, 100);
        await Up(h, 350, 100);

        Assert.Single(h.Connections);
        Assert.Empty(h.Edges);
    }

    [Fact]
    public async Task Delete_key_removes_selected_nodes_and_their_edges()
    {
        var h = Render();
        await Measure(h);
        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Up(h, 50, 20);

        await h.Cut.Find(".wf-flow").KeyDownAsync(new KeyboardEventArgs { Key = "Delete" });

        Assert.Equal(["b"], h.Nodes.Select(n => n.Id));
        Assert.Empty(h.Edges);
        Assert.Single(h.Cut.FindAll(".wf-node"));
    }

    [Fact]
    public async Task Non_deletable_nodes_survive_the_delete_key()
    {
        var h = Render([A with { Deletable = false }, B]);
        await Measure(h);
        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Up(h, 50, 20);

        await h.Cut.Find(".wf-flow").KeyDownAsync(new KeyboardEventArgs { Key = "Delete" });

        Assert.Equal(2, h.Nodes.Count);
    }

    [Fact]
    public async Task Clicking_an_edge_selects_it()
    {
        var h = Render();
        await Measure(h);

        await Down(h.Cut.Find(".wf-edge-hit"), 200, 70);
        await Up(h, 200, 70);

        Assert.True(h.Edges.Single().Selected);
        Assert.Contains("wf-selected", h.Cut.Find(".wf-edge").ClassList);
    }

    [Fact]
    public async Task Box_selection_with_shift_drag_selects_overlapping_nodes()
    {
        var h = Render();
        await Measure(h);

        await Down(h.Cut.Find(".wf-flow"), -10, -10, shift: true);
        await Move(h, 120, 60); // recouvre a (0..100 × 0..40) mais pas b
        Assert.NotEmpty(h.Cut.FindAll(".wf-selection-box"));
        await Up(h, 120, 60);

        Assert.Equal(["a"], h.Nodes.Where(n => n.Selected).Select(n => n.Id));
        Assert.Empty(h.Cut.FindAll(".wf-selection-box"));
    }

    [Fact]
    public async Task Controlled_mode_publishes_changes_without_applying_them()
    {
        var h = Render(configure: p => p.Add(f => f.AutoApplyChanges, false));
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Move(h, 80, 50);
        await Up(h, 80, 50);

        Assert.Contains(h.NodeChanges.SelectMany(c => c), c => c is NodePositionChange { Id: "a" });
        Assert.Equal(new XYPosition(0, 0), h.Flow.Nodes.Single(n => n.Id == "a").Position);
    }

    [Fact]
    public async Task Arrow_markers_are_defined_for_edges_that_ask_for_them()
    {
        var h = Render(edges: [AB with { MarkerEnd = EdgeMarker.ArrowClosed }]);
        await Measure(h);

        var path = h.Cut.Find(".wf-edge-path");
        var markerId = path.GetAttribute("marker-end")!.Replace("url(#", "").TrimEnd(')');
        Assert.NotEmpty(h.Cut.FindAll($"marker[id='{markerId}']"));
        Assert.Null(path.GetAttribute("marker-start"));
    }

    [Theory]
    [InlineData("straight")]
    [InlineData("step")]
    [InlineData("smoothstep")]
    [InlineData("simplebezier")]
    [InlineData("default")]
    public async Task Every_builtin_edge_type_renders_a_path(string type)
    {
        var h = Render(edges: [AB with { Type = type }]);
        await Measure(h);

        Assert.StartsWith("M", h.Cut.Find(".wf-edge-path").GetAttribute("d"));
    }

    [Fact]
    public async Task Edge_labels_and_animation_are_rendered()
    {
        var h = Render(edges: [AB with { Label = "oui", Animated = true }]);
        await Measure(h);

        Assert.Equal("oui", h.Cut.Find(".wf-edge-label").TextContent);
        Assert.Contains("wf-edge-animated", h.Cut.Find(".wf-edge").ClassList);
    }

    [Fact]
    public async Task Hidden_nodes_and_their_edges_are_not_rendered()
    {
        var h = Render([A, B with { Hidden = true }]);
        await Measure(h);

        Assert.Single(h.Cut.FindAll(".wf-node"));
        Assert.Empty(h.Cut.FindAll(".wf-edge-path"));
    }

    [Fact]
    public async Task Child_nodes_are_placed_relative_to_their_parent()
    {
        var parent = new Node("p", new XYPosition(100, 100), "group");
        var child = new Node("c", new XYPosition(10, 20), ParentId: "p");
        var h = Render([parent, child], []);
        await Measure(h);

        Assert.Contains("translate(110px,120px)", h.Cut.Find("[data-node-id='c']").GetAttribute("style"));
    }

    [Fact]
    public void Node_template_replaces_the_default_content_and_can_fall_back_to_it()
    {
        var h = Render(configure: p => p.Add(f => f.NodeTemplate, n => (RenderFragment)(b =>
        {
            if (n.Id == "a") b.AddMarkupContent(0, "<span class='custom'>sur mesure</span>");
            else
            {
                b.OpenComponent<FlowDefaultNode>(1);
                b.AddAttribute(2, nameof(FlowDefaultNode.Node), n);
                b.CloseComponent();
            }
        })));

        Assert.Equal("sur mesure", h.Cut.Find("[data-node-id='a'] .custom").TextContent);
        Assert.Empty(h.Cut.FindAll("[data-node-id='a'] .wf-node-default"));
        Assert.NotEmpty(h.Cut.FindAll("[data-node-id='b'] .wf-node-default"));
    }

    [Fact]
    public async Task Controls_zoom_and_fit_the_view()
    {
        var h = Render(configure: p => p.Add(f => f.ChildContent, (RenderFragment)(b => { b.OpenComponent<FlowControls>(0); b.CloseComponent(); })));
        await Measure(h);

        h.Cut.Find("button[aria-label='Zoom avant']").Click();
        Assert.True(h.Flow.Viewport.Zoom > 1);
        h.Cut.Find("button[aria-label='Zoom arrière']").Click();
        Assert.Equal(1, h.Flow.Viewport.Zoom, 6);

        h.Cut.Find("button[aria-label='Cadrer la vue']").Click();
        Assert.NotEqual(Viewport.Identity, h.Flow.Viewport);
    }

    [Fact]
    public async Task MiniMap_draws_one_rectangle_per_measured_node()
    {
        var h = Render(configure: p => p.Add(f => f.ChildContent, (RenderFragment)(b => { b.OpenComponent<FlowMiniMap>(0); b.CloseComponent(); })));
        await Measure(h);

        Assert.Equal(2, h.Cut.FindAll(".wf-minimap rect").Count);
    }

    [Fact]
    public void Dark_and_background_parameters_change_the_markup()
    {
        var h = Render(configure: p => p.Add(f => f.Dark, true).Add(f => f.BackgroundVariant, BackgroundVariant.Lines));
        Assert.Contains("wf-dark", h.Cut.Find(".wf-flow").ClassList);
        Assert.NotEmpty(h.Cut.FindAll(".wf-background pattern path"));
    }

    [Fact]
    public void Numbers_in_markup_use_the_invariant_culture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
        try
        {
            var h = Render(configure: p => p.Add(f => f.Viewport, new Viewport(10.5, 20.25, 1.5)));
            Assert.Contains("translate(10.5px,20.25px) scale(1.5)", h.Cut.Find(".wf-viewport").GetAttribute("style"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
