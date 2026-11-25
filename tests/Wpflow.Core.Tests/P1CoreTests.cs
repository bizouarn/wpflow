using Wpflow.Core;

namespace Wpflow.Core.Tests;

public class GraphQueriesTests
{
    private static readonly Node A = new("a", XYPosition.Zero), B = new("b", XYPosition.Zero), C = new("c", XYPosition.Zero), D = new("d", XYPosition.Zero);
    private static readonly Node[] Nodes = [A, B, C, D];
    private static readonly Edge[] Edges = [new("e1", "a", "b"), new("e2", "a", "c"), new("e3", "b", "c")];

    [Fact]
    public void Incomers_are_predecessors_and_outgoers_successors()
    {
        Assert.Equal(["a", "b"], GraphQueries.GetIncomers(C, Nodes, Edges).Select(n => n.Id));
        Assert.Equal(["b", "c"], GraphQueries.GetOutgoers(A, Nodes, Edges).Select(n => n.Id));
        Assert.Empty(GraphQueries.GetIncomers(A, Nodes, Edges));
        Assert.Empty(GraphQueries.GetOutgoers(D, Nodes, Edges));
    }

    [Fact]
    public void Connected_edges_touch_any_of_the_nodes()
    {
        Assert.Equal(["e1", "e3"], GraphQueries.GetConnectedEdges([B], Edges).Select(e => e.Id));
        Assert.Equal(3, GraphQueries.GetConnectedEdges([A, C], Edges).Count);
        Assert.Empty(GraphQueries.GetConnectedEdges([D], Edges));
    }

    [Fact]
    public void AddEdge_skips_duplicates_and_generates_missing_ids()
    {
        var added = GraphQueries.AddEdge(new Edge("", "c", "d"), Edges);
        Assert.Equal(4, added.Count);
        Assert.Equal("xy-edge__c-d", added[^1].Id);

        Assert.Same(Edges, GraphQueries.AddEdge(new Edge("e1", "x", "y"), Edges)); // id existant
        Assert.Same(Edges, GraphQueries.AddEdge(new Edge("autre", "a", "b"), Edges)); // même connexion
        Assert.Equal(4, GraphQueries.AddEdge(new Edge("autre", "a", "b", SourceHandle: "h"), Edges).Count); // handle différent
    }
}

public class NodeResizeAspectTests
{
    private static readonly Rect Start = new(100, 100, 200, 100); // rapport 2

    [Fact]
    public void Corner_keeps_the_ratio_and_the_opposite_corner()
    {
        var r = NodeResize.Resize(ResizeDirection.BottomRight, Start, 40, 5, keepAspectRatio: true);
        Assert.Equal(2, r.Width / r.Height, 6);
        Assert.Equal(240, r.Width, 6); // la dimension qui a le plus changé (largeur +20 %) l'emporte
        Assert.Equal((100, 100), (r.X, r.Y));
    }

    [Fact]
    public void Top_left_corner_anchors_the_bottom_right()
    {
        var r = NodeResize.Resize(ResizeDirection.TopLeft, Start, -40, -10, keepAspectRatio: true);
        Assert.Equal(2, r.Width / r.Height, 6);
        Assert.Equal(Start.Right, r.Right, 6);
        Assert.Equal(Start.Bottom, r.Bottom, 6);
    }

    [Fact]
    public void Side_handle_scales_the_other_dimension()
    {
        var right = NodeResize.Resize(ResizeDirection.Right, Start, 100, 0, keepAspectRatio: true);
        Assert.Equal((300, 150), (right.Width, right.Height));
        var bottom = NodeResize.Resize(ResizeDirection.Bottom, Start, 0, 50, keepAspectRatio: true);
        Assert.Equal((300, 150), (bottom.Width, bottom.Height));
        Assert.Equal(Start.X, bottom.X);
    }

    [Fact]
    public void Limits_apply_to_the_scale_and_keep_the_ratio()
    {
        var small = NodeResize.Resize(ResizeDirection.BottomRight, Start, -190, -90, minWidth: 100, minHeight: 20, keepAspectRatio: true);
        Assert.Equal(100, small.Width, 6); // minWidth/200 = 0,5 est le facteur plancher
        Assert.Equal(50, small.Height, 6);

        var big = NodeResize.Resize(ResizeDirection.BottomRight, Start, 1000, 0, maxWidth: 400, keepAspectRatio: true);
        Assert.Equal((400, 200), (big.Width, big.Height));
    }

    [Fact]
    public void Without_the_option_dimensions_stay_independent()
    {
        var r = NodeResize.Resize(ResizeDirection.BottomRight, Start, 40, 5);
        Assert.Equal((240, 105), (r.Width, r.Height));
    }
}

public class NodeFieldsJsonTests
{
    [Fact]
    public void New_node_fields_roundtrip()
    {
        var node = new Node("n", new XYPosition(1, 2), SourcePosition: Position.Right, TargetPosition: Position.Left,
            Connectable: false, Deletable: false);
        var back = GraphSerializer.FromJson(GraphSerializer.ToJson([node], [], Viewport.Identity)).Nodes[0];
        Assert.Equal(Position.Right, back.SourcePosition);
        Assert.Equal(Position.Left, back.TargetPosition);
        Assert.False(back.Connectable);
        Assert.False(back.Deletable);
    }

    [Fact]
    public void Defaults_are_not_written()
    {
        var json = GraphSerializer.ToJson([new Node("n", XYPosition.Zero)], [], Viewport.Identity);
        Assert.DoesNotContain("sourcePosition", json);
        Assert.DoesNotContain("connectable", json);
        Assert.DoesNotContain("deletable", json);
    }
}
