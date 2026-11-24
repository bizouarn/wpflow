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
    ///     minimale/maximale est atteinte.
    /// </summary>
    public static Rect Resize(ResizeDirection direction, Rect start, double dx, double dy,
        double minWidth = 10, double minHeight = 10,
        double maxWidth = double.PositiveInfinity, double maxHeight = double.PositiveInfinity)
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

        return new Rect(x, y, w, h);
    }
}
