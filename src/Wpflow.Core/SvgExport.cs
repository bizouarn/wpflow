using System.Globalization;
using System.Security;
using System.Text;

namespace Wpflow.Core;

/// <summary>Un edge à dessiner : le record et le tracé que la vue a calculé pour lui.</summary>
public sealed record SvgEdge(Edge Edge, EdgePath Path, bool MarkerStart, bool MarkerEnd);

/// <summary>
///     Dessin vectoriel simplifié d'un graphe, commun aux vues WPF et Blazor (<c>FlowView.ExportSvg</c>) : les nodes en
///     rectangles avec leur texte, les edges avec leur tracé, leur label et leurs flèches. Un node à contenu personnalisé
///     apparaît comme son rectangle et son <see cref="Node.Data" />.
/// </summary>
public static class SvgExport
{
    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <param name="nodes">Les nodes affichables, en coordonnées absolues ; ceux qui ne sont pas mesurés sont ignorés.</param>
    /// <param name="edges">Les edges affichés.</param>
    /// <param name="dark">Palette sombre.</param>
    /// <param name="padding">Marge autour des nodes.</param>
    /// <param name="background">Couleur de fond (<c>null</c> : celle de la palette).</param>
    public static string Build(IEnumerable<Node> nodes, IEnumerable<SvgEdge> edges, bool dark = false, double padding = 20,
        string? background = null)
    {
        var shown = nodes.Where(n => !n.Hidden).ToList();
        var bounds = GraphUtils.GetNodesBounds(shown) ?? new Rect(0, 0, 100, 100);
        var bg = background ?? (dark ? "#141414" : "#ffffff");
        var fg = dark ? "#f2f2f2" : "#222222";
        var stroke = dark ? "#888888" : "#1a192b";
        var edgeColor = dark ? "#666666" : "#b1b1b7";
        var x = bounds.X - padding;
        var y = bounds.Y - padding;
        var w = bounds.Width + 2 * padding;
        var h = bounds.Height + 2 * padding;
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(w)}\" height=\"{F(h)}\" viewBox=\"{F(x)} {F(y)} {F(w)} {F(h)}\">");
        sb.Append($"<defs><marker id=\"a\" viewBox=\"-10 -10 20 20\" markerWidth=\"20\" markerHeight=\"20\" orient=\"auto-start-reverse\"><polyline points=\"-5,-4 0,0 -5,4 -5,-4\" fill=\"{edgeColor}\" stroke=\"{edgeColor}\"/></marker></defs>");
        sb.Append($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(w)}\" height=\"{F(h)}\" fill=\"{SecurityElement.Escape(bg)}\"/>");
        foreach (var (edge, path, markerStart, markerEnd) in edges)
        {
            // Une variable CSS n'aurait pas de valeur hors de la page.
            var color = edge.Style?.Stroke is { } c && !c.StartsWith("var(") ? SecurityElement.Escape(c) : edgeColor;
            sb.Append($"<path d=\"{path.Path}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{F(edge.Style?.StrokeWidth ?? 1)}\"");
            if (edge.Animated) sb.Append(" stroke-dasharray=\"5\"");
            if (markerEnd) sb.Append(" marker-end=\"url(#a)\"");
            if (markerStart) sb.Append(" marker-start=\"url(#a)\"");
            sb.Append("/>");
            if (!string.IsNullOrEmpty(edge.Label))
                sb.Append($"<text x=\"{F(path.LabelX)}\" y=\"{F(path.LabelY)}\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-size=\"10\" fill=\"{fg}\">{SecurityElement.Escape(edge.Label)}</text>");
        }

        foreach (var n in shown.Where(n => n.Width != null && n.Height != null))
        {
            var p = n.Position;
            var fill = n.Type == "group" ? "none" : dark ? "#1e1e1e" : "#ffffff";
            sb.Append($"<rect x=\"{F(p.X)}\" y=\"{F(p.Y)}\" width=\"{F(n.Width!.Value)}\" height=\"{F(n.Height!.Value)}\" fill=\"{fill}\" stroke=\"{stroke}\"/>");
            if (n.Type != "group")
                sb.Append($"<text x=\"{F(p.X + n.Width.Value / 2)}\" y=\"{F(p.Y + n.Height.Value / 2)}\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-size=\"12\" fill=\"{fg}\">{SecurityElement.Escape(n.Data?.ToString() ?? n.Id)}</text>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }
}
