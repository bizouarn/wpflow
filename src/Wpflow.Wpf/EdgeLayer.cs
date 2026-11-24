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

/// <summary>Forme d'un marqueur personnalisé (ressource <c>wpflow-marker-{clé}</c>) : orientée comme les flèches intégrées (pointe en (0,0), vers +x).</summary>
internal sealed record MarkerShape(Geometry Geometry, bool Filled)
{
    /// <summary>Remplie si toutes ses figures sont fermées (un cercle, un losange) ; une polyligne ouverte reste un trait.</summary>
    public static MarkerShape From(Geometry geometry)
    {
        var figures = geometry.GetFlattenedPathGeometry().Figures;
        return new MarkerShape(geometry, figures.Count > 0 && figures.All(f => f.IsClosed));
    }
}

/// <summary>
///     Tracé d'un edge : chemin, zone de clic élargie, flèches et animation, le tout dessiné dans <c>OnRender</c>.
///     Un seul élément visuel par edge (contre un <c>Canvas</c> et cinq enfants auparavant), ce qui compte quand
///     le graphe en contient des milliers.
/// </summary>
internal sealed class EdgeVisual : FrameworkElement, IEdgePart
{
    private static readonly Pen DefaultHitPen = Frozen(new Pen(Brushes.Transparent, EdgeStyle.DefaultHitWidth));
    private static readonly Geometry OpenArrow = Frozen(Geometry.Parse("M-7,-5 L0,0 L-7,5"));
    private static readonly Geometry ClosedArrow = Frozen(Geometry.Parse("M-7,-5 L0,0 L-7,5 Z"));

    private readonly record struct Marker(EdgeMarker Kind, MarkerShape? Custom, Matrix Transform);

    private readonly record struct UpdateKey(Edge Edge, string Path, XYPosition Start, Position StartSide, XYPosition End,
        Position EndSide, EdgeStyle Style, MarkerShape? StartShape, MarkerShape? EndShape);

    private UpdateKey? _lastUpdate;
    private Geometry? _geometry;
    private Pen? _linePen, _markerPen, _hitPen = DefaultHitPen;
    private Brush _stroke = Brushes.Gray;
    private Marker _markerStart, _markerEnd;
    private DashStyle? _dash;

    public EdgeVisual(string edgeId)
    {
        EdgeId = edgeId;
    }

    public string EdgeId { get; }

    /// <summary>Chemin SVG actuellement dessiné (utilisé par les tests).</summary>
    internal string? PathData => _lastUpdate?.Path;

    /// <summary>Vrai si une forme personnalisée remplace la flèche de fin.</summary>
    internal bool HasCustomEndMarker => _markerEnd.Custom != null;

    private static T Frozen<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }

    /// <summary>Met à jour le tracé ; ne fait rien (ni parsing, ni redessin) si rien n'a changé.</summary>
    public void Update(Edge edge, EdgePath path, XYPosition start, Position startSide, XYPosition end, Position endSide,
        EdgeStyle style, MarkerShape? startShape, MarkerShape? endShape)
    {
        var key = new UpdateKey(edge, path.Path, start, startSide, end, endSide, style, startShape, endShape);
        if (_lastUpdate == key) return;
        var geometryChanged = _lastUpdate?.Path != path.Path;
        var hitChanged = _lastUpdate?.Style.HitWidth != style.HitWidth;
        _lastUpdate = key;

        if (geometryChanged)
        {
            _geometry = Geometry.Parse(path.Path);
            _geometry.Freeze();
        }

        if (hitChanged)
            _hitPen = style.HitWidth == EdgeStyle.DefaultHitWidth
                ? DefaultHitPen
                : Frozen(new Pen(Brushes.Transparent, style.HitWidth));

        _stroke = edge.Selected ? style.SelectedStroke : style.Stroke;
        _linePen = new Pen(_stroke, style.Thickness) { LineJoin = PenLineJoin.Round };
        var hasMarker = edge.MarkerEnd != EdgeMarker.None || edge.MarkerStart != EdgeMarker.None || startShape != null || endShape != null;
        _markerPen = hasMarker ? new Pen(_stroke, style.Thickness) { LineJoin = PenLineJoin.Round } : null; // la plupart des edges n'en ont pas
        SetAnimated(edge.Animated);
        _markerEnd = new Marker(edge.MarkerEnd, endShape, ArrowTransform(end, endSide));
        _markerStart = new Marker(edge.MarkerStart, startShape, ArrowTransform(start, startSide));
        InvalidateVisual();
    }

    /// <summary>Une flèche pointe vers le node : sens opposé au côté où le handle est attaché.</summary>
    internal static Matrix ArrowTransform(XYPosition at, Position side)
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
        dc.DrawGeometry(null, _hitPen, _geometry); // large et transparent : sert au clic
        dc.DrawGeometry(null, _linePen, _geometry);
        DrawMarker(dc, _markerEnd);
        DrawMarker(dc, _markerStart);
    }

    private void DrawMarker(DrawingContext dc, Marker marker)
    {
        Geometry geometry;
        bool filled;
        if (marker.Custom is { } custom)
        {
            (geometry, filled) = (custom.Geometry, custom.Filled);
        }
        else
        {
            if (marker.Kind == EdgeMarker.None) return;
            filled = marker.Kind == EdgeMarker.ArrowClosed;
            geometry = filled ? ClosedArrow : OpenArrow;
        }

        dc.PushTransform(new MatrixTransform(marker.Transform));
        dc.DrawGeometry(filled ? _stroke : null, _markerPen, geometry);
        dc.Pop();
    }
}

/// <summary>
///     Contexte d'un edge rendu par un template (<c>wpflow-edge-template-{type}</c>) : c'est le <c>DataContext</c> du template.
///     Le chemin est déjà calculé (par le fournisseur du type) ; le template n'a qu'à le dessiner.
/// </summary>
public sealed record EdgeRenderContext(Edge Edge, Geometry Geometry, string PathData, Point Start, Point End,
    Position StartSide, Position EndSide, Point LabelPosition, bool IsSelected, bool IsAnimated, Brush Stroke,
    double StrokeThickness);

/// <summary>Edge dessiné par un <c>DataTemplate</c> : plus souple que <see cref="EdgeVisual" />, mais un vrai arbre visuel par edge.</summary>
internal sealed class EdgeTemplateVisual : ContentControl, IEdgePart
{
    private string? _pathData;
    private Geometry? _geometry;
    private (Edge, string, XYPosition, Position, XYPosition, Position, EdgeStyle)? _last;

    public EdgeTemplateVisual(string edgeId)
    {
        EdgeId = edgeId;
        Focusable = false;
        IsTabStop = false;
    }

    public string EdgeId { get; }

    public void Update(Edge edge, EdgePath path, XYPosition start, Position startSide, XYPosition end, Position endSide,
        EdgeStyle style, DataTemplate template)
    {
        var key = (edge, path.Path, start, startSide, end, endSide, style);
        if (_last == key && ReferenceEquals(ContentTemplate, template)) return;
        _last = key;

        if (_pathData != path.Path)
        {
            _pathData = path.Path;
            _geometry = Geometry.Parse(path.Path);
            _geometry.Freeze();
        }

        if (!ReferenceEquals(ContentTemplate, template)) ContentTemplate = template;
        Content = new EdgeRenderContext(edge, _geometry!, path.Path, new Point(start.X, start.Y), new Point(end.X, end.Y),
            startSide, endSide, new Point(path.LabelX, path.LabelY), edge.Selected, edge.Animated,
            edge.Selected ? style.SelectedStroke : style.Stroke, style.Thickness);
    }
}

/// <summary>Label d'un edge : texte simple ou contenu issu d'un template, centré sur le tracé.</summary>
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

    public void Update(Edge edge, EdgePath path, EdgeStyle style, DataTemplate? template)
    {
        if (template != null)
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

/// <summary>Couche regroupant les éléments d'edges (un par <see cref="Edge" />) et leurs <see cref="EdgeLabel" />.</summary>
public sealed class EdgeLayer : Canvas
{
    private readonly Dictionary<string, FrameworkElement> _visuals = new(); // EdgeVisual ou EdgeTemplateVisual
    private readonly Dictionary<string, EdgeLabel> _labels = new();

    /// <summary>Ce que les ressources de la vue définissent pour un type d'edge ; résolu une seule fois par rafraîchissement.</summary>
    private sealed record TypeInfo(IEdgePathProvider Provider, DataTemplate? Template, DataTemplate? Label);

    private sealed class Resolver(FlowView flow)
    {
        private readonly Dictionary<string, TypeInfo> _types = new();
        private readonly Dictionary<string, MarkerShape?> _markers = new();

        public TypeInfo Type(string type)
        {
            if (_types.TryGetValue(type, out var info)) return info;
            info = new TypeInfo(
                flow.TryFindResource("wpflow-edge-" + type) as IEdgePathProvider ?? EdgePathProviders.ForType(type),
                flow.TryFindResource("wpflow-edge-template-" + type) as DataTemplate,
                flow.TryFindResource("wpflow-edge-label-" + type) as DataTemplate ?? flow.EdgeLabelTemplate);
            return _types[type] = info;
        }

        public MarkerShape? Marker(string? key)
        {
            if (key == null) return null;
            if (_markers.TryGetValue(key, out var shape)) return shape;
            shape = flow.TryFindResource("wpflow-marker-" + key) is Geometry g ? MarkerShape.From(g) : null;
            return _markers[key] = shape;
        }
    }

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
            flow.ThemeBrush("Flow.NodeBackground"), flow.ThemeBrush("Flow.NodeText"), flow.EdgeHitWidth);
        var resolver = new Resolver(flow);
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

            var info = resolver.Type(edge.Type);
            var path = info.Provider.GetPath(new EdgeGeometry(s, sp, t, tp));

            // Le type a changé de mode de rendu (tracé direct <-> template) : on repart d'un nouvel élément.
            if (exists && (visual is EdgeTemplateVisual) != (info.Template != null))
            {
                RemoveVisual(edge.Id);
                exists = false;
            }

            if (!exists)
            {
                visual = info.Template != null ? new EdgeTemplateVisual(edge.Id) : new EdgeVisual(edge.Id);
                _visuals[edge.Id] = visual;
                Children.Add(visual);
            }

            if (visual is EdgeTemplateVisual templated)
                templated.Update(edge, path, s, sp, t, tp, style, info.Template!);
            else
                ((EdgeVisual)visual!).Update(edge, path, s, sp, t, tp, style, resolver.Marker(edge.MarkerStartKey),
                    resolver.Marker(edge.MarkerEndKey));
            UpdateLabel(edge, path, style, info.Label);
            SetVisibility(edge.Id, visual!, visibility);
        }

        if (seen == null) return;
        foreach (var id in _visuals.Keys.Where(k => !seen.Contains(k)).ToList()) Remove(id);
    }

    private void SetVisibility(string id, FrameworkElement visual, Visibility visibility)
    {
        if (visual.Visibility != visibility) visual.Visibility = visibility;
        if (_labels.TryGetValue(id, out var label) && label.Visibility != visibility) label.Visibility = visibility;
    }

    // Les labels sont créés à la demande : la plupart des edges n'en ont pas.
    private void UpdateLabel(Edge edge, EdgePath path, EdgeStyle style, DataTemplate? template)
    {
        if (template == null && string.IsNullOrEmpty(edge.Label))
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

        label.Update(edge, path, style, template);
    }

    private void RemoveVisual(string id)
    {
        if (_visuals.Remove(id, out var visual)) Children.Remove(visual);
    }

    private void Remove(string id)
    {
        RemoveVisual(id);
        if (_labels.Remove(id, out var label)) Children.Remove(label);
    }
}

/// <summary>Brosses et réglages d'affichage des edges, résolus une seule fois par rafraîchissement (et non par edge).</summary>
internal sealed record EdgeStyle(Brush Stroke, Brush SelectedStroke, double Thickness, Brush LabelBackground,
    Brush LabelForeground, double HitWidth)
{
    public const double DefaultHitWidth = 10;
}
