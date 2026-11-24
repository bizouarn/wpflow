using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Wpflow.Core;
using Path = System.Windows.Shapes.Path;

namespace Wpflow.Wpf;

/// <summary>Rendu d'un edge : chemin, zone de clic élargie, marqueur et label.</summary>
internal sealed class EdgeVisual : Canvas
{
    private readonly Path _hit = new() { Stroke = Brushes.Transparent, StrokeThickness = 10, Fill = null };
    private readonly Path _line = new() { Fill = null, StrokeLineJoin = PenLineJoin.Round };
    private readonly Path _marker = new() { StrokeLineJoin = PenLineJoin.Round };
    private readonly Path _markerStart = new() { StrokeLineJoin = PenLineJoin.Round };
    private readonly Border _label;
    private readonly TextBlock _labelText = new() { FontSize = 10 };
    private readonly ContentPresenter _custom = new();
    private bool _animated;
    private XYPosition _labelPos;

    public EdgeVisual(string edgeId)
    {
        EdgeId = edgeId;
        _label = new Border
        {
            Background = Brushes.White,
            Padding = new Thickness(2, 2, 2, 2),
            CornerRadius = new CornerRadius(2),
            Child = _labelText,
            Visibility = Visibility.Collapsed
        };
        _label.SizeChanged += (_, _) => PositionLabel();
        Children.Add(_hit);
        Children.Add(_line);
        Children.Add(_marker);
        Children.Add(_markerStart);
        Children.Add(_label);
    }

    public string EdgeId { get; }

    public void Update(Edge edge, EdgePath path, XYPosition start, Position startSide, XYPosition end, Position endSide,
        Brush stroke, double thickness, DataTemplate? labelTemplate, Brush labelBackground, Brush labelForeground)
    {
        var geometry = Geometry.Parse(path.Path);
        geometry.Freeze();
        _hit.Data = geometry;
        _line.Data = geometry;
        _line.Stroke = stroke;
        _line.StrokeThickness = thickness;

        SetAnimated(edge.Animated);

        ApplyMarker(_marker, edge.MarkerEnd, end, endSide, stroke, thickness);
        ApplyMarker(_markerStart, edge.MarkerStart, start, startSide, stroke, thickness);

        if (labelTemplate != null)
        {
            // Contenu libre (équivalent d'EdgeLabelRenderer) : le DataContext est l'edge.
            _custom.ContentTemplate = labelTemplate;
            _custom.Content = edge;
            _label.Child = _custom;
            _label.Background = Brushes.Transparent;
            _label.Padding = new Thickness(0);
            _label.Visibility = Visibility.Visible;
            _labelPos = new XYPosition(path.LabelX, path.LabelY);
            PositionLabel();
        }
        else if (string.IsNullOrEmpty(edge.Label))
        {
            _label.Visibility = Visibility.Collapsed;
        }
        else
        {
            _labelText.Text = edge.Label;
            _labelText.Foreground = labelForeground;
            _label.Child = _labelText;
            _label.Background = labelBackground;
            _label.Padding = new Thickness(2);
            _label.Visibility = Visibility.Visible;
            _labelPos = new XYPosition(path.LabelX, path.LabelY);
            PositionLabel();
        }
    }

    /// <summary>Flèche à une extrémité : elle pointe vers le node, donc dans le sens opposé au côté du handle.</summary>
    private static void ApplyMarker(Path marker, EdgeMarker kind, XYPosition at, Position side, Brush stroke, double thickness)
    {
        if (kind == EdgeMarker.None)
        {
            marker.Visibility = Visibility.Collapsed;
            return;
        }

        marker.Visibility = Visibility.Visible;
        var closed = kind == EdgeMarker.ArrowClosed;
        marker.Data = Geometry.Parse(closed ? "M-7,-5 L0,0 L-7,5 Z" : "M-7,-5 L0,0 L-7,5");
        marker.Stroke = stroke;
        marker.StrokeThickness = thickness;
        marker.Fill = closed ? stroke : null;

        var angle = side switch
        {
            Position.Left => 0,
            Position.Top => 90,
            Position.Right => 180,
            _ => 270
        };
        var m = Matrix.Identity;
        m.Rotate(angle);
        m.Translate(at.X, at.Y);
        marker.RenderTransform = new MatrixTransform(m);
    }

    private void PositionLabel()
    {
        SetLeft(_label, _labelPos.X - _label.ActualWidth / 2);
        SetTop(_label, _labelPos.Y - _label.ActualHeight / 2);
    }

    private void SetAnimated(bool animated)
    {
        if (animated == _animated) return;
        _animated = animated;
        if (animated)
        {
            _line.StrokeDashArray = [5, 5];
            _line.BeginAnimation(Shape.StrokeDashOffsetProperty,
                new DoubleAnimation(10, 0, TimeSpan.FromSeconds(0.5)) { RepeatBehavior = RepeatBehavior.Forever });
        }
        else
        {
            _line.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
            _line.StrokeDashArray = null;
        }
    }
}

/// <summary>Couche regroupant les <see cref="EdgeVisual" /> ; une instance par <see cref="Edge" />.</summary>
public sealed class EdgeLayer : Canvas
{
    private readonly Dictionary<string, EdgeVisual> _visuals = new();

    /// <summary>
    ///     Met à jour les edges. Avec <paramref name="touchedNodes" />, seuls les edges reliés à ces nodes (ou sans visuel
    ///     existant) sont recalculés ; sans, tous les edges le sont et les visuels obsolètes sont supprimés.
    /// </summary>
    internal void Refresh(FlowView flow, IReadOnlySet<string>? touchedNodes = null)
    {
        var seen = touchedNodes == null ? new HashSet<string>() : null;
        foreach (var edge in flow.Edges)
        {
            seen?.Add(edge.Id);
            if (touchedNodes != null && _visuals.ContainsKey(edge.Id)
                                     && !touchedNodes.Contains(edge.Source) && !touchedNodes.Contains(edge.Target))
                continue;
            var ends = flow.GetEdgeEnds(edge);
            if (ends is null)
            {
                if (_visuals.Remove(edge.Id, out var stale)) Children.Remove(stale);
                continue;
            }

            if (!_visuals.TryGetValue(edge.Id, out var visual))
            {
                visual = new EdgeVisual(edge.Id);
                _visuals[edge.Id] = visual;
                Children.Add(visual);
            }

            var (s, sp, t, tp) = ends.Value;
            var path = edge.Type switch
            {
                "straight" => EdgePaths.Straight(s, t),
                "step" => EdgePaths.Step(s, sp, t, tp),
                "smoothstep" => EdgePaths.SmoothStep(s, sp, t, tp),
                _ => EdgePaths.Bezier(s, sp, t, tp)
            };

            var stroke = edge.Selected ? flow.ThemeBrush("Flow.EdgeSelected") : flow.EdgeStroke ?? flow.ThemeBrush("Flow.Edge");
            visual.Update(edge, path, s, sp, t, tp, stroke, flow.EdgeStrokeThickness, flow.EdgeLabelTemplate,
                flow.ThemeBrush("Flow.NodeBackground"), flow.ThemeBrush("Flow.NodeText"));
        }

        if (seen == null) return;
        foreach (var id in _visuals.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            Children.Remove(_visuals[id]);
            _visuals.Remove(id);
        }
    }

    /// <summary>Masque les edges hors zone visible (<c>visible</c> = null : tous visibles).</summary>
    internal void ApplyVisibility(FlowView flow, Core.Rect? visible)
    {
        foreach (var edge in flow.Edges)
        {
            if (!_visuals.TryGetValue(edge.Id, out var v)) continue;
            var want = Visibility.Visible;
            if (visible is { } view && flow.NodeRectOf(edge.Source) is { } s && flow.NodeRectOf(edge.Target) is { } t)
                want = GraphUtils.IsEdgeVisible(s, t, view) ? Visibility.Visible : Visibility.Collapsed;
            if (v.Visibility != want) v.Visibility = want;
        }
    }
}
