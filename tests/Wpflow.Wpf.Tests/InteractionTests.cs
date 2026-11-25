using System.Windows;
using Wpflow.Core;
using Wpflow.Wpf;

namespace Wpflow.Wpf.Tests;

/// <summary>Options d'interaction globales, auto-défilement et finitions du lot P1.</summary>
public class InteractionTests
{
    // Le calcul du défilement automatique est dans le noyau (AutoPan.Delta, testé dans Wpflow.Core.Tests).

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
