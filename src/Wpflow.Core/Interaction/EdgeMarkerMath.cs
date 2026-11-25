namespace Wpflow.Core;

/// <summary>Taille, épaisseur et orientation d'un marqueur d'edge (flèche), communes aux vues.</summary>
public static class EdgeMarkerMath
{
    /// <summary>Taille par défaut de xyflow : la flèche telle qu'elle est dessinée (échelle 1).</summary>
    public const double DefaultSize = 12.5;

    /// <summary>Les sommets de la flèche (pointe en (0,0), orientée vers +x) : <c>-5,-4 → 0,0 → -5,4</c>.</summary>
    public static readonly IReadOnlyList<XYPosition> ArrowPoints = [new(-5, -4), new(0, 0), new(-5, 4)];

    /// <summary>Échelle horizontale et verticale de la flèche pour des <see cref="EdgeMarkerOptions.Width" /> / <see cref="EdgeMarkerOptions.Height" />.</summary>
    public static (double X, double Y) Scale(EdgeMarkerOptions? options) =>
        ((options?.Width ?? DefaultSize) / DefaultSize, (options?.Height ?? DefaultSize) / DefaultSize);

    /// <summary>Épaisseur du trait de la flèche : celle des options, sinon celle de l'edge.</summary>
    public static double StrokeWidth(EdgeMarkerOptions? options, double edgeThickness) => options?.StrokeWidth ?? edgeThickness;

    /// <summary>Une flèche pointe vers le node : angle (degrés) opposé au côté où le handle est attaché.</summary>
    public static double SideAngle(Position side) => side switch
    {
        Position.Left => 0,
        Position.Top => 90,
        Position.Right => 180,
        _ => 270
    };
}
