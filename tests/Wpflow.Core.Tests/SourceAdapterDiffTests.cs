using Wpflow.Core;

namespace Wpflow.Core.Tests;

/// <summary>
///     Les changes qui restaurent un état d'Undo/Redo dans les ViewModels : plus fins que ceux de <see cref="Changesets" />
///     (position, sélection et taille séparées), et un id en double ne les fait pas échouer.
/// </summary>
public class SourceAdapterDiffTests
{
    private static readonly Node A = new("a", new XYPosition(0, 0));

    [Fact]
    public void Removals_come_first_then_additions_and_changes_in_order()
    {
        var added = new Node("c", new XYPosition(5, 5));

        var changes = NodeSourceAdapter.Diff([A, new Node("b", XYPosition.Zero)], [added, A with { Selected = true }]);

        Assert.Equal([new NodeRemoveChange("b"), new NodeAddChange(added), new NodeSelectChange("a", true)], changes);
    }

    [Fact]
    public void An_equal_node_gives_no_change() => Assert.Empty(NodeSourceAdapter.Diff([A], [A with { }]));

    [Fact]
    public void Position_selection_and_size_are_separate_changes()
    {
        var sized = A with { FixedWidth = 10, FixedHeight = 20 };

        var changes = NodeSourceAdapter.Diff([sized], [sized with { Position = new XYPosition(3, 4), Selected = true, FixedWidth = 30 }]);

        Assert.Equal([new NodePositionChange("a", new XYPosition(3, 4)), new NodeSelectChange("a", true), new NodeResizeChange("a", 30, 20)],
            changes);
    }

    [Fact]
    public void Another_difference_or_an_automatic_size_replaces_the_node()
    {
        var typed = A with { Type = "other", Position = new XYPosition(1, 1) };
        var sized = A with { FixedWidth = 10, FixedHeight = 20 };

        Assert.Equal([new NodeReplaceChange("a", typed)], NodeSourceAdapter.Diff([A], [typed]));
        Assert.Equal([new NodeReplaceChange("a", A)], NodeSourceAdapter.Diff([sized], [A]));
    }

    [Fact]
    public void A_duplicated_id_in_the_old_state_keeps_the_first() =>
        Assert.Empty(NodeSourceAdapter.Diff([A, A with { Selected = true }], [A]));

    [Fact]
    public void Edges_are_selected_or_replaced()
    {
        var edge = new Edge("e", "a", "b");
        var relabelled = edge with { Label = "x", Selected = true };
        var added = new Edge("f", "b", "a");

        Assert.Equal([new EdgeSelectChange("e", true)], EdgeSourceAdapter.Diff([edge], [edge with { Selected = true }]));
        Assert.Equal([new EdgeReplaceChange("e", relabelled)], EdgeSourceAdapter.Diff([edge], [relabelled]));
        Assert.Equal([new EdgeRemoveChange("e"), new EdgeAddChange(added)], EdgeSourceAdapter.Diff([edge], [added]));
    }
}
