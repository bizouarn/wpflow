using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace demo;

/// <summary>Nuage de cubes/pyramides isométriques (remplace la scène 3D de la démo React Flow).</summary>
public sealed class CubeField : FrameworkElement
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(DemoState), typeof(CubeField),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnStateChanged));

    private readonly (double A, double R, double H)[] _cloud;

    public CubeField()
    {
        var rng = new Random(11);
        _cloud = Enumerable.Range(0, 70)
            .Select(_ => (A: rng.NextDouble() * Math.PI * 2, R: Math.Sqrt(rng.NextDouble()), H: rng.NextDouble()))
            .ToArray();
    }

    public DemoState? State
    {
        get => (DemoState?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var field = (CubeField)d;
        if (e.OldValue is DemoState old) old.PropertyChanged -= field.OnStatePropertyChanged;
        if (e.NewValue is DemoState @new) @new.PropertyChanged += field.OnStatePropertyChanged;
    }

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    private static Brush Shade(Color c, double factor) =>
        new SolidColorBrush(Color.FromRgb(
            (byte)Math.Min(255, c.R * factor), (byte)Math.Min(255, c.G * factor), (byte)Math.Min(255, c.B * factor)));

    protected override void OnRender(DrawingContext dc)
    {
        if (State is not { } state || ActualWidth <= 0) return;

        var top = Shade(state.Color, 1.25);
        var left = Shade(state.Color, 1.0);
        var right = Shade(state.Color, 0.72);
        var scale = 0.5 + state.Zoom * 1.1;
        var spread = Math.Min(ActualWidth, ActualHeight) * 0.42 * (0.55 + state.Zoom * 0.6);
        var cx = ActualWidth / 2;
        var cy = ActualHeight / 2;

        // Tri par y pour un rendu correct (peintre).
        var items = _cloud
            .Select(p => (X: cx + Math.Cos(p.A) * p.R * spread * 1.25, Y: cy + Math.Sin(p.A) * p.R * spread * 0.9, p.H))
            .OrderBy(p => p.Y);

        foreach (var (x, y, h) in items)
        {
            var s = (6 + h * 9) * scale;
            const double k = 0.87;
            if (state.IsCube)
            {
                Face(dc, top, (x, y - s), (x + k * s, y - s / 2), (x, y), (x - k * s, y - s / 2));
                Face(dc, left, (x - k * s, y - s / 2), (x, y), (x, y + s), (x - k * s, y + s / 2));
                Face(dc, right, (x, y), (x + k * s, y - s / 2), (x + k * s, y + s / 2), (x, y + s));
            }
            else
            {
                Face(dc, left, (x, y - s), (x - k * s, y + s * 0.3), (x, y + s));
                Face(dc, right, (x, y - s), (x, y + s), (x + k * s, y + s * 0.3));
            }
        }
    }

    private static void Face(DrawingContext dc, Brush fill, params (double X, double Y)[] pts)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(pts[0].X, pts[0].Y), true, true);
            ctx.PolyLineTo(pts.Skip(1).Select(p => new Point(p.X, p.Y)).ToList(), false, false);
        }

        g.Freeze();
        dc.DrawGeometry(fill, null, g);
    }
}
