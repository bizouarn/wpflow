namespace Wpflow.Core;

/// <summary>Handle (point de connexion) d'un node, positionné relativement au coin du node.</summary>
public sealed record HandleInfo(
    string? Id,
    HandleType Type,
    Position Position,
    double X,
    double Y,
    double Width,
    double Height,
    bool Connectable = true,
    bool ConnectableStart = true,
    bool ConnectableEnd = true,
    Func<Connection, bool>? IsValidConnection = null);

public enum HandleType
{
    Source,
    Target
}

/// <summary>Node immuable. Toute modification passe par des <see cref="NodeChange" />.</summary>
public sealed record Node(
    string Id,
    XYPosition Position,
    string Type = "default",
    object? Data = null,
    double? Width = null,
    double? Height = null,
    bool Selected = false,
    bool Draggable = true,
    bool Selectable = true,
    double? FixedWidth = null,
    double? FixedHeight = null,
    string? ParentId = null,
    bool ExtentParent = false,
    Position? SourcePosition = null,
    Position? TargetPosition = null,
    bool Connectable = true,
    bool Deletable = true,
    bool Hidden = false,
    string? DragHandle = null,
    Rect? Extent = null,
    bool ExpandParent = false,
    int? ZIndex = null,
    string? AriaLabel = null,
    XYPosition? Origin = null,
    double Rotation = 0,
    double? InitialWidth = null,
    double? InitialHeight = null,
    IReadOnlyList<HandleInfo>? Handles = null,
    bool Dragging = false,
    bool Resizing = false)
{
    /// <summary>Rectangle du node en coordonnées flow ; <c>null</c> tant que le node n'a pas été mesuré.</summary>
    public Rect? Rect => Width is { } w && Height is { } h ? new Rect(Position.X, Position.Y, w, h) : null;
}

/// <summary>
///     Edge immuable entre deux nodes. <c>MarkerEndKey</c> / <c>MarkerStartKey</c> désignent un marqueur personnalisé
///     (ressource <c>wpflow-marker-{clé}</c>) et l'emportent sur <c>MarkerEnd</c> / <c>MarkerStart</c>.
/// </summary>
public sealed record Edge(
    string Id,
    string Source,
    string Target,
    string? SourceHandle = null,
    string? TargetHandle = null,
    string Type = "default",
    string? Label = null,
    bool Animated = false,
    bool Selected = false,
    EdgeMarker MarkerEnd = EdgeMarker.None,
    EdgeMarker MarkerStart = EdgeMarker.None,
    string? MarkerEndKey = null,
    string? MarkerStartKey = null,
    object? Data = null,
    bool Hidden = false,
    bool Selectable = true,
    bool Deletable = true,
    bool Reconnectable = true,
    double? InteractionWidth = null,
    int? ZIndex = null,
    string? AriaLabel = null,
    EdgeStyleOptions? Style = null,
    EdgeLabelOptions? LabelStyle = null,
    EdgePathOptions? PathOptions = null,
    EdgeMarkerOptions? MarkerEndOptions = null,
    EdgeMarkerOptions? MarkerStartOptions = null);

/// <summary>Marqueur de fin d'edge (équivalent de <c>MarkerType</c>).</summary>
public enum EdgeMarker
{
    None,
    Arrow,
    ArrowClosed
}

/// <summary>Connexion en cours de création ou terminée.</summary>
public sealed record Connection(string Source, string Target, string? SourceHandle, string? TargetHandle);

/// <summary>Trait d'un edge (équivalent de <c>edge.style</c>) ; les couleurs sont des chaînes CSS/WPF (<c>#rrggbb</c>, nom de couleur).</summary>
public sealed record EdgeStyleOptions(string? Stroke = null, double? StrokeWidth = null, IReadOnlyList<double>? DashArray = null);

/// <summary>Apparence du label d'un edge (<c>labelStyle</c>, <c>labelShowBg</c>, <c>labelBgStyle</c>…).</summary>
public sealed record EdgeLabelOptions(string? Foreground = null, string? Background = null, bool ShowBackground = true,
    double? FontSize = null, double? Padding = null, double? CornerRadius = null);

/// <summary>Options de tracé par edge (<c>pathOptions</c>) : <c>curvature</c> (bézier), <c>borderRadius</c>, <c>offset</c>, <c>stepPosition</c> (step / smoothstep).</summary>
public sealed record EdgePathOptions(double? Curvature = null, double? BorderRadius = null, double? Offset = null,
    double? StepPosition = null);

/// <summary>Options d'un marqueur d'edge : couleur, taille (12,5 = flèche par défaut), épaisseur du trait et <c>Orient</c> (angle fixe en degrés ; <c>null</c> = orienté selon le côté du handle, comme <c>orient="auto"</c>).</summary>
public sealed record EdgeMarkerOptions(string? Color = null, double? Width = null, double? Height = null,
    double? StrokeWidth = null, double? Orient = null);
