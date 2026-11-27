using Wpflow.Core;

namespace Wpflow.Blazor;

/// <summary>
///     Quand la couche des nodes et des edges peut être gardée telle quelle. La reconstruire coûte des centaines de
///     millisecondes sur un grand graphe en WebAssembly : un pas de pan ne doit modifier que la transformation du calque.
/// </summary>
public partial class FlowView
{
    private int _viewportCallbacks; // l'application est en train d'être prévenue d'un changement de vue
    private readonly HashSet<string> _unresolvedEdges = new(); // sans extrémités à la dernière reconstruction

    /// <summary>
    ///     Prévient l'application d'un changement de vue. Les rendus qui ont lieu pendant ce temps, dont celui que provoque un
    ///     parent qui écoute (<c>@bind-Viewport</c>), gardent la couche ; ce qu'il change pour de bon (nodes, edges, modèles
    ///     redéclarés, réglages) la fait refaire.
    /// </summary>
    private async Task NotifyViewport(Func<Task> notify)
    {
        _viewportCallbacks++;
        try
        {
            await notify();
        }
        finally
        {
            _viewportCallbacks--;
        }
    }

    /// <summary>Change à chaque reconstruction de la couche : la minimap redessine alors aussi ses rectangles.</summary>
    internal int LayerRevision => _layerRevision;

    /// <summary>Décide, une fois par rendu, si la couche est refaite.</summary>
    private bool KeepsLayer() =>
        !_forceLayer && (_viewportOnly || _viewportCallbacks > 0) && !(OnlyRenderVisibleElements && VisibleSetOutdated());

    /// <summary>
    ///     Avec la virtualisation, la vue a-t-elle assez bougé pour changer ce qui doit être affiché ? Un élément apparaît
    ///     quand il entre dans la zone d'affichage ; il n'est retiré qu'en sortant de la zone, plus large, où on le garde.
    ///     Sans cette marge, la couche serait refaite à chaque pas au bord de la vue.
    /// </summary>
    private bool VisibleSetOutdated()
    {
        if (!Virtualizes) return false;
        var (show, keep) = Virtualization.Zones(ViewportMath.GetVisibleRect(Viewport, _width, _height), Viewport.Zoom);
        foreach (var n in Nodes)
        {
            if (n.Hidden) continue;
            var shown = _renderedIds.Contains(n.Id);
            if (n.Selected)
            {
                if (!shown) return true;
                continue;
            }

            var rect = Virtualization.AssumedRect(n, _abs[n.Id]);
            if (shown ? !keep.Overlaps(rect) : show.Overlaps(rect)) return true;
        }

        foreach (var e in Edges)
        {
            if (e.Hidden || !_absNodes.ContainsKey(e.Source) || !_absNodes.ContainsKey(e.Target)) continue;
            var (from, to) = (ViewRect(e.Source), ViewRect(e.Target)); // les mêmes tailles que pour le dessin
            if (_edgeContexts.ContainsKey(e.Id)
                    ? !GraphUtils.IsEdgeVisible(from, to, keep)
                    : !_unresolvedEdges.Contains(e.Id) && GraphUtils.IsEdgeVisible(from, to, show)) return true;
        }

        return false;
    }
}
