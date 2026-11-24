using Wpflow.Core;

namespace Wpflow.Core.Tests;

public class GraphUtilsTests
{
    private static Node N(string id, double x, double y, double? w = 100, double? h = 50) =>
        new(id, new XYPosition(x, y), Width: w, Height: h);

    [Fact]
    public void GetNodesBounds_returns_null_without_measured_nodes()
    {
        Assert.Null(GraphUtils.GetNodesBounds([]));
        Assert.Null(GraphUtils.GetNodesBounds([N("a", 0, 0, null, null)]));
    }

    [Fact]
    public void GetNodesBounds_is_the_union_and_ignores_unmeasured_nodes()
    {
        var bounds = GraphUtils.GetNodesBounds([N("a", 0, 0), N("b", 200, 100), N("c", -999, -999, null, null)]);
        Assert.Equal(new Rect(0, 0, 300, 150), bounds);
    }

    [Theory]
    [InlineData(Position.Top, 60, 20)]
    [InlineData(Position.Bottom, 60, 70)]
    [InlineData(Position.Left, 10, 45)]
    [InlineData(Position.Right, 110, 45)]
    public void GetHandlePosition_defaults_to_the_middle_of_the_side(Position side, double x, double y)
    {
        var p = GraphUtils.GetHandlePosition(N("a", 10, 20), side);
        Assert.Equal(new XYPosition(x, y), p);
    }

    [Fact]
    public void GetHandlePosition_uses_the_handle_rect_when_given()
    {
        var handle = new HandleInfo("h", HandleType.Source, Position.Left, -4, 10, 8, 8);
        var p = GraphUtils.GetHandlePosition(N("a", 10, 20), Position.Left, handle);
        Assert.Equal(new XYPosition(6, 34), p);
    }

    [Fact]
    public void GetClosestHandle_picks_nearest_within_radius()
    {
        var a = new HandleInfo("a", HandleType.Target, Position.Top, 0, 0, 1, 1);
        var b = new HandleInfo("b", HandleType.Target, Position.Top, 0, 0, 1, 1);
        var found = GraphUtils.GetClosestHandle(new XYPosition(0, 0), 20,
            [(a, new XYPosition(15, 0)), (b, new XYPosition(5, 0))]);
        Assert.Same(b, found);
    }

    [Fact]
    public void GetClosestHandle_returns_null_outside_radius()
    {
        var a = new HandleInfo("a", HandleType.Target, Position.Top, 0, 0, 1, 1);
        Assert.Null(GraphUtils.GetClosestHandle(new XYPosition(0, 0), 10, [(a, new XYPosition(50, 0))]));
    }

    [Theory]
    [InlineData(-50, 20, 0, 20)]
    [InlineData(500, 500, 300, 150)]
    [InlineData(100, 100, 100, 100)]
    public void ClampToExtent_keeps_the_whole_node_inside(double x, double y, double expectedX, double expectedY)
    {
        var p = GraphUtils.ClampToExtent(new XYPosition(x, y), 100, 50, new Rect(0, 0, 400, 200));
        Assert.Equal(new XYPosition(expectedX, expectedY), p);
    }

    [Fact]
    public void ClampToExtent_pins_to_origin_when_node_is_larger_than_extent()
    {
        var p = GraphUtils.ClampToExtent(new XYPosition(30, 30), 500, 500, new Rect(10, 10, 100, 100));
        Assert.Equal(new XYPosition(10, 10), p);
    }

    [Fact]
    public void ConnectionExists_compares_all_four_fields()
    {
        var edges = new[] { new Edge("e1", "a", "b", "s", "t") };
        Assert.True(GraphUtils.ConnectionExists(new Connection("a", "b", "s", "t"), edges));
        Assert.False(GraphUtils.ConnectionExists(new Connection("a", "b", "s", null), edges));
    }
}
