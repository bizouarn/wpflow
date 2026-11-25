using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Wpflow.Core;
using Rect = System.Windows.Rect;

namespace Wpflow.Wpf;

/// <summary>Clic sur un node dans la <see cref="MiniMap" /> (<c>onNodeClick</c>).</summary>
public sealed class MiniMapNodeClickEventArgs(Node node) : EventArgs
{
    public Node Node { get; } = node;
}

/// <summary>Vue d'ensemble du graphe ; cliquer/glisser déplace le viewport. À placer dans le contenu (overlay) d'un <see cref="FlowView" />.</summary>
public sealed class MiniMap : FrameworkElement
{
    private static readonly Pen BorderPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)), 1));

    private FlowView? _flow;
    private double _scale = 1;
    private double _originX, _originY;
    private Point _dragStart;
    private XYPosition _dragCenter;

    public MiniMap()
    {
        Width = 200;
        Height = 150;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Bottom;
        Margin = new Thickness(15);
        Cursor = Cursors.Hand;
        Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 6, ShadowDepth = 0, Opacity = 0.15 };
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    /// <summary>La <see cref="FlowView" /> représentée ; par défaut, la vue qui contient la minimap. À renseigner hors de la vue : <c>Flow="{Binding ElementName=Flow}"</c>.</summary>
    public static readonly DependencyProperty FlowProperty = DependencyProperty.Register(
        nameof(Flow), typeof(FlowView), typeof(MiniMap), new PropertyMetadata(null, (d, _) => ((MiniMap)d).Attach()));

    /// <summary>Couleur d'un node (<c>nodeColor</c>) ; <c>null</c> ou résultat <c>null</c> = couleur du thème.</summary>
    public static readonly DependencyProperty NodeColorProperty = DependencyProperty.Register(
        nameof(NodeColor), typeof(Func<Node, Brush?>), typeof(MiniMap), new PropertyMetadata(null, Redraw));

    /// <summary>Contour d'un node (<c>nodeStrokeColor</c>).</summary>
    public static readonly DependencyProperty NodeStrokeColorProperty = DependencyProperty.Register(
        nameof(NodeStrokeColor), typeof(Func<Node, Brush?>), typeof(MiniMap), new PropertyMetadata(null, Redraw));

    public static readonly DependencyProperty NodeStrokeWidthProperty = DependencyProperty.Register(
        nameof(NodeStrokeWidth), typeof(double), typeof(MiniMap), new PropertyMetadata(2.0, Redraw));

    public static readonly DependencyProperty NodeBorderRadiusProperty = DependencyProperty.Register(
        nameof(NodeBorderRadius), typeof(double), typeof(MiniMap), new PropertyMetadata(3.0, Redraw));

    /// <summary>Dessin libre d'un node (<c>nodeComponent</c>) : reçoit le contexte, le node et son rectangle dans la minimap.</summary>
    public static readonly DependencyProperty NodeRendererProperty = DependencyProperty.Register(
        nameof(NodeRenderer), typeof(Action<DrawingContext, Node, Rect>), typeof(MiniMap), new PropertyMetadata(null, Redraw));

    /// <summary>Voile qui masque ce qui est hors de la vue (<c>maskColor</c>) ; <c>null</c> = couleur du thème.</summary>
    public static readonly DependencyProperty MaskColorProperty = DependencyProperty.Register(
        nameof(MaskColor), typeof(Brush), typeof(MiniMap), new PropertyMetadata(null, Redraw));

    /// <summary>Glisser dans la minimap déplace la vue (<c>pannable</c>).</summary>
    public static readonly DependencyProperty PannableProperty = DependencyProperty.Register(
        nameof(Pannable), typeof(bool), typeof(MiniMap), new PropertyMetadata(true));

    /// <summary>La molette zoome la vue (<c>zoomable</c>).</summary>
    public static readonly DependencyProperty ZoomableProperty = DependencyProperty.Register(
        nameof(Zoomable), typeof(bool), typeof(MiniMap), new PropertyMetadata(false));

    /// <summary>Le glisser déplace la vue dans le sens inverse (<c>inversePan</c>).</summary>
    public static readonly DependencyProperty InversePanProperty = DependencyProperty.Register(
        nameof(InversePan), typeof(bool), typeof(MiniMap), new PropertyMetadata(false));

    /// <summary>Sensibilité de la molette (<c>zoomStep</c>) ; 10 = comme la surface.</summary>
    public static readonly DependencyProperty ZoomStepProperty = DependencyProperty.Register(
        nameof(ZoomStep), typeof(double), typeof(MiniMap), new PropertyMetadata(10.0));

    /// <summary>Un node de la minimap a été cliqué (<c>onNodeClick</c>).</summary>
    public event EventHandler<MiniMapNodeClickEventArgs>? NodeClick;

    private static void Redraw(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MiniMap)d).InvalidateVisual();

    public FlowView? Flow
    {
        get => (FlowView?)GetValue(FlowProperty);
        set => SetValue(FlowProperty, value);
    }

    public Func<Node, Brush?>? NodeColor
    {
        get => (Func<Node, Brush?>?)GetValue(NodeColorProperty);
        set => SetValue(NodeColorProperty, value);
    }

    public Func<Node, Brush?>? NodeStrokeColor
    {
        get => (Func<Node, Brush?>?)GetValue(NodeStrokeColorProperty);
        set => SetValue(NodeStrokeColorProperty, value);
    }

    public double NodeStrokeWidth
    {
        get => (double)GetValue(NodeStrokeWidthProperty);
        set => SetValue(NodeStrokeWidthProperty, value);
    }

    public double NodeBorderRadius
    {
        get => (double)GetValue(NodeBorderRadiusProperty);
        set => SetValue(NodeBorderRadiusProperty, value);
    }

    public Action<DrawingContext, Node, Rect>? NodeRenderer
    {
        get => (Action<DrawingContext, Node, Rect>?)GetValue(NodeRendererProperty);
        set => SetValue(NodeRendererProperty, value);
    }

    public Brush? MaskColor
    {
        get => (Brush?)GetValue(MaskColorProperty);
        set => SetValue(MaskColorProperty, value);
    }

    public bool Pannable
    {
        get => (bool)GetValue(PannableProperty);
        set => SetValue(PannableProperty, value);
    }

    public bool Zoomable
    {
        get => (bool)GetValue(ZoomableProperty);
        set => SetValue(ZoomableProperty, value);
    }

    public bool InversePan
    {
        get => (bool)GetValue(InversePanProperty);
        set => SetValue(InversePanProperty, value);
    }

    public double ZoomStep
    {
        get => (double)GetValue(ZoomStepProperty);
        set => SetValue(ZoomStepProperty, value);
    }

    private void Attach()
    {
        Detach();
        _flow = Flow ?? (IsLoaded ? FlowView.FindAncestor<FlowView>(this) : null);
        if (_flow != null) _flow.StateChanged += OnFlowChanged;
        InvalidateVisual();
    }

    private void Detach()
    {
        if (_flow != null) _flow.StateChanged -= OnFlowChanged;
        _flow = null;
    }

    private static T Frozen<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }

    private void OnFlowChanged(object? sender, EventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        dc.DrawRoundedRectangle(_flow?.ThemeBrush("Flow.MiniMapBackground") ?? Brushes.White, BorderPen, new Rect(0, 0, w, h), 4, 4);
        if (_flow == null || w <= 0 || h <= 0) return;

        var visible = ViewportMath.GetVisibleRect(_flow.Viewport, _flow.ActualWidth, _flow.ActualHeight);
        var bounds = GraphUtils.GetNodesBounds(_flow.AbsoluteNodes) is { } nb ? Core.Rect.Union(nb, visible) : visible;

        // Cadre le contenu en conservant le ratio de la minimap (même logique que xyflow).
        var viewScale = Math.Max(bounds.Width / w, bounds.Height / h);
        var viewW = viewScale * w;
        var viewH = viewScale * h;
        var padding = 5 * viewScale;
        var vbX = bounds.X - (viewW - bounds.Width) / 2 - padding;
        var vbY = bounds.Y - (viewH - bounds.Height) / 2 - padding;
        _scale = w / (viewW + 2 * padding);
        _originX = vbX;
        _originY = vbY;

        // Les brosses sont lues une fois : une recherche de ressource par node et par image coûterait très cher.
        var nodeBrush = _flow.ThemeBrush("Flow.MiniMapNode");
        var selectedBrush = _flow.ThemeBrush("Flow.MiniMapNodeSelected");
        var color = NodeColor;
        var stroke = NodeStrokeColor;
        var renderer = NodeRenderer;
        var radius = NodeBorderRadius;
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h)));
        foreach (var node in _flow.AbsoluteNodes)
        {
            if (node.Rect is not { } r) continue;
            var rect = new Rect((r.X - vbX) * _scale, (r.Y - vbY) * _scale, r.Width * _scale, r.Height * _scale);
            if (renderer != null)
            {
                renderer(dc, node, rect);
                continue;
            }

            var fill = color?.Invoke(node) ?? (node.Selected ? selectedBrush : nodeBrush);
            var pen = stroke?.Invoke(node) is { } s ? new Pen(s, NodeStrokeWidth) : null;
            dc.DrawRoundedRectangle(fill, pen, rect, radius, radius);
        }

        var view = new Rect((visible.X - vbX) * _scale, (visible.Y - vbY) * _scale, visible.Width * _scale,
            visible.Height * _scale);
        var mask = new CombinedGeometry(GeometryCombineMode.Exclude,
            new RectangleGeometry(new Rect(0, 0, w, h)), new RectangleGeometry(view));
        dc.DrawGeometry(MaskColor ?? _flow.ThemeBrush("Flow.MiniMapMask"), null, mask);
        dc.Pop();
    }

    private XYPosition ToFlow(Point p) => new(_originX + p.X / _scale, _originY + p.Y / _scale);

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var p = e.GetPosition(this);
        e.Handled = true;
        if (_flow == null) return;

        var at = ToFlow(p);
        if (NodeClick != null && _flow.AbsoluteNodes.LastOrDefault(n => n.Rect is { } r && r.Contains(at)) is { } hit)
            NodeClick(this, new MiniMapNodeClickEventArgs(hit));

        if (!Pannable) return;
        CaptureMouse();
        _dragStart = p;
        CenterOn(at);
        _dragCenter = at;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!IsMouseCaptured || _flow == null) return;
        var p = e.GetPosition(this);
        var sign = InversePan ? -1 : 1;
        CenterOn(new XYPosition(_dragCenter.X + sign * (p.X - _dragStart.X) / _scale,
            _dragCenter.Y + sign * (p.Y - _dragStart.Y) / _scale));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        ReleaseMouseCapture();
        e.Handled = true;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!Zoomable || _flow == null) return;
        var factor = Math.Pow(ViewportMath.WheelZoomFactor(-e.Delta), ZoomStep / 10);
        _flow.ZoomBy(factor);
        e.Handled = true;
    }

    private void CenterOn(XYPosition flowPoint)
    {
        if (_flow == null) return;
        var z = _flow.Viewport.Zoom;
        _flow.Viewport = new Viewport(_flow.ActualWidth / 2 - flowPoint.X * z, _flow.ActualHeight / 2 - flowPoint.Y * z, z);
    }
}
