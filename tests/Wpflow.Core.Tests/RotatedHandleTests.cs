using Wpflow.Core;

namespace Wpflow.Core.Tests;

/// <summary>Un node tourné emporte ses handles : les edges et les connexions doivent les suivre.</summary>
public class RotatedHandleTests
{
    // Node 200 x 100 en (0, 0) : son centre est (100, 50). Handle de sortie 8 x 8 au milieu du côté droit.
    private static readonly HandleInfo Right = new(null, HandleType.Source, Position.Right, 196, 46, 8, 8);

    private static Node Box(double rotation) => new("a", new XYPosition(0, 0), Width: 200, Height: 100, Rotation: rotation);

    private static void Near(XYPosition expected, XYPosition actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
    }

    [Theory]
    [InlineData(0, 204, 50)]
    [InlineData(90, 100, 154)]
    [InlineData(180, -4, 50)]
    [InlineData(-90, 100, -54)]
    public void The_attach_point_turns_with_the_node(double rotation, double x, double y) =>
        Near(new XYPosition(x, y), GraphUtils.GetHandlePosition(Box(rotation), Position.Right, Right));

    [Fact]
    public void The_center_used_to_reach_a_handle_turns_too() =>
        Near(new XYPosition(100, 150), GraphUtils.GetHandleCenter(Box(90), Right));

    [Theory]
    [InlineData(0, Position.Right)]
    [InlineData(30, Position.Right)]
    [InlineData(60, Position.Bottom)]
    [InlineData(90, Position.Bottom)]
    [InlineData(180, Position.Left)]
    [InlineData(270, Position.Top)]
    [InlineData(-90, Position.Top)]
    [InlineData(450, Position.Bottom)]
    public void The_edge_leaves_from_the_side_the_handle_now_faces(double rotation, Position side)
    {
        var source = Box(rotation);
        var target = new Node("b", new XYPosition(600, 0), Width: 100, Height: 100);

        var ends = EdgeEndpoints.Resolve(new Edge("e", "a", "b"), id => id == "a" ? source : target,
            id => id == "a" ? [Right] : null, Position.Right, Position.Left)!.Value;

        Assert.Equal(side, ends.SourcePosition);
        Near(GraphUtils.GetHandlePosition(source, Position.Right, Right), ends.Source);
    }

    [Fact]
    public void Without_a_handle_the_side_of_the_turned_node_is_used()
    {
        var source = Box(90);
        var target = new Node("b", new XYPosition(600, 0), Width: 100, Height: 100);

        var ends = EdgeEndpoints.Resolve(new Edge("e", "a", "b"), id => id == "a" ? source : target, _ => null,
            Position.Right, Position.Left)!.Value;

        Near(new XYPosition(100, 150), ends.Source);
        Assert.Equal(Position.Bottom, ends.SourcePosition);
        Assert.Equal(Position.Left, ends.TargetPosition);
    }

    [Fact]
    public void A_wire_dropped_where_the_handle_is_shown_finds_it()
    {
        var turned = Box(90) with { Id = "b" };
        var handle = Right with { Type = HandleType.Target };
        var from = new HandleRef("a", new HandleInfo(null, HandleType.Source, Position.Right, 0, 0, 8, 8));
        var handles = new Dictionary<string, IReadOnlyList<HandleInfo>> { ["b"] = [handle] };

        var found = ConnectionSearch.FindCandidate(from, new XYPosition(100, 150), 10, ConnectionMode.Strict, handles, _ => turned);
        var oldPlace = ConnectionSearch.FindCandidate(from, new XYPosition(200, 50), 10, ConnectionMode.Strict, handles, _ => turned);

        Assert.Equal("b", found?.NodeId);
        Assert.Null(oldPlace);
    }
}
