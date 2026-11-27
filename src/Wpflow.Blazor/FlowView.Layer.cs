using Wpflow.Core;

namespace Wpflow.Blazor;

/// <summary>
///     Quand la couche des nodes et des edges peut être gardée telle quelle. La reconstruire coûte des centaines de
///     millisecondes sur un grand graphe en WebAssembly : un pas de pan ne doit modifier que la transformation du calque.
/// </summary>
public partial class FlowView
{
    private int _keepingCallbacks; // l'application est en train d'être prévenue d'un changement de vue
    private readonly HashSet<string> _unresolvedEdges = new(); // sans extrémités à la dernière reconstruction

    /// <summary>
    ///     Prévient l'application d'un changement de vue. Les rendus qui ont lieu pendant ce temps, dont celui que provoque un
    ///     parent qui écoute (<c>@bind-Viewport</c>), gardent la couche ; ce qu'il change pour de bon (nodes, edges, modèles
    ///     redéclarés, réglages) la fait refaire.
    /// </summary>
    private async Task NotifyKeepingLayer(Func<Task> notify)
    {
        _keepingCallbacks++;
        try
        {
            await notify();
        }
        finally
        {
            _keepingCallbacks--;
        }
    }

    private Node[] _seenNodes = [];
    private Edge[] _seenEdges = [];

    /// <summary>
    ///     Les nodes et les edges reçus sont-ils, un à un, ceux du dernier rendu du parent ? Comparer les éléments et non les
    ///     listes prend aussi en compte une liste que l'application modifie sur place. Les records sont immuables : la même
    ///     référence dit le même élément.
    /// </summary>
    private bool SameElements()
    {
        var same = true;
        if (!Same(Nodes, _seenNodes))
        {
            _seenNodes = Nodes.ToArray();
            same = false;
        }

        if (!Same(Edges, _seenEdges))
        {
            _seenEdges = Edges.ToArray();
            same = false;
        }

        return same;

        static bool Same<T>(IReadOnlyList<T> list, T[] seen) where T : class
        {
            if (list.Count != seen.Length) return false;
            for (var i = 0; i < seen.Length; i++)
                if (!ReferenceEquals(list[i], seen[i]))
                    return false;
            return true;
        }
    }

    /// <summary>Décide, une fois par rendu, si la couche est refaite.</summary>
    private bool KeepsLayer() =>
        !_forceLayer && (_keepsNextRender || _keepingCallbacks > 0) && !(OnlyRenderVisibleElements && VisibleSetOutdated());

    /// <summary>Les zones d'affichage et de maintien de la vue actuelle (virtualisation).</summary>
    private (Rect Show, Rect Keep) ViewZones() =>
        Virtualization.Zones(ViewportMath.GetVisibleRect(Viewport, _width, _height), Viewport.Zoom);

    private VirtualizationRule.Presence NodePresence(Node n, (Rect Show, Rect Keep) zones) =>
        VirtualizationRule.OfNode(n.Selected, Virtualization.AssumedRect(n, AbsolutePosition(n.Id)), zones.Show, zones.Keep);

    private VirtualizationRule.Presence EdgePresence(Edge e, (Rect Show, Rect Keep) zones) =>
        VirtualizationRule.OfEdge(ViewRect(e.Source), ViewRect(e.Target), zones.Show, zones.Keep);

    /// <summary>
    ///     Avec la virtualisation, la vue a-t-elle assez bougé pour changer ce qui doit être affiché
    ///     (<see cref="VirtualizationRule.IsOutdated" />) ?
    /// </summary>
    private bool VisibleSetOutdated()
    {
        if (!Virtualizes) return false;
        var zones = ViewZones();
        foreach (var n in Nodes)
            if (!n.Hidden && VirtualizationRule.IsOutdated(_renderedIds.Contains(n.Id), NodePresence(n, zones)))
                return true;

        foreach (var e in Edges)
        {
            if (e.Hidden || _unresolvedEdges.Contains(e.Id) || !_absNodes.ContainsKey(e.Source) || !_absNodes.ContainsKey(e.Target)) continue;
            if (VirtualizationRule.IsOutdated(_edgeContexts.ContainsKey(e.Id), EdgePresence(e, zones))) return true;
        }

        return false;
    }
}
