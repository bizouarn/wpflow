using Wpflow.Core;

namespace Wpflow.Core.Tests;

public class ViewportMathTests
{
    [Fact]
    public void ScreenToFlow_and_FlowToScreen_are_inverse()
    {
        var v = new Viewport(40, -20, 1.5);
        var flow = new XYPosition(12.5, 99);
        var back = ViewportMath.ScreenToFlow(v, ViewportMath.FlowToScreen(v, flow));
        Assert.Equal(flow.X, back.X, 9);
        Assert.Equal(flow.Y, back.Y, 9);
    }

    [Fact]
    public void FlowToScreen_applies_zoom_then_translation()
    {
        var p = ViewportMath.FlowToScreen(new Viewport(10, 20, 2), new XYPosition(5, 5));
        Assert.Equal(new XYPosition(20, 30), p);
    }

    [Fact]
    public void ScreenToFlow_snaps_to_grid()
    {
        var p = ViewportMath.ScreenToFlow(Viewport.Identity, new XYPosition(7, 8), true, 15, 15);
        Assert.Equal(new XYPosition(0, 15), p);
    }

    [Fact]
    public void ZoomAt_keeps_the_anchor_point_fixed()
    {
        var v = new Viewport(30, 70, 1);
        var anchor = new XYPosition(200, 150);
        var flowBefore = ViewportMath.ScreenToFlow(v, anchor);

        var zoomed = ViewportMath.ZoomAt(v, anchor, 1.8);
        var flowAfter = ViewportMath.ScreenToFlow(zoomed, anchor);

        Assert.Equal(1.8, zoomed.Zoom, 9);
        Assert.Equal(flowBefore.X, flowAfter.X, 9);
        Assert.Equal(flowBefore.Y, flowAfter.Y, 9);
    }

    [Theory]
    [InlineData(10, 2)]
    [InlineData(0.01, 0.5)]
    public void ZoomAt_clamps_zoom(double requested, double expected)
    {
        var v = ViewportMath.ZoomAt(Viewport.Identity, XYPosition.Zero, requested);
        Assert.Equal(expected, v.Zoom);
    }

    [Fact]
    public void PanBy_translates_without_changing_zoom()
    {
        var v = ViewportMath.PanBy(new Viewport(1, 2, 3), 10, -5);
        Assert.Equal(new Viewport(11, -3, 3), v);
    }

    [Fact]
    public void GetViewportForBounds_centers_and_clamps_to_max_zoom()
    {
        var v = ViewportMath.GetViewportForBounds(new Rect(0, 0, 100, 100), 220, 220);
        Assert.Equal(10, v.X, 9);
        Assert.Equal(10, v.Y, 9);
        Assert.Equal(2, v.Zoom, 9);
    }

    [Fact]
    public void GetViewportForBounds_respects_custom_max_zoom()
    {
        var v = ViewportMath.GetViewportForBounds(new Rect(0, 0, 100, 100), 220, 220, maxZoom: 1.5);
        Assert.Equal(new Viewport(35, 35, 1.5), v);
    }

    [Fact]
    public void GetVisibleRect_is_expressed_in_flow_coordinates()
    {
        var r = ViewportMath.GetVisibleRect(new Viewport(-100, -50, 2), 800, 600);
        Assert.Equal(new Rect(50, 25, 400, 300), r);
    }

    [Fact]
    public void WheelZoomFactor_matches_d3_formula()
    {
        Assert.Equal(Math.Pow(2, 0.2), ViewportMath.WheelZoomFactor(-100), 9);
        Assert.Equal(1, ViewportMath.WheelZoomFactor(0));
    }
}
