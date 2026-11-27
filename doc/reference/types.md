# Types

Namespace `Wpflow.Core` (`net8.0`, `net472`, `netstandard2.0`, no UI dependency).

## Node

```csharp
public sealed record Node(
    string Id,
    XYPosition Position,
    string Type = "default",
    object? Data = null,
    double? Width = null,           // measured (do not size with it)
    double? Height = null,          // measured
    bool Selected = false,
    bool Draggable = true,
    bool Selectable = true,
    double? FixedWidth = null,      // imposed size (NodeResizer, JSON "width")
    double? FixedHeight = null,
    string? ParentId = null,        // position relative to this parent
    bool ExtentParent = false,      // the child does not leave the parent
    Position? SourcePosition = null, // side of the source handle of the default node (default: Bottom)
    Position? TargetPosition = null, // side of the target handle of the default node (default: Top)
    bool Connectable = true,
    bool Deletable = true,
    bool Hidden = false,             // not drawn (nor its edges); ignored by bounds, minimap, selection
    string? DragHandle = null,       // key of the area that moves the node (NodeHost.DragHandle in the template)
    Rect? Extent = null,             // movement bounds specific to the node (relative to the parent, if any)
    bool ExpandParent = false,       // enlarges the parent when moved beyond it (to the left / top, the parent moves back and its children are readjusted)
    int? ZIndex = null,
    string? AriaLabel = null,        // UI Automation name
    XYPosition? Origin = null,       // anchor point of Position (default: FlowView.NodeOrigin); a parent's origin also shifts its children
    double Rotation = 0,             // degrees, around the centre (NodeRotator); handles and edges stay unrotated
    double? InitialWidth = null,     // size before the first measurement: edges are drawn straight away
    double? InitialHeight = null,
    IReadOnlyList<HandleInfo>? Handles = null,   // announced handles (relative to the corner): used while the node is not measured
    bool Dragging = false,           // true during a move (NodeInteractionChange)
    bool Resizing = false);          // true during a resize

public Rect? Rect { get; }          // rectangle (relative position); null until measured
```

## Edge

```csharp
public sealed record Edge(
    string Id, string Source, string Target,
    string? SourceHandle = null, string? TargetHandle = null,
    string Type = "default",        // default | straight | step | smoothstep | simplebezier, or a custom type (see Customization)
    string? Label = null,
    bool Animated = false,
    bool Selected = false,
    EdgeMarker MarkerEnd = EdgeMarker.None,
    EdgeMarker MarkerStart = EdgeMarker.None,
    string? MarkerEndKey = null,       // custom marker: resource wpflow-marker-{key}
    string? MarkerStartKey = null,
    object? Data = null,
    bool Hidden = false, bool Selectable = true, bool Deletable = true, bool Reconnectable = true,
    double? InteractionWidth = null,   // width of the clickable area (otherwise FlowView.EdgeHitWidth)
    int? ZIndex = null, string? AriaLabel = null,
    EdgeStyleOptions? Style = null,    // Stroke ("#rrggbb"), StrokeWidth, DashArray
    EdgeLabelOptions? LabelStyle = null, // Foreground, Background, ShowBackground, FontSize, Padding, CornerRadius
    EdgePathOptions? PathOptions = null, // Curvature (bézier), BorderRadius, Offset, StepPosition
    EdgeMarkerOptions? MarkerEndOptions = null,   // Color, Width, Height (12.5 = default size), StrokeWidth, Orient (fixed angle in degrees)
    EdgeMarkerOptions? MarkerStartOptions = null);

public enum EdgeMarker { None, Arrow, ArrowClosed }
```

## Connection

```csharp
public sealed record Connection(string Source, string Target, string? SourceHandle, string? TargetHandle);
```

## HandleInfo, HandleType, Position

```csharp
public sealed record HandleInfo(string? Id, HandleType Type, Position Position,
                                double X, double Y, double Width, double Height);   // relative to the corner of the node
public enum HandleType { Source, Target }
public enum Position { Left, Top, Right, Bottom }
```

## XYPosition, Rect, Viewport

```csharp
public readonly record struct XYPosition(double X, double Y);   // +, -, XYPosition.Zero

public readonly record struct Rect(double X, double Y, double Width, double Height)
{
    double Right, Bottom;  XYPosition Center;
    static Rect Union(Rect a, Rect b);
    bool Contains(XYPosition p);
    bool Overlaps(Rect other);
}

public readonly record struct Viewport(double X, double Y, double Zoom);   // screen = flow × Zoom + (X, Y)
// Viewport.Identity == (0, 0, 1)
```

> `Wpflow.Core.Rect` is not `System.Windows.Rect`. In a file that imports both namespaces, qualify it or create an alias (`using Rect = Wpflow.Core.Rect;`).

## ConnectionMode

```csharp
public enum ConnectionMode { Strict, Loose }
```

## EdgePath

```csharp
public sealed record EdgePath(string Path, double LabelX, double LabelY, double OffsetX, double OffsetY);
// Path: SVG syntax, usable with Geometry.Parse
```

## FlowGraph

```csharp
public sealed record FlowGraph(IReadOnlyList<Node> Nodes, IReadOnlyList<Edge> Edges, Viewport Viewport);
```

Result of `GraphSerializer.FromJson`.

## ResizeDirection

```csharp
public enum ResizeDirection { Top, Right, Bottom, Left, TopLeft, TopRight, BottomLeft, BottomRight }
```
