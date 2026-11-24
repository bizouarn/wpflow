namespace Wpflow.Core;

/// <summary>Handle (point de connexion) d'un node, positionné relativement au coin du node.</summary>
public sealed record HandleInfo(
    string? Id,
    HandleType Type,
    Position Position,
    double X,
    double Y,
    double Width,
    double Height);

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
    IReadOnlyList<HandleInfo>? Handles = null,
    double? FixedWidth = null,
    double? FixedHeight = null,
    string? ParentId = null,
    bool ExtentParent = false)
{
    /// <summary>Rectangle du node en coordonnées flow ; <c>null</c> tant que le node n'a pas été mesuré.</summary>
    public Rect? Rect => Width is { } w && Height is { } h ? new Rect(Position.X, Position.Y, w, h) : null;
}

/// <summary>Edge immuable entre deux nodes.</summary>
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
    EdgeMarker MarkerStart = EdgeMarker.None);

/// <summary>Marqueur de fin d'edge (équivalent de <c>MarkerType</c>).</summary>
public enum EdgeMarker
{
    None,
    Arrow,
    ArrowClosed
}

/// <summary>Connexion en cours de création ou terminée.</summary>
public sealed record Connection(string Source, string Target, string? SourceHandle, string? TargetHandle);
