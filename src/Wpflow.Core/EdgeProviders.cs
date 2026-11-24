namespace Wpflow.Core;

/// <summary>Extrémités d'un edge : points d'attache et côtés des handles (en coordonnées flow).</summary>
public readonly record struct EdgeGeometry(XYPosition Source, Position SourcePosition, XYPosition Target,
    Position TargetPosition);

/// <summary>
///     Calcule le tracé d'un type d'edge (équivalent d'une fonction <c>getXxxPath</c> de xyflow).
///     Implémentez-la pour créer un type d'edge personnalisé (<c>Edge.Type</c>).
/// </summary>
public interface IEdgePathProvider
{
    EdgePath GetPath(EdgeGeometry geometry);
}

/// <summary>Fournisseur défini par un délégué.</summary>
public sealed class DelegateEdgePathProvider(Func<EdgeGeometry, EdgePath> getPath) : IEdgePathProvider
{
    public EdgePath GetPath(EdgeGeometry geometry) => getPath(geometry);
}

public static class EdgePathProviders
{
    private static readonly IEdgePathProvider StraightProvider =
        new DelegateEdgePathProvider(g => EdgePaths.Straight(g.Source, g.Target));

    private static readonly IEdgePathProvider StepProvider =
        new DelegateEdgePathProvider(g => EdgePaths.Step(g.Source, g.SourcePosition, g.Target, g.TargetPosition));

    private static readonly IEdgePathProvider SmoothStepProvider =
        new DelegateEdgePathProvider(g => EdgePaths.SmoothStep(g.Source, g.SourcePosition, g.Target, g.TargetPosition));

    private static readonly IEdgePathProvider BezierProvider =
        new DelegateEdgePathProvider(g => EdgePaths.Bezier(g.Source, g.SourcePosition, g.Target, g.TargetPosition));

    /// <summary>Fournisseur d'un type intégré (<c>straight</c>, <c>step</c>, <c>smoothstep</c>) ; tout autre nom donne le bézier par défaut.</summary>
    public static IEdgePathProvider ForType(string type) => type switch
    {
        "straight" => StraightProvider,
        "step" => StepProvider,
        "smoothstep" => SmoothStepProvider,
        _ => BezierProvider
    };

    /// <summary>Vrai pour les types dont le tracé est fourni par la bibliothèque.</summary>
    public static bool IsBuiltIn(string type) => type is "default" or "straight" or "step" or "smoothstep";
}
