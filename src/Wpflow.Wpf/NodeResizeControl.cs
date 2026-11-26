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

    private readonly ResizeGesture _gesture;

    public NodeResizeControl()
    {
        Width = Height = 12;
        Focusable = false;
        Cursor = Cursors.SizeNWSE;
        _gesture = new ResizeGesture(this);
        DragStarted += (_, _) =>
        {
            if (_gesture.Start(Direction) is { } args) ResizeStarted?.Invoke(this, args);
        };
        DragDelta += (_, _) =>
        {
            if (_gesture.Move(Direction, NodeMinWidth, NodeMinHeight, NodeMaxWidth, NodeMaxHeight, KeepAspectRatio, ShouldResize) is { } args)
                Resizing?.Invoke(this, args);
        };
        DragCompleted += (_, _) =>
        {
            if (_gesture.End(Direction) is { } args) ResizeEnded?.Invoke(this, args);
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
}
