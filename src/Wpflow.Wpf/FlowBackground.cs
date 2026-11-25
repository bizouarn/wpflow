using System.Windows;
using System.Windows.Media;
using Wpflow.Core;
using Rect = System.Windows.Rect;

namespace Wpflow.Wpf;

/// <summary>
///     Un motif de fond (<c>&lt;Background id="…" variant gap size lineWidth offset color /&gt;</c>). On en superpose plusieurs
///     avec <see cref="FlowView.AdditionalBackgrounds" />, par exemple une grille fine et une grille large.
/// </summary>
/// <param name="Variant">Points, lignes ou croix.</param>
/// <param name="Gap">Écart entre deux motifs, en unités flow.</param>
/// <param name="Color">Couleur ; <c>null</c> = couleur du thème.</param>
/// <param name="Size">Rayon d'un point ou demi-longueur d'une croix ; <c>null</c> = 1,1 (points) / 3 (croix).</param>
/// <param name="LineWidth">Épaisseur des lignes et des croix.</param>
/// <param name="Offset">Décalage du motif, en unités flow.</param>
public sealed record BackgroundLayer(BackgroundVariant Variant = BackgroundVariant.Dots, double Gap = 20,
    Brush? Color = null, double? Size = null, double LineWidth = 1, double Offset = 0);

/// <summary>Fond de la surface (points / lignes / croix) qui suit le viewport grâce à des <c>DrawingBrush</c> en tuiles.</summary>
public sealed class FlowBackground : FrameworkElement
{
    private sealed class Tile(DrawingBrush? brush)
    {
        public DrawingBrush? Brush { get; } = brush;
        public double Offset { get; init; }
    }

    private List<Tile> _tiles = new();
    private IReadOnlyList<(BackgroundLayer Layer, Brush Color)> _layers = [];
    private Viewport _viewport = Viewport.Identity;

    public FlowBackground()
    {
        IsHitTestVisible = false;
    }

    /// <summary>Un seul motif (rétrocompatible).</summary>
    internal void Configure(BackgroundVariant variant, double gap, Brush color) =>
        Configure([(new BackgroundLayer(variant, gap), color)]);

    internal void Configure(IReadOnlyList<(BackgroundLayer Layer, Brush Color)> layers)
    {
        _layers = layers;
        _tiles = new List<Tile>();
        InvalidateVisual();
    }

    internal void SetViewport(Viewport v)
    {
        _viewport = v;
        foreach (var t in _tiles)
            if (t.Brush != null)
                t.Brush.Transform = TransformFor(t.Offset);
        InvalidateVisual();
    }

    private Transform TransformFor(double offset) =>
        new MatrixTransform(_viewport.Zoom, 0, 0, _viewport.Zoom, _viewport.X + offset * _viewport.Zoom,
            _viewport.Y + offset * _viewport.Zoom);

    private Tile Build(BackgroundLayer layer, Brush color)
    {
        var g = Math.Max(2, layer.Gap);
        var drawing = new DrawingGroup();
        var pen = new Pen(color, layer.LineWidth);
        switch (layer.Variant)
        {
            case BackgroundVariant.None:
                return new Tile(null);
            case BackgroundVariant.Dots:
                var r = layer.Size ?? 1.1;
                drawing.Children.Add(new GeometryDrawing(color, null, new EllipseGeometry(new Point(0, 0), r, r)));
                break;
            case BackgroundVariant.Lines:
                drawing.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(-g / 2, 0), new Point(g / 2, 0))));
                drawing.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(0, -g / 2), new Point(0, g / 2))));
                break;
            case BackgroundVariant.Cross:
                var s = layer.Size ?? 3;
                drawing.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(-s, 0), new Point(s, 0))));
                drawing.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(0, -s), new Point(0, s))));
                break;
        }

        // Rectangle invisible de la taille de la tuile pour que le motif ne soit pas rogné.
        drawing.Children.Insert(0, new GeometryDrawing(Brushes.Transparent, null,
            new RectangleGeometry(new Rect(-g / 2, -g / 2, g, g))));

        var tile = new Rect(-g / 2, -g / 2, g, g);
        return new Tile(new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Viewport = tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = tile,
            ViewboxUnits = BrushMappingMode.Absolute,
            Transform = TransformFor(layer.Offset)
        })
        {
            Offset = layer.Offset
        };
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_tiles.Count != _layers.Count)
            _tiles = _layers.Select(l => Build(l.Layer, l.Color)).ToList();
        foreach (var t in _tiles)
            if (t.Brush != null)
                dc.DrawRectangle(t.Brush, null, new Rect(0, 0, ActualWidth, ActualHeight));
    }
}
