using Wpflow.Core;

namespace Wpflow.Core.Tests;

public class P2CoreTests
{
    private static Node N(string id, double x, double y, double w = 100, double h = 50, bool selected = false, string? parent = null) =>
        new(id, new XYPosition(x, y), Width: w, Height: h, Selected: selected, ParentId: parent);

    [Fact]
    public void Intersecting_nodes_partially_or_fully_and_ignore_hidden()
    {
        var nodes = new[] { N("a", 0, 0), N("b", 80, 20), N("c", 500, 500), N("d", 90, 10) with { Hidden = true } };
        var area = new Rect(50, 0, 100, 100);

        Assert.Equal(["a", "b"], GraphQueries.GetIntersectingNodes(area, nodes).Select(n => n.Id));
        Assert.Equal(["b"], GraphQueries.GetIntersectingNodes(new Rect(70, 10, 200, 100), nodes, partially: false).Select(n => n.Id));
        Assert.Equal(["b"], GraphQueries.GetIntersectingNodes(nodes[0], nodes).Select(n => n.Id));
        Assert.False(GraphQueries.IsNodeIntersecting(new Node("x", XYPosition.Zero), area)); // non mesuré
    }

    [Fact]
    public void GetNodeConnections_filters_by_type_and_handle()
    {
        var edges = new[]
        {
            new Edge("1", "a", "b", "out1"), new Edge("2", "c", "a", null, "in"), new Edge("3", "a", "d", "out2")
        };
        Assert.Equal(["1", "2", "3"], GraphQueries.GetNodeConnections("a", edges).Select(c => c.EdgeId));
        Assert.Equal(["1", "3"], GraphQueries.GetNodeConnections("a", edges, HandleType.Source).Select(c => c.EdgeId));
        Assert.Equal(["2"], GraphQueries.GetNodeConnections("a", edges, HandleType.Target).Select(c => c.EdgeId));
        Assert.Equal(["3"], GraphQueries.GetNodeConnections("a", edges, HandleType.Source, "out2").Select(c => c.EdgeId));
    }

    [Fact]
    public void ReconnectEdge_changes_ends_and_refuses_duplicates()
    {
        var edges = new List<Edge> { new("e1", "a", "b"), new("e2", "a", "c") };
        var moved = GraphQueries.ReconnectEdge(edges[0], new Connection("a", "d", null, null), edges, shouldReplaceId: false);
        Assert.Equal("d", moved[0].Target);
        Assert.Equal("e1", moved[0].Id);

        var renamed = GraphQueries.ReconnectEdge(edges[0], new Connection("a", "d", null, null), edges);
        Assert.Equal("xy-edge__a-d", renamed[0].Id);

        Assert.Same(edges, GraphQueries.ReconnectEdge(edges[0], new Connection("a", "c", null, null), edges)); // existe déjà
    }

    [Fact]
    public void Clipboard_copies_selection_with_internal_edges_and_pastes_with_new_ids()
    {
        var nodes = new[] { N("a", 10, 10, selected: true), N("b", 200, 10, selected: true), N("c", 400, 10) };
        var edges = new[] { new Edge("e1", "a", "b"), new Edge("e2", "b", "c") };

        var data = GraphClipboard.Copy(nodes, edges)!;
        Assert.Equal(2, data.Nodes.Count);
        Assert.Equal(["e1"], data.Edges.Select(e => e.Id)); // e2 sort de la sélection

        var pasted = GraphClipboard.Paste(data, nodes, edges, new XYPosition(20, 20));
        Assert.Equal(["a-copy", "b-copy"], pasted.Nodes.Select(n => n.Id));
        Assert.Equal(new XYPosition(30, 30), pasted.Nodes[0].Position);
        Assert.All(pasted.Nodes, n => Assert.True(n.Selected));
        Assert.Equal(("a-copy", "b-copy"), (pasted.Edges[0].Source, pasted.Edges[0].Target));

        // Un deuxième collage ne réutilise pas les ids.
        var again = GraphClipboard.Paste(data, nodes.Concat(pasted.Nodes).ToList(), edges, XYPosition.Zero);
        Assert.Equal(["a-copy2", "b-copy2"], again.Nodes.Select(n => n.Id));
    }

    [Fact]
    public void Clipboard_flattens_children_whose_parent_is_not_copied_and_keeps_copied_hierarchy()
    {
        var nodes = new[] { N("g", 100, 100), N("child", 10, 10, selected: true, parent: "g") };
        var alone = GraphClipboard.Copy(nodes, [])!;
        Assert.Null(alone.Nodes[0].ParentId);
        Assert.Equal(new XYPosition(110, 110), alone.Nodes[0].Position);

        var both = GraphClipboard.Copy([nodes[0] with { Selected = true }, nodes[1]], [])!;
        var pasted = GraphClipboard.Paste(both, nodes, [], new XYPosition(50, 0));
        Assert.Equal("g-copy", pasted.Nodes.Single(n => n.Id == "child-copy").ParentId);
        Assert.Equal(new XYPosition(10, 10), pasted.Nodes.Single(n => n.Id == "child-copy").Position); // relatif : inchangé
    }

    [Fact]
    public void Clipboard_returns_null_without_selection()
    {
        Assert.Null(GraphClipboard.Copy([N("a", 0, 0)], []));
    }

    [Fact]
    public void SimpleBezier_uses_midpoint_controls()
    {
        var p = EdgePaths.SimpleBezier(new XYPosition(0, 0), Position.Right, new XYPosition(100, 50), Position.Left);
        Assert.Equal("M0,0 C50,0 50,50 100,50", p.Path);
        Assert.Equal(50, p.LabelX, 6);
        Assert.Equal(25, p.LabelY, 6);
        Assert.Same(EdgePathProviders.ForType("simplebezier"), EdgePathProviders.ForType("simplebezier"));
        Assert.True(EdgePathProviders.IsBuiltIn("simplebezier"));
    }

    [Fact]
    public void Path_options_reach_the_providers()
    {
        var g = new EdgeGeometry(new XYPosition(100, 0), Position.Right, new XYPosition(0, 100), Position.Left);
        var flat = EdgePathProviders.ForType("default").GetPath(g with { Options = new EdgePathOptions(Curvature: 0) });
        var curved = EdgePathProviders.ForType("default").GetPath(g with { Options = new EdgePathOptions(Curvature: 1) });
        Assert.NotEqual(flat.Path, curved.Path);

        var sharp = EdgePathProviders.ForType("smoothstep").GetPath(g with { Options = new EdgePathOptions(BorderRadius: 0) });
        var round = EdgePathProviders.ForType("smoothstep").GetPath(g);
        Assert.NotEqual(round.Path, sharp.Path);
    }

    [Fact]
    public void Viewport_helpers_center_and_interpolate()
    {
        var v = ViewportMath.GetViewportForCenter(100, 50, 2, 800, 600);
        Assert.Equal(new XYPosition(400, 300), ViewportMath.FlowToScreen(v, new XYPosition(100, 50)));

        var mid = ViewportMath.Interpolate(new Viewport(0, 0, 1), new Viewport(100, 200, 4), 0.5);
        Assert.Equal(2, mid.Zoom, 6);
        Assert.Equal(50, mid.X, 6);
        Assert.Equal(new Viewport(0, 0, 1), ViewportMath.Interpolate(new Viewport(0, 0, 1), new Viewport(100, 200, 4), -3));
        Assert.Equal(0, ViewportMath.EaseInOut(0));
        Assert.Equal(1, ViewportMath.EaseInOut(1), 6);
        Assert.Equal(0.5, ViewportMath.EaseInOut(0.5), 6);
    }

    [Fact]
    public void Layered_layout_supports_all_directions()
    {
        var nodes = new[] { N("a", 0, 0, 100, 40), N("b", 0, 0, 100, 40) };
        var edges = new[] { new Edge("e", "a", "b") };

        var lr = GraphLayout.Layered(nodes, edges, LayoutDirection.LeftToRight);
        Assert.True(lr["b"].X > lr["a"].X);
        Assert.Equal(lr["a"].Y, lr["b"].Y, 6);

        var rl = GraphLayout.Layered(nodes, edges, LayoutDirection.RightToLeft);
        Assert.True(rl["b"].X < rl["a"].X);

        var tb = GraphLayout.Layered(nodes, edges, LayoutDirection.TopToBottom);
        Assert.True(tb["b"].Y > tb["a"].Y);
        Assert.Equal(tb["a"].X, tb["b"].X, 6);

        var bt = GraphLayout.Layered(nodes, edges, LayoutDirection.BottomToTop);
        Assert.True(bt["b"].Y < bt["a"].Y);
    }

    [Fact]
    public void Serializer_round_trips_the_new_node_and_edge_fields()
    {
        var node = new Node("n", new XYPosition(1, 2), Hidden: true, DragHandle: "grip", Extent: new Rect(0, 0, 500, 400),
            ExpandParent: true, ZIndex: 7, AriaLabel: "Mon node", Origin: new XYPosition(0.5, 0.5));
        var edge = new Edge("e", "n", "m", Hidden: true, Selectable: false, Deletable: false, Reconnectable: false,
            InteractionWidth: 30, ZIndex: 3, AriaLabel: "lien",
            Style: new EdgeStyleOptions("#ff0000", 3, [4, 2]),
            LabelStyle: new EdgeLabelOptions("#111111", "#eeeeee", false, 14, 6, 4),
            PathOptions: new EdgePathOptions(0.5, 8, 30, 0.25),
            MarkerEnd: EdgeMarker.ArrowClosed, MarkerEndOptions: new EdgeMarkerOptions("#00ff00", 20, 10, 2));

        var back = GraphSerializer.FromJson(GraphSerializer.ToJson([node], [edge], Viewport.Identity));
        var n = back.Nodes[0];
        Assert.Equal((true, "grip", 7, "Mon node"), (n.Hidden, n.DragHandle, n.ZIndex, n.AriaLabel));
        Assert.Equal(new Rect(0, 0, 500, 400), n.Extent);
        Assert.True(n.ExpandParent);
        Assert.Equal(new XYPosition(0.5, 0.5), n.Origin);

        var e = back.Edges[0];
        Assert.Equal((true, false, false, false, 30.0, 3, "lien"),
            (e.Hidden, e.Selectable, e.Deletable, e.Reconnectable, e.InteractionWidth, e.ZIndex, e.AriaLabel));
        Assert.Equal("#ff0000", e.Style!.Stroke);
        Assert.Equal(3, e.Style.StrokeWidth);
        Assert.Equal([4.0, 2.0], e.Style.DashArray);
        Assert.Equal(new EdgeLabelOptions("#111111", "#eeeeee", false, 14, 6, 4), e.LabelStyle);
        Assert.Equal(new EdgePathOptions(0.5, 8, 30, 0.25), e.PathOptions);
        Assert.Equal(EdgeMarker.ArrowClosed, e.MarkerEnd);
        Assert.Equal(new EdgeMarkerOptions("#00ff00", 20, 10, 2), e.MarkerEndOptions);
    }

    [Fact]
    public void Serializer_reads_edge_data_as_json()
    {
        var back = GraphSerializer.FromJson("""{"nodes":[],"edges":[{"id":"e","source":"a","target":"b","data":{"weight":3}}]}""");
        Assert.NotNull(back.Edges[0].Data);
    }
}

public class ChangesetTests
{
    [Fact]
    public void Diff_then_apply_reproduces_the_target_state()
    {
        var a = new Node("a", new XYPosition(0, 0));
        var b = new Node("b", new XYPosition(1, 1));
        var before = new[] { a, b };
        var after = new[] { a with { Position = new XYPosition(9, 9) }, new Node("c", XYPosition.Zero) };

        var changes = Changesets.Diff(before, after);
        Assert.Contains(changes, c => c is NodeRemoveChange { Id: "b" });
        Assert.Contains(changes, c => c is NodeAddChange { Item.Id: "c" });
        Assert.Contains(changes, c => c is NodeReplaceChange { Id: "a" });
        Assert.Equal(after.OrderBy(n => n.Id), Changes.Apply(changes, before).OrderBy(n => n.Id));
        Assert.Empty(Changesets.Diff(before, before));
    }

    [Fact]
    public void Edge_diff_detects_add_remove_and_replace()
    {
        var before = new[] { new Edge("1", "a", "b"), new Edge("2", "a", "c") };
        var after = new[] { new Edge("1", "a", "b", Animated: true), new Edge("3", "b", "c") };
        var changes = Changesets.Diff(before, after);
        Assert.Equal(3, changes.Count);
        Assert.Equal(after.OrderBy(e => e.Id), Changes.Apply(changes, before).OrderBy(e => e.Id));
    }
}

public class LayoutCrossingTests
{
    [Fact]
    public void Layered_layout_reorders_a_layer_to_remove_an_edge_crossing()
    {
        // a→x et b→y, mais x est déclaré avant y : sans réordonnancement, a→y croise b→x.
        Node N(string id) => new(id, XYPosition.Zero, Width: 100, Height: 40);
        var nodes = new[] { N("a"), N("b"), N("y"), N("x") };
        var edges = new[] { new Edge("1", "a", "x"), new Edge("2", "b", "y") };
        var p = GraphLayout.LayeredLeftToRight(nodes, edges);
        Assert.Equal(p["a"].Y < p["b"].Y, p["x"].Y < p["y"].Y); // même ordre vertical des deux côtés : pas de croisement
    }

    [Fact]
    public void Node_rotation_round_trips_through_json()
    {
        var back = GraphSerializer.FromJson(GraphSerializer.ToJson([new Node("n", XYPosition.Zero, Rotation: 45)], [], Viewport.Identity));
        Assert.Equal(45, back.Nodes[0].Rotation);
        Assert.DoesNotContain("rotation", GraphSerializer.ToJson([new Node("n", XYPosition.Zero)], [], Viewport.Identity));
    }
}

public class MarkerOrientTests
{
    [Fact]
    public void Marker_orient_round_trips_through_json()
    {
        var edge = new Edge("e", "a", "b", MarkerEnd: EdgeMarker.ArrowClosed, MarkerEndOptions: new EdgeMarkerOptions(Orient: 45));
        var back = GraphSerializer.FromJson(GraphSerializer.ToJson([], [edge], Viewport.Identity));
        Assert.Equal(45, back.Edges[0].MarkerEndOptions!.Orient);
    }
}

public class InteractionFlagsTests
{
    [Fact]
    public void NodeInteractionChange_sets_dragging_and_resizing_independently()
    {
        var nodes = new[] { new Node("a", XYPosition.Zero) };
        var dragging = Changes.Apply([new NodeInteractionChange("a", Dragging: true)], nodes);
        Assert.True(dragging[0].Dragging);
        Assert.False(dragging[0].Resizing);
        var both = Changes.Apply([new NodeInteractionChange("a", Resizing: true)], dragging);
        Assert.True(both[0].Dragging && both[0].Resizing); // null laisse la valeur
        var done = Changes.Apply([new NodeInteractionChange("a", false, false)], both);
        Assert.False(done[0].Dragging || done[0].Resizing);
    }

    [Fact]
    public void Initial_size_round_trips_through_json()
    {
        var back = GraphSerializer.FromJson(GraphSerializer.ToJson([new Node("a", XYPosition.Zero, InitialWidth: 120, InitialHeight: 60)], [], Viewport.Identity));
        Assert.Equal((120.0, 60.0), (back.Nodes[0].InitialWidth, back.Nodes[0].InitialHeight));
    }
}
