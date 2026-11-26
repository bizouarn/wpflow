using System.Windows;
using System.Windows.Media;
using Wpflow.Core;
using Wpflow.Wpf;

namespace Wpflow.Wpf.Tests;

/// <summary>Dans la vue réelle, l'edge d'un node tourné aboutit au handle tel qu'il est affiché.</summary>
public class RotatedNodeTests
{
    [Theory]
    [InlineData(35)]
    [InlineData(90)]
    [InlineData(-120)]
    public void The_drawn_edge_ends_on_the_displayed_handle(double rotation) => Sta.Run(() =>
    {
        var flow = CustomizationTests.Realize();
        // Les handles ne sont mesurés que dans une fenêtre affichée : hors écran, sans prendre le focus.
        var window = new Window
        {
            Content = flow, Width = 820, Height = 640, Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false
        };
        window.Show();
        try
        {
            Check(flow, rotation);
        }
        finally
        {
            window.Close();
        }
    });

    private static void Check(FlowView flow, double rotation)
    {
        Flush(flow); // les handles sont mesurés après la mise en page

        flow.UpdateNode("b", n => n with { Rotation = rotation });
        Flush(flow);

        var host = FlowView.FindDescendants<NodeHost>(flow).Single(h => h.Node?.Id == "b");
        Assert.NotEmpty(host.Handles); // sans eux, l'edge viserait le côté du node et le test ne prouverait rien
        var handle = FlowView.FindDescendants<Handle>(host).Single(h => h.Type == HandleType.Target);
        // La transformation vers la vue inclut la rotation du node.
        var shown = handle.TransformToAncestor(flow).Transform(new Point(handle.ActualWidth / 2, handle.ActualHeight / 2));
        var path = Assert.Single(FlowView.FindDescendants<EdgeVisual>(flow)).PathData!;
        Geometry.Parse(path).GetFlattenedPathGeometry().GetPointAtFractionLength(1, out var end, out _);
        var drawn = flow.FlowToScreen(new XYPosition(end.X, end.Y));

        var gap = (drawn - shown).Length;
        Assert.True(gap <= handle.ActualWidth / 2 + 1, $"à {rotation}° : {gap:0.0} px entre l'edge {drawn} et le handle {shown}");
    }

    private static void Flush(FlowView flow)
    {
        flow.UpdateLayout();
        flow.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        flow.UpdateLayout();
    }
}
