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
