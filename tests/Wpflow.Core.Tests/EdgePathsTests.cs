using Wpflow.Core;

namespace Wpflow.Core.Tests;

public class EdgePathsTests
{
    [Fact]
    public void Straight_path_and_center()
    {
        var e = EdgePaths.Straight(new XYPosition(0, 0), new XYPosition(100, 50));
        Assert.Equal("M 0,0L 100,50", e.Path);
        Assert.Equal((50d, 25d), (e.LabelX, e.LabelY));
        Assert.Equal((50d, 25d), (e.OffsetX, e.OffsetY));
    }

    [Fact]
    public void Bezier_horizontal_forward()
    {
        var e = EdgePaths.Bezier(new XYPosition(0, 0), Position.Right, new XYPosition(100, 0), Position.Left);
        Assert.Equal("M0,0 C50,0 50,0 100,0", e.Path);
        Assert.Equal(50, e.LabelX, 9);
        Assert.Equal(0, e.LabelY, 9);
    }

    [Fact]
    public void Bezier_backward_uses_curvature_for_control_points()
    {
        var e = EdgePaths.Bezier(new XYPosition(100, 0), Position.Right, new XYPosition(0, 0), Position.Left);
        Assert.Equal("M100,0 C162.5,0 -62.5,0 0,0", e.Path);
    }

    [Fact]
    public void Bezier_vertical()
    {
        var e = EdgePaths.Bezier(new XYPosition(0, 0), Position.Bottom, new XYPosition(0, 100), Position.Top);
        Assert.Equal("M0,0 C0,50 0,50 0,100", e.Path);
    }

    [Fact]
    public void StepPoints_opposite_handles_make_two_bends_at_the_center()
    {
        var (points, cx, cy, _, _) = EdgePaths.StepPoints(
            new XYPosition(0, 0), Position.Right, new XYPosition(100, 100), Position.Left);

        // Les points alignés (les « gaps » de 20 px) sont retirés, comme dans xyflow récent.
        Assert.Equal(
            [new XYPosition(0, 0), new XYPosition(50, 0), new XYPosition(50, 100), new XYPosition(100, 100)],
            points);
        Assert.Equal((50d, 50d), (cx, cy));
    }

    [Fact]
    public void StepPosition_moves_the_bend_between_source_and_target()
    {
        var (points, cx, _, _, _) = EdgePaths.StepPoints(new XYPosition(0, 0), Position.Right,
            new XYPosition(100, 100), Position.Left, stepPosition: 0.25);
        // sourceGapped.x = 20, targetGapped.x = 80 -> 20 + 60 * 0.25 = 35
        Assert.Equal(35, cx);
        Assert.Equal(35, points[1].X);
    }

    [Fact]
    public void Explicit_center_overrides_the_bend_position()
    {
        var path = EdgePaths.SmoothStep(new XYPosition(0, 0), Position.Right, new XYPosition(100, 100),
            Position.Left, centerX: 70);
        Assert.Equal(70, path.LabelX);
    }

    [Fact]
    public void SmoothStep_rounds_the_corners()
    {
        var e = EdgePaths.SmoothStep(new XYPosition(0, 0), Position.Right, new XYPosition(100, 100), Position.Left);
        Assert.Equal("M0 0L 45,0Q 50,0 50,5L 50,95Q 50,100 55,100L100 100", e.Path);
    }

    [Fact]
    public void Step_has_no_rounding()
    {
        var e = EdgePaths.Step(new XYPosition(0, 0), Position.Right, new XYPosition(100, 100), Position.Left);
        Assert.StartsWith("M0 0", e.Path);
        Assert.EndsWith("L100 100", e.Path);
        Assert.Contains("Q 50,0 50,0", e.Path);
    }

    [Fact]
    public void SmoothStep_aligned_points_produce_a_straight_line()
    {
        var e = EdgePaths.SmoothStep(new XYPosition(0, 0), Position.Right, new XYPosition(100, 0), Position.Left);
        Assert.DoesNotContain("Q", e.Path);
        Assert.StartsWith("M0 0", e.Path);
        Assert.EndsWith("L100 0", e.Path);
    }

    [Fact]
    public void StepPoints_mixed_handles_make_a_single_bend()
    {
        // Right -> Top : un seul coude entre les points ecartes des handles.
        var (points, _, _, _, _) = EdgePaths.StepPoints(
            new XYPosition(0, 0), Position.Right, new XYPosition(100, 100), Position.Top);
        Assert.Equal(new XYPosition(100, 0), points[1]);
        Assert.Equal(3, points.Count);
    }
}
