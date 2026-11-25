using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace demo;

/// <summary>
///     Nuage de cubes/pyramides en 3D (projection orthographique, faces triées et ombrées), animé en continu :
///     le nuage tourne sur lui-même et chaque solide pivote et flotte. Remplace la scène WebGL de la démo React Flow.
///     La couleur, la forme et le zoom viennent de <see cref="DemoState" /> (valeurs « effectives », donc seulement si
///     les nodes correspondants sont reliés).
/// </summary>
public sealed class CubeField : FrameworkElement
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(DemoState), typeof(CubeField),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnStateChanged));

    private const int Count = 64;

    private readonly (double X, double Y, double Z, double Phase, double Speed, double Size)[] _solids;
    private readonly DateTime _start = DateTime.UtcNow;
    private double _zoom = DemoState.DefaultZoom; // zoom lissé (suit EffectiveZoom avec une inertie)
    private DateTime _last = DateTime.UtcNow;

    public CubeField()
    {
        var rng = new Random(11);
        _solids = Enumerable.Range(0, Count).Select(_ =>
        {
            // Point uniforme dans une sphère de rayon 1.
            double x, y, z;
            do
            {
                x = rng.NextDouble() * 2 - 1;
                y = rng.NextDouble() * 2 - 1;
                z = rng.NextDouble() * 2 - 1;
            } while (x * x + y * y + z * z > 1);

            return (x, y, z, rng.NextDouble() * Math.PI * 2, 0.4 + rng.NextDouble() * 0.9, 0.07 + rng.NextDouble() * 0.06);
        }).ToArray();

        // L'abonnement à l'état et à la boucle de rendu ne vit que tant que le contrôle est dans l'arbre (pas de fuite quand le node disparaît).
        Loaded += (_, _) =>
        {
            CompositionTarget.Rendering += OnFrame;
            if (State != null) State.PropertyChanged += OnStatePropertyChanged;
        };
        Unloaded += (_, _) =>
        {
            CompositionTarget.Rendering -= OnFrame;
            if (State != null) State.PropertyChanged -= OnStatePropertyChanged;
        };
    }

    public DemoState? State
    {
        get => (DemoState?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>Angle de rotation courant du nuage (radians) ; croît avec le temps, utile pour vérifier l'animation.</summary>
    public double Angle => (DateTime.UtcNow - _start).TotalSeconds * 0.45;

    /// <summary>Zoom actuellement affiché (lissé).</summary>
    public double DisplayedZoom => _zoom;

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var field = (CubeField)d;
        if (!field.IsLoaded) return;
        if (e.OldValue is DemoState old) old.PropertyChanged -= field.OnStatePropertyChanged;
        if (e.NewValue is DemoState @new) @new.PropertyChanged += field.OnStatePropertyChanged;
    }

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    private void OnFrame(object? sender, EventArgs e)
    {
        // Inertie du zoom : on se rapproche de la cible à chaque image (indépendant de la cadence).
        var now = DateTime.UtcNow;
        var dt = Math.Min((now - _last).TotalSeconds, 0.1);
        _last = now;
        if (State is { } s) _zoom += (s.EffectiveZoom - _zoom) * (1 - Math.Exp(-8 * dt));
        InvalidateVisual();
    }

    private static Brush Shade(Color c, double factor) =>
        new SolidColorBrush(Color.FromRgb(
            (byte)Math.Clamp(c.R * factor, 0, 255), (byte)Math.Clamp(c.G * factor, 0, 255),
            (byte)Math.Clamp(c.B * factor, 0, 255)));

    private readonly record struct V(double X, double Y, double Z)
    {
        public static V operator +(V a, V b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V operator -(V a, V b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V operator *(V a, double k) => new(a.X * k, a.Y * k, a.Z * k);
        public double Dot(V o) => X * o.X + Y * o.Y + Z * o.Z;
        public V Cross(V o) => new(Y * o.Z - Z * o.Y, Z * o.X - X * o.Z, X * o.Y - Y * o.X);

        public V RotY(double a) => new(X * Math.Cos(a) + Z * Math.Sin(a), Y, -X * Math.Sin(a) + Z * Math.Cos(a));
        public V RotX(double a) => new(X, Y * Math.Cos(a) - Z * Math.Sin(a), Y * Math.Sin(a) + Z * Math.Cos(a));
        public V RotZ(double a) => new(X * Math.Cos(a) - Y * Math.Sin(a), X * Math.Sin(a) + Y * Math.Cos(a), Z);
    }

    // Faces d'un cube (indices de sommets, sens anti-horaire vu de l'extérieur).
    private static readonly int[][] CubeFaces =
    [
        [0, 1, 2, 3], [5, 4, 7, 6], [4, 0, 3, 7], [1, 5, 6, 2], [3, 2, 6, 7], [4, 5, 1, 0]
    ];

    private static readonly V[] CubeVerts =
    [
        new(-1, -1, 1), new(1, -1, 1), new(1, 1, 1), new(-1, 1, 1),
        new(-1, -1, -1), new(1, -1, -1), new(1, 1, -1), new(-1, 1, -1)
    ];

    private static readonly V[] PyramidVerts = [new(-1, -1, 1), new(1, -1, 1), new(1, -1, -1), new(-1, -1, -1), new(0, 1.3, 0)];
    private static readonly int[][] PyramidFaces = [[0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4], [3, 2, 1, 0]];

    protected override void OnRender(DrawingContext dc)
    {
        if (State is not { } state || ActualWidth <= 0 || ActualHeight <= 0) return;

        var t = (DateTime.UtcNow - _start).TotalSeconds;
        var color = state.EffectiveColor;
        var cube = state.EffectiveIsCube;
        var verts = cube ? CubeVerts : PyramidVerts;
        var faces = cube ? CubeFaces : PyramidFaces;

        // Le zoom joue sur l'échelle de la « caméra » : nuage et solides grossissent ensemble.
        var scale = Math.Min(ActualWidth, ActualHeight) * (0.10 + _zoom * 0.34);
        var cx = ActualWidth / 2;
        var cy = ActualHeight / 2 + 6;
        var spin = t * 0.45;
        const double tilt = 0.45;
        var light = new V(-0.4, 0.8, 0.6);
        var lightLen = Math.Sqrt(light.Dot(light));
        light = light * (1 / lightLen);

        // Position monde de chaque solide : rotation du nuage + légère flottaison verticale.
        var items = _solids.Select(s =>
        {
            var p = new V(s.X * 2.1, s.Y * 1.6 + Math.Sin(t * s.Speed + s.Phase) * 0.12, s.Z * 2.1);
            p = p.RotY(spin).RotX(tilt);
            return (S: s, P: p);
        }).OrderBy(i => i.P.Z); // les plus lointains d'abord

        foreach (var (s, p) in items)
        {
            var own = t * s.Speed + s.Phase;
            var size = s.Size * 1.6;
            // Sommets : rotation propre, puis celle du nuage, puis translation.
            var world = verts.Select(v => (v * size).RotY(own).RotZ(own * 0.6).RotY(spin).RotX(tilt) + p * 1).ToArray();

            foreach (var face in faces)
            {
                var a = world[face[0]];
                var b = world[face[1]];
                var c = world[face[2]];
                var n = (b - a).Cross(c - a);
                var len = Math.Sqrt(n.Dot(n));
                if (len < 1e-9) continue;
                n = n * (1 / len);
                if (n.Z <= 0) continue; // face dos à la caméra

                var brightness = 0.55 + 0.6 * Math.Max(0, n.Dot(light));
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(Project(world[face[0]], cx, cy, scale), true, true);
                    ctx.PolyLineTo(face.Skip(1).Select(i => Project(world[i], cx, cy, scale)).ToList(), false, false);
                }

                geo.Freeze();
                dc.DrawGeometry(Shade(color, brightness), null, geo);
            }
        }
    }

    private static Point Project(V v, double cx, double cy, double scale) => new(cx + v.X * scale, cy - v.Y * scale);
}
