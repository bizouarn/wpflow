using System.Windows.Controls;
using System.Windows.Media;
using Wpflow.Core;
using Wpflow.Wpf;

namespace Consumer;

/// <summary>Un fournisseur de tracé de l'application : compte ses appels pour prouver qu'il est utilisé.</summary>
public sealed class WaveEdge : IEdgePathProvider
{
    public static int Calls { get; private set; }

    public EdgePath GetPath(EdgeGeometry geometry)
    {
        Calls++;
        return EdgePaths.SmoothStep(geometry.Source, geometry.SourcePosition, geometry.Target, geometry.TargetPosition,
            geometry.Edge?.PathOptions?.BorderRadius ?? 12);
    }
}

internal static class Custom
{
    public static void Fill(FlowView flow)
    {
        flow.Nodes =
        [
            new Node("a", new XYPosition(0, 0), "card", "A"),
            new Node("b", new XYPosition(300, 120), "card", "B")
        ];
        flow.Edges = [new Edge("a-b", "a", "b", Type: "wave", MarkerEndKey: "dot")];
    }

    public static void Check(FlowView view, Action<string, bool> check)
    {
        check("template de node de l'application utilisé", Probe.Find<TextBlock>(view).Count(t => Equals(t.Tag, "card")) == 2);
        check("fournisseur de tracé de l'application appelé", WaveEdge.Calls > 0);
        check("marqueur de l'application résolu", view.TryFindResource("wpflow-marker-dot") is Geometry);
    }
}
