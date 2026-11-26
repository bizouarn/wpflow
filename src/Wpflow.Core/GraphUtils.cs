namespace Wpflow.Core;

/// <summary>Utilitaires de graphe : bounds, position des handles, connexions.</summary>
public static class GraphUtils
{
    /// <summary>Rectangle englobant les nodes mesurés ; <c>null</c> s'il n'y en a aucun.</summary>
    public static Rect? GetNodesBounds(IEnumerable<Node> nodes)
    {
        var list = nodes as IReadOnlyList<Node> ?? nodes.ToList();
        // Les enfants ont une position relative : on travaille en coordonnées absolues.
        var abs = list.Any(n => n.ParentId != null) ? NodeTree.AbsolutePositions(list) : null;

        Rect? bounds = null;
        foreach (var n in list)
        {
            if (n.Width is not { } w || n.Height is not { } h) continue;
            var p = abs?[n.Id] ?? n.Position;
            var r = new Rect(p.X, p.Y, w, h);
            bounds = bounds is { } b ? Rect.Union(b, r) : r;
        }

        return bounds;
    }

    /// <summary>Centre d'un handle (utilisé pour la distance de connexion, comme <c>getHandlePosition(..., center = true)</c>).</summary>
    public static XYPosition GetHandleCenter(Node node, HandleInfo handle) =>
        new(node.Position.X + handle.X + handle.Width / 2, node.Position.Y + handle.Y + handle.Height / 2);

    /// <summary>
    ///     Un edge est visible si le rectangle englobant ses deux nodes recoupe la zone visible
    ///     (port de <c>isEdgeVisible</c>, utilisé pour <c>onlyRenderVisibleElements</c>).
    /// </summary>
    public static bool IsEdgeVisible(Rect sourceNode, Rect targetNode, Rect visible)
    {
        var box = Rect.Union(sourceNode, targetNode);
        // Un segment dégénéré (largeur ou hauteur nulle) doit rester testable.
        box = new Rect(box.X, box.Y, Math.Max(box.Width, 1), Math.Max(box.Height, 1));
        return box.Overlaps(visible);
    }

    /// <summary>
    ///     Position absolue (coordonnées flow) du point d'attache d'un handle. Sans handle explicite,
    ///     on prend le milieu du côté du node correspondant à <paramref name="position" />.
    /// </summary>
    public static XYPosition GetHandlePosition(Node node, Position position, HandleInfo? handle = null)
    {
        var width = handle?.Width ?? node.Width ?? 0;
        var height = handle?.Height ?? node.Height ?? 0;
        var offsetX = handle?.X ?? 0;
        var offsetY = handle?.Y ?? 0;

        var (dx, dy) = position switch
        {
            Position.Top => (offsetX + width / 2, offsetY),
            Position.Bottom => (offsetX + width / 2, offsetY + height),
            Position.Left => (offsetX, offsetY + height / 2),
            Position.Right => (offsetX + width, offsetY + height / 2),
            _ => throw new ArgumentOutOfRangeException(nameof(position))
        };

        return new XYPosition(node.Position.X + dx, node.Position.Y + dy);
    }

    /// <summary>Distance entre deux points.</summary>
    internal static double Distance(XYPosition a, XYPosition b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>Handle le plus proche de <paramref name="point" /> dans le rayon donné (<c>connectionRadius</c>), sinon <c>null</c>.</summary>
    public static T? GetClosestHandle<T>(XYPosition point, double radius, IEnumerable<(T Handle, XYPosition Position)> candidates)
        where T : class
    {
        T? closest = null;
        var min = double.MaxValue;
        foreach (var (handle, pos) in candidates)
        {
            var dist = Distance(point, pos);
            if (dist <= radius && dist < min)
            {
                min = dist;
                closest = handle;
            }
        }

        return closest;
    }

    /// <summary>Contraint la position d'un node de taille donnée à l'intérieur de <paramref name="extent" /> (<c>nodeExtent</c>).</summary>
    public static XYPosition ClampToExtent(XYPosition position, double width, double height, Rect extent) =>
        new(ViewportMath.Clamp(position.X, extent.X, Math.Max(extent.X, extent.Right - width)),
            ViewportMath.Clamp(position.Y, extent.Y, Math.Max(extent.Y, extent.Bottom - height)));

    /// <summary>Un edge identique (mêmes extrémités et handles) existe-t-il déjà ?</summary>
    public static bool ConnectionExists(Connection c, IEnumerable<Edge> edges) =>
        edges.Any(e => e.Source == c.Source && e.Target == c.Target
                       && SameHandle(e.SourceHandle, c.SourceHandle) && SameHandle(e.TargetHandle, c.TargetHandle));

    // null et "" désignent le handle par défaut (comme dans xyflow).
    private static bool SameHandle(string? a, string? b) => string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b) || a == b;
}
