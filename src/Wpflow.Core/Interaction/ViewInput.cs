namespace Wpflow.Core;

/// <summary>Défilement automatique du viewport quand le pointeur s'approche d'un bord pendant un geste.</summary>
public static class AutoPan
{
    /// <summary>Distance au bord (pixels) à partir de laquelle la vue défile.</summary>
    public const double Edge = 40;

    /// <summary>Défilement (en pixels, signe du déplacement du <b>contenu</b>) pour une position de pointeur donnée.</summary>
    public static (double X, double Y) Delta(double x, double y, double width, double height, double speed)
    {
        double Axis(double p, double size)
        {
            if (p < Edge) return speed * Math.Min(1, (Edge - Math.Max(p, 0)) / Edge);
            if (p > size - Edge) return -speed * Math.Min(1, (Edge - Math.Max(size - p, 0)) / Edge);
            return 0;
        }

        return (Axis(x, width), Axis(y, height));
    }
}

public enum WheelKind
{
    None,
    Zoom,
    Pan
}

/// <summary>Ce que fait un cran de molette : zoom (facteur), défilement (pixels) ou rien ; <see cref="Handled" /> = la page ne défile pas.</summary>
public readonly record struct WheelResult(WheelKind Kind, double ZoomFactor, double PanX, double PanY, bool Handled);

/// <summary>Interprétation de la molette (<c>zoomOnScroll</c>, <c>panOnScroll</c>, touche d'activation du zoom).</summary>
public static class WheelInput
{
    /// <summary>
    ///     <paramref name="deltaY" /> et <paramref name="deltaX" /> sont positifs quand la molette défile vers le bas / la droite
    ///     (convention du navigateur ; en WPF, passer <c>-e.Delta</c>). <paramref name="pinch" /> : geste de pincement du pavé tactile
    ///     (Ctrl + molette dans un navigateur), plus sensible.
    /// </summary>
    public static WheelResult Resolve(double deltaX, double deltaY, bool zoomKeyHeld, bool shift, bool zoomOnScroll, bool panOnScroll,
        double panOnScrollSpeed, PanOnScrollMode panMode, bool preventScrolling, bool pinch = false)
    {
        if (panOnScroll && !zoomKeyHeld)
        {
            var stepY = -deltaY * panOnScrollSpeed;
            var stepX = -deltaX * panOnScrollSpeed;
            // Maj + molette : défilement horizontal, comme dans la plupart des applications.
            var horizontal = panMode == PanOnScrollMode.Horizontal || (panMode == PanOnScrollMode.Free && shift);
            var (dx, dy) = horizontal ? (stepY != 0 ? stepY : stepX, 0.0) : (stepX, stepY);
            if (panMode == PanOnScrollMode.Vertical) dx = 0;
            return new WheelResult(WheelKind.Pan, 1, dx, dy, preventScrolling);
        }

        if (zoomOnScroll || zoomKeyHeld)
            return new WheelResult(WheelKind.Zoom, ViewportMath.WheelZoomFactor(deltaY, pinch), 0, 0, preventScrolling);
        return new WheelResult(WheelKind.None, 1, 0, 0, false);
    }
}

/// <summary>Cadrage (<c>fitView</c>) : calcul du viewport pour un ensemble de nodes mesurés.</summary>
public static class ViewFitting
{
    /// <summary>Le viewport qui cadre les nodes demandés, ou <c>null</c> si la zone est vide ou aucun node n'est mesuré.</summary>
    public static Viewport? Compute(IEnumerable<Node> nodes, double width, double height, double minZoom, double maxZoom,
        double defaultPadding, FitViewOptions? options = null)
    {
        if (width <= 0 || height <= 0) return null;
        var ids = options?.NodeIds?.ToHashSet();
        var picked = nodes.Where(n => (options?.IncludeHiddenNodes == true || !n.Hidden) && n.Rect != null && (ids == null || ids.Contains(n.Id)));
        if (GraphUtils.GetNodesBounds(picked) is not { } bounds) return null;
        return ViewportMath.GetViewportForBounds(bounds, width, height, options?.MinZoom ?? minZoom, options?.MaxZoom ?? maxZoom,
            options?.Padding ?? defaultPadding);
    }
}

/// <summary>Virtualisation : zones de création des éléments autour de la vue.</summary>
public static class Virtualization
{
    /// <summary>Affichés : la vue + 300 px (on mesure un peu avant l'entrée).</summary>
    public const double ShowMargin = 300;

    /// <summary>Gardés : + 900 px, pour ne pas recréer un node à chaque va-et-vient du pan.</summary>
    public const double KeepMargin = 900;

    public static (Rect Show, Rect Keep) Zones(Rect view, double zoom) => (Inflate(view, ShowMargin / zoom), Inflate(view, KeepMargin / zoom));

    public static Rect Inflate(Rect r, double m) => new(r.X - m, r.Y - m, r.Width + 2 * m, r.Height + 2 * m);

    /// <summary>Taille supposée d'un node jamais mesuré, pour décider s'il faut le créer.</summary>
    public static Rect AssumedRect(Node n, XYPosition? position = null) =>
        new((position ?? n.Position).X, (position ?? n.Position).Y, n.Width ?? n.InitialWidth ?? 200, n.Height ?? n.InitialHeight ?? 100);

    /// <summary>Le node avec sa taille initiale annoncée (<c>initialWidth</c>/<c>initialHeight</c>) tant qu'il n'est pas mesuré.</summary>
    public static Node WithInitialSize(Node n) =>
        n.Rect == null && n.InitialWidth is { } w && n.InitialHeight is { } h ? n with { Width = w, Height = h } : n;
}

/// <summary>Cadrage de la minimap : la zone du flow affichée, au ratio de la minimap, avec une marge.</summary>
public static class MiniMapMath
{
    public readonly record struct Layout(double X, double Y, double Width, double Height, double Padding, double Scale);

    /// <summary>Même logique que xyflow : le contenu (nodes ∪ zone visible) est centré et cadré en conservant le ratio de la minimap.</summary>
    public static Layout Compute(Rect? nodesBounds, Rect visible, double width, double height)
    {
        var bounds = nodesBounds is { } nb ? Rect.Union(nb, visible) : visible;
        var viewScale = Math.Max(bounds.Width / width, bounds.Height / height);
        var viewW = viewScale * width;
        var viewH = viewScale * height;
        var padding = 5 * viewScale;
        var vbX = bounds.X - (viewW - bounds.Width) / 2 - padding;
        var vbY = bounds.Y - (viewH - bounds.Height) / 2 - padding;
        return new Layout(vbX, vbY, viewW + 2 * padding, viewH + 2 * padding, padding, width / (viewW + 2 * padding));
    }
}
