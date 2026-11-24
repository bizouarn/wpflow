using Wpflow.Core;

namespace Wpflow.Core.Tests;

public class NodeTreeTests
{
    private static readonly List<Node> Tree =
    [
        new("group", new XYPosition(100, 100), Width: 400, Height: 300),
        new("child", new XYPosition(10, 20), Width: 50, Height: 30, ParentId: "group"),
        new("grandchild", new XYPosition(5, 5), Width: 10, Height: 10, ParentId: "child"),
        new("alone", new XYPosition(-1, -1))
    ];

    [Fact]
    public void AbsolutePositions_accumulate_parent_positions()
    {
        var abs = NodeTree.AbsolutePositions(Tree);
        Assert.Equal(new XYPosition(100, 100), abs["group"]);
        Assert.Equal(new XYPosition(110, 120), abs["child"]);
        Assert.Equal(new XYPosition(115, 125), abs["grandchild"]);
        Assert.Equal(new XYPosition(-1, -1), abs["alone"]);
    }

    [Fact]
    public void AbsolutePositions_survive_cycles_and_missing_parents()
    {
        var nodes = new List<Node>
        {
            new("a", new XYPosition(1, 1), ParentId: "b"),
            new("b", new XYPosition(2, 2), ParentId: "a"),
            new("c", new XYPosition(3, 3), ParentId: "nope")
        };
        var abs = NodeTree.AbsolutePositions(nodes);
        Assert.Equal(3, abs.Count);
        Assert.Equal(new XYPosition(3, 3), abs["c"]);
    }

    [Fact]
    public void Depths_and_descendants()
    {
        var depths = NodeTree.Depths(Tree);
        Assert.Equal((0, 1, 2), (depths["group"], depths["child"], depths["grandchild"]));
        Assert.Equal(new[] { "child", "grandchild" }.Order(), NodeTree.Descendants("group", Tree).Order());
        Assert.Empty(NodeTree.Descendants("alone", Tree));
    }

    [Fact]
    public void ClampToParent_only_applies_with_ExtentParent()
    {
        var parent = Tree[0];
        var child = Tree[1] with { ExtentParent = true };
        Assert.Equal(new XYPosition(350, 270), NodeTree.ClampToParent(child, new XYPosition(999, 999), parent));
        Assert.Equal(new XYPosition(0, 0), NodeTree.ClampToParent(child, new XYPosition(-30, -9), parent));
        Assert.Equal(new XYPosition(999, 999), NodeTree.ClampToParent(Tree[1], new XYPosition(999, 999), parent));
    }

    [Fact]
    public void NodesBounds_use_absolute_positions()
    {
        var bounds = GraphUtils.GetNodesBounds(Tree);
        // group (100,100,400,300) englobe child et grandchild ; alone n'est pas mesuré.
        Assert.Equal(new Rect(100, 100, 400, 300), bounds);
    }
}

public class ConnectionTests
{
    [Theory]
    [InlineData(HandleType.Source, HandleType.Target, ConnectionMode.Strict, true)]
    [InlineData(HandleType.Source, HandleType.Source, ConnectionMode.Strict, false)]
    [InlineData(HandleType.Target, HandleType.Target, ConnectionMode.Strict, false)]
    [InlineData(HandleType.Source, HandleType.Source, ConnectionMode.Loose, true)]
    [InlineData(HandleType.Target, HandleType.Target, ConnectionMode.Loose, true)]
    public void Type_compatibility_depends_on_mode(HandleType from, HandleType to, ConnectionMode mode, bool expected) =>
        Assert.Equal(expected, Connections.AreTypesCompatible(from, to, mode));

    [Fact]
    public void Create_orients_the_connection_from_the_start_handle_type()
    {
        Assert.Equal(new Connection("a", "b", "s", "t"), Connections.Create("a", "s", HandleType.Source, "b", "t"));
        Assert.Equal(new Connection("b", "a", "t", "s"), Connections.Create("a", "s", HandleType.Target, "b", "t"));
    }

    [Fact]
    public void Custom_validator_can_reject()
    {
        var c = new Connection("a", "b", null, null);
        Assert.True(Connections.IsValid(c, null));
        Assert.False(Connections.IsValid(c, x => x.Source != "a"));
    }

    [Fact]
    public void ConnectionExists_treats_null_and_empty_handle_as_the_same()
    {
        var edges = new[] { new Edge("e", "a", "b", "", null) };
        Assert.True(GraphUtils.ConnectionExists(new Connection("a", "b", null, ""), edges));
    }
}

public class ViewportExtentTests
{
    private static readonly Rect Extent = new(0, 0, 1000, 1000);

    [Fact]
    public void Panning_past_the_left_or_top_edge_is_clamped()
    {
        var v = ViewportMath.ClampToExtent(new Viewport(100, 50, 1), 500, 500, Extent);
        Assert.Equal(new Viewport(0, 0, 1), v);
    }

    [Fact]
    public void Panning_past_the_right_or_bottom_edge_is_clamped()
    {
        var v = ViewportMath.ClampToExtent(new Viewport(-800, -900, 1), 500, 500, Extent);
        Assert.Equal(new Viewport(-500, -500, 1), v);
    }

    [Fact]
    public void Viewport_inside_the_extent_is_unchanged()
    {
        var v = new Viewport(-100, -100, 1);
        Assert.Equal(v, ViewportMath.ClampToExtent(v, 500, 500, Extent));
    }

    [Fact]
    public void Extent_smaller_than_the_view_is_centered()
    {
        var v = ViewportMath.ClampToExtent(new Viewport(0, 0, 1), 1000, 1000, new Rect(0, 0, 200, 200));
        Assert.Equal(new Viewport(400, 400, 1), v);
    }
}

public class EdgeVisibilityTests
{
    [Fact]
    public void Edge_is_visible_when_its_bounding_box_overlaps_the_view()
    {
        var view = new Rect(0, 0, 100, 100);
        Assert.True(GraphUtils.IsEdgeVisible(new Rect(-500, 10, 50, 50), new Rect(500, 20, 50, 50), view)); // traverse la vue
        Assert.False(GraphUtils.IsEdgeVisible(new Rect(200, 200, 50, 50), new Rect(400, 400, 50, 50), view));
    }

    [Fact]
    public void GetHandleCenter_is_the_middle_of_the_handle_rect()
    {
        var node = new Node("n", new XYPosition(10, 20));
        var handle = new HandleInfo("h", HandleType.Source, Position.Right, 96, 11, 8, 8);
        Assert.Equal(new XYPosition(110, 35), GraphUtils.GetHandleCenter(node, handle));
    }
}

public class GraphSerializerTests
{
    [Fact]
    public void Roundtrip_preserves_the_graph()
    {
        var nodes = new List<Node>
        {
            new("a", new XYPosition(1.5, 2), "input", FixedWidth: 120, FixedHeight: 40, Selected: true),
            new("g", new XYPosition(0, 0), "group", FixedWidth: 300, FixedHeight: 200),
            new("b", new XYPosition(10, 10), ParentId: "g", ExtentParent: true, Draggable: false)
        };
        var edges = new List<Edge>
        {
            new("e1", "a", "b", "out", "in", "smoothstep", "hello", true, false, EdgeMarker.ArrowClosed, EdgeMarker.Arrow)
        };

        var graph = GraphSerializer.FromJson(GraphSerializer.ToJson(nodes, edges, new Viewport(5, 6, 1.25)));

        Assert.Equal(new Viewport(5, 6, 1.25), graph.Viewport);
        Assert.Equal(3, graph.Nodes.Count);
        Assert.Equal(nodes[0] with { Data = null, Width = null, Height = null }, graph.Nodes[0]);
        Assert.Equal("g", graph.Nodes[2].ParentId);
        Assert.True(graph.Nodes[2].ExtentParent);
        Assert.False(graph.Nodes[2].Draggable);
        Assert.Equal(edges[0], graph.Edges[0]);
    }

    [Fact]
    public void Reads_a_react_flow_style_document()
    {
        const string json = """
            {
              "nodes": [
                { "id": "1", "type": "input", "position": { "x": 250, "y": 5 }, "data": { "label": "Input" } },
                { "id": "2", "position": { "x": 100, "y": 100 }, "data": { "label": "Default" }, "parentId": "1", "extent": "parent" }
              ],
              "edges": [
                { "id": "e1-2", "source": "1", "target": "2", "animated": true, "markerEnd": { "type": "arrowclosed" } },
                { "id": "e2-1", "source": "2", "target": "1", "markerEnd": "arrow" }
              ],
              "viewport": { "x": 0, "y": 0, "zoom": 2 }
            }
            """;

        var graph = GraphSerializer.FromJson(json);

        Assert.Equal(2, graph.Viewport.Zoom);
        Assert.Equal("input", graph.Nodes[0].Type);
        Assert.Equal("default", graph.Nodes[1].Type);
        Assert.True(graph.Nodes[1].ExtentParent);
        Assert.Equal("Input", ((System.Text.Json.JsonElement)graph.Nodes[0].Data!).GetProperty("label").GetString());
        Assert.Equal(EdgeMarker.ArrowClosed, graph.Edges[0].MarkerEnd);
        Assert.Equal(EdgeMarker.Arrow, graph.Edges[1].MarkerEnd);
        Assert.True(graph.Edges[0].Animated);
    }

    [Fact]
    public void Missing_ids_are_rejected()
    {
        Assert.Throws<System.Text.Json.JsonException>(() =>
            GraphSerializer.FromJson("""{ "nodes": [ { "position": { "x": 0, "y": 0 } } ] }"""));
    }
}

public class GraphLayoutTests
{
    [Fact]
    public void Layers_follow_the_longest_path_and_do_not_overlap()
    {
        var nodes = new List<Node> { new("a", XYPosition.Zero), new("b", XYPosition.Zero), new("c", XYPosition.Zero), new("d", XYPosition.Zero) };
        var edges = new List<Edge> { new("1", "a", "b"), new("2", "a", "c"), new("3", "b", "d"), new("4", "a", "d") };

        var pos = GraphLayout.LayeredLeftToRight(nodes, edges, 100, 20, 100, 40);

        Assert.Equal(0, pos["a"].X);
        Assert.Equal(200, pos["b"].X);
        Assert.Equal(pos["b"].X, pos["c"].X);
        Assert.Equal(400, pos["d"].X); // d dépend de b (couche 1) donc couche 2, malgré a -> d
        Assert.NotEqual(pos["b"].Y, pos["c"].Y);
        Assert.Equal(60, Math.Abs(pos["b"].Y - pos["c"].Y)); // hauteur 40 + écart 20
    }

    [Fact]
    public void Cycles_do_not_hang()
    {
        var nodes = new List<Node> { new("a", XYPosition.Zero), new("b", XYPosition.Zero) };
        var edges = new List<Edge> { new("1", "a", "b"), new("2", "b", "a") };
        Assert.Equal(2, GraphLayout.LayeredLeftToRight(nodes, edges).Count);
    }
}
