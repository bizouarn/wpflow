using Wpflow.Core;

namespace Wpflow.Core.Tests;

public sealed class NodeTreeAncestorsTests
{
    [Fact]
    public void Ancestors_go_from_the_nearest_parent_up()
    {
        var nodes = new[]
        {
            new Node("root", XYPosition.Zero),
            new Node("mid", XYPosition.Zero, ParentId: "root"),
            new Node("leaf", XYPosition.Zero, ParentId: "mid")
        };

        Assert.Equal(["mid", "root"], NodeTree.Ancestors("leaf", nodes));
        Assert.Empty(NodeTree.Ancestors("root", nodes));
        Assert.Empty(NodeTree.Ancestors("missing", nodes));
    }

    [Fact]
    public void A_cycle_is_cut_instead_of_looping()
    {
        var nodes = new[]
        {
            new Node("a", XYPosition.Zero, ParentId: "b"),
            new Node("b", XYPosition.Zero, ParentId: "a")
        };

        Assert.Equal(["b"], NodeTree.Ancestors("a", nodes));
    }
}
