using System.Globalization;

namespace Wpflow.Core;

/// <summary>
///     Une propriété mappée : comment la lire dans le ViewModel (<paramref name="Read" />), la retrouver dans le record
///     (<paramref name="Value" />) et la recopier d'un record à un autre (<paramref name="Copy" />).
///     <paramref name="Always" /> : écrite même à sa valeur par défaut quand la vue ajoute un élément.
/// </summary>
internal sealed record ModelBinding<T>(string Path, Func<T, object?> Value, Func<T, T, T> Copy, Func<T, object?, T> Read, bool Always);

/// <summary>Les tables de correspondance : une seule description par propriété, pour la lecture, l'écriture et l'historique.</summary>
internal static class ModelBindings
{
    private static bool Flag(object? raw, bool current) => raw is bool b ? b : current;
    private static T? Struct<T>(object? raw) where T : struct => raw is T v ? v : null;

    private static double? Number(object? raw)
    {
        if (raw is not IConvertible c) return null;
        try
        {
            var d = c.ToDouble(CultureInfo.InvariantCulture);
            return double.IsNaN(d) ? null : d;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static int? Integer(object? raw) => Number(raw) is { } d ? (int)d : null;

    public static List<ModelBinding<Node>> For(NodeMappingBase m)
    {
        var list = new List<ModelBinding<Node>>();

        void Add(string? path, Func<Node, object?> value, Func<Node, Node, Node> copy, Func<Node, object?, Node> read, bool always = false)
        {
            if (path != null) list.Add(new ModelBinding<Node>(path, value, copy, read, always));
        }

        Add(m.XPath, n => n.Position.X, (t, f) => t with { Position = new XYPosition(f.Position.X, t.Position.Y) },
            (n, raw) => n with { Position = new XYPosition(Number(raw) ?? n.Position.X, n.Position.Y) }, true);
        Add(m.YPath, n => n.Position.Y, (t, f) => t with { Position = new XYPosition(t.Position.X, f.Position.Y) },
            (n, raw) => n with { Position = new XYPosition(n.Position.X, Number(raw) ?? n.Position.Y) }, true);
        Add(m.TypePath, n => n.Type, (t, f) => t with { Type = f.Type }, (n, raw) => n with { Type = raw?.ToString() ?? "default" }, true);
        Add(m.ParentIdPath, n => n.ParentId, (t, f) => t with { ParentId = f.ParentId }, (n, raw) => n with { ParentId = raw?.ToString() }, true);
        Add(m.WidthPath, n => n.FixedWidth, (t, f) => t with { FixedWidth = f.FixedWidth }, (n, raw) => n with { FixedWidth = Number(raw) }, true);
        Add(m.HeightPath, n => n.FixedHeight, (t, f) => t with { FixedHeight = f.FixedHeight }, (n, raw) => n with { FixedHeight = Number(raw) }, true);
        Add(m.SelectedPath, n => n.Selected, (t, f) => t with { Selected = f.Selected }, (n, raw) => n with { Selected = Flag(raw, n.Selected) }, true);
        Add(m.DraggablePath, n => n.Draggable, (t, f) => t with { Draggable = f.Draggable }, (n, raw) => n with { Draggable = Flag(raw, n.Draggable) });
        Add(m.SelectablePath, n => n.Selectable, (t, f) => t with { Selectable = f.Selectable }, (n, raw) => n with { Selectable = Flag(raw, n.Selectable) });
        Add(m.HiddenPath, n => n.Hidden, (t, f) => t with { Hidden = f.Hidden }, (n, raw) => n with { Hidden = Flag(raw, n.Hidden) });
        Add(m.ConnectablePath, n => n.Connectable, (t, f) => t with { Connectable = f.Connectable }, (n, raw) => n with { Connectable = Flag(raw, n.Connectable) });
        Add(m.DeletablePath, n => n.Deletable, (t, f) => t with { Deletable = f.Deletable }, (n, raw) => n with { Deletable = Flag(raw, n.Deletable) });
        Add(m.ExtentParentPath, n => n.ExtentParent, (t, f) => t with { ExtentParent = f.ExtentParent }, (n, raw) => n with { ExtentParent = Flag(raw, n.ExtentParent) });
        Add(m.ExtentPath, n => n.Extent, (t, f) => t with { Extent = f.Extent }, (n, raw) => n with { Extent = Struct<Rect>(raw) });
        Add(m.ExpandParentPath, n => n.ExpandParent, (t, f) => t with { ExpandParent = f.ExpandParent }, (n, raw) => n with { ExpandParent = Flag(raw, n.ExpandParent) });
        Add(m.SourcePositionPath, n => n.SourcePosition, (t, f) => t with { SourcePosition = f.SourcePosition }, (n, raw) => n with { SourcePosition = Struct<Position>(raw) });
        Add(m.TargetPositionPath, n => n.TargetPosition, (t, f) => t with { TargetPosition = f.TargetPosition }, (n, raw) => n with { TargetPosition = Struct<Position>(raw) });
        Add(m.DragHandlePath, n => n.DragHandle, (t, f) => t with { DragHandle = f.DragHandle }, (n, raw) => n with { DragHandle = raw?.ToString() });
        Add(m.ZIndexPath, n => n.ZIndex, (t, f) => t with { ZIndex = f.ZIndex }, (n, raw) => n with { ZIndex = Integer(raw) });
        Add(m.AriaLabelPath, n => n.AriaLabel, (t, f) => t with { AriaLabel = f.AriaLabel }, (n, raw) => n with { AriaLabel = raw?.ToString() });
        Add(m.OriginPath, n => n.Origin, (t, f) => t with { Origin = f.Origin }, (n, raw) => n with { Origin = Struct<XYPosition>(raw) });
        Add(m.RotationPath, n => n.Rotation, (t, f) => t with { Rotation = f.Rotation }, (n, raw) => n with { Rotation = Number(raw) ?? 0 });
        Add(m.InitialWidthPath, n => n.InitialWidth, (t, f) => t with { InitialWidth = f.InitialWidth }, (n, raw) => n with { InitialWidth = Number(raw) });
        Add(m.InitialHeightPath, n => n.InitialHeight, (t, f) => t with { InitialHeight = f.InitialHeight }, (n, raw) => n with { InitialHeight = Number(raw) });
        Add(m.HandlesPath, n => n.Handles, (t, f) => t with { Handles = f.Handles }, (n, raw) => n with { Handles = raw as IReadOnlyList<HandleInfo> });
        return list;
    }

    public static List<ModelBinding<Edge>> For(EdgeMappingBase m)
    {
        var list = new List<ModelBinding<Edge>>();

        void Add(string? path, Func<Edge, object?> value, Func<Edge, Edge, Edge> copy, Func<Edge, object?, Edge> read, bool always = false)
        {
            if (path != null) list.Add(new ModelBinding<Edge>(path, value, copy, read, always));
        }

        Add(m.SourcePath, e => e.Source, (t, f) => t with { Source = f.Source }, (e, raw) => e with { Source = raw?.ToString() ?? "" }, true);
        Add(m.TargetPath, e => e.Target, (t, f) => t with { Target = f.Target }, (e, raw) => e with { Target = raw?.ToString() ?? "" }, true);
        Add(m.SourceHandlePath, e => e.SourceHandle, (t, f) => t with { SourceHandle = f.SourceHandle }, (e, raw) => e with { SourceHandle = raw?.ToString() }, true);
        Add(m.TargetHandlePath, e => e.TargetHandle, (t, f) => t with { TargetHandle = f.TargetHandle }, (e, raw) => e with { TargetHandle = raw?.ToString() }, true);
        Add(m.TypePath, e => e.Type, (t, f) => t with { Type = f.Type }, (e, raw) => e with { Type = raw?.ToString() ?? "default" }, true);
        Add(m.LabelPath, e => e.Label, (t, f) => t with { Label = f.Label }, (e, raw) => e with { Label = raw?.ToString() }, true);
        Add(m.AnimatedPath, e => e.Animated, (t, f) => t with { Animated = f.Animated }, (e, raw) => e with { Animated = Flag(raw, e.Animated) }, true);
        Add(m.SelectedPath, e => e.Selected, (t, f) => t with { Selected = f.Selected }, (e, raw) => e with { Selected = Flag(raw, e.Selected) }, true);
        Add(m.MarkerStartPath, e => e.MarkerStart, (t, f) => t with { MarkerStart = f.MarkerStart }, (e, raw) => e with { MarkerStart = Struct<EdgeMarker>(raw) ?? e.MarkerStart }, true);
        Add(m.MarkerEndPath, e => e.MarkerEnd, (t, f) => t with { MarkerEnd = f.MarkerEnd }, (e, raw) => e with { MarkerEnd = Struct<EdgeMarker>(raw) ?? e.MarkerEnd }, true);
        Add(m.HiddenPath, e => e.Hidden, (t, f) => t with { Hidden = f.Hidden }, (e, raw) => e with { Hidden = Flag(raw, e.Hidden) });
        Add(m.SelectablePath, e => e.Selectable, (t, f) => t with { Selectable = f.Selectable }, (e, raw) => e with { Selectable = Flag(raw, e.Selectable) });
        Add(m.DeletablePath, e => e.Deletable, (t, f) => t with { Deletable = f.Deletable }, (e, raw) => e with { Deletable = Flag(raw, e.Deletable) });
        Add(m.ReconnectablePath, e => e.Reconnectable, (t, f) => t with { Reconnectable = f.Reconnectable }, (e, raw) => e with { Reconnectable = Flag(raw, e.Reconnectable) });
        Add(m.MarkerStartKeyPath, e => e.MarkerStartKey, (t, f) => t with { MarkerStartKey = f.MarkerStartKey }, (e, raw) => e with { MarkerStartKey = raw?.ToString() });
        Add(m.MarkerEndKeyPath, e => e.MarkerEndKey, (t, f) => t with { MarkerEndKey = f.MarkerEndKey }, (e, raw) => e with { MarkerEndKey = raw?.ToString() });
        Add(m.InteractionWidthPath, e => e.InteractionWidth, (t, f) => t with { InteractionWidth = f.InteractionWidth }, (e, raw) => e with { InteractionWidth = Number(raw) });
        Add(m.ZIndexPath, e => e.ZIndex, (t, f) => t with { ZIndex = f.ZIndex }, (e, raw) => e with { ZIndex = Integer(raw) });
        Add(m.AriaLabelPath, e => e.AriaLabel, (t, f) => t with { AriaLabel = f.AriaLabel }, (e, raw) => e with { AriaLabel = raw?.ToString() });
        Add(m.StylePath, e => e.Style, (t, f) => t with { Style = f.Style }, (e, raw) => e with { Style = raw as EdgeStyleOptions });
        Add(m.LabelStylePath, e => e.LabelStyle, (t, f) => t with { LabelStyle = f.LabelStyle }, (e, raw) => e with { LabelStyle = raw as EdgeLabelOptions });
        Add(m.PathOptionsPath, e => e.PathOptions, (t, f) => t with { PathOptions = f.PathOptions }, (e, raw) => e with { PathOptions = raw as EdgePathOptions });
        Add(m.MarkerStartOptionsPath, e => e.MarkerStartOptions, (t, f) => t with { MarkerStartOptions = f.MarkerStartOptions }, (e, raw) => e with { MarkerStartOptions = raw as EdgeMarkerOptions });
        Add(m.MarkerEndOptionsPath, e => e.MarkerEndOptions, (t, f) => t with { MarkerEndOptions = f.MarkerEndOptions }, (e, raw) => e with { MarkerEndOptions = raw as EdgeMarkerOptions });
        return list;
    }
}
