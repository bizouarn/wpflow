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

    private static double Dist(XYPosition a, XYPosition b) =>
        Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));

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

        List<XYPosition> points;
        double cx, cy;
        var sourceGapOffset = XYPosition.Zero;
        var targetGapOffset = XYPosition.Zero;

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

            var verticalSplit = new List<XYPosition> { new(cx, sourceGapped.Y), new(cx, targetGapped.Y) };
            var horizontalSplit = new List<XYPosition> { new(sourceGapped.X, cy), new(targetGapped.X, cy) };

            points = Get(sourceDir, onX) == currDir
                ? onX ? verticalSplit : horizontalSplit
                : onX
                    ? horizontalSplit
                    : verticalSplit;
        }
        else
        {
            var sourceTarget = new List<XYPosition> { new(sourceGapped.X, targetGapped.Y) };
            var targetSource = new List<XYPosition> { new(targetGapped.X, sourceGapped.Y) };

            points = onX
                ? sourceDir.X == currDir ? targetSource : sourceTarget
                : sourceDir.Y == currDir
                    ? sourceTarget
                    : targetSource;

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
                if (flip) points = onX ? sourceTarget : targetSource;
            }

            var sourceGapPoint = sourceGapped + sourceGapOffset;
            var targetGapPoint = targetGapped + targetGapOffset;
            var maxX = Math.Max(Math.Abs(sourceGapPoint.X - points[0].X), Math.Abs(targetGapPoint.X - points[0].X));
            var maxY = Math.Max(Math.Abs(sourceGapPoint.Y - points[0].Y), Math.Abs(targetGapPoint.Y - points[0].Y));

            // Le label se place sur le plus long segment.
            if (maxX >= maxY)
            {
                cx = (sourceGapPoint.X + targetGapPoint.X) / 2;
                cy = points[0].Y;
            }
            else
            {
                cx = points[0].X;
                cy = (sourceGapPoint.Y + targetGapPoint.Y) / 2;
            }
        }

        var candidates = new List<XYPosition> { source, sourceGapped + sourceGapOffset };
        candidates.AddRange(points);
        candidates.Add(targetGapped + targetGapOffset);
        candidates.Add(target);

        // On retire un point si le chemin passe tout droit par lui (sinon il réduirait le coude suivant).
        var path = new List<XYPosition>();
        foreach (var point in candidates)
        {
            if (path.Count >= 2 && IsBetween(path[^2], path[^1], point)) path.RemoveAt(path.Count - 1);
            path.Add(point);
        }

        return (path, cx, cy, defaultOx, defaultOy);
    }

    // b est-il sur le segment droit a -> c (b == c inclus) ?
    private static bool IsBetween(XYPosition a, XYPosition b, XYPosition c) =>
        (a.X == b.X && b.X == c.X && (b.Y - a.Y) * (c.Y - b.Y) >= 0) ||
        (a.Y == b.Y && b.Y == c.Y && (b.X - a.X) * (c.X - b.X) >= 0);

    private static string Bend(XYPosition a, XYPosition b, XYPosition c, double size)
    {
        var bendSize = Math.Min(Math.Min(Dist(a, b) / 2, Dist(b, c) / 2), size);
        var (x, y) = (b.X, b.Y);

        // Alignés : pas de coude.
        if ((a.X == x && x == c.X) || (a.Y == y && y == c.Y)) return $"L{F(x)} {F(y)}";

        if (a.Y == y)
        {
            var xDir = a.X < c.X ? -1 : 1;
            var yDir = a.Y < c.Y ? 1 : -1;
            return $"L {F(x + bendSize * xDir)},{F(y)}Q {F(x)},{F(y)} {F(x)},{F(y + bendSize * yDir)}";
        }

        var xd = a.X < c.X ? 1 : -1;
        var yd = a.Y < c.Y ? -1 : 1;
        return $"L {F(x)},{F(y + bendSize * yd)}Q {F(x)},{F(y)} {F(x + bendSize * xd)},{F(y)}";
    }

    /// <summary>Chemin à angles droits ; <paramref name="borderRadius" /> = 0 donne l'edge de type « step ».</summary>
    public static EdgePath SmoothStep(XYPosition source, Position sourcePosition, XYPosition target,
        Position targetPosition, double borderRadius = 5, double offset = 20, double stepPosition = 0.5,
        double? centerX = null, double? centerY = null)
    {
        var (points, cx, cy, ox, oy) =
            StepPoints(source, sourcePosition, target, targetPosition, offset, stepPosition, centerX, centerY);

        var sb = new StringBuilder($"M{F(points[0].X)} {F(points[0].Y)}");
        for (var i = 1; i < points.Count - 1; i++)
            sb.Append(Bend(points[i - 1], points[i], points[i + 1], borderRadius));
        sb.Append($"L{F(points[^1].X)} {F(points[^1].Y)}");

        return new EdgePath(sb.ToString(), cx, cy, ox, oy);
    }

    public static EdgePath Step(XYPosition source, Position sourcePosition, XYPosition target, Position targetPosition,
        double offset = 20, double stepPosition = 0.5) =>
        SmoothStep(source, sourcePosition, target, targetPosition, 0, offset, stepPosition);
}
