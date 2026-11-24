using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Wpflow.Core;

namespace Wpflow.Wpf;

/// <summary>Élément de la couche d'edges qui appartient à un edge (tracé ou label) : sert à retrouver l'edge sous la souris.</summary>
internal interface IEdgePart
{
    string EdgeId { get; }
}

/// <summary>
///     Tracé d'un edge : chemin, zone de clic élargie, flèches et animation, le tout dessiné dans <c>OnRender</c>.
///     Un seul élément visuel par edge (contre un <c>Canvas</c> et cinq enfants auparavant), ce qui compte quand
///     le graphe en contient des milliers.
/// </summary>
internal sealed class EdgeVisual : FrameworkElement, IEdgePart
{
    private static readonly Pen HitPen = Frozen(new Pen(Brushes.Transparent, 10));
    private static readonly Geometry OpenArrow = Frozen(Geometry.Parse("M-7,-5 L0,0 L-7,5"));
    private static readonly Geometry ClosedArrow = Frozen(Geometry.Parse("M-7,-5 L0,0 L-7,5 Z"));

    private readonly record struct Marker(EdgeMarker Kind, Matrix Transform);

    private readonly record struct UpdateKey(Edge Edge, string Path, XYPosition Start, Position StartSide, XYPosition End,
        Position EndSide, EdgeStyle Style);

    private UpdateKey? _lastUpdate;
    private Geometry? _geometry;
    private Pen? _linePen;
    private Brush _stroke = Brushes.Gray;
    private double _thickness = 1;
    private Marker _markerStart, _markerEnd;
    private DashStyle? _dash;

    public EdgeVisual(string edgeId)
    {
        EdgeId = edgeId;
    }

    public string EdgeId { get; }

    private static T Frozen<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }

    /// <summary>Met à jour le tracé ; ne fait rien (ni parsing, ni redessin) si rien n'a changé.</summary>
    public void Update(Edge edge, EdgePath path, XYPosition start, Position startSide, XYPosition end, Position endSide,
        EdgeStyle style)
    {
        var key = new UpdateKey(edge, path.Path, start, startSide, end, endSide, style);
        if (_lastUpdate == key) return;
        var geometryChanged = _lastUpdate?.Path != path.Path;
        _lastUpdate = key;

        if (geometryChanged)
        {
            _geometry = Geometry.Parse(path.Path);
            _geometry.Freeze();
        }

        _stroke = edge.Selected ? style.SelectedStroke : style.Stroke;
        _thickness = style.Thickness;
        _linePen = new Pen(_stroke, _thickness) { LineJoin = PenLineJoin.Round };
        SetAnimated(edge.Animated);
        _markerEnd = new Marker(edge.MarkerEnd, ArrowTransform(end, endSide));
        _markerStart = new Marker(edge.MarkerStart, ArrowTransform(start, startSide));
        InvalidateVisual();
    }

    /// <summary>Une flèche pointe vers le node : sens opposé au côté où le handle est attaché.</summary>
    private static Matrix ArrowTransform(XYPosition at, Position side)
    {
        var m = Matrix.Identity;
        m.Rotate(side switch { Position.Left => 0, Position.Top => 90, Position.Right => 180, _ => 270 });
        m.Translate(at.X, at.Y);
        return m;
    }

    private void SetAnimated(bool animated)
    {
        if (!animated)
        {
            _dash?.BeginAnimation(DashStyle.OffsetProperty, null);
            _dash = null;
            return;
        }

        // Pointillés qui défilent de la source vers la cible (comme les edges animés de React Flow).
        if (_dash == null) // l'animation continue d'un rafraîchissement à l'autre au lieu de repartir de zéro
        {
            _dash = new DashStyle([5, 5], 0);
            _dash.BeginAnimation(DashStyle.OffsetProperty,
                new DoubleAnimation(10, 0, TimeSpan.FromSeconds(0.5)) { RepeatBehavior = RepeatBehavior.Forever });
        }

        _linePen!.DashStyle = _dash;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_geometry == null || _linePen == null) return;
        dc.DrawGeometry(null, HitPen, _geometry); // large et transparent : sert au clic
        dc.DrawGeometry(null, _linePen, _geometry);
        DrawMarker(dc, _markerEnd);
        DrawMarker(dc, _markerStart);
    }

    private void DrawMarker(DrawingContext dc, Marker marker)
    {
        if (marker.Kind == EdgeMarker.None) return;
        var closed = marker.Kind == EdgeMarker.ArrowClosed;
        dc.PushTransform(new MatrixTransform(marker.Transform));
        dc.DrawGeometry(closed ? _stroke : null, new Pen(_stroke, _thickness) { LineJoin = PenLineJoin.Round },
            closed ? ClosedArrow : OpenArrow);
        dc.Pop();
    }
}

/// <summary>Label d'un edge : texte simple ou contenu issu de <see cref="FlowView.EdgeLabelTemplate" />, centré sur le tracé.</summary>
internal sealed class EdgeLabel : Border, IEdgePart
{
    private readonly TextBlock _text = new() { FontSize = 10 };
    private readonly ContentPresenter _custom = new();
    private XYPosition _position;

    public EdgeLabel(string edgeId)
    {
        EdgeId = edgeId;
        CornerRadius = new CornerRadius(2);
        Panel.SetZIndex(this, 1); // au-dessus des edges
        SizeChanged += (_, _) => Place();
    }

    public string EdgeId { get; }

    public void Update(Edge edge, EdgePath path, EdgeStyle style)
    {
        if (style.LabelTemplate is { } template)
        {
            // Contenu libre (équivalent d'EdgeLabelRenderer) : le DataContext est l'edge.
            _custom.ContentTemplate = template;
            _custom.Content = edge;
            Child = _custom;
            Background = Brushes.Transparent;
            Padding = new Thickness(0);
        }
        else
        {
            _text.Text = edge.Label;
            _text.Foreground = style.LabelForeground;
            Child = _text;
            Background = style.LabelBackground;
            Padding = new Thickness(2);
        }

        _position = new XYPosition(path.LabelX, path.LabelY);
        Place();
    }

    private void Place()
    {
        Canvas.SetLeft(this, _position.X - ActualWidth / 2);
        Canvas.SetTop(this, _position.Y - ActualHeight / 2);
    }
}

/// <summary>Couche regroupant les <see cref="EdgeVisual" /> (un par <see cref="Edge" />) et leurs <see cref="EdgeLabel" />.</summary>
public sealed class EdgeLayer : Canvas
{
    private readonly Dictionary<string, EdgeVisual> _visuals = new();
    private readonly Dictionary<string, EdgeLabel> _labels = new();

    /// <summary>
    ///     Met à jour les edges. Avec <paramref name="touchedNodes" />, seuls les edges reliés à ces nodes (ou sans visuel
    ///     existant) sont recalculés ; sans, tous les edges le sont et les visuels obsolètes sont supprimés.
    ///     Avec <paramref name="show" /> et <paramref name="keep" /> (virtualisation), un edge n'existe que s'il touche la
    ///     zone <paramref name="keep" /> et n'est affiché que s'il touche <paramref name="show" />.
    /// </summary>
    internal void Refresh(FlowView flow, IReadOnlySet<string>? touchedNodes = null, Core.Rect? show = null,
        Core.Rect? keep = null)
    {
        // Brosses lues une fois pour tous les edges : une recherche de ressource par edge coûterait trop cher.
        var style = new EdgeStyle(
            flow.EdgeStroke ?? flow.ThemeBrush("Flow.Edge"), flow.ThemeBrush("Flow.EdgeSelected"), flow.EdgeStrokeThickness,
            flow.EdgeLabelTemplate, flow.ThemeBrush("Flow.NodeBackground"), flow.ThemeBrush("Flow.NodeText"));
        var seen = touchedNodes == null ? new HashSet<string>() : null;

        foreach (var edge in flow.Edges)
        {
            seen?.Add(edge.Id);

            var visibility = Visibility.Visible;
            if (show is { } shown && keep is { } kept)
            {
                if (flow.ViewRectOf(edge.Source) is not { } a || flow.ViewRectOf(edge.Target) is not { } b)
                {
                    Remove(edge.Id);
                    continue;
                }

                if (!GraphUtils.IsEdgeVisible(a, b, shown))
                {
                    if (!GraphUtils.IsEdgeVisible(a, b, kept))
                    {
                        Remove(edge.Id); // trop loin : pas d'élément visuel du tout
                        continue;
                    }

                    visibility = Visibility.Collapsed;
                }
            }

            var exists = _visuals.TryGetValue(edge.Id, out var visual);
            if (exists && touchedNodes != null && !touchedNodes.Contains(edge.Source) && !touchedNodes.Contains(edge.Target))
            {
                SetVisibility(edge.Id, visual!, visibility);
                continue;
            }

            if (flow.GetEdgeEnds(edge) is not var (s, sp, t, tp))
            {
                Remove(edge.Id);
                continue;
            }

            if (!exists)
            {
                visual = new EdgeVisual(edge.Id);
                _visuals[edge.Id] = visual;
                Children.Add(visual);
            }

            var path = edge.Type switch
            {
                "straight" => EdgePaths.Straight(s, t),
                "step" => EdgePaths.Step(s, sp, t, tp),
                "smoothstep" => EdgePaths.SmoothStep(s, sp, t, tp),
                _ => EdgePaths.Bezier(s, sp, t, tp)
            };
            visual!.Update(edge, path, s, sp, t, tp, style);
            UpdateLabel(edge, path, style);
            SetVisibility(edge.Id, visual, visibility);
        }

        if (seen == null) return;
        foreach (var id in _visuals.Keys.Where(k => !seen.Contains(k)).ToList()) Remove(id);
    }

    private void SetVisibility(string id, EdgeVisual visual, Visibility visibility)
    {
        if (visual.Visibility != visibility) visual.Visibility = visibility;
        if (_labels.TryGetValue(id, out var label) && label.Visibility != visibility) label.Visibility = visibility;
    }


    // Les labels sont créés à la demande : la plupart des edges n'en ont pas.
    private void UpdateLabel(Edge edge, EdgePath path, EdgeStyle style)
    {
        if (style.LabelTemplate == null && string.IsNullOrEmpty(edge.Label))
        {
            if (_labels.Remove(edge.Id, out var stale)) Children.Remove(stale);
            return;
        }

        if (!_labels.TryGetValue(edge.Id, out var label))
        {
            label = new EdgeLabel(edge.Id);
            _labels[edge.Id] = label;
            Children.Add(label);
        }

        label.Update(edge, path, style);
    }

    private void Remove(string id)
    {
        if (_visuals.Remove(id, out var visual)) Children.Remove(visual);
        if (_labels.Remove(id, out var label)) Children.Remove(label);
    }
}

/// <summary>Brosses et réglages d'affichage des edges, résolus une seule fois par rafraîchissement (et non par edge).</summary>
internal sealed record EdgeStyle(Brush Stroke, Brush SelectedStroke, double Thickness, DataTemplate? LabelTemplate,
    Brush LabelBackground, Brush LabelForeground);
