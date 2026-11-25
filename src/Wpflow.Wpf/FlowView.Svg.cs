using Wpflow.Core;

namespace Wpflow.Wpf;

public partial class FlowView
{
    /// <summary>
    ///     Dessin vectoriel simplifié du graphe (voir <see cref="SvgExport" />) : nodes en rectangles avec leur texte, edges
    ///     avec leur tracé et leurs flèches. Pour une image fidèle (modèles personnalisés, thème), utilisez
    ///     <see cref="SaveImage" />. Les nodes doivent avoir été mesurés par la vue.
    /// </summary>
    public string ExportSvg(double padding = 20, string? background = null)
    {
        var edges = new List<SvgEdge>();
        foreach (var edge in Edges)
        {
            if (edge.Hidden || GetEdgeEnds(edge) is not var (s, sp, t, tp)) continue;
            var provider = TryFindResource("wpflow-edge-" + edge.Type) as IEdgePathProvider ?? EdgePathProviders.ForType(edge.Type);
            edges.Add(new SvgEdge(edge, provider.GetPath(new EdgeGeometry(s, sp, t, tp, edge)),
                edge.MarkerStart != EdgeMarker.None || edge.MarkerStartKey != null,
                edge.MarkerEnd != EdgeMarker.None || edge.MarkerEndKey != null));
        }

        return SvgExport.Build(AbsoluteNodes, edges, ColorMode == ColorMode.Dark, padding, background);
    }
}
