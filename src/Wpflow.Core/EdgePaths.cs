using System.Globalization;
using System.Text;

namespace Wpflow.Core;

/// <summary>Résultat du calcul d'un chemin : syntaxe SVG (utilisable avec <c>Geometry.Parse</c> en WPF) et centre du label.</summary>
public sealed record EdgePath(string Path, double LabelX, double LabelY, double OffsetX, double OffsetY);

/// <summary>Génération des chemins d'edges, portée de <c>getBezierPath</c>, <c>getSmoothStepPath</c>, <c>getStraightPath</c>.</summary>
public static class EdgePaths
{
    private static string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

    // ---------- Straight ----------

    public static EdgePath Straight(XYPosition source, XYPosition target)
    {
        var (cx, cy, ox, oy) = EdgeCenter(source, target);
        return new EdgePath($"M {F(source.X)},{F(source.Y)}L {F(target.X)},{F(target.Y)}", cx, cy, ox, oy);
    }

    public static (double X, double Y, double OffsetX, double OffsetY) EdgeCenter(XYPosition source, XYPosition target)
    {
        var xOffset = Math.Abs(target.X - source.X) / 2;
        var yOffset = Math.Abs(target.Y - source.Y) / 2;
        return (target.X < source.X ? target.X + xOffset : target.X - xOffset,
            target.Y < source.Y ? target.Y + yOffset : target.Y - yOffset,
            xOffset, yOffset);
    }

    // ---------- Bezier ----------

    private static double ControlOffset(double distance, double curvature) =>
        distance >= 0 ? 0.5 * distance : curvature * 25 * Math.Sqrt(-distance);

    private static XYPosition Control(Position pos, XYPosition a, XYPosition b, double c) => pos switch
    {
        Position.Left => new XYPosition(a.X - ControlOffset(a.X - b.X, c), a.Y),
        Position.Right => new XYPosition(a.X + ControlOffset(b.X - a.X, c), a.Y),
        Position.Top => new XYPosition(a.X, a.Y - ControlOffset(a.Y - b.Y, c)),
        Position.Bottom => new XYPosition(a.X, a.Y + ControlOffset(b.Y - a.Y, c)),
        _ => throw new ArgumentOutOfRangeException(nameof(pos))
    };

    public static EdgePath Bezier(XYPosition source, Position sourcePosition, XYPosition target, Position targetPosition,
        double curvature = 0.25)
    {
        var sc = Control(sourcePosition, source, target, curvature);
        var tc = Control(targetPosition, target, source, curvature);

        // Point milieu d'une bézier cubique (t = 0.5).
        var cx = source.X * 0.125 + sc.X * 0.375 + tc.X * 0.375 + target.X * 0.125;
        var cy = source.Y * 0.125 + sc.Y * 0.375 + tc.Y * 0.375 + target.Y * 0.125;

        var path = $"M{F(source.X)},{F(source.Y)} C{F(sc.X)},{F(sc.Y)} {F(tc.X)},{F(tc.Y)} {F(target.X)},{F(target.Y)}";
        return new EdgePath(path, cx, cy, Math.Abs(cx - source.X), Math.Abs(cy - source.Y));
    }

    /// <summary>Bézier « simple » (<c>getSimpleBezierPath</c>) : points de contrôle à mi-distance sur l'axe du handle.</summary>
    public static EdgePath SimpleBezier(XYPosition source, Position sourcePosition, XYPosition target, Position targetPosition)
    {
        static XYPosition Ctrl(Position pos, XYPosition a, XYPosition b) =>
            pos is Position.Left or Position.Right ? new XYPosition(0.5 * (a.X + b.X), a.Y) : new XYPosition(a.X, 0.5 * (a.Y + b.Y));

        var sc = Ctrl(sourcePosition, source, target);
        var tc = Ctrl(targetPosition, target, source);
        var cx = source.X * 0.125 + sc.X * 0.375 + tc.X * 0.375 + target.X * 0.125;
        var cy = source.Y * 0.125 + sc.Y * 0.375 + tc.Y * 0.375 + target.Y * 0.125;
        var path = $"M{F(source.X)},{F(source.Y)} C{F(sc.X)},{F(sc.Y)} {F(tc.X)},{F(tc.Y)} {F(target.X)},{F(target.Y)}";
        return new EdgePath(path, cx, cy, Math.Abs(cx - source.X), Math.Abs(cy - source.Y));
    }

    // ---------- Smooth step / step ----------

    private static XYPosition Dir(Position p) => p switch
    {
        Position.Left => new XYPosition(-1, 0),
        Position.Right => new XYPosition(1, 0),
        Position.Top => new XYPosition(0, -1),
        Position.Bottom => new XYPosition(0, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(p))
    };

    private static XYPosition DirectionBetween(XYPosition source, Position sourcePosition, XYPosition target) =>
        sourcePosition is Position.Left or Position.Right
            ? source.X < target.X ? new XYPosition(1, 0) : new XYPosition(-1, 0)
            : source.Y < target.Y
                ? new XYPosition(0, 1)
                : new XYPosition(0, -1);

    private static double Get(XYPosition p, bool x) => x ? p.X : p.Y;
    private static XYPosition With(XYPosition p, bool x, double v) => x ? p with { X = v } : p with { Y = v };

    private static double Dist(XYPosition a, XYPosition b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static void AddPoint(List<XYPosition> path, XYPosition point)
    {
        if (path.Count >= 2 && IsBetween(path[^2], path[^1], point)) path.RemoveAt(path.Count - 1);
        path.Add(point);
    }

    /// <summary>
    ///     Points du chemin « step » (port de <c>getPoints</c> de xyflow, version récente : <c>stepPosition</c>,
    ///     <c>centerX/centerY</c> et suppression des points alignés).
    /// </summary>
    public static (List<XYPosition> Points, double CenterX, double CenterY, double OffsetX, double OffsetY) StepPoints(
        XYPosition source, Position sourcePosition, XYPosition target, Position targetPosition, double offset = 20,
        double stepPosition = 0.5, double? centerX = null, double? centerY = null)
    {
        var sourceDir = Dir(sourcePosition);
        var targetDir = Dir(targetPosition);
        var sourceGapped = new XYPosition(source.X + sourceDir.X * offset, source.Y + sourceDir.Y * offset);
        var targetGapped = new XYPosition(target.X + targetDir.X * offset, target.Y + targetDir.Y * offset);

        var dir = DirectionBetween(sourceGapped, sourcePosition, targetGapped);
        var onX = dir.X != 0;
        var currDir = Get(dir, onX);
        var (_, _, defaultOx, defaultOy) = EdgeCenter(source, target);

        double cx, cy;
        var sourceGapOffset = XYPosition.Zero;
        var targetGapOffset = XYPosition.Zero;
        var path = new List<XYPosition>(6);

        if (Get(sourceDir, onX) * Get(targetDir, onX) == -1)
        {
            // Handles opposés : deux coudes, positionnés par stepPosition.
            if (onX)
            {
                cx = centerX ?? sourceGapped.X + (targetGapped.X - sourceGapped.X) * stepPosition;
                cy = centerY ?? (sourceGapped.Y + targetGapped.Y) / 2;
            }
            else
            {
                cx = centerX ?? (sourceGapped.X + targetGapped.X) / 2;
                cy = centerY ?? sourceGapped.Y + (targetGapped.Y - sourceGapped.Y) * stepPosition;
            }

            var vertical = Get(sourceDir, onX) == currDir ? onX : !onX;
            var mid1 = vertical ? new XYPosition(cx, sourceGapped.Y) : new XYPosition(sourceGapped.X, cy);
            var mid2 = vertical ? new XYPosition(cx, targetGapped.Y) : new XYPosition(targetGapped.X, cy);

            AddPoint(path, source);
            AddPoint(path, sourceGapped);
            AddPoint(path, mid1);
            AddPoint(path, mid2);
            AddPoint(path, targetGapped);
            AddPoint(path, target);
        }
        else
        {
            var useSourceTarget = onX ? sourceDir.X != currDir : sourceDir.Y == currDir;

            if (sourcePosition == targetPosition)
            {
                var diff = Math.Abs(Get(source, onX) - Get(target, onX));
                if (diff <= offset)
                {
                    var gapOffset = Math.Min(offset - 1, offset - diff);
                    if (Get(sourceDir, onX) == currDir)
                        sourceGapOffset = With(sourceGapOffset, onX,
                            (Get(sourceGapped, onX) > Get(source, onX) ? -1 : 1) * gapOffset);
                    else
                        targetGapOffset = With(targetGapOffset, onX,
                            (Get(targetGapped, onX) > Get(target, onX) ? -1 : 1) * gapOffset);
                }
            }

            if (sourcePosition != targetPosition)
            {
                var opposite = !onX;
                var isSameDir = Get(sourceDir, onX) == Get(targetDir, opposite);
                var sourceGt = Get(sourceGapped, opposite) > Get(targetGapped, opposite);
                var sourceLt = Get(sourceGapped, opposite) < Get(targetGapped, opposite);
                var flip = Get(sourceDir, onX) == 1
                    ? (!isSameDir && sourceGt) || (isSameDir && sourceLt)
                    : (!isSameDir && sourceLt) || (isSameDir && sourceGt);
                if (flip) useSourceTarget = onX;
            }

            var mid = useSourceTarget
                ? new XYPosition(sourceGapped.X, targetGapped.Y)
                : new XYPosition(targetGapped.X, sourceGapped.Y);

            var sourceGapPoint = sourceGapped + sourceGapOffset;
            var targetGapPoint = targetGapped + targetGapOffset;
            var maxX = Math.Max(Math.Abs(sourceGapPoint.X - mid.X), Math.Abs(targetGapPoint.X - mid.X));
            var maxY = Math.Max(Math.Abs(sourceGapPoint.Y - mid.Y), Math.Abs(targetGapPoint.Y - mid.Y));

            // Le label se place sur le plus long segment.
            if (maxX >= maxY)
            {
                cx = (sourceGapPoint.X + targetGapPoint.X) / 2;
                cy = mid.Y;
            }
            else
            {
                cx = mid.X;
                cy = (sourceGapPoint.Y + targetGapPoint.Y) / 2;
            }

            AddPoint(path, source);
            AddPoint(path, sourceGapPoint);
            AddPoint(path, mid);
            AddPoint(path, targetGapPoint);
            AddPoint(path, target);
        }

        return (path, cx, cy, defaultOx, defaultOy);
    }

    // b est-il sur le segment droit a -> c (b == c inclus) ?
    private static bool IsBetween(XYPosition a, XYPosition b, XYPosition c) =>
        (a.X == b.X && b.X == c.X && (b.Y - a.Y) * (c.Y - b.Y) >= 0) ||
        (a.Y == b.Y && b.Y == c.Y && (b.X - a.X) * (c.X - b.X) >= 0);

    private static void AppendNumber(StringBuilder sb, double v)
    {
#if NET8_0_OR_GREATER
        Span<char> buffer = stackalloc char[32];
        if (v.TryFormat(buffer, out var written, "0.####", CultureInfo.InvariantCulture))
        {
            sb.Append(buffer[..written]);
            return;
        }
#endif
        sb.Append(v.ToString("0.####", CultureInfo.InvariantCulture));
    }

    private static void AppendBend(StringBuilder sb, XYPosition a, XYPosition b, XYPosition c, double size)
    {
        var bendSize = Math.Min(Math.Min(Dist(a, b) / 2, Dist(b, c) / 2), size);
        var (x, y) = (b.X, b.Y);

        // Alignés : pas de coude.
        if ((a.X == x && x == c.X) || (a.Y == y && y == c.Y))
        {
            sb.Append('L');
            AppendNumber(sb, x);
            sb.Append(' ');
            AppendNumber(sb, y);
            return;
        }

        if (a.Y == y)
        {
            var xDir = a.X < c.X ? -1 : 1;
            var yDir = a.Y < c.Y ? 1 : -1;
            sb.Append("L ");
            AppendNumber(sb, x + bendSize * xDir);
            sb.Append(',');
            AppendNumber(sb, y);
            sb.Append("Q ");
            AppendNumber(sb, x);
            sb.Append(',');
            AppendNumber(sb, y);
            sb.Append(' ');
            AppendNumber(sb, x);
            sb.Append(',');
            AppendNumber(sb, y + bendSize * yDir);
            return;
        }

        var xd = a.X < c.X ? 1 : -1;
        var yd = a.Y < c.Y ? -1 : 1;
        sb.Append("L ");
        AppendNumber(sb, x);
        sb.Append(',');
        AppendNumber(sb, y + bendSize * yd);
        sb.Append("Q ");
        AppendNumber(sb, x);
        sb.Append(',');
        AppendNumber(sb, y);
        sb.Append(' ');
        AppendNumber(sb, x + bendSize * xd);
        sb.Append(',');
        AppendNumber(sb, y);
    }

    /// <summary>Chemin à angles droits ; <paramref name="borderRadius" /> = 0 donne l'edge de type « step ».</summary>
    public static EdgePath SmoothStep(XYPosition source, Position sourcePosition, XYPosition target,
        Position targetPosition, double borderRadius = 5, double offset = 20, double stepPosition = 0.5,
        double? centerX = null, double? centerY = null)
    {
        var (points, cx, cy, ox, oy) =
            StepPoints(source, sourcePosition, target, targetPosition, offset, stepPosition, centerX, centerY);

        var sb = new StringBuilder(64);
        sb.Append('M');
        AppendNumber(sb, points[0].X);
        sb.Append(' ');
        AppendNumber(sb, points[0].Y);
        for (var i = 1; i < points.Count - 1; i++)
            AppendBend(sb, points[i - 1], points[i], points[i + 1], borderRadius);
        sb.Append('L');
        AppendNumber(sb, points[^1].X);
        sb.Append(' ');
        AppendNumber(sb, points[^1].Y);

        return new EdgePath(sb.ToString(), cx, cy, ox, oy);
    }

    public static EdgePath Step(XYPosition source, Position sourcePosition, XYPosition target, Position targetPosition,
        double offset = 20, double stepPosition = 0.5) =>
        SmoothStep(source, sourcePosition, target, targetPosition, 0, offset, stepPosition);
}
