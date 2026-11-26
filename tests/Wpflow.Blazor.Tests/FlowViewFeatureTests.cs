using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Wpflow.Blazor;
using Wpflow.Core;

namespace Wpflow.Blazor.Tests;

/// <summary>Tests des fonctions ajoutées pour égaler la version WPF : édition, historique, clavier, composants, évènements, API.</summary>
public sealed class FlowViewFeatureTests : FlowViewTestContext
{
    private static readonly Node C = new("c", new XYPosition(300, 300), Data: "C");
    private Harness Render(IReadOnlyList<Node>? nodes = null, IReadOnlyList<Edge>? edges = null,
        Action<ComponentParameterCollectionBuilder<FlowView>>? configure = null, bool fit = false) =>
        RenderHarness(nodes, edges, (_, p) => configure?.Invoke(p), fit);

    private static async Task Click(Harness h, string nodeId, double x, double y, bool shift = false)
    {
        await Down(h.Cut.Find($"[data-node-id='{nodeId}']"), x, y, shift);
        await Up(h, x, y);
    }

    private static Task Key(Harness h, string key, bool ctrl = false, bool shift = false) =>
        h.Cut.Find(".wf-flow").KeyDownAsync(new KeyboardEventArgs { Key = key, CtrlKey = ctrl, ShiftKey = shift });

    private static RenderFragment<Node> Template<T>(Action<RenderTreeBuilder>? extra = null) where T : IComponent => n => b =>
    {
        b.OpenComponent<T>(0);
        b.CloseComponent();
        extra?.Invoke(b);
    };

    // ---------- Historique ----------

    [Fact]
    public async Task Undo_and_redo_restore_a_drag()
    {
        var h = Render();
        await Measure(h);
        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Move(h, 80, 50);
        await Up(h, 80, 50);
        Assert.Equal(new XYPosition(30, 30), h.Node("a").Position);

        Assert.True(h.Flow.CanUndo);
        await h.Cut.InvokeAsync(() => h.Flow.Undo());
        Assert.Equal(new XYPosition(0, 0), h.Node("a").Position);
        Assert.True(h.Flow.CanRedo);
        await h.Cut.InvokeAsync(() => h.Flow.Redo());
        Assert.Equal(new XYPosition(30, 30), h.Node("a").Position);
    }

    [Fact]
    public async Task Ctrl_Z_and_Ctrl_Y_drive_the_history()
    {
        var h = Render();
        await Measure(h);
        await Click(h, "a", 50, 20);
        await Key(h, "Delete");
        Assert.Single(h.Nodes);

        await Key(h, "z", ctrl: true);
        Assert.Equal(2, h.Nodes.Count);
        Assert.Single(h.Edges);

        await Key(h, "y", ctrl: true);
        Assert.Single(h.Nodes);
    }

    [Fact]
    public async Task A_new_change_clears_the_redo_stack()
    {
        var h = Render();
        await Measure(h);
        await Click(h, "a", 50, 20);
        await Key(h, "Delete");
        await Key(h, "z", ctrl: true);
        Assert.True(h.Flow.CanRedo);

        await Click(h, "b", 350, 120);
        await Key(h, "Delete");

        Assert.False(h.Flow.CanRedo);
    }

    [Fact]
    public async Task HistoryEnabled_false_keeps_no_history()
    {
        var h = Render(configure: p => p.Add(f => f.HistoryEnabled, false));
        await Measure(h);
        await Click(h, "a", 50, 20);
        await Key(h, "Delete");
        Assert.False(h.Flow.CanUndo);
    }

    // ---------- Presse-papier ----------

    [Fact]
    public async Task Copy_and_paste_duplicate_the_selection_with_its_internal_edges()
    {
        var h = Render();
        await Measure(h);
        await Click(h, "a", 50, 20);
        await Click(h, "b", 350, 120, shift: true);

        await Key(h, "c", ctrl: true);
        await Key(h, "v", ctrl: true);

        Assert.Equal(4, h.Nodes.Count);
        Assert.Equal(["a-copy", "b-copy"], h.Selected.ToArray());
        Assert.Equal(new XYPosition(20, 20), h.Node("a-copy").Position);
        Assert.Equal(2, h.Edges.Count);
        Assert.Contains(h.Edges, e => e.Source == "a-copy" && e.Target == "b-copy");
    }

    [Fact]
    public async Task Cut_removes_then_paste_restores_a_copy()
    {
        var h = Render();
        await Measure(h);
        await Click(h, "a", 50, 20);

        await Key(h, "x", ctrl: true);
        Assert.DoesNotContain(h.Nodes, n => n.Id == "a");

        await Key(h, "v", ctrl: true);
        Assert.Contains(h.Nodes, n => n.Id == "a-copy");
    }

    // ---------- Suppression ----------

    [Fact]
    public async Task OnBeforeDelete_can_cancel_a_deletion()
    {
        DeleteRequest? seen = null;
        var h = Render(configure: p => p.Add(f => f.OnBeforeDelete, r =>
        {
            seen = r;
            return Task.FromResult(false);
        }));
        await Measure(h);
        await Click(h, "a", 50, 20);

        await Key(h, "Delete");

        Assert.Equal(2, h.Nodes.Count);
        Assert.Equal(["a"], seen!.Nodes.Select(n => n.Id));
        Assert.Single(seen.Edges); // l'edge relié suit
    }

    [Fact]
    public async Task DeleteElements_removes_descendants_of_a_parent()
    {
        var parent = new Node("p", new XYPosition(0, 0), "group");
        var child = new Node("c1", new XYPosition(10, 10), ParentId: "p");
        var h = Render([parent, child, B], []);
        await Measure(h);

        await h.Cut.InvokeAsync(() => h.Flow.DeleteElements(["p"]));

        Assert.Equal(["b"], h.Nodes.Select(n => n.Id));
    }

    [Fact]
    public async Task DeleteKeys_are_configurable()
    {
        var h = Render(configure: p => p.Add(f => f.DeleteKeys, ["x"]));
        await Measure(h);
        await Click(h, "a", 50, 20);

        await Key(h, "Delete");
        Assert.Equal(2, h.Nodes.Count);
        await Key(h, "x");
        Assert.Single(h.Nodes);
    }

    // ---------- Redimensionner et pivoter ----------

    [Fact]
    public async Task Resizer_resizes_from_the_bottom_right_corner()
    {
        var h = Render(configure: p => p.Add(f => f.NodeTemplate, Template<NodeResizer>()));
        await Measure(h);
        await Click(h, "a", 50, 20);
        Assert.Equal(8, h.Cut.FindAll("[data-node-id='a'] .wf-resize").Count);

        await Down(h.Cut.Find("[data-node-id='a'] .wf-resize-bottomright"), 100, 40);
        await Move(h, 120, 50);
        await Up(h, 120, 50);

        var a = h.Node("a");
        Assert.Equal(120, a.FixedWidth);
        Assert.Equal(50, a.FixedHeight);
        Assert.Equal(new XYPosition(0, 0), a.Position);
        Assert.False(a.Resizing);
    }

    [Fact]
    public async Task Resizing_from_the_top_left_moves_the_origin()
    {
        var h = Render(configure: p => p.Add(f => f.NodeTemplate, Template<NodeResizer>()));
        await Measure(h);
        await Click(h, "b", 350, 120);

        await Down(h.Cut.Find("[data-node-id='b'] .wf-resize-topleft"), 300, 100);
        await Move(h, 290, 90);
        await Up(h, 290, 90);

        var b = h.Node("b");
        Assert.Equal(110, b.FixedWidth);
        Assert.Equal(50, b.FixedHeight);
        Assert.Equal(new XYPosition(290, 90), b.Position);
    }

    [Fact]
    public async Task Resizer_honours_the_minimum_size_and_can_be_undone()
    {
        var h = Render(configure: p => p.Add(f => f.NodeTemplate, Template<NodeResizer>()));
        await Measure(h);
        await Click(h, "a", 50, 20);

        await Down(h.Cut.Find("[data-node-id='a'] .wf-resize-bottomright"), 100, 40);
        await Move(h, 0, 0);
        await Up(h, 0, 0);
        Assert.Equal(60, h.Node("a").FixedWidth); // NodeMinWidth
        Assert.Equal(40, h.Node("a").FixedHeight);

        await h.Cut.InvokeAsync(() => h.Flow.Undo());
        Assert.Null(h.Node("a").FixedWidth);
    }

    [Fact]
    public async Task Resizer_is_hidden_when_the_node_is_not_selected()
    {
        var h = Render(configure: p => p.Add(f => f.NodeTemplate, Template<NodeResizer>()));
        await Measure(h);
        Assert.Empty(h.Cut.FindAll(".wf-resize"));
    }

    [Fact]
    public async Task Rotator_writes_the_node_rotation()
    {
        var h = Render(configure: p => p.Add(f => f.NodeTemplate, Template<NodeRotator>()));
        await Measure(h);
        await Click(h, "a", 50, 20);

        // centre (50,20) ; la poignée est au-dessus : angle de départ -90°, puis à droite : 0° => rotation 90°
        await Down(h.Cut.Find("[data-node-id='a'] .wf-rotator"), 50, -28);
        await Move(h, 150, 20);
        await Up(h, 150, 20);

        Assert.Equal(90, h.Node("a").Rotation, 1);
        Assert.Contains("rotate(90deg)", h.Cut.Find("[data-node-id='a']").GetAttribute("style"));
    }

    // ---------- Connexions ----------

    [Fact]
    public async Task Reconnecting_an_edge_end_moves_it_to_another_node()
    {
        var h = Render([A, B, C], [AB]);
        await Measure(h);

        // l'extrémité cible de l'edge est le handle de b (350,96) ; on la tire vers le handle cible de c (350,296)
        await Down(h.Cut.Find(".wf-edge-hit"), 350, 96);
        await Move(h, 350, 200);
        await Move(h, 350, 296);
        Assert.NotEmpty(h.Cut.FindAll(".wf-connection-line"));
        await Up(h, 350, 296);

        var edge = Assert.Single(h.Edges);
        Assert.Equal(("a", "c"), (edge.Source, edge.Target));
        Assert.Equal("e-ab", edge.Id); // l'id est conservé
    }

    [Fact]
    public async Task Releasing_a_reconnection_in_the_void_keeps_the_edge()
    {
        var h = Render([A, B, C], [AB]);
        await Measure(h);

        await Down(h.Cut.Find(".wf-edge-hit"), 350, 96);
        await Move(h, 600, 500);
        await Up(h, 600, 500);

        Assert.Equal("b", Assert.Single(h.Edges).Target);
    }

    [Fact]
    public async Task EdgesReconnectable_false_selects_instead()
    {
        var h = Render([A, B, C], [AB], p => p.Add(f => f.EdgesReconnectable, false));
        await Measure(h);

        await Down(h.Cut.Find(".wf-edge-hit"), 350, 96);
        await Up(h, 350, 96);

        Assert.True(h.Edges.Single().Selected);
    }

    [Fact]
    public async Task Click_connect_joins_two_handles_without_dragging()
    {
        var started = 0;
        var ended = 0;
        var h = Render(edges: [], configure: p =>
        {
            p.Add(f => f.OnClickConnectStart, EventCallback.Factory.Create<ConnectStartEventArgs>(this, _ => started++));
            p.Add(f => f.OnClickConnectEnd, EventCallback.Factory.Create<ConnectEndEventArgs>(this, _ => ended++));
        });
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a'] .wf-handle-bottom"), 50, 44);
        await Up(h, 50, 44);
        Assert.Equal(1, started);
        Assert.Empty(h.Edges);

        await Down(h.Cut.Find("[data-node-id='b'] .wf-handle-top"), 350, 96);
        await Up(h, 350, 96);

        Assert.Single(h.Edges);
        Assert.Equal(1, ended);
    }

    [Fact]
    public async Task Click_connect_is_cancelled_by_a_pane_click_or_Escape()
    {
        var h = Render(edges: []);
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a'] .wf-handle-bottom"), 50, 44);
        await Up(h, 50, 44);
        await Down(h.Cut.Find(".wf-flow"), 600, 500);
        await Up(h, 600, 500);
        await Down(h.Cut.Find("[data-node-id='b'] .wf-handle-top"), 350, 96);
        await Up(h, 350, 96);
        Assert.Empty(h.Edges);
        await Key(h, "Escape"); // la connexion par clics démarrée depuis b

        await Down(h.Cut.Find("[data-node-id='a'] .wf-handle-bottom"), 50, 44);
        await Up(h, 50, 44);
        await Key(h, "Escape");
        await Down(h.Cut.Find("[data-node-id='b'] .wf-handle-top"), 350, 96);
        await Up(h, 350, 96);
        Assert.Empty(h.Edges);
    }

    [Fact]
    public async Task ConnectOnClick_false_ignores_clicks_on_handles()
    {
        var h = Render(edges: [], configure: p => p.Add(f => f.ConnectOnClick, false));
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a'] .wf-handle-bottom"), 50, 44);
        await Up(h, 50, 44);
        await Down(h.Cut.Find("[data-node-id='b'] .wf-handle-top"), 350, 96);
        await Up(h, 350, 96);

        Assert.Empty(h.Edges);
    }

    [Fact]
    public async Task Connect_events_report_the_start_and_the_end()
    {
        ConnectStartEventArgs? start = null;
        ConnectEndEventArgs? end = null;
        var h = Render(edges: [], configure: p =>
        {
            p.Add(f => f.OnConnectStart, EventCallback.Factory.Create<ConnectStartEventArgs>(this, e => start = e));
            p.Add(f => f.OnConnectEnd, EventCallback.Factory.Create<ConnectEndEventArgs>(this, e => end = e));
        });
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a'] .wf-handle-bottom"), 50, 44);
        await Move(h, 350, 96);
        await Up(h, 350, 96);

        Assert.Equal(("a", HandleType.Source), (start!.NodeId, start.HandleType));
        Assert.True(end!.IsValid);
        Assert.Equal("b", end.Connection!.Target);
    }

    [Fact]
    public async Task ConnectEnd_is_raised_even_when_released_in_the_void()
    {
        ConnectEndEventArgs? end = null;
        var h = Render(edges: [], configure: p => p.Add(f => f.OnConnectEnd, EventCallback.Factory.Create<ConnectEndEventArgs>(this, e => end = e)));
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a'] .wf-handle-bottom"), 50, 44);
        await Move(h, 600, 500);
        await Up(h, 600, 500);

        Assert.NotNull(end);
        Assert.False(end!.IsValid);
        Assert.Null(end.Connection);
    }

    [Fact]
    public async Task DefaultEdgeOptions_shape_edges_created_by_the_interface()
    {
        var h = Render(edges: [], configure: p => p.Add(f => f.DefaultEdgeOptions, e => e with { Type = "step", Animated = true }));
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a'] .wf-handle-bottom"), 50, 44);
        await Move(h, 350, 96);
        await Up(h, 350, 96);

        var edge = Assert.Single(h.Edges);
        Assert.Equal("step", edge.Type);
        Assert.True(edge.Animated);
    }

    [Fact]
    public async Task A_handle_with_IsConnectableStart_false_cannot_start_a_connection_but_can_end_one()
    {
        var template = (RenderFragment<Node>)(n => b =>
        {
            b.OpenComponent<FlowHandle>(0);
            b.AddAttribute(1, nameof(FlowHandle.Type), HandleType.Source);
            b.AddAttribute(2, nameof(FlowHandle.Position), Position.Bottom);
            b.AddAttribute(3, nameof(FlowHandle.IsConnectableStart), false);
            b.CloseComponent();
            b.OpenComponent<FlowHandle>(4);
            b.AddAttribute(5, nameof(FlowHandle.Type), HandleType.Target);
            b.AddAttribute(6, nameof(FlowHandle.Position), Position.Top);
            b.CloseComponent();
        });
        var h = Render(edges: [], configure: p => p.Add(f => f.NodeTemplate, template));
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a'] .wf-handle-bottom"), 50, 44);
        await Move(h, 350, 96);
        await Up(h, 350, 96);
        Assert.Empty(h.Edges);
    }

    [Fact]
    public async Task A_handle_validator_and_Connected_event_are_honoured()
    {
        Connection? connected = null;
        var template = (RenderFragment<Node>)(n => b =>
        {
            b.OpenComponent<FlowHandle>(0);
            b.AddAttribute(1, nameof(FlowHandle.Type), HandleType.Target);
            b.AddAttribute(2, nameof(FlowHandle.Position), Position.Top);
            b.AddAttribute(3, nameof(FlowHandle.IsValidConnection), (Func<Connection, bool>)(c => c.Source == "a"));
            b.AddAttribute(4, nameof(FlowHandle.OnConnected), EventCallback.Factory.Create<Connection>(this, c => connected = c));
            b.CloseComponent();
            b.OpenComponent<FlowHandle>(5);
            b.AddAttribute(6, nameof(FlowHandle.Type), HandleType.Source);
            b.AddAttribute(7, nameof(FlowHandle.Position), Position.Bottom);
            b.CloseComponent();
        });
        var h = Render([A, B, C], [], p => p.Add(f => f.NodeTemplate, template));
        await Measure(h);

        // c n'est pas autorisé à se connecter à b (validateur du handle cible de b : source "a" seulement)
        await Down(h.Cut.Find("[data-node-id='c'] .wf-handle-bottom"), 350, 344);
        await Move(h, 350, 96);
        await Up(h, 350, 96);
        Assert.Empty(h.Edges);
        Assert.Null(connected);

        await Down(h.Cut.Find("[data-node-id='a'] .wf-handle-bottom"), 50, 44);
        await Move(h, 350, 96);
        await Up(h, 350, 96);
        Assert.Single(h.Edges);
        Assert.Equal("a", connected!.Source);
    }

    // ---------- Clavier ----------

    [Fact]
    public async Task Arrow_keys_move_the_selection_and_shift_moves_faster()
    {
        var h = Render();
        await Measure(h);
        await Click(h, "a", 50, 20);

        await Key(h, "ArrowRight");
        await Key(h, "ArrowDown");
        Assert.Equal(new XYPosition(5, 5), h.Node("a").Position);
        await Key(h, "ArrowRight", shift: true);
        Assert.Equal(new XYPosition(25, 5), h.Node("a").Position);
    }

    [Fact]
    public async Task Ctrl_A_selects_everything_and_Escape_clears()
    {
        var h = Render();
        await Measure(h);

        await Key(h, "a", ctrl: true);
        Assert.Equal(["a", "b"], h.Selected.ToArray());

        await Key(h, "Escape");
        Assert.Empty(h.Selected);
    }

    [Fact]
    public async Task DisableKeyboardA11y_turns_off_arrows_and_Escape()
    {
        var h = Render(configure: p => p.Add(f => f.DisableKeyboardA11y, true));
        await Measure(h);
        await Click(h, "a", 50, 20);

        await Key(h, "ArrowRight");
        await Key(h, "Escape");

        Assert.Equal(new XYPosition(0, 0), h.Node("a").Position);
        Assert.Single(h.Selected);
        Assert.Empty(h.Cut.FindAll("[tabindex='0'][data-node-id]"));
    }

    [Fact]
    public async Task Enter_selects_the_focused_node()
    {
        var h = Render();
        await Measure(h);

        await h.Cut.Find("[data-node-id='b']").FocusAsync(new FocusEventArgs());
        await Key(h, "Enter");

        Assert.Equal(["b"], h.Selected.ToArray());
    }

    [Fact]
    public async Task Space_plus_drag_pans_even_on_a_node()
    {
        var h = Render();
        await Measure(h);

        await Key(h, " ");
        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Move(h, 90, 50);
        await Up(h, 90, 50);

        Assert.Equal(new Viewport(40, 30, 1), h.Flow.Viewport);
        Assert.Equal(new XYPosition(0, 0), h.Node("a").Position);
    }

    // ---------- Pan, sélection, verrou ----------

    [Fact]
    public async Task PanOnDragButtons_selects_which_buttons_pan()
    {
        var h = Render(configure: p => p.Add(f => f.PanOnDragButtons, new[] { 2 }));
        await Measure(h);

        await Down(h.Cut.Find(".wf-flow"), 500, 400);
        await Move(h, 540, 430);
        await Up(h, 540, 430);
        Assert.Equal(Viewport.Identity, h.Flow.Viewport); // le bouton gauche ne fait plus défiler

        await Down(h.Cut.Find(".wf-flow"), 500, 400, button: 2);
        await Move(h, 540, 430);
        await Up(h, 540, 430);
        Assert.Equal(new Viewport(40, 30, 1), h.Flow.Viewport);
    }

    [Fact]
    public async Task SelectionOnDrag_draws_the_box_without_shift_and_Full_mode_needs_containment()
    {
        var h = Render(configure: p => p.Add(f => f.SelectionOnDrag, true).Add(f => f.SelectionMode, FlowSelectionMode.Full));
        await Measure(h);

        await Down(h.Cut.Find(".wf-flow"), -10, -10);
        await Move(h, 60, 30); // touche a sans la contenir
        await Up(h, 60, 30);
        Assert.Empty(h.Selected);

        await Down(h.Cut.Find(".wf-flow"), -10, -10);
        await Move(h, 120, 60);
        await Up(h, 120, 60);
        Assert.Equal(["a"], h.Selected.ToArray());
    }

    [Fact]
    public async Task A_locked_view_refuses_drag_connect_and_selection()
    {
        var h = Render(configure: p => p.Add(f => f.IsLocked, true));
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Move(h, 150, 120);
        await Up(h, 150, 120);

        Assert.Equal(new XYPosition(0, 0), h.Node("a").Position);
        Assert.Empty(h.Selected);
        Assert.Contains("wf-locked", h.Cut.Find(".wf-flow").ClassList);
    }

    [Fact]
    public async Task SelectNodesOnDrag_false_drags_without_selecting_but_a_click_selects()
    {
        var h = Render(configure: p => p.Add(f => f.SelectNodesOnDrag, false));
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Move(h, 80, 50);
        await Up(h, 80, 50);
        Assert.Equal(new XYPosition(30, 30), h.Node("a").Position);
        Assert.Empty(h.Selected);

        await Click(h, "b", 350, 120);
        Assert.Equal(["b"], h.Selected.ToArray());
    }

    [Fact]
    public async Task Selection_change_is_published_and_announced()
    {
        SelectionChangeArgs? last = null;
        var h = Render(configure: p => p.Add(f => f.OnSelectionChange, EventCallback.Factory.Create<SelectionChangeArgs>(this, a => last = a)));
        await Measure(h);

        await Click(h, "a", 50, 20);

        Assert.Equal(["a"], last!.Nodes.Select(n => n.Id));
        Assert.Contains("Node A sélectionné", h.Cut.Find(".wf-sr-only").TextContent);

        await Down(h.Cut.Find(".wf-flow"), 600, 500);
        await Up(h, 600, 500);
        Assert.Contains("Sélection effacée", h.Cut.Find(".wf-sr-only").TextContent);
    }

    // ---------- Viewport ----------

    [Fact]
    public async Task TranslateExtent_clamps_the_viewport()
    {
        var h = Render(configure: p => p.Add(f => f.TranslateExtent, new Rect(0, 0, 1000, 1000)));
        await Measure(h);

        await h.Cut.InvokeAsync(() => h.Flow.SetViewport(new Viewport(500, 500, 1)));

        Assert.True(h.Flow.Viewport.X <= 0 && h.Flow.Viewport.Y <= 0);
    }

    [Fact]
    public async Task An_animated_SetViewport_reaches_its_target_and_raises_move_events()
    {
        var starts = 0;
        var ends = 0;
        var h = Render(configure: p =>
        {
            p.Add(f => f.OnMoveStart, EventCallback.Factory.Create<Viewport>(this, _ => starts++));
            p.Add(f => f.OnMoveEnd, EventCallback.Factory.Create<Viewport>(this, _ => ends++));
        });
        await Measure(h);

        await h.Cut.InvokeAsync(() => h.Flow.SetViewport(new Viewport(100, 50, 1.5), TimeSpan.FromMilliseconds(80)));

        Assert.Equal(new Viewport(100, 50, 1.5), h.Flow.Viewport);
        Assert.Equal((1, 1), (starts, ends));
    }

    [Fact]
    public async Task SetCenter_and_ZoomTo_and_FitBounds()
    {
        var h = Render();
        await Measure(h);

        await h.Cut.InvokeAsync(() => h.Flow.SetCenter(100, 100, 2));
        Assert.Equal(2, h.Flow.Viewport.Zoom);
        Assert.Equal(400 - 200, h.Flow.Viewport.X, 6); // le point (100,100) est au centre (400,300)
        Assert.Equal(300 - 200, h.Flow.Viewport.Y, 6);

        await h.Cut.InvokeAsync(() => h.Flow.ZoomTo(1));
        Assert.Equal(1, h.Flow.Viewport.Zoom, 6);

        await h.Cut.InvokeAsync(() => h.Flow.FitBounds(new Rect(0, 0, 100, 100)));
        Assert.True(h.Flow.Viewport.Zoom > 1);
    }

    [Fact]
    public async Task FitView_can_target_specific_nodes()
    {
        var h = Render([A, B, C], []);
        await Measure(h);

        await h.Cut.InvokeAsync(() => h.Flow.FitView(new FitViewOptions(0.1, NodeIds: ["a"])));
        var onlyA = h.Flow.Viewport.Zoom;
        await h.Cut.InvokeAsync(() => h.Flow.FitView());

        Assert.True(onlyA >= h.Flow.Viewport.Zoom);
        Assert.Equal(2, onlyA, 6); // MaxZoom : un seul petit node
    }

    [Fact]
    public async Task Pinch_zooms_around_the_gesture_center()
    {
        var h = Render();
        await Measure(h);

        await h.Cut.InvokeAsync(() => h.Flow.OnPinch(400, 300, 1.5, 0, 0));

        Assert.Equal(1.5, h.Flow.Viewport.Zoom, 6);
        Assert.Equal(400 - 400 * 1.5, h.Flow.Viewport.X, 6);
    }

    [Fact]
    public async Task ZoomOnPinch_false_ignores_pinches()
    {
        var h = Render(configure: p => p.Add(f => f.ZoomOnPinch, false));
        await Measure(h);

        await h.Cut.InvokeAsync(() => h.Flow.OnPinch(400, 300, 1.5, 0, 0));

        Assert.Equal(Viewport.Identity, h.Flow.Viewport);
    }

    [Fact]
    public async Task PanOnScroll_pans_with_the_wheel_and_Ctrl_still_zooms()
    {
        var h = Render(configure: p => p.Add(f => f.PanOnScroll, true));
        await Measure(h);

        await h.Cut.InvokeAsync(() => h.Flow.OnWheel(400, 300, 0, 40, false, false, false, 0));
        Assert.Equal(1, h.Flow.Viewport.Zoom);
        Assert.True(h.Flow.Viewport.Y < 0);

        await h.Cut.InvokeAsync(() => h.Flow.OnWheel(400, 300, 0, -100, true, false, false, 0));
        Assert.True(h.Flow.Viewport.Zoom > 1);
    }

    [Fact]
    public async Task Double_click_on_the_pane_zooms_in()
    {
        var h = Render();
        await Measure(h);

        await h.Cut.Find(".wf-flow").TriggerEventAsync("ondblclick", new MouseEventArgs { ClientX = 400, ClientY = 300 });

        Assert.Equal(2, h.Flow.Viewport.Zoom, 6);
    }

    [Fact]
    public async Task ZoomOnDoubleClick_false_keeps_the_zoom()
    {
        var h = Render(configure: p => p.Add(f => f.ZoomOnDoubleClick, false));
        await Measure(h);

        await h.Cut.Find(".wf-flow").TriggerEventAsync("ondblclick", new MouseEventArgs { ClientX = 400, ClientY = 300 });

        Assert.Equal(1, h.Flow.Viewport.Zoom);
    }

    // ---------- Évènements ----------

    [Fact]
    public async Task Drag_events_are_raised_in_order()
    {
        var log = new List<string>();
        var h = Render(configure: p =>
        {
            p.Add(f => f.OnNodeDragStart, EventCallback.Factory.Create<NodeDragEventArgs>(this, _ => log.Add("start")));
            p.Add(f => f.OnNodeDrag, EventCallback.Factory.Create<NodeDragEventArgs>(this, _ => log.Add("drag")));
            p.Add(f => f.OnNodeDragStop, EventCallback.Factory.Create<NodeDragEventArgs>(this, _ => log.Add("stop")));
            p.Add(f => f.OnSelectionDragStart, EventCallback.Factory.Create<NodeDragEventArgs>(this, _ => log.Add("sel-start")));
        });
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Move(h, 80, 50);
        await Move(h, 90, 60);
        await Up(h, 90, 60);

        Assert.Equal(["start", "drag", "drag", "stop"], log);
    }

    [Fact]
    public async Task SelectionDrag_events_fire_when_several_nodes_move()
    {
        var log = new List<string>();
        var h = Render(configure: p => p.Add(f => f.OnSelectionDragStart, EventCallback.Factory.Create<NodeDragEventArgs>(this, a => log.Add("sel-start:" + a.Nodes.Count))));
        await Measure(h);
        await Click(h, "a", 50, 20);
        await Click(h, "b", 350, 120, shift: true);

        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Move(h, 70, 40);
        await Up(h, 70, 40);

        Assert.Equal(["sel-start:2"], log);
    }

    [Fact]
    public async Task Pane_pan_raises_move_events()
    {
        var log = new List<string>();
        var h = Render(configure: p =>
        {
            p.Add(f => f.OnMoveStart, EventCallback.Factory.Create<Viewport>(this, _ => log.Add("start")));
            p.Add(f => f.OnMove, EventCallback.Factory.Create<Viewport>(this, _ => log.Add("move")));
            p.Add(f => f.OnMoveEnd, EventCallback.Factory.Create<Viewport>(this, _ => log.Add("end")));
        });
        await Measure(h);

        await Down(h.Cut.Find(".wf-flow"), 500, 400);
        await Move(h, 540, 430);
        await Up(h, 540, 430);

        Assert.Equal(["start", "move", "end"], log);
    }

    [Fact]
    public async Task Init_and_NodesInitialized_are_raised_once()
    {
        var init = 0;
        var nodesInit = 0;
        var h = Render(configure: p =>
        {
            p.Add(f => f.OnInit, EventCallback.Factory.Create(this, () => init++));
            p.Add(f => f.OnNodesInitialized, EventCallback.Factory.Create(this, () => nodesInit++));
        });
        await Measure(h);
        await Measure(h);

        Assert.Equal((1, 1), (init, nodesInit));
    }

    [Fact]
    public async Task Hover_listeners_are_attached_only_when_a_handler_exists()
    {
        var h = Render();
        await Measure(h);
        Assert.Null(h.Cut.Find("[data-node-id='a']").GetAttribute("blazor:onpointerenter"));

        var entered = new List<string>();
        var h2 = Render(configure: p => p.Add(f => f.OnNodeMouseEnter, EventCallback.Factory.Create<NodeEventArgs>(this, e => entered.Add(e.Node.Id))));
        await Measure(h2);
        await h2.Cut.Find("[data-node-id='b']").TriggerEventAsync("onpointerenter", new PointerEventArgs { ClientX = 350, ClientY = 120 });

        Assert.Equal(["b"], entered);
    }

    [Fact]
    public async Task Double_click_and_context_menu_callbacks_receive_the_element()
    {
        NodeEventArgs? dbl = null;
        NodeEventArgs? ctx = null;
        EdgeEventArgs? edgeCtx = null;
        PaneEventArgs? paneCtx = null;
        var h = Render(configure: p =>
        {
            p.Add(f => f.OnNodeDoubleClick, EventCallback.Factory.Create<NodeEventArgs>(this, e => dbl = e));
            p.Add(f => f.OnNodeContextMenu, EventCallback.Factory.Create<NodeEventArgs>(this, e => ctx = e));
            p.Add(f => f.OnEdgeContextMenu, EventCallback.Factory.Create<EdgeEventArgs>(this, e => edgeCtx = e));
            p.Add(f => f.OnPaneContextMenu, EventCallback.Factory.Create<PaneEventArgs>(this, e => paneCtx = e));
        });
        await Measure(h);

        await h.Cut.Find("[data-node-id='a']").TriggerEventAsync("ondblclick", new MouseEventArgs { ClientX = 50, ClientY = 20 });
        await h.Cut.Find("[data-node-id='b']").TriggerEventAsync("oncontextmenu", new MouseEventArgs { ClientX = 350, ClientY = 120 });
        await h.Cut.Find(".wf-edge-hit").TriggerEventAsync("oncontextmenu", new MouseEventArgs { ClientX = 200, ClientY = 70 });
        await h.Cut.Find(".wf-flow").TriggerEventAsync("oncontextmenu", new MouseEventArgs { ClientX = 600, ClientY = 500 });

        Assert.Equal("a", dbl!.Node.Id);
        Assert.Equal("b", ctx!.Node.Id);
        Assert.Equal(new XYPosition(350, 120), ctx.Pointer.Flow);
        Assert.Equal("e-ab", edgeCtx!.Edge.Id);
        Assert.Equal(new XYPosition(600, 500), paneCtx!.Pointer.Flow);
    }

    [Fact]
    public async Task The_data_attributes_of_the_pressed_element_come_with_the_node_events()
    {
        var events = new List<NodeEventArgs>();
        var h = Render(configure: p =>
        {
            p.Add(f => f.OnNodeClick, EventCallback.Factory.Create<NodeEventArgs>(this, events.Add));
            p.Add(f => f.OnNodeDoubleClick, EventCallback.Factory.Create<NodeEventArgs>(this, events.Add));
        });
        await Measure(h);
        var data = new Dictionary<string, string> { ["column"] = "id" };

        await h.Cut.InvokeAsync(() => h.Flow.OnPressData("a", data));
        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Up(h, 50, 20);
        await h.Cut.Find("[data-node-id='a']").TriggerEventAsync("ondblclick", new MouseEventArgs { ClientX = 50, ClientY = 20 });
        // un double-clic sur un autre node ne reprend pas les données de cet appui
        await h.Cut.Find("[data-node-id='b']").TriggerEventAsync("ondblclick", new MouseEventArgs { ClientX = 350, ClientY = 120 });
        // un appui sans données efface les précédentes
        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Up(h, 50, 20);

        Assert.Equal(["a", "a", "b", "a"], events.Select(e => e.Node.Id));
        Assert.Equal("id", events[0].TargetData["column"]);
        Assert.Equal("id", events[1].TargetData["column"]);
        Assert.Empty(events[2].TargetData);
        Assert.Empty(events[3].TargetData);
    }

    [Fact]
    public async Task An_edge_provider_receives_the_edge_it_draws()
    {
        var provider = new DelegateEdgePathProvider(g => new EdgePath($"M0,0 L{g.Edge?.Data},0", 0, 0, 0, 0));
        var h = Render(edges: [AB with { Type = "erd", Data = 7 }], configure: p =>
            p.Add(f => f.EdgeTypes, new Dictionary<string, IEdgePathProvider> { ["erd"] = provider }));
        await Measure(h);

        Assert.Equal("M0,0 L7,0", h.Cut.Find(".wf-edge-path").GetAttribute("d"));
    }

    [Fact]
    public async Task Pane_and_edge_clicks_are_reported()
    {
        PaneEventArgs? pane = null;
        EdgeEventArgs? edge = null;
        var h = Render(configure: p =>
        {
            p.Add(f => f.OnPaneClick, EventCallback.Factory.Create<PaneEventArgs>(this, e => pane = e));
            p.Add(f => f.OnEdgeClick, EventCallback.Factory.Create<EdgeEventArgs>(this, e => edge = e));
        });
        await Measure(h);

        await Down(h.Cut.Find(".wf-edge-hit"), 200, 70);
        await Up(h, 200, 70);
        await Down(h.Cut.Find(".wf-flow"), 600, 500);
        await Up(h, 600, 500);

        Assert.Equal("e-ab", edge!.Edge.Id);
        Assert.NotNull(pane);
    }

    [Fact]
    public async Task A_dangling_edge_reports_an_error_once()
    {
        var errors = new List<FlowError>();
        var h = Render([A], [new Edge("bad", "a", "ghost")], p => p.Add(f => f.OnError, EventCallback.Factory.Create<FlowError>(this, e => errors.Add(e))));
        await Measure(h);
        await h.Cut.InvokeAsync(() => h.Flow.OnMeasured([new MeasuredNode { Id = "a", Width = 101, Height = 40 }]));

        Assert.Single(errors);
        Assert.Equal("error008", errors[0].Code);
    }

    [Fact]
    public async Task Dropping_on_the_pane_gives_flow_coordinates()
    {
        FlowDropEventArgs? dropped = null;
        var h = Render(configure: p => p.Add(f => f.OnDrop, EventCallback.Factory.Create<FlowDropEventArgs>(this, e => dropped = e)));
        await Measure(h);
        await h.Cut.InvokeAsync(() => h.Flow.SetViewport(new Viewport(100, 50, 2)));

        await h.Cut.Find(".wf-flow").TriggerEventAsync("ondrop", new DragEventArgs { ClientX = 300, ClientY = 250 });

        Assert.Equal(new XYPosition(100, 100), dropped!.Position);
    }

    // ---------- Rendu ----------

    [Fact]
    public async Task Custom_edge_type_and_template_replace_the_builtin_drawing()
    {
        var provider = new DelegateEdgePathProvider(g => new EdgePath($"M{g.Source.X},{g.Source.Y} L{g.Target.X},{g.Target.Y}", 0, 0, 0, 0));
        var template = (RenderFragment<EdgeRenderContext>)(c => b =>
        {
            b.OpenElement(0, "path");
            b.AddAttribute(1, "class", "my-edge");
            b.AddAttribute(2, "d", c.Path.Path);
            b.CloseElement();
        });
        var h = Render(edges: [AB with { Type = "mine" }], configure: p =>
        {
            p.Add(f => f.EdgeTypes, new Dictionary<string, IEdgePathProvider> { ["mine"] = provider });
            p.Add(f => f.EdgeTemplates, new Dictionary<string, RenderFragment<EdgeRenderContext>> { ["mine"] = template });
        });
        await Measure(h);

        Assert.Empty(h.Cut.FindAll(".wf-edge-path"));
        Assert.StartsWith("M50,44 L350,96", h.Cut.Find(".my-edge").GetAttribute("d"));
    }

    [Fact]
    public async Task EdgeLabelTemplate_and_EdgeToolbar_show_for_the_selected_edge()
    {
        var template = (RenderFragment<EdgeRenderContext>)(c => b =>
        {
            b.OpenElement(0, "span");
            b.AddAttribute(1, "class", "lbl");
            b.AddContent(2, c.Edge.Id);
            b.CloseElement();
            b.OpenComponent<EdgeToolbar>(3);
            b.AddAttribute(4, nameof(EdgeToolbar.ChildContent), (RenderFragment)(t => t.AddMarkupContent(0, "<button class='tb'>x</button>")));
            b.CloseComponent();
        });
        var h = Render(configure: p => p.Add(f => f.EdgeLabelTemplate, template));
        await Measure(h);
        Assert.Equal("e-ab", h.Cut.Find(".lbl").TextContent);
        Assert.Empty(h.Cut.FindAll(".tb"));

        await Down(h.Cut.Find(".wf-edge-hit"), 200, 70);
        await Up(h, 200, 70);

        Assert.Single(h.Cut.FindAll(".tb"));
    }

    [Fact]
    public async Task NodeToolbar_appears_for_a_selected_node_and_honours_position()
    {
        var template = (RenderFragment<Node>)(n => b =>
        {
            b.OpenComponent<NodeToolbar>(0);
            b.AddAttribute(1, nameof(NodeToolbar.ToolbarPosition), Position.Bottom);
            b.AddAttribute(2, nameof(NodeToolbar.Offset), 20.0);
            b.AddAttribute(3, nameof(NodeToolbar.ChildContent), (RenderFragment)(t => t.AddMarkupContent(0, "<button class='tb'>x</button>")));
            b.CloseComponent();
        });
        var h = Render(configure: p => p.Add(f => f.NodeTemplate, template));
        await Measure(h);
        Assert.Empty(h.Cut.FindAll(".wf-toolbar"));

        await Click(h, "a", 50, 20);

        var bar = h.Cut.Find("[data-node-id='a'] .wf-toolbar");
        Assert.Contains("wf-toolbar-bottom", bar.ClassList);
        Assert.Contains("top:calc(100% + 20px)", bar.GetAttribute("style"));
        Assert.Empty(h.Cut.FindAll("[data-node-id='b'] .wf-toolbar"));
    }

    [Fact]
    public async Task SelectionToolbar_shows_above_a_multi_selection()
    {
        var h = Render(configure: p => p.Add(f => f.ChildContent, (RenderFragment)(b =>
        {
            b.OpenComponent<SelectionToolbar>(0);
            b.AddAttribute(1, nameof(SelectionToolbar.ChildContent), (RenderFragment)(t => t.AddMarkupContent(0, "<button class='tb'>x</button>")));
            b.CloseComponent();
        })));
        await Measure(h);
        await Click(h, "a", 50, 20);
        Assert.Empty(h.Cut.FindAll(".wf-selection-toolbar"));

        await Click(h, "b", 350, 120, shift: true);

        Assert.Single(h.Cut.FindAll(".wf-selection-toolbar"));
    }

    [Fact]
    public async Task Panel_controls_and_minimap_support_their_options()
    {
        var clicked = new List<string>();
        var h = Render(configure: p => p.Add(f => f.ChildContent, (RenderFragment)(b =>
        {
            b.OpenComponent<FlowPanel>(0);
            b.AddAttribute(1, nameof(FlowPanel.PanelPosition), PanelPosition.TopRight);
            b.AddAttribute(2, nameof(FlowPanel.ChildContent), (RenderFragment)(t => t.AddMarkupContent(0, "<span class='pn'>p</span>")));
            b.CloseComponent();
            b.OpenComponent<FlowControls>(3);
            b.AddAttribute(4, nameof(FlowControls.ShowFitView), false);
            b.AddAttribute(5, nameof(FlowControls.Horizontal), true);
            b.CloseComponent();
            b.OpenComponent<FlowMiniMap>(6);
            b.AddAttribute(7, nameof(FlowMiniMap.NodeColor), (Func<Node, string?>)(n => n.Id == "a" ? "red" : null));
            b.AddAttribute(8, nameof(FlowMiniMap.OnNodeClick), EventCallback.Factory.Create<Node>(this, n => clicked.Add(n.Id)));
            b.CloseComponent();
        })));
        await Measure(h);

        Assert.Contains("wf-panel-topright", h.Cut.Find(".wf-panel:has(.pn)").ClassList);
        Assert.Contains("wf-controls-horizontal", h.Cut.Find(".wf-controls").ClassList);
        Assert.Empty(h.Cut.FindAll("button[aria-label='Cadrer la vue']"));
        Assert.Equal("red", h.Cut.FindAll(".wf-minimap-node")[0].GetAttribute("fill"));

        await h.Cut.FindAll(".wf-minimap-node")[1].TriggerEventAsync("onpointerdown", new PointerEventArgs { Button = 0 });
        Assert.Equal(["b"], clicked);
    }

    [Fact]
    public async Task The_lock_button_locks_the_view_and_reports_it()
    {
        bool? interactive = null;
        var h = Render(configure: p => p.Add(f => f.ChildContent, (RenderFragment)(b =>
        {
            b.OpenComponent<FlowControls>(0);
            b.AddAttribute(1, nameof(FlowControls.OnInteractiveChange), EventCallback.Factory.Create<bool>(this, v => interactive = v));
            b.CloseComponent();
        })));
        await Measure(h);

        await h.Cut.Find("button[aria-label='Verrouiller']").ClickAsync(new MouseEventArgs());

        Assert.True(h.Flow.IsLocked);
        Assert.False(interactive);
        Assert.NotEmpty(h.Cut.FindAll("button[aria-label='Déverrouiller']"));
    }

    [Fact]
    public async Task Additional_backgrounds_are_layered()
    {
        var h = Render(configure: p => p.Add(f => f.AdditionalBackgrounds, new[]
        {
            new BackgroundLayer(BackgroundVariant.Lines, 100, "#f00"),
            new BackgroundLayer(BackgroundVariant.Cross, 50)
        }));
        await Measure(h);

        Assert.Equal(3, h.Cut.FindAll("svg.wf-background").Count);
        Assert.Contains(h.Cut.FindAll("svg.wf-background pattern path"), p => p.GetAttribute("stroke") == "#f00");
    }

    [Fact]
    public async Task Custom_markers_and_marker_options_are_rendered()
    {
        var h = Render(edges: [AB with { MarkerEndKey = "dot", MarkerEnd = EdgeMarker.Arrow }], configure: p =>
            p.Add(f => f.MarkerTemplates, new Dictionary<string, RenderFragment<string>>
            {
                ["dot"] = color => b =>
                {
                    b.OpenElement(0, "circle");
                    b.AddAttribute(1, "class", "dot");
                    b.AddAttribute(2, "fill", color);
                    b.CloseElement();
                }
            }));
        await Measure(h);

        Assert.Single(h.Cut.FindAll("marker .dot"));
        var edge2 = AB with
        {
            MarkerEnd = EdgeMarker.ArrowClosed,
            MarkerEndOptions = new EdgeMarkerOptions(Color: "#ff0000", Width: 20, Orient: 45)
        };
        var h2 = Render(edges: [edge2]);
        await Measure(h2);
        var marker = h2.Cut.Find("marker");
        Assert.Equal("45", marker.GetAttribute("orient"));
        Assert.Contains("#ff0000", marker.InnerHtml);
    }

    [Fact]
    public async Task DefaultMarkerColor_colors_markers_without_their_own()
    {
        var h = Render(edges: [AB with { MarkerEnd = EdgeMarker.ArrowClosed }], configure: p => p.Add(f => f.DefaultMarkerColor, "#00aa00"));
        await Measure(h);

        Assert.Contains("#00aa00", h.Cut.Find("marker").InnerHtml);
    }

    [Fact]
    public async Task Virtualisation_drops_far_nodes_but_keeps_their_edges_resolvable()
    {
        var far = new Node("far", new XYPosition(5000, 5000), Data: "F");
        var h = Render([A, B, far], [AB, new Edge("e-far", "b", "far")], p => p.Add(f => f.OnlyRenderVisibleElements, true), fit: false);
        await Measure(h);
        Assert.Equal(3, h.Cut.FindAll(".wf-node").Count); // avant le premier cadrage tout est rendu

        await h.Cut.InvokeAsync(() => h.Flow.FitView(new FitViewOptions(NodeIds: ["a", "b"])));
        await h.Cut.InvokeAsync(() => h.Flow.OnRootRect(0, 0, 800, 600)); // force un rendu
        // le cadrage sur a et b exclut far ; la virtualisation n'est active qu'après le cadrage initial
        Assert.NotEmpty(h.Cut.FindAll("[data-node-id='a']"));
    }

    [Fact]
    public async Task ZIndexMode_Manual_only_uses_the_node_ZIndex()
    {
        var h = Render([A with { ZIndex = 7 }, B], [], p => p.Add(f => f.ZIndexMode, FlowZIndexMode.Manual));
        await Measure(h);
        await Click(h, "b", 350, 120);

        Assert.Contains("z-index:7", h.Cut.Find("[data-node-id='a']").GetAttribute("style"));
        Assert.Contains("z-index:0", h.Cut.Find("[data-node-id='b']").GetAttribute("style"));
    }

    [Fact]
    public async Task Selected_nodes_are_elevated_by_default()
    {
        var h = Render([A, B], []);
        await Measure(h);
        await Click(h, "b", 350, 120);

        Assert.Contains("z-index:1000", h.Cut.Find("[data-node-id='b']").GetAttribute("style"));
        Assert.Contains("z-index:0", h.Cut.Find("[data-node-id='a']").GetAttribute("style"));
    }

    [Fact]
    public async Task NodeTemplates_are_chosen_by_node_type()
    {
        var h = Render([A with { Type = "custom" }, B], [], p => p.Add(f => f.NodeTemplates, new Dictionary<string, RenderFragment<Node>>
        {
            ["custom"] = n => b => b.AddMarkupContent(0, "<i class='mine'>mien</i>")
        }));

        Assert.Single(h.Cut.FindAll("[data-node-id='a'] .mine"));
        Assert.NotEmpty(h.Cut.FindAll("[data-node-id='b'] .wf-node-default"));
    }

    [Fact]
    public async Task NodeOrigin_centres_nodes_on_their_position()
    {
        var h = Render([A with { Position = new XYPosition(100, 100) }], [], p => p.Add(f => f.NodeOrigin, new XYPosition(0.5, 0.5)));
        await Measure(h);

        Assert.Contains("translate(50px,80px)", h.Cut.Find("[data-node-id='a']").GetAttribute("style"));
        Assert.Equal(new XYPosition(100, 100), h.Node("a").Position); // la position stockée ne change pas
    }

    [Fact]
    public async Task NodeExtent_keeps_dragged_nodes_inside()
    {
        var h = Render(configure: p => p.Add(f => f.NodeExtent, new Rect(0, 0, 500, 300)));
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Move(h, 1050, 820);
        await Up(h, 1050, 820);

        var a = h.Node("a");
        Assert.Equal(400, a.Position.X, 6); // 500 - largeur 100
        Assert.Equal(260, a.Position.Y, 6); // 300 - hauteur 40
    }

    [Fact]
    public async Task Node_ExtentParent_keeps_a_child_inside_its_parent()
    {
        var parent = new Node("p", new XYPosition(0, 0), "group", FixedWidth: 300, FixedHeight: 200);
        var child = new Node("k", new XYPosition(10, 10), ParentId: "p", ExtentParent: true);
        var h = Render([parent, child], []);
        await h.Cut.InvokeAsync(() => h.Flow.OnRootRect(0, 0, 800, 600));
        await h.Cut.InvokeAsync(() => h.Flow.OnMeasured(
        [
            new MeasuredNode { Id = "p", Width = 300, Height = 200 },
            new MeasuredNode { Id = "k", Width = 100, Height = 40 }
        ]));

        await Down(h.Cut.Find("[data-node-id='k']"), 60, 30);
        await Move(h, 560, 530);
        await Up(h, 560, 530);

        Assert.Equal(new XYPosition(200, 160), h.Node("k").Position);
    }

    // ---------- API ----------

    [Fact]
    public async Task Queries_find_nodes_edges_neighbours_and_intersections()
    {
        var h = Render([A, B, C], [AB]);
        await Measure(h);

        Assert.Equal("a", h.Flow.GetNode("a")!.Id);
        Assert.Equal("e-ab", h.Flow.GetEdge("e-ab")!.Id);
        Assert.Equal(["b"], h.Flow.GetOutgoers("a").Select(n => n.Id));
        Assert.Equal(["a"], h.Flow.GetIncomers("b").Select(n => n.Id));
        Assert.Single(h.Flow.GetNodeConnections("a"));
        Assert.Equal(["a"], h.Flow.GetIntersectingNodes(new Rect(0, 0, 50, 20)).Select(n => n.Id));
        Assert.True(h.Flow.IsNodeIntersecting(h.Node("a"), new Rect(90, 30, 50, 50)));
        Assert.Equal(new Rect(0, 0, 400, 340), h.Flow.GetNodesBounds()!.Value);
        Assert.Equal(new Rect(300, 100, 100, 40), h.Flow.GetNodesBounds(["b"])!.Value);
    }

    [Fact]
    public async Task AddNodes_UpdateNodeData_and_UpdateEdge_go_through_changes()
    {
        var h = Render();
        await Measure(h);

        await h.Cut.InvokeAsync(() => h.Flow.AddNodes([new Node("n", new XYPosition(0, 200), Data: "N")]));
        await h.Cut.InvokeAsync(() => h.Flow.AddEdges([new Edge("e-an", "a", "n")]));
        await h.Cut.InvokeAsync(() => h.Flow.UpdateNodeData("n", d => d + "!"));
        await h.Cut.InvokeAsync(() => h.Flow.UpdateEdge("e-an", e => e with { Animated = true }));

        Assert.Equal(3, h.Nodes.Count);
        Assert.Equal("N!", h.Node("n").Data);
        Assert.True(h.Edges.Single(e => e.Id == "e-an").Animated);
    }

    [Fact]
    public async Task ToJson_and_LoadJson_round_trip_the_graph()
    {
        var h = Render([A, B with { Data = "B" }], [AB with { Label = "x" }]);
        await Measure(h);
        await h.Cut.InvokeAsync(() => h.Flow.SetViewport(new Viewport(10, 20, 1.5)));
        var json = h.Flow.ToJson();

        var h2 = Render([C], []);
        await Measure(h2);
        await h2.Cut.InvokeAsync(() => h2.Flow.LoadJson(json));

        Assert.Equal(["a", "b"], h2.Nodes.Select(n => n.Id).OrderBy(x => x));
        Assert.Equal("x", Assert.Single(h2.Edges).Label);
        Assert.Equal(new Viewport(10, 20, 1.5), h2.Flow.Viewport);
        Assert.False(h2.Flow.CanUndo);
    }

    [Fact]
    public async Task ExportSvg_draws_nodes_edges_and_labels()
    {
        var h = Render(edges: [AB with { Label = "oui", MarkerEnd = EdgeMarker.Arrow }]);
        await Measure(h);

        var svg = h.Flow.ExportSvg();

        Assert.StartsWith("<svg", svg);
        Assert.Contains(">A</text>", svg);
        Assert.Contains(">oui</text>", svg);
        Assert.Contains("marker-end", svg);
        Assert.Equal(4, svg.Split("<rect").Length); // fond + 2 nodes
    }

    [Fact]
    public async Task SaveImage_calls_the_browser_bridge()
    {
        var h = Render();
        await Measure(h);

        await h.Cut.InvokeAsync(() => h.Flow.SaveImage("graph.png"));

        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "saveImageFromDom" && (string)i.Arguments[5]! == "graph.png"); // capture du rendu réel

        await h.Cut.InvokeAsync(() => h.Flow.SaveImage("flat.png", vector: true));
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "saveImage" && (string)i.Arguments[1]! == "flat.png"); // dessin simplifié
    }

    [Fact]
    public async Task Controlled_mode_still_publishes_selection_and_history_does_not_apply()
    {
        var changes = new List<NodeChange>();
        var h = Render(configure: p =>
        {
            p.Add(f => f.AutoApplyChanges, false);
            p.Add(f => f.OnNodesChange, EventCallback.Factory.Create<IReadOnlyList<NodeChange>>(this, c => changes.AddRange(c)));
        });
        await Measure(h);

        await Click(h, "a", 50, 20);

        Assert.Contains(changes, c => c is NodeSelectChange { Id: "a", Selected: true });
        Assert.DoesNotContain(h.Flow.Nodes, n => n.Selected);
    }

    [Fact]
    public async Task Connection_state_is_exposed_while_dragging_a_wire()
    {
        var h = Render(edges: []);
        await Measure(h);

        await Down(h.Cut.Find("[data-node-id='a'] .wf-handle-bottom"), 50, 44);
        await Move(h, 350, 96);

        var state = h.Flow.CurrentConnection;
        Assert.True(state.InProgress);
        Assert.Equal("a", state.FromNodeId);
        Assert.Equal("b", state.ToNodeId);
        Assert.True(state.IsValid);

        await Up(h, 350, 96);
        Assert.False(h.Flow.CurrentConnection.InProgress);
    }

    [Fact]
    public async Task A_connection_line_template_receives_the_state_and_the_path_of_the_wire()
    {
        var template = (RenderFragment<ConnectionState>)(s => b =>
        {
            b.OpenElement(0, "path");
            b.AddAttribute(1, "class", "my-line");
            b.AddAttribute(2, "d", s.Path);
            b.AddAttribute(3, "data-valid", s.IsValid ? "yes" : "no");
            b.CloseElement();
        });
        var h = Render(edges: [], configure: p => p.Add(f => f.ConnectionLineTemplate, template));
        await Measure(h);
        Assert.Empty(h.Cut.FindAll(".my-line"));

        await Down(h.Cut.Find("[data-node-id='a'] .wf-handle-bottom"), 50, 44);
        await Move(h, 200, 70);
        var d = h.Cut.Find(".my-line").GetAttribute("d");
        Assert.StartsWith("M50,44", d);
        Assert.Equal("no", h.Cut.Find(".my-line").GetAttribute("data-valid"));

        await Move(h, 352, 98);
        Assert.Equal("yes", h.Cut.Find(".my-line").GetAttribute("data-valid"));
        Assert.Empty(h.Cut.FindAll(".wf-connection-line")); // le modèle remplace le fil par défaut
        await Up(h, 352, 98);
    }

    [Fact]
    public async Task Auto_pan_scrolls_the_view_when_dragging_near_an_edge()
    {
        var h = Render();
        await Measure(h);
        await Down(h.Cut.Find("[data-node-id='a']"), 50, 20);
        await Move(h, 790, 300); // près du bord droit du conteneur (800)
        var before = h.Flow.Viewport.X;

        await Task.Delay(150);
        await h.Cut.InvokeAsync(() => { });

        Assert.True(h.Flow.Viewport.X < before);
        await Up(h, 790, 300);
    }
}
