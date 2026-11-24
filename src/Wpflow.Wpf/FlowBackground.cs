using System.Windows;
using System.Windows.Media;
using Wpflow.Core;
using Rect = System.Windows.Rect;

namespace Wpflow.Wpf;

public enum BackgroundVariant
{
    None,
    Dots,
    Lines,
    Cross
}

/// <summary>Fond de la surface (points / lignes / croix) qui suit le viewport grâce à un <c>DrawingBrush</c> en tuiles.</summary>
public sealed class FlowBackground : FrameworkElement
{
    private DrawingBrush? _brush;
    private BackgroundVariant _variant = BackgroundVariant.Dots;
    private double _gap = 20;
    private Brush _color = Brushes.LightGray;
    private Viewport _viewport = Viewport.Identity;

    public FlowBackground()
    {
        IsHitTestVisible = false;
    }

    internal void Configure(BackgroundVariant variant, double gap, Brush color)
    {
        _variant = variant;
        _gap = Math.Max(2, gap);
        _color = color;
        _brush = null;
        InvalidateVisual();
    }

    internal void SetViewport(Viewport v)
    {
        _viewport = v;
        if (_brush != null) _brush.Transform = new MatrixTransform(v.Zoom, 0, 0, v.Zoom, v.X, v.Y);
        InvalidateVisual();
    }

    private DrawingBrush? BuildBrush()
    {
        var g = _gap;
        var drawing = new DrawingGroup();
        var pen = new Pen(_color, 1);
        switch (_variant)
        {
            case BackgroundVariant.None:
                return null;
            case BackgroundVariant.Dots:
                drawing.Children.Add(new GeometryDrawing(_color, null, new EllipseGeometry(new Point(0, 0), 1.1, 1.1)));
                break;
            case BackgroundVariant.Lines:
                drawing.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(-g / 2, 0), new Point(g / 2, 0))));
                drawing.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(0, -g / 2), new Point(0, g / 2))));
                break;
            case BackgroundVariant.Cross:
                drawing.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(-3, 0), new Point(3, 0))));
                drawing.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(0, -3), new Point(0, 3))));
                break;
        }

        // Rectangle invisible de la taille de la tuile pour que le motif ne soit pas rogné.
        drawing.Children.Insert(0, new GeometryDrawing(Brushes.Transparent, null,
            new RectangleGeometry(new Rect(-g / 2, -g / 2, g, g))));

        var tile = new Rect(-g / 2, -g / 2, g, g);
        return new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Viewport = tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = tile,
            ViewboxUnits = BrushMappingMode.Absolute,
            Transform = new MatrixTransform(_viewport.Zoom, 0, 0, _viewport.Zoom, _viewport.X, _viewport.Y)
        };
    }

    protected override void OnRender(DrawingContext dc)
    {
        _brush ??= BuildBrush();
        if (_brush == null) return;
        dc.DrawRectangle(_brush, null, new Rect(0, 0, ActualWidth, ActualHeight));
    }
}
