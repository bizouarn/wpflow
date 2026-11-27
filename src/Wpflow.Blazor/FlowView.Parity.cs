using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Wpflow.Blazor;

/// <summary>La molette au-dessus du graphe (<c>onPaneScroll</c>).</summary>
public sealed record PaneScrollEventArgs(FlowPointer Pointer, double DeltaX, double DeltaY);

// Évènements, réglages et états que la vue WPF avait seule.
public partial class FlowView
{
    private const double DefaultEdgeHitWidth = 20;

    [Parameter] public EventCallback<NodeEventArgs> OnNodeMouseMove { get; set; }

    [Parameter] public EventCallback<EdgeEventArgs> OnEdgeMouseMove { get; set; }

    /// <summary>La molette a tourné au-dessus du graphe, qu'elle zoome, fasse défiler ou ne fasse rien.</summary>
    [Parameter] public EventCallback<PaneScrollEventArgs> OnPaneScroll { get; set; }

    /// <summary>Après un rendu qui a changé les nodes, les edges, le viewport, le verrou, le thème ou la taille.</summary>
    [Parameter] public EventCallback OnStateChange { get; set; }

    /// <summary>Un node atteint au clavier (Tab, flèches) est ramené dans la vue.</summary>
    [Parameter] public bool AutoPanOnNodeFocus { get; set; } = true;

    /// <summary>Largeur (unités du graphe) de la zone cliquable d'un edge ; <c>Edge.InteractionWidth</c> l'emporte.</summary>
    [Parameter] public double EdgeHitWidth { get; set; } = DefaultEdgeHitWidth;

    /// <summary>Vrai quand tous les nodes ont été mesurés (voir <see cref="OnNodesInitialized" />).</summary>
    public bool NodesInitialized => _nodesInitialized;

    /// <summary>Le presse-papier a un contenu et la vue n'est pas verrouillée.</summary>
    public bool CanPaste => _clipboard != null && !IsLocked;

    public bool IsViewportAnimating => _animating > 0;

    /// <summary>Une connexion est en cours de tracé.</summary>
    public bool IsConnecting => CurrentConnection.InProgress;

    public double Zoom => Viewport.Zoom;

    /// <summary>Les nodes affichables, en coordonnées absolues (les sous-nodes ne sont plus relatifs à leur parent).</summary>
    public IEnumerable<Node> AbsoluteNodes => VisibleAbsoluteNodes();

    private int _animating;
    private bool _tabbed; // la dernière action est une tabulation : le focus qui suit vient du clavier

    private (IReadOnlyList<Node>, IReadOnlyList<Edge>, Viewport, bool, bool, double, double)? _state;

    private Task RaiseStateChangeIfNeeded()
    {
        if (!OnStateChange.HasDelegate) return Task.CompletedTask;
        var state = (Nodes, Edges, Viewport, IsLocked, Dark, _width, _height);
        if (_state == state) return Task.CompletedTask;
        // Seule la vue a bougé : le parent qui se redessine en l'apprenant ne fait pas refaire la couche.
        var viewportOnly = _state is { } was && was with { Item3 = state.Viewport } == state;
        _state = state;
        return viewportOnly ? NotifyViewport(() => OnStateChange.InvokeAsync()) : OnStateChange.InvokeAsync();
    }

    private string? EdgeHitStyle(Edge edge)
    {
        var width = edge.InteractionWidth ?? (EdgeHitWidth == DefaultEdgeHitWidth ? null : EdgeHitWidth);
        return width is { } w ? $"stroke-width:{F(w)}px" : null;
    }

    internal EventCallback<PointerEventArgs> NodeMoveHandler(Node n) =>
        OnNodeMouseMove.HasDelegate ? Cb(e => OnNodeMouseMove.InvokeAsync(new NodeEventArgs(n, MakePointer(e)))) : default;

    private EventCallback<PointerEventArgs> EdgeMoveHandler(Edge edge) =>
        OnEdgeMouseMove.HasDelegate ? Cb(e => OnEdgeMouseMove.InvokeAsync(new EdgeEventArgs(edge, MakePointer(e)))) : default;

    /// <summary>Déplace la vue le moins possible pour que ce node soit entièrement visible.</summary>
    private async Task EnsureVisible(string nodeId, double margin = 30)
    {
        if (_width <= 0 || _height <= 0 || !_absNodes.TryGetValue(nodeId, out var n) || n.Width is not { } w || n.Height is not { } h) return;
        var shown = ViewLogic.PanToShow(Viewport, new Rect(n.Position.X, n.Position.Y, w, h), _width, _height, margin);
        if (shown != Viewport) await SetViewport(shown);
    }
}
