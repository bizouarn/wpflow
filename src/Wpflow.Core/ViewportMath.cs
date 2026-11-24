namespace Wpflow.Core;

/// <summary>Calculs de viewport (pan / zoom / fit), portés de <c>@xyflow/system</c>.</summary>
public static class ViewportMath
{
    public const double DefaultMinZoom = 0.5;
    public const double DefaultMaxZoom = 2;

    public static XYPosition ScreenToFlow(Viewport v, XYPosition screen, bool snapToGrid = false, double gridX = 15, double gridY = 15)
    {
        var p = new XYPosition((screen.X - v.X) / v.Zoom, (screen.Y - v.Y) / v.Zoom);
        return snapToGrid ? SnapPosition(p, gridX, gridY) : p;
    }

    public static XYPosition FlowToScreen(Viewport v, XYPosition flow) =>
        new(flow.X * v.Zoom + v.X, flow.Y * v.Zoom + v.Y);

    public static XYPosition SnapPosition(XYPosition p, double gridX, double gridY) =>
        new(gridX * Math.Round(p.X / gridX, MidpointRounding.AwayFromZero),
            gridY * Math.Round(p.Y / gridY, MidpointRounding.AwayFromZero));

    public static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));

    public static Viewport PanBy(Viewport v, double dx, double dy) => v with { X = v.X + dx, Y = v.Y + dy };

    /// <summary>Change le zoom en gardant fixe le point écran <paramref name="screenPoint" /> (zoom sur le curseur).</summary>
    public static Viewport ZoomAt(Viewport v, XYPosition screenPoint, double newZoom,
        double minZoom = DefaultMinZoom, double maxZoom = DefaultMaxZoom)
    {
        var zoom = Clamp(newZoom, minZoom, maxZoom);
        var ratio = zoom / v.Zoom;
        return new Viewport(
            screenPoint.X - (screenPoint.X - v.X) * ratio,
            screenPoint.Y - (screenPoint.Y - v.Y) * ratio,
            zoom);
    }

    /// <summary>
    ///     Facteur de zoom pour un cran de molette (comportement d3-zoom utilisé par xyflow :
    ///     <c>2^(-deltaY * 0.002)</c>, ×10 si Ctrl/pinch).
    /// </summary>
    public static double WheelZoomFactor(double deltaY, bool ctrlKey = false) =>
        Math.Pow(2, -deltaY * 0.002 * (ctrlKey ? 10 : 1));

    /// <summary>Viewport qui cadre <paramref name="bounds" /> dans une zone de <paramref name="width" />×<paramref name="height" />.</summary>
    public static Viewport GetViewportForBounds(Rect bounds, double width, double height,
        double minZoom = DefaultMinZoom, double maxZoom = DefaultMaxZoom, double padding = 0.1)
    {
        var xZoom = width / (bounds.Width * (1 + padding));
        var yZoom = height / (bounds.Height * (1 + padding));
        var zoom = Clamp(Math.Min(xZoom, yZoom), minZoom, maxZoom);

        var center = bounds.Center;
        return new Viewport(width / 2 - center.X * zoom, height / 2 - center.Y * zoom, zoom);
    }

    /// <summary>
    ///     Contraint le viewport pour que la zone visible reste dans <paramref name="extent" /> (<c>translateExtent</c>,
    ///     même algorithme que d3-zoom : si l'extent est plus petit que la vue, il est centré).
    /// </summary>
    public static Viewport ClampToExtent(Viewport v, double width, double height, Rect extent)
    {
        var k = v.Zoom;
        var dx0 = -v.X / k - extent.X;
        var dx1 = (width - v.X) / k - extent.Right;
        var dy0 = -v.Y / k - extent.Y;
        var dy1 = (height - v.Y) / k - extent.Bottom;

        static double Shift(double d0, double d1) =>
            d1 > d0 ? (d0 + d1) / 2 : Math.Min(0, d0) != 0 ? Math.Min(0, d0) : Math.Max(0, d1);

        return new Viewport(v.X + k * Shift(dx0, dx1), v.Y + k * Shift(dy0, dy1), k);
    }

    /// <summary>Rectangle visible (en coordonnées flow) pour un viewport et une taille de contrôle donnés.</summary>
    public static Rect GetVisibleRect(Viewport v, double width, double height) =>
        new(-v.X / v.Zoom, -v.Y / v.Zoom, width / v.Zoom, height / v.Zoom);
}
