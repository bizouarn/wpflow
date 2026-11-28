using System.Windows;
using Wpflow.Core;
using Wpflow.Wpf;

namespace Wpflow.Wpf.Tests;

/// <summary>La poignée de rotation (<see cref="NodeRotator" />) n'apparaît que sur le node sélectionné, comme dans l'exemple « Rotation ».</summary>
public class NodeRotatorTests
{
    /// <summary>Un template de node qui contient la poignée, comme celui de la démo.</summary>
    private static DataTemplate RotatableTemplate()
    {
        var grid = new FrameworkElementFactory(typeof(System.Windows.Controls.Grid));
        grid.AppendChild(new FrameworkElementFactory(typeof(NodeRotator)));
        return new DataTemplate { VisualTree = grid };
    }

    [Fact]
    public void Rotator_is_shown_only_on_the_selected_node() => Sta.Run(() =>
    {
        var flow = CustomizationTests.Realize(
            configure: f => f.Resources.Add("wpflow-node-rotatable", RotatableTemplate()),
            nodes:
            [
                new Node("a", new XYPosition(0, 0), "rotatable", "A", Width: 100, Height: 40, FixedWidth: 100, FixedHeight: 40, Selected: true),
                new Node("b", new XYPosition(300, 100), "rotatable", "B", Width: 100, Height: 40, FixedWidth: 100, FixedHeight: 40)
            ]);
        // La visibilité suit NodeHost.IsSelected : il faut une fenêtre affichée pour que les templates soient appliqués.
        var window = new Window
        {
            Content = flow, Width = 820, Height = 640, Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false
        };
        window.Show();
        try
        {
            flow.UpdateLayout();

            var rotator = (NodeHost host) => FlowView.FindDescendants<NodeRotator>(host).Single();
            var hosts = FlowView.FindDescendants<NodeHost>(flow).ToDictionary(h => h.Node!.Id);
            Assert.Equal(System.Windows.Visibility.Visible, rotator(hosts["a"]).Visibility);
            Assert.Equal(System.Windows.Visibility.Collapsed, rotator(hosts["b"]).Visibility);
        }
        finally
        {
            window.Close();
        }
    });
}
