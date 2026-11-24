using Wpflow.Core;

namespace Wpflow.Core.Tests;

public class NodeResizeTests
{
    private static readonly Rect Start = new(100, 100, 200, 100);

    [Fact]
    public void Right_and_bottom_grow_without_moving_the_origin()
    {
        var r = NodeResize.Resize(ResizeDirection.BottomRight, Start, 30, 20);
        Assert.Equal(new Rect(100, 100, 230, 120), r);
    }

    [Fact]
    public void Left_and_top_keep_the_opposite_edge_fixed()
    {
        var r = NodeResize.Resize(ResizeDirection.TopLeft, Start, -40, -10);
        Assert.Equal(new Rect(60, 90, 240, 110), r);
        Assert.Equal(Start.Right, r.Right);
        Assert.Equal(Start.Bottom, r.Bottom);
    }

    [Fact]
    public void Minimum_size_is_respected_and_opposite_edge_stays_put()
    {
        var r = NodeResize.Resize(ResizeDirection.Left, Start, 500, 0, minWidth: 50);
        Assert.Equal(50, r.Width);
        Assert.Equal(Start.Right, r.Right);
        Assert.Equal(Start.Height, r.Height);
    }

    [Fact]
    public void Maximum_size_is_respected()
    {
        var r = NodeResize.Resize(ResizeDirection.Right, Start, 1000, 0, maxWidth: 400);
        Assert.Equal(400, r.Width);
    }

    [Fact]
    public void Side_handles_only_change_one_axis()
    {
        var r = NodeResize.Resize(ResizeDirection.Top, Start, 99, -25);
        Assert.Equal(new Rect(100, 75, 200, 125), r);
    }
}
