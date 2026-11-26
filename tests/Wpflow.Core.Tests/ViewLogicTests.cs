using Wpflow.Core;

namespace Wpflow.Core.Tests;

/// <summary>La logique commune aux deux vues qui n'est pas publique (<c>Interaction/Shared</c>), et la recherche du handle visé.</summary>
public class ViewLogicTests
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(3.9, 0, true)]
    [InlineData(4, 0, false)]
    [InlineData(7, 10, true)] // un seuil de glisser plus grand laisse le clic jusqu'à lui
    [InlineData(10, 10, false)]
    public void A_press_is_a_click_up_to_the_tolerance_or_the_drag_threshold(double travelled, double threshold, bool click) =>
        Assert.Equal(click, ViewLogic.IsClick(travelled, threshold));

    [Fact]
    public void Opposite_sides_face_each_other()
    {
        foreach (var side in Enum.GetValues<Position>())
        {
            Assert.NotEqual(side, ViewLogic.Opposite(side));
            Assert.Equal(side, ViewLogic.Opposite(ViewLogic.Opposite(side)));
        }
    }

    [Fact]
    public void The_connection_line_follows_its_type()
    {
        var (a, b) = (new XYPosition(0, 0), new XYPosition(200, 120));
        string Line(ConnectionLineType type) => ViewLogic.ConnectionLine(type, a, Position.Right, b, Position.Left).Path;

        Assert.Equal(EdgePaths.Straight(a, b).Path, Line(ConnectionLineType.Straight));
        Assert.Equal(EdgePaths.Step(a, Position.Right, b, Position.Left).Path, Line(ConnectionLineType.Step));
        Assert.Equal(EdgePaths.SmoothStep(a, Position.Right, b, Position.Left).Path, Line(ConnectionLineType.SmoothStep));
        Assert.Equal(EdgePaths.SimpleBezier(a, Position.Right, b, Position.Left).Path, Line(ConnectionLineType.SimpleBezier));
        Assert.Equal(EdgePaths.Bezier(a, Position.Right, b, Position.Left).Path, Line(ConnectionLineType.Bezier));
    }

    [Fact]
    public void Panning_to_show_a_rectangle_moves_as_little_as_possible()
    {
        var view = new Viewport(0, 0, 2);

        Assert.Equal(view, ViewLogic.PanToShow(view, new Rect(50, 50, 20, 20), 400, 300, 30)); // déjà visible
        // à droite et en haut : le bord droit (340*2 = 680) revient à 370, le bord haut (-10*2) à 30
        Assert.Equal(new Viewport(-310, 50, 2), ViewLogic.PanToShow(view, new Rect(300, -10, 40, 20), 400, 300, 30));
    }

    [Fact]
    public void The_tracker_reports_only_real_selection_changes()
    {
        var tracker = new SelectionTracker();
        Node a = new("a", XYPosition.Zero), b = new("b", XYPosition.Zero);
        Edge e = new("e", "a", "b");

        Assert.False(tracker.Update([a, b], [e])); // au départ, rien de sélectionné
        Assert.False(tracker.HasSelection);
        Assert.True(tracker.Update([a with { Selected = true }, b], [e]));
        Assert.True(tracker.HasSelection);
        Assert.False(tracker.Update([a with { Selected = true, Position = new XYPosition(5, 5) }, b], [e]));
        Assert.True(tracker.Update([a with { Selected = true }, b with { Selected = true }], [e])); // un de plus
        Assert.True(tracker.Update([a, b with { Selected = true }], [e])); // un de moins
        Assert.True(tracker.Update([a, b with { Selected = true }], [e with { Selected = true }]));
        Assert.True(tracker.Update([a, b], [e]));
        Assert.False(tracker.HasSelection);
    }

    [Fact]
    public void At_equal_distance_the_opposite_handle_type_wins()
    {
        var from = new HandleRef("a", new HandleInfo(null, HandleType.Source, Position.Right, 0, 0, 8, 8));
        var nodes = new[] { "b", "c" }.ToDictionary(id => id, id => new Node(id, XYPosition.Zero, Width: 100, Height: 40));
        var same = new HandleInfo("same", HandleType.Source, Position.Left, 0, 0, 8, 8);
        var opposite = new HandleInfo("opposite", HandleType.Target, Position.Left, 0, 0, 8, 8);
        var handles = new Dictionary<string, IReadOnlyList<HandleInfo>> { ["b"] = [same], ["c"] = [opposite] };

        var found = ConnectionSearch.FindCandidate(from, new XYPosition(4, 4), 20, ConnectionMode.Loose, handles, id => nodes.GetValueOrDefault(id));

        Assert.Equal("opposite", found!.Info.Id);
    }
}

public class SharedInteractionTests
{
    [Theory]
    [InlineData(5, 20, HandleType.Source)] // seule la source est à portée
    [InlineData(35, 20, HandleType.Target)] // seule l'arrivée
    [InlineData(18, 30, HandleType.Source)] // les deux : la plus proche
    [InlineData(22, 30, HandleType.Target)]
    [InlineData(20, 30, HandleType.Target)] // à égale distance : la cible
    public void A_press_reconnects_the_closest_end_in_reach(double x, double reach, HandleType expected) =>
        Assert.Equal(expected, ViewLogic.ReconnectedEnd(new XYPosition(x, 0), XYPosition.Zero, new XYPosition(40, 0), reach));

    [Fact]
    public void A_press_far_from_both_ends_reconnects_nothing() =>
        Assert.Null(ViewLogic.ReconnectedEnd(new XYPosition(50, 0), XYPosition.Zero, new XYPosition(100, 0), 20));

    [Fact]
    public void The_minimap_wheel_zooms_like_the_surface_at_zoom_step_10()
    {
        Assert.Equal(ViewportMath.WheelZoomFactor(-100), ViewLogic.MiniMapZoomFactor(-100, 10), 9);
        Assert.Equal(Math.Sqrt(ViewportMath.WheelZoomFactor(-100)), ViewLogic.MiniMapZoomFactor(-100, 5), 9);
    }

    private static readonly Node Group = new("g", new XYPosition(100, 100)) { Width = 200, Height = 100 };

    private static Node Child(string id, double x, double y, bool expand = true) =>
        new(id, new XYPosition(x, y)) { ParentId = "g", ExpandParent = expand, Width = 100, Height = 40 };

    private static ParentExpansion.Expansion? Expand(XYPosition nodeOrigin, params Node[] children)
    {
        Node[] nodes = [Group, .. children];
        Func<string, Node?> get = id => nodes.FirstOrDefault(n => n.Id == id);
        return ParentExpansion.Compute("g", children.Select(n => n.Id), get, nodes, nodeOrigin);
    }

    [Fact]
    public void Only_parents_of_moved_ExpandParent_children_can_grow()
    {
        Node[] nodes = [Group, Child("c", 0, 0), Child("d", 0, 0), Child("s", 0, 0, expand: false), new("top", XYPosition.Zero) { ExpandParent = true }];

        Assert.Equal(["g"], ParentExpansion.Parents(["c", "d", "top", "inconnu"], id => nodes.FirstOrDefault(n => n.Id == id)));
        Assert.Empty(ParentExpansion.Parents(["s", "g"], id => nodes.FirstOrDefault(n => n.Id == id)));
    }

    [Fact]
    public void A_child_that_fits_changes_nothing() => Assert.Null(Expand(XYPosition.Zero, Child("c", 100, 60)));

    [Fact]
    public void Growing_right_and_bottom_only_resizes_the_parent()
    {
        var expansion = Expand(XYPosition.Zero, Child("c", 150, 80))!;

        Assert.Equal(XYPosition.Zero, expansion.Shift);
        Assert.Equal(new NodeResizeChange("g", 250, 120, Group.Position), Assert.Single(expansion.Changes));
    }

    [Fact]
    public void Growing_left_and_top_moves_the_parent_back_and_compensates_every_child()
    {
        var expansion = Expand(XYPosition.Zero, Child("c", -30, -20), Child("s", 50, 50, expand: false))!;

        Assert.Equal(new XYPosition(-30, -20), expansion.Shift);
        Assert.Equal(
        [
            new NodeResizeChange("g", 230, 120, new XYPosition(70, 80)),
            new NodePositionChange("c", XYPosition.Zero),
            new NodePositionChange("s", new XYPosition(80, 70))
        ], expansion.Changes);
    }

    [Fact]
    public void The_node_origin_is_taken_into_account()
    {
        // Origine au centre : la position stockée (150, 80) place le coin de l'enfant en (100, 60), qui tient dans le groupe.
        Assert.Null(Expand(new XYPosition(0.5, 0.5), Child("c", 150, 80)));
    }
}
