using Wpflow.Core;

namespace Wpflow.Core.Tests;

public class ChangesTests
{
    private static readonly IReadOnlyList<Node> Nodes =
    [
        new("a", new XYPosition(0, 0)),
        new("b", new XYPosition(10, 10))
    ];

    [Fact]
    public void Position_change_updates_only_the_target_node_and_keeps_the_input_immutable()
    {
        var result = Changes.Apply([new NodePositionChange("b", new XYPosition(5, 6))], Nodes);

        Assert.Equal(new XYPosition(5, 6), result[1].Position);
        Assert.Equal(new XYPosition(0, 0), result[0].Position);
        Assert.Equal(new XYPosition(10, 10), Nodes[1].Position);
    }

    [Fact]
    public void Dimensions_and_select_changes_apply()
    {
        var result = Changes.Apply(
            new NodeChange[] { new NodeDimensionsChange("a", 120, 40), new NodeSelectChange("a", true) }, Nodes);

        Assert.Equal(120, result[0].Width);
        Assert.Equal(40, result[0].Height);
        Assert.True(result[0].Selected);
    }

    [Fact]
    public void Resize_change_sets_explicit_and_measured_size_and_optionally_the_position()
    {
        var result = Changes.Apply([new NodeResizeChange("a", 300, 120, new XYPosition(-5, -6))], Nodes);

        Assert.Equal((300d, 120d), (result[0].FixedWidth, result[0].FixedHeight));
        Assert.Equal((300d, 120d), (result[0].Width, result[0].Height));
        Assert.Equal(new XYPosition(-5, -6), result[0].Position);

        var keep = Changes.Apply([new NodeResizeChange("b", 50, 40)], Nodes);
        Assert.Equal(new XYPosition(10, 10), keep[1].Position);
    }

    [Fact]
    public void Add_remove_and_replace_nodes()
    {
        var added = Changes.Apply([new NodeAddChange(new Node("c", XYPosition.Zero))], Nodes);
        Assert.Equal(3, added.Count);

        var removed = Changes.Apply([new NodeRemoveChange("a")], added);
        Assert.Equal(["b", "c"], removed.Select(n => n.Id));

        var replaced = Changes.Apply([new NodeReplaceChange("b", new Node("b", new XYPosition(1, 1)))], removed);
        Assert.Equal(new XYPosition(1, 1), replaced[0].Position);
    }

    [Fact]
    public void Unknown_id_is_ignored()
    {
        var result = Changes.Apply([new NodeSelectChange("zzz", true)], Nodes);
        Assert.Equal(Nodes, result);
    }

    [Fact]
    public void Edge_changes_apply()
    {
        IReadOnlyList<Edge> edges = [new Edge("e1", "a", "b")];

        var result = Changes.Apply(new EdgeChange[]
        {
            new EdgeSelectChange("e1", true),
            new EdgeAddChange(new Edge("e2", "b", "a"))
        }, edges);

        Assert.True(result[0].Selected);
        Assert.Equal(2, result.Count);
        Assert.Empty(Changes.Apply([new EdgeRemoveChange("e1"), new EdgeRemoveChange("e2")], result));
    }
}
