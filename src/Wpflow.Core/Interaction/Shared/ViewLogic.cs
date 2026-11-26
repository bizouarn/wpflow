// Logique commune aux deux vues qui ne fait pas partie de l'API publique. Ce fichier est compilé dans Wpflow.Core
// (pour ses tests) et, par un lien dans leur csproj, dans Wpflow.Wpf et Wpflow.Blazor : chaque assembly a sa copie, donc
// aucun InternalsVisibleTo entre paquets (qui casserait avec des versions de paquets mélangées).

namespace Wpflow.Core;

/// <summary>Petits calculs des gestes : clic, côté opposé, fil de connexion, défilement vers un élément.</summary>
internal static class ViewLogic
{
    /// <summary>En deçà (en pixels), un tremblement reste un clic, quels que soient les seuils de glisser.</summary>
    public const double ClickTolerance = 4;

    /// <summary>
    ///     Un appui relâché après avoir parcouru au plus <paramref name="travelled" /> pixels est-il un clic ? Un seuil de glisser
    ///     plus grand que la tolérance laisse le clic jusqu'à lui.
    /// </summary>
    public static bool IsClick(double travelled, double dragThreshold) => travelled < Math.Max(ClickTolerance, dragThreshold);

    /// <summary>
    ///     L'extrémité qu'un appui sur un edge peut déplacer : la plus proche des deux à portée (la cible à égale distance), ou
    ///     <c>null</c> si aucune ne l'est.
    /// </summary>
    public static HandleType? ReconnectedEnd(XYPosition press, XYPosition source, XYPosition target, double reach)
    {
        static double Distance(XYPosition a, XYPosition b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        var (toSource, toTarget) = (Distance(press, source), Distance(press, target));
        if (Math.Min(toSource, toTarget) > reach) return null;
        return toTarget <= toSource ? HandleType.Target : HandleType.Source;
    }

    /// <summary>
    ///     Facteur de zoom d'un défilement sur la minimap : celui de la surface, élevé à <c>zoomStep</c> / 10 (10 = comme la
    ///     surface). <paramref name="deltaY" /> suit la convention de <see cref="WheelInput.Resolve" />.
    /// </summary>
    public static double MiniMapZoomFactor(double deltaY, double zoomStep) =>
        Math.Pow(ViewportMath.WheelZoomFactor(deltaY), zoomStep / 10);

    public static Position Opposite(Position side) => side switch
    {
        Position.Left => Position.Right,
        Position.Right => Position.Left,
        Position.Top => Position.Bottom,
        _ => Position.Top
    };

    /// <summary>Tracé du fil de connexion en cours (<c>connectionLineType</c>).</summary>
    public static EdgePath ConnectionLine(ConnectionLineType type, XYPosition start, Position startSide, XYPosition end, Position endSide) =>
        EdgePathProviders.ForType(type switch
        {
            ConnectionLineType.Straight => "straight",
            ConnectionLineType.Step => "step",
            ConnectionLineType.SmoothStep => "smoothstep",
            ConnectionLineType.SimpleBezier => "simplebezier",
            _ => "default"
        }).GetPath(new EdgeGeometry(start, startSide, end, endSide));

    /// <summary>
    ///     Le viewport déplacé le moins possible pour que <paramref name="flow" /> soit visible à <paramref name="margin" /> pixels
    ///     des bords ; le même s'il l'est déjà.
    /// </summary>
    public static Viewport PanToShow(Viewport viewport, Rect flow, double width, double height, double margin)
    {
        var topLeft = ViewportMath.FlowToScreen(viewport, new XYPosition(flow.X, flow.Y));
        var bottomRight = ViewportMath.FlowToScreen(viewport, new XYPosition(flow.Right, flow.Bottom));
        static double Shift(double near, double far, double size, double margin) =>
            near < margin ? margin - near : far > size - margin ? size - margin - far : 0;
        return ViewportMath.PanBy(viewport, Shift(topLeft.X, bottomRight.X, width, margin),
            Shift(topLeft.Y, bottomRight.Y, height, margin));
    }

    /// <summary>
    ///     Un ViewModel a changé de lui-même : le changement est reporté dans les états gardés par l'historique, pour que
    ///     l'annulation d'un geste ne le défasse pas.
    /// </summary>
    public static void Rebase(GraphHistory? history, NodeSourceAdapter adapter, Node before, Node after) =>
        history?.Rewrite(s => s with { Nodes = s.Nodes.Select(n => n.Id == before.Id ? adapter.Rebase(n, before, after) : n).ToList() });

    /// <inheritdoc cref="Rebase(GraphHistory?,NodeSourceAdapter,Node,Node)" />
    public static void Rebase(GraphHistory? history, EdgeSourceAdapter adapter, Edge before, Edge after) =>
        history?.Rewrite(s => s with { Edges = s.Edges.Select(e => e.Id == before.Id ? adapter.Rebase(e, before, after) : e).ToList() });
}

/// <summary>Dit si la sélection a changé, sans rien allouer quand elle est la même (appelé à chaque changement des nodes).</summary>
internal sealed class SelectionTracker
{
    private readonly List<string> _nodes = new();
    private readonly List<string> _edges = new();

    /// <summary>Vrai si quelque chose était sélectionné au dernier état retenu.</summary>
    public bool HasSelection => _nodes.Count > 0 || _edges.Count > 0;

    /// <summary>Retient la sélection de ces listes ; vrai si elle diffère de la précédente (au départ : rien de sélectionné).</summary>
    public bool Update(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges)
    {
        // Les deux sont toujours mis à jour.
        var changed = Store(_nodes, nodes, static n => n.Selected, static n => n.Id);
        return Store(_edges, edges, static e => e.Selected, static e => e.Id) || changed;
    }

    private static bool Store<T>(List<string> kept, IReadOnlyList<T> items, Func<T, bool> selected, Func<T, string> idOf)
    {
        var count = 0;
        var same = true;
        for (var i = 0; i < items.Count; i++)
        {
            if (!selected(items[i])) continue;
            same = count < kept.Count && kept[count] == idOf(items[i]);
            if (!same) break;
            count++;
        }

        if (same && count == kept.Count) return false;
        kept.Clear();
        for (var i = 0; i < items.Count; i++)
            if (selected(items[i]))
                kept.Add(idOf(items[i]));
        return true;
    }
}
