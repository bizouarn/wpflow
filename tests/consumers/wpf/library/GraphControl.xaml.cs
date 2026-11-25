using System.Windows.Controls;
using Wpflow.Core;
using Wpflow.Wpf;

namespace Graphs;

public partial class GraphControl : UserControl
{
    public GraphControl()
    {
        InitializeComponent();
        View.Nodes =
        [
            new Node("a", new XYPosition(0, 0), "card", "A"),
            new Node("b", new XYPosition(300, 120), "card", "B")
        ];
        View.Edges = [new Edge("a-b", "a", "b")];
    }

    public FlowView Flow => View;
}
