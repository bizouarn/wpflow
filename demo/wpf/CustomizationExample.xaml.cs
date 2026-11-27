using System.Windows.Controls;
using Wpflow.Core;

namespace demo;

/// <summary>Personnalisation depuis l'extérieur de la bibliothèque : templates, styles et ressources uniquement.</summary>
public partial class CustomizationExample : UserControl
{
    public CustomizationExample()
    {
        InitializeComponent();
        Flow.Nodes =
        [
            new Node("a", new XYPosition(0, 0), Data: "Start"),
            new Node("b", new XYPosition(320, -70), Data: "Wave"),
            new Node("c", new XYPosition(320, 90), Data: "Pulse"),
            new Node("d", new XYPosition(640, 10), Data: "End")
        ];
        Flow.Edges =
        [
            new Edge("wave", "a", "b", Type: "wave", MarkerEndKey: "dot"),
            new Edge("pulse", "a", "c", Type: "pulse", Label: "pulse"),
            new Edge("diamond", "b", "d", Type: "smoothstep", Animated: true, MarkerEndKey: "diamond"),
            new Edge("arrow", "c", "d", MarkerEnd: EdgeMarker.ArrowClosed)
        ];
    }
}
