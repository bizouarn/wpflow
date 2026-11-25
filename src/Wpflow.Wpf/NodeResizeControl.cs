using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Wpflow.Core;

namespace Wpflow.Wpf;

/// <summary>
///     Poignée de redimensionnement personnalisée (<c>NodeResizeControl</c>) : un seul <see cref="Thumb" /> que vous placez
///     et habillez comme vous voulez (<c>Template</c>, <c>Content</c>) dans le template du node, avec la
///     <see cref="Direction" /> qu'elle pilote. Même logique que <see cref="NodeResizer" /> (tailles min/max, ratio, historique).
/// </summary>
public sealed class NodeResizeControl : Thumb
{
    public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(
        nameof(Direction), typeof(ResizeDirection), typeof(NodeResizeControl), new PropertyMetadata(ResizeDirection.BottomRight));

    public static readonly DependencyProperty NodeMinWidthProperty = DependencyProperty.Register(
        nameof(NodeMinWidth), typeof(double), typeof(NodeResizeControl), new PropertyMetadata(60.0));

    public static readonly DependencyProperty NodeMinHeightProperty = DependencyProperty.Register(
        nameof(NodeMinHeight), typeof(double), typeof(NodeResizeControl), new PropertyMetadata(40.0));

    public static readonly DependencyProperty NodeMaxWidthProperty = DependencyProperty.Register(
        nameof(NodeMaxWidth), typeof(double), typeof(NodeResizeControl), new PropertyMetadata(double.PositiveInfinity));

    public static readonly DependencyProperty NodeMaxHeightProperty = DependencyProperty.Register(
        nameof(NodeMaxHeight), typeof(double), typeof(NodeResizeControl), new PropertyMetadata(double.PositiveInfinity));

    public static readonly DependencyProperty KeepAspectRatioProperty = DependencyProperty.Register(
        nameof(KeepAspectRatio), typeof(bool), typeof(NodeResizeControl), new PropertyMetadata(false));

    /// <summary>Peut refuser une taille proposée (<c>shouldResize</c>).</summary>
    public static readonly DependencyProperty ShouldResizeProperty = DependencyProperty.Register(
        nameof(ShouldResize), typeof(Func<ResizeDirection, Core.Rect, bool>), typeof(NodeResizeControl), new PropertyMetadata(null));

    private Core.Rect _startRect;
    private XYPosition _startMouse;

    public NodeResizeControl()
    {
        Width = Height = 12;
        Focusable = false;
        Cursor = Cursors.SizeNWSE;
        DragStarted += (_, _) => OnStarted();
        DragDelta += (_, _) => OnDelta();
        DragCompleted += (_, _) =>
        {
            if (Context() is { } c && c.Host.Node is { } node)
            {
                c.Flow.ApplyNodeChanges([new NodeInteractionChange(node.Id, Resizing: false)]);
                ResizeEnded?.Invoke(this, new NodeResizeEventArgs(node.Id, Direction, _startRect, node.Rect ?? _startRect));
            }
        };
    }

    public ResizeDirection Direction
    {
        get => (ResizeDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public double NodeMinWidth
    {
        get => (double)GetValue(NodeMinWidthProperty);
        set => SetValue(NodeMinWidthProperty, value);
    }

    public double NodeMinHeight
    {
        get => (double)GetValue(NodeMinHeightProperty);
        set => SetValue(NodeMinHeightProperty, value);
    }

    public double NodeMaxWidth
    {
        get => (double)GetValue(NodeMaxWidthProperty);
        set => SetValue(NodeMaxWidthProperty, value);
    }

    public double NodeMaxHeight
    {
        get => (double)GetValue(NodeMaxHeightProperty);
        set => SetValue(NodeMaxHeightProperty, value);
    }

    public bool KeepAspectRatio
    {
        get => (bool)GetValue(KeepAspectRatioProperty);
        set => SetValue(KeepAspectRatioProperty, value);
    }

    public Func<ResizeDirection, Core.Rect, bool>? ShouldResize
    {
        get => (Func<ResizeDirection, Core.Rect, bool>?)GetValue(ShouldResizeProperty);
        set => SetValue(ShouldResizeProperty, value);
    }

    public event EventHandler<NodeResizeEventArgs>? ResizeStarted;
    public event EventHandler<NodeResizeEventArgs>? Resizing;
    public event EventHandler<NodeResizeEventArgs>? ResizeEnded;

    private (FlowView Flow, NodeHost Host)? Context() =>
        FlowView.FindAncestor<NodeHost>(this) is { } host && FlowView.FindAncestor<FlowView>(host) is { } flow ? (flow, host) : null;

    private void OnStarted()
    {
        if (Context() is not { } c || c.Host.Node?.Rect is not { } rect) return;
        _startRect = rect;
        _startMouse = c.Flow.ScreenToFlow(Mouse.GetPosition(c.Flow));
        c.Flow.PushHistory();
        c.Flow.ApplyNodeChanges([new NodeInteractionChange(c.Host.Node.Id, Resizing: true)]);
        ResizeStarted?.Invoke(this, new NodeResizeEventArgs(c.Host.Node.Id, Direction, rect, rect));
    }

    private void OnDelta()
    {
        if (Context() is not { } c || c.Host.Node is not { } node) return;
        var m = c.Flow.ScreenToFlow(Mouse.GetPosition(c.Flow));
        var r = NodeResize.Resize(Direction, _startRect, m.X - _startMouse.X, m.Y - _startMouse.Y,
            NodeMinWidth, NodeMinHeight, NodeMaxWidth, NodeMaxHeight, KeepAspectRatio);
        if (ShouldResize is { } allowed && !allowed(Direction, r)) return;
        c.Flow.ApplyNodeChanges([new NodeResizeChange(node.Id, r.Width, r.Height, new XYPosition(r.X, r.Y))]);
        Resizing?.Invoke(this, new NodeResizeEventArgs(node.Id, Direction, _startRect, r));
    }
}
