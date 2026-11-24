using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Wpflow.Core;
using Rect = System.Windows.Rect;

namespace Wpflow.Wpf;

/// <summary>Vue d'ensemble du graphe ; cliquer/glisser déplace le viewport. À placer dans le contenu (overlay) d'un <see cref="FlowView" />.</summary>
public sealed class MiniMap : FrameworkElement
{
    private static readonly Pen BorderPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)), 1));

    private FlowView? _flow;
    private double _scale = 1;
    private double _originX, _originY;

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
        Loaded += (_, _) =>
        {
            _flow = FlowView.FindAncestor<FlowView>(this);
            if (_flow != null) _flow.StateChanged += OnFlowChanged;
            InvalidateVisual();
        };
        Unloaded += (_, _) =>
        {
            if (_flow != null) _flow.StateChanged -= OnFlowChanged;
        };
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
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h)));
        foreach (var node in _flow.AbsoluteNodes)
        {
            if (node.Rect is not { } r) continue;
            dc.DrawRoundedRectangle(node.Selected ? selectedBrush : nodeBrush, null,
                new Rect((r.X - vbX) * _scale, (r.Y - vbY) * _scale, r.Width * _scale, r.Height * _scale), 3, 3);
        }

        var view = new Rect((visible.X - vbX) * _scale, (visible.Y - vbY) * _scale, visible.Width * _scale,
            visible.Height * _scale);
        var mask = new CombinedGeometry(GeometryCombineMode.Exclude,
            new RectangleGeometry(new Rect(0, 0, w, h)), new RectangleGeometry(view));
        dc.DrawGeometry(_flow.ThemeBrush("Flow.MiniMapMask"), null, mask);
        dc.Pop();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        CaptureMouse();
        CenterOn(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (IsMouseCaptured) CenterOn(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        ReleaseMouseCapture();
        e.Handled = true;
    }

    private void CenterOn(Point p)
    {
        if (_flow == null) return;
        var fx = _originX + p.X / _scale;
        var fy = _originY + p.Y / _scale;
        var z = _flow.Viewport.Zoom;
        _flow.Viewport = new Viewport(_flow.ActualWidth / 2 - fx * z, _flow.ActualHeight / 2 - fy * z, z);
    }
}
