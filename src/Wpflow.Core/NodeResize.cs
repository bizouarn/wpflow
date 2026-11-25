namespace Wpflow.Core;

/// <summary>Poignée de redimensionnement d'un node.</summary>
public enum ResizeDirection
{
    Top,
    Right,
    Bottom,
    Left,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

/// <summary>Calcul du redimensionnement d'un node (port de la logique de <c>XYResizer</c>).</summary>
public static class NodeResize
{
    /// <summary>
    ///     Nouveau rectangle après déplacement de la poignée de <paramref name="dx" />, <paramref name="dy" />
    ///     (coordonnées flow, relatives au rectangle de départ). Le bord opposé reste fixe, y compris quand une taille
    ///     minimale/maximale est atteinte. Avec <paramref name="keepAspectRatio" />, le rapport largeur/hauteur de départ
    ///     est conservé (le facteur d'échelle suit la dimension qui a le plus changé, puis est borné par les min/max).
    /// </summary>
    public static Rect Resize(ResizeDirection direction, Rect start, double dx, double dy,
        double minWidth = 10, double minHeight = 10,
        double maxWidth = double.PositiveInfinity, double maxHeight = double.PositiveInfinity,
        bool keepAspectRatio = false)
    {
        var left = direction is ResizeDirection.Left or ResizeDirection.TopLeft or ResizeDirection.BottomLeft;
        var right = direction is ResizeDirection.Right or ResizeDirection.TopRight or ResizeDirection.BottomRight;
        var top = direction is ResizeDirection.Top or ResizeDirection.TopLeft or ResizeDirection.TopRight;
        var bottom = direction is ResizeDirection.Bottom or ResizeDirection.BottomLeft or ResizeDirection.BottomRight;

        double x = start.X, y = start.Y, w = start.Width, h = start.Height;

        if (right) w = ViewportMath.Clamp(start.Width + dx, minWidth, maxWidth);
        if (left)
        {
            w = ViewportMath.Clamp(start.Width - dx, minWidth, maxWidth);
            x = start.Right - w;
        }

        if (bottom) h = ViewportMath.Clamp(start.Height + dy, minHeight, maxHeight);
        if (top)
        {
            h = ViewportMath.Clamp(start.Height - dy, minHeight, maxHeight);
            y = start.Bottom - h;
        }

        if (!keepAspectRatio || start.Width <= 0 || start.Height <= 0) return new Rect(x, y, w, h);

        // Une seule échelle pour les deux dimensions.
        var scaleX = (right || left) ? (start.Width + (right ? dx : -dx)) / start.Width : 1;
        var scaleY = (bottom || top) ? (start.Height + (bottom ? dy : -dy)) / start.Height : 1;
        var scale = Math.Abs(scaleX - 1) >= Math.Abs(scaleY - 1) ? scaleX : scaleY;
        var lower = Math.Max(minWidth / start.Width, minHeight / start.Height);
        var upper = Math.Min(maxWidth / start.Width, maxHeight / start.Height);
        scale = ViewportMath.Clamp(scale, lower, Math.Max(lower, upper));

        w = start.Width * scale;
        h = start.Height * scale;
        x = left ? start.Right - w : start.X;
        y = top ? start.Bottom - h : start.Y;
        return new Rect(x, y, w, h);
    }
}
