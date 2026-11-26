# Types

Espace de noms `Wpflow.Core` (`net8.0`, `net472`, `netstandard2.0`, sans dépendance UI).

## Node

```csharp
public sealed record Node(
    string Id,
    XYPosition Position,
    string Type = "default",
    object? Data = null,
    double? Width = null,           // mesuré (ne pas dimensionner avec)
    double? Height = null,          // mesuré
    bool Selected = false,
    bool Draggable = true,
    bool Selectable = true,
    double? FixedWidth = null,      // taille imposée (NodeResizer, JSON "width")
    double? FixedHeight = null,
    string? ParentId = null,        // position relative à ce parent
    bool ExtentParent = false,      // l'enfant ne sort pas du parent
    Position? SourcePosition = null, // côté du handle source du node par défaut (défaut : Bottom)
    Position? TargetPosition = null, // côté du handle cible du node par défaut (défaut : Top)
    bool Connectable = true,
    bool Deletable = true,
    bool Hidden = false,             // non dessiné (ni ses edges) ; ignoré par bornes, minimap, sélection
    string? DragHandle = null,       // clé de la zone qui déplace le node (NodeHost.DragHandle dans le template)
    Rect? Extent = null,             // bornes de déplacement propres au node (relatives au parent s'il y en a un)
    bool ExpandParent = false,       // agrandit le parent quand on le déplace au-delà (à gauche / en haut, le parent recule et ses enfants sont recalés)
    int? ZIndex = null,
    string? AriaLabel = null,        // nom UI Automation
    XYPosition? Origin = null,       // point d'ancrage de Position (défaut : FlowView.NodeOrigin) ; celui d'un parent décale aussi ses enfants
    double Rotation = 0,             // degrés, autour du centre (NodeRotator) ; handles et edges restent non tournés
    double? InitialWidth = null,     // taille avant la première mesure : les edges se dessinent tout de suite
    double? InitialHeight = null,
    IReadOnlyList<HandleInfo>? Handles = null,   // handles annoncés (relatifs au coin) : utilisés tant que le node n'est pas mesuré
    bool Dragging = false,           // vrai pendant un déplacement (NodeInteractionChange)
    bool Resizing = false);          // vrai pendant un redimensionnement

public Rect? Rect { get; }          // rectangle (position relative) ; null tant que non mesuré
```

## Edge

```csharp
public sealed record Edge(
    string Id, string Source, string Target,
    string? SourceHandle = null, string? TargetHandle = null,
    string Type = "default",        // default | straight | step | smoothstep | simplebezier, ou un type personnalisé (voir Personnaliser)
    string? Label = null,
    bool Animated = false,
    bool Selected = false,
    EdgeMarker MarkerEnd = EdgeMarker.None,
    EdgeMarker MarkerStart = EdgeMarker.None,
    string? MarkerEndKey = null,       // marqueur personnalisé : ressource wpflow-marker-{clé}
    string? MarkerStartKey = null,
    object? Data = null,
    bool Hidden = false, bool Selectable = true, bool Deletable = true, bool Reconnectable = true,
    double? InteractionWidth = null,   // largeur de la zone cliquable (sinon FlowView.EdgeHitWidth)
    int? ZIndex = null, string? AriaLabel = null,
    EdgeStyleOptions? Style = null,    // Stroke ("#rrggbb"), StrokeWidth, DashArray
    EdgeLabelOptions? LabelStyle = null, // Foreground, Background, ShowBackground, FontSize, Padding, CornerRadius
    EdgePathOptions? PathOptions = null, // Curvature (bézier), BorderRadius, Offset, StepPosition
    EdgeMarkerOptions? MarkerEndOptions = null,   // Color, Width, Height (12,5 = taille par défaut), StrokeWidth, Orient (angle fixe en degrés)
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
                                double X, double Y, double Width, double Height);   // relatif au coin du node
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

public readonly record struct Viewport(double X, double Y, double Zoom);   // écran = flow × Zoom + (X, Y)
// Viewport.Identity == (0, 0, 1)
```

> `Wpflow.Core.Rect` n'est pas `System.Windows.Rect`. Dans un fichier qui importe les deux espaces de noms, qualifiez ou créez un alias (`using Rect = Wpflow.Core.Rect;`).

## ConnectionMode

```csharp
public enum ConnectionMode { Strict, Loose }
```

## EdgePath

```csharp
public sealed record EdgePath(string Path, double LabelX, double LabelY, double OffsetX, double OffsetY);
// Path : syntaxe SVG, utilisable avec Geometry.Parse
```

## FlowGraph

```csharp
public sealed record FlowGraph(IReadOnlyList<Node> Nodes, IReadOnlyList<Edge> Edges, Viewport Viewport);
```

Résultat de `GraphSerializer.FromJson`.

## ResizeDirection

```csharp
public enum ResizeDirection { Top, Right, Bottom, Left, TopLeft, TopRight, BottomLeft, BottomRight }
```
