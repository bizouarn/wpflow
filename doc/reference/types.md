# Types

Espace de noms `Wpflow.Core` (`net8.0`, sans dépendance UI).

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
    IReadOnlyList<HandleInfo>? Handles = null,
    double? FixedWidth = null,      // taille imposée (NodeResizer, JSON "width")
    double? FixedHeight = null,
    string? ParentId = null,        // position relative à ce parent
    bool ExtentParent = false);     // l'enfant ne sort pas du parent

public Rect? Rect { get; }          // rectangle (position relative) ; null tant que non mesuré
```

## Edge

```csharp
public sealed record Edge(
    string Id, string Source, string Target,
    string? SourceHandle = null, string? TargetHandle = null,
    string Type = "default",        // default | straight | step | smoothstep
    string? Label = null,
    bool Animated = false,
    bool Selected = false,
    EdgeMarker MarkerEnd = EdgeMarker.None,
    EdgeMarker MarkerStart = EdgeMarker.None);

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
