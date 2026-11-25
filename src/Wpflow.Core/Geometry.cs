namespace Wpflow.Core;

/// <summary>Côté d'un node auquel un handle est attaché (équivalent de <c>Position</c> dans xyflow).</summary>
public enum Position
{
    Left,
    Top,
    Right,
    Bottom
}

/// <summary>Point en coordonnées « flow » (ou écran selon le contexte).</summary>
public readonly record struct XYPosition(double X, double Y)
{
    public static readonly XYPosition Zero = new(0, 0);

    public static XYPosition operator +(XYPosition a, XYPosition b) => new(a.X + b.X, a.Y + b.Y);
    public static XYPosition operator -(XYPosition a, XYPosition b) => new(a.X - b.X, a.Y - b.Y);
}

/// <summary>Rectangle défini par son coin supérieur gauche et sa taille.</summary>
public readonly record struct Rect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public XYPosition Center => new(X + Width / 2, Y + Height / 2);

    public static Rect Union(Rect a, Rect b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        return new Rect(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
    }

    public bool Contains(XYPosition p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;

    /// <summary>Vrai si <paramref name="other" /> est entièrement dans ce rectangle.</summary>
    public bool Contains(Rect other) => other.X >= X && other.Right <= Right && other.Y >= Y && other.Bottom <= Bottom;

    public bool Overlaps(Rect other) =>
        X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
}

/// <summary>
///     Transformation du viewport : <c>écran = flow * Zoom + (X, Y)</c>.
///     Équivalent du <c>Viewport</c> / <c>Transform</c> de xyflow.
/// </summary>
public readonly record struct Viewport(double X, double Y, double Zoom)
{
    public static readonly Viewport Identity = new(0, 0, 1);
}
