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

public class BatchedChangesTests
{
    private static List<Node> Many(int n) =>
        Enumerable.Range(0, n).Select(i => new Node($"n{i}", new XYPosition(i, 0))).ToList();

    [Fact]
    public void Large_batches_give_the_same_result_as_one_by_one()
    {
        var nodes = Many(50);
        var changes = Enumerable.Range(0, 50)
            .Select(i => (NodeChange)new NodeDimensionsChange($"n{i}", 100 + i, 40))
            .Append(new NodeRemoveChange("n3"))
            .Append(new NodeSelectChange("n10", true))       // l'index doit rester valide après une suppression
            .Append(new NodeAddChange(new Node("new", XYPosition.Zero)))
            .Append(new NodePositionChange("new", new XYPosition(5, 5)))   // ... et après un ajout
            .Append(new NodeReplaceChange("n7", new Node("n7b", XYPosition.Zero)))
            .Append(new NodeSelectChange("n7b", true))        // ... et après un remplacement qui change l'id
            .ToList();

        var batched = Changes.Apply(changes, nodes);
        var oneByOne = changes.Aggregate((IReadOnlyList<Node>)nodes, (acc, c) => Changes.Apply([c], acc));

        Assert.Equal(oneByOne, batched);
        Assert.DoesNotContain(batched, n => n.Id == "n3");
        Assert.True(batched.First(n => n.Id == "n10").Selected);
        Assert.Equal(new XYPosition(5, 5), batched.First(n => n.Id == "new").Position);
        Assert.True(batched.First(n => n.Id == "n7b").Selected);
    }

    [Fact]
    public void Empty_changes_return_the_same_list_instance()
    {
        var nodes = Many(3);
        Assert.Same(nodes, Changes.Apply(Array.Empty<NodeChange>(), nodes));
    }

    [Fact]
    public void DescendantsOf_collects_several_roots_in_one_pass()
    {
        var nodes = new List<Node>
        {
            new("a", XYPosition.Zero), new("a1", XYPosition.Zero, ParentId: "a"), new("a2", XYPosition.Zero, ParentId: "a1"),
            new("b", XYPosition.Zero), new("b1", XYPosition.Zero, ParentId: "b"), new("c", XYPosition.Zero)
        };
        Assert.Equal(new[] { "a1", "a2", "b1" }.OrderBy(x => x), NodeTree.DescendantsOf(["a", "b"], nodes).OrderBy(x => x));
        Assert.Empty(NodeTree.DescendantsOf(["c"], nodes));
    }
}
