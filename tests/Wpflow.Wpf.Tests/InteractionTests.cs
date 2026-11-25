using System.Windows;
using Wpflow.Core;
using Wpflow.Wpf;

namespace Wpflow.Wpf.Tests;

/// <summary>Options d'interaction globales, auto-défilement et finitions du lot P1.</summary>
public class InteractionTests
{
    [Fact]
    public void AutoPanDelta_is_zero_inside_and_pushes_content_away_from_the_edge()
    {
        Assert.Equal((0.0, 0.0), FlowView.AutoPanDelta(new Point(200, 150), 400, 300, 15));

        var (x, y) = FlowView.AutoPanDelta(new Point(0, 150), 400, 300, 15);
        Assert.Equal(15, x, 6); // souris au bord gauche : le contenu glisse vers la droite
        Assert.Equal(0, y);

        var (x2, y2) = FlowView.AutoPanDelta(new Point(400, 300), 400, 300, 15);
        Assert.Equal(-15, x2, 6);
        Assert.Equal(-15, y2, 6);

        // Vitesse proportionnelle à la proximité du bord.
        var (half, _) = FlowView.AutoPanDelta(new Point(20, 150), 400, 300, 15);
        Assert.Equal(7.5, half, 6);
    }

    [Fact]
    public void Global_interaction_options_default_to_true()
    {
        Sta.Run(() =>
        {
            var flow = new FlowView();
            Assert.True(flow.NodesDraggable);
            Assert.True(flow.NodesConnectable);
            Assert.True(flow.ElementsSelectable);
            Assert.True(flow.EdgesReconnectable);
            Assert.True(flow.AutoPanOnNodeDrag);
            Assert.Equal(15, flow.AutoPanSpeed);
        });
    }

    [Fact]
    public void Handle_exposes_connectability_in_its_measured_info()
    {
        Sta.Run(() =>
        {
            var handle = new Handle { IsConnectableStart = false, HandleId = "h" };
            Assert.True(handle.IsConnectable);
            Assert.False(handle.IsConnectableStart);
            Assert.True(handle.IsConnectableEnd);
            Assert.Null(handle.IsValidConnection);
        });
    }

    [Fact]
    public void NodeResizer_keeps_aspect_ratio_only_when_asked()
    {
        Sta.Run(() =>
        {
            var resizer = new NodeResizer();
            Assert.False(resizer.KeepAspectRatio);
            resizer.KeepAspectRatio = true;
            Assert.True(resizer.KeepAspectRatio);
        });
    }
}
