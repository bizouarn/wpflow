using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Wpflow.Core;

namespace Wpflow.Wpf;

/// <summary>
///     Poignées de redimensionnement (équivalent de <c>&lt;NodeResizer&gt;</c>), visibles quand le node est sélectionné.
///     À placer dans le template du node, <b>avant</b> les <see cref="Handle" /> pour qu'ils restent cliquables :
///     <c>&lt;Grid&gt;&lt;flow:NodeResizer /&gt;&lt;Border …/&gt;&lt;flow:Handle …/&gt;&lt;/Grid&gt;</c>.
///     Le contenu du node doit s'étirer (pas de <c>Width</c>/<c>Height</c> fixes) ; la taille initiale se donne avec
///     <see cref="Node.FixedWidth" /> / <see cref="Node.FixedHeight" />.
/// </summary>
public sealed class NodeResizer : Grid
{
    public static readonly DependencyProperty NodeMinWidthProperty = DependencyProperty.Register(
        nameof(NodeMinWidth), typeof(double), typeof(NodeResizer), new PropertyMetadata(60.0));

    public static readonly DependencyProperty NodeMinHeightProperty = DependencyProperty.Register(
        nameof(NodeMinHeight), typeof(double), typeof(NodeResizer), new PropertyMetadata(40.0));

    public static readonly DependencyProperty NodeMaxWidthProperty = DependencyProperty.Register(
        nameof(NodeMaxWidth), typeof(double), typeof(NodeResizer), new PropertyMetadata(double.PositiveInfinity));

    public static readonly DependencyProperty NodeMaxHeightProperty = DependencyProperty.Register(
        nameof(NodeMaxHeight), typeof(double), typeof(NodeResizer), new PropertyMetadata(double.PositiveInfinity));

    /// <summary>Conserve le rapport largeur/hauteur du node pendant le redimensionnement.</summary>
    public static readonly DependencyProperty KeepAspectRatioProperty = DependencyProperty.Register(
        nameof(KeepAspectRatio), typeof(bool), typeof(NodeResizer), new PropertyMetadata(false));

    /// <summary>Affiche les poignées quand le node est sélectionné (<c>isVisible</c>) ; faux les masque.</summary>
    public static readonly DependencyProperty ShowResizerProperty = DependencyProperty.Register(
        nameof(ShowResizer), typeof(bool), typeof(NodeResizer), new PropertyMetadata(true));

    /// <summary>Apparence : poignées aux coins (<c>Handle</c>) ou lignes sur les côtés (<c>Line</c>).</summary>
    /// <summary>Garde les poignées à taille constante à l'écran quel que soit le zoom (<c>autoScale</c>).</summary>
    public static readonly DependencyProperty AutoScaleProperty = DependencyProperty.Register(
        nameof(AutoScale), typeof(bool), typeof(NodeResizer), new PropertyMetadata(false, (d, _) => ((NodeResizer)d).ApplyScale()));

    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
        nameof(Variant), typeof(ResizerVariant), typeof(NodeResizer),
        new PropertyMetadata(ResizerVariant.Handle, (d, _) => ((NodeResizer)d).ApplyVariant()));

    /// <summary>Peut refuser une taille (<c>shouldResize</c>) : reçoit la poignée et le rectangle proposé ; faux = ignoré.</summary>
    public static readonly DependencyProperty ShouldResizeProperty = DependencyProperty.Register(
        nameof(ShouldResize), typeof(Func<ResizeDirection, Core.Rect, bool>), typeof(NodeResizer), new PropertyMetadata(null));

    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Brush), typeof(NodeResizer),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x33, 0x67, 0xd9))));

    private static readonly ControlTemplate ThumbTemplate = BuildThumbTemplate();

    private readonly Dictionary<ResizeDirection, Thumb> _thumbs = new();
    private Core.Rect _startRect;
    private XYPosition _startMouse;

    public NodeResizer()
    {
        SetBinding(VisibilityProperty, new MultiBinding
        {
            Bindings =
            {
                new Binding
                {
                    Path = new PropertyPath(NodeHost.IsSelectedProperty),
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(NodeHost), 1)
                },
                new Binding { Path = new PropertyPath(ShowResizerProperty), Source = this }
            },
            Converter = new SelectedAndShownConverter()
        });

        Loaded += (_, _) =>
        {
            _scaleFlow = FlowView.FindAncestor<FlowView>(this);
            if (_scaleFlow != null) _scaleFlow.StateChanged += OnScaleFlowChanged;
            ApplyScale();
        };
        Unloaded += (_, _) =>
        {
            if (_scaleFlow != null) _scaleFlow.StateChanged -= OnScaleFlowChanged;
            _scaleFlow = null;
        };

        // Côtés d'abord (sous les coins), puis coins.
        AddThumb(ResizeDirection.Top, HorizontalAlignment.Stretch, VerticalAlignment.Top, Cursors.SizeNS, true);
        AddThumb(ResizeDirection.Bottom, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, Cursors.SizeNS, true);
        AddThumb(ResizeDirection.Left, HorizontalAlignment.Left, VerticalAlignment.Stretch, Cursors.SizeWE, true);
        AddThumb(ResizeDirection.Right, HorizontalAlignment.Right, VerticalAlignment.Stretch, Cursors.SizeWE, true);
        AddThumb(ResizeDirection.TopLeft, HorizontalAlignment.Left, VerticalAlignment.Top, Cursors.SizeNWSE, false);
        AddThumb(ResizeDirection.TopRight, HorizontalAlignment.Right, VerticalAlignment.Top, Cursors.SizeNESW, false);
        AddThumb(ResizeDirection.BottomLeft, HorizontalAlignment.Left, VerticalAlignment.Bottom, Cursors.SizeNESW, false);
        AddThumb(ResizeDirection.BottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom, Cursors.SizeNWSE,
            false);
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

    public bool ShowResizer
    {
        get => (bool)GetValue(ShowResizerProperty);
        set => SetValue(ShowResizerProperty, value);
    }

    public bool AutoScale
    {
        get => (bool)GetValue(AutoScaleProperty);
        set => SetValue(AutoScaleProperty, value);
    }

    private FlowView? _scaleFlow;

    private void OnScaleFlowChanged(object? sender, EventArgs e)
    {
        if (AutoScale) ApplyScale();
    }

    private void ApplyScale()
    {
        var scale = AutoScale && _scaleFlow is { } f ? 1 / f.Viewport.Zoom : 1;
        foreach (var thumb in _thumbs.Values)
        {
            thumb.RenderTransformOrigin = new Point(0.5, 0.5);
            thumb.RenderTransform = scale == 1 ? Transform.Identity : new ScaleTransform(scale, scale);
        }
    }

    public ResizerVariant Variant
    {
        get => (ResizerVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public Func<ResizeDirection, Core.Rect, bool>? ShouldResize
    {
        get => (Func<ResizeDirection, Core.Rect, bool>?)GetValue(ShouldResizeProperty);
        set => SetValue(ShouldResizeProperty, value);
    }

    public event EventHandler<NodeResizeEventArgs>? ResizeStarted;
    public event EventHandler<NodeResizeEventArgs>? Resizing;
    public event EventHandler<NodeResizeEventArgs>? ResizeEnded;

    public Brush Accent
    {
        get => (Brush)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>Poignée donnée (utile pour les tests d'interface).</summary>
    public Thumb? GetThumb(ResizeDirection direction) => _thumbs.GetValueOrDefault(direction);

    private static ControlTemplate BuildThumbTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetBinding(Border.BackgroundProperty, new Binding { RelativeSource = RelativeSource.TemplatedParent, Path = new PropertyPath(Control.BackgroundProperty) });
        border.SetBinding(Border.BorderBrushProperty, new Binding { RelativeSource = RelativeSource.TemplatedParent, Path = new PropertyPath(Control.BorderBrushProperty) });
        border.SetBinding(Border.BorderThicknessProperty, new Binding { RelativeSource = RelativeSource.TemplatedParent, Path = new PropertyPath(Control.BorderThicknessProperty) });
        return new ControlTemplate(typeof(Thumb)) { VisualTree = border };
    }

    private void AddThumb(ResizeDirection dir, HorizontalAlignment h, VerticalAlignment v, Cursor cursor, bool isSide)
    {
        var thumb = new Thumb
        {
            Template = ThumbTemplate,
            HorizontalAlignment = h,
            VerticalAlignment = v,
            Cursor = cursor,
            Focusable = false
        };

        if (isSide)
        {
            // Bande de 8 px à cheval sur le bord, transparente (comme les lignes de React Flow).
            thumb.Background = Brushes.Transparent;
            if (h == HorizontalAlignment.Stretch)
            {
                thumb.Height = 8;
                thumb.Margin = new Thickness(0, v == VerticalAlignment.Top ? -4 : 0, 0, v == VerticalAlignment.Bottom ? -4 : 0);
            }
            else
            {
                thumb.Width = 8;
                thumb.Margin = new Thickness(h == HorizontalAlignment.Left ? -4 : 0, 0, h == HorizontalAlignment.Right ? -4 : 0, 0);
            }
        }
        else
        {
            thumb.Width = thumb.Height = 9;
            thumb.Background = Brushes.White;
            thumb.BorderBrush = Accent;
            thumb.BorderThickness = new Thickness(1.5);
            thumb.Margin = new Thickness(h == HorizontalAlignment.Left ? -5 : 0, v == VerticalAlignment.Top ? -5 : 0,
                h == HorizontalAlignment.Right ? -5 : 0, v == VerticalAlignment.Bottom ? -5 : 0);
            Panel.SetZIndex(thumb, 1);
        }

        thumb.DragStarted += (_, _) => OnDragStarted(dir);
        thumb.DragCompleted += (_, _) => OnDragCompleted(dir);
        thumb.DragDelta += (_, _) => OnDragDelta(dir);
        _thumbs[dir] = thumb;
        Children.Add(thumb);
    }

    private (FlowView Flow, NodeHost Host)? Context() =>
        FlowView.FindAncestor<NodeHost>(this) is { } host && FlowView.FindAncestor<FlowView>(host) is { } flow
            ? (flow, host)
            : null;

    private XYPosition MouseInFlow(FlowView flow) => flow.ScreenToFlow(Mouse.GetPosition(flow));

    private void OnDragStarted(ResizeDirection dir)
    {
        if (Context() is not { } c || c.Host.Node?.Rect is not { } rect) return;
        _startRect = rect;
        _startMouse = MouseInFlow(c.Flow);
        c.Flow.PushHistory();
        c.Flow.ApplyNodeChanges([new NodeInteractionChange(c.Host.Node.Id, Resizing: true)]);
        ResizeStarted?.Invoke(this, new NodeResizeEventArgs(c.Host.Node.Id, dir, rect, rect));
    }

    private void OnDragDelta(ResizeDirection dir)
    {
        if (Context() is not { } c || c.Host.Node is not { } node) return;
        var m = MouseInFlow(c.Flow);
        var r = NodeResize.Resize(dir, _startRect, m.X - _startMouse.X, m.Y - _startMouse.Y,
            NodeMinWidth, NodeMinHeight, NodeMaxWidth, NodeMaxHeight, KeepAspectRatio);
        if (ShouldResize is { } allowed && !allowed(dir, r)) return;
        c.Flow.ApplyNodeChanges([new NodeResizeChange(node.Id, r.Width, r.Height, new XYPosition(r.X, r.Y))]);
        Resizing?.Invoke(this, new NodeResizeEventArgs(node.Id, dir, _startRect, r));
    }

    private void OnDragCompleted(ResizeDirection dir)
    {
        if (Context() is not { } c || c.Host.Node is not { } node) return;
        var end = node.Rect ?? _startRect;
        c.Flow.ApplyNodeChanges([new NodeInteractionChange(node.Id, Resizing: false)]);
        ResizeEnded?.Invoke(this, new NodeResizeEventArgs(node.Id, dir, _startRect, end));
    }

    // Handle : carrés aux coins, côtés invisibles. Line : traits sur les côtés, coins invisibles.
    private void ApplyVariant()
    {
        foreach (var (dir, thumb) in _thumbs)
        {
            var side = dir is ResizeDirection.Top or ResizeDirection.Bottom or ResizeDirection.Left or ResizeDirection.Right;
            if (Variant == ResizerVariant.Line)
            {
                thumb.Background = side ? Accent : Brushes.Transparent;
                thumb.BorderBrush = Brushes.Transparent;
                thumb.Opacity = 1;
            }
            else
            {
                thumb.Background = side ? Brushes.Transparent : Brushes.White;
                thumb.BorderBrush = side ? Brushes.Transparent : Accent;
            }
        }
    }
}

/// <summary>Apparence de <see cref="NodeResizer" />.</summary>
public enum ResizerVariant
{
    Handle,
    Line
}

/// <summary>Un redimensionnement : début, en cours ou terminé (<c>onResizeStart</c>, <c>onResize</c>, <c>onResizeEnd</c>).</summary>
public sealed class NodeResizeEventArgs(string nodeId, ResizeDirection direction, Core.Rect startRect, Core.Rect rect) : EventArgs
{    public string NodeId { get; } = nodeId;    public ResizeDirection Direction { get; } = direction;    public Core.Rect StartRect { get; } = startRect;    public Core.Rect Rect { get; } = rect;}

/// <summary>Visible si le node est sélectionné ET si le resizer est autorisé.</summary>
internal sealed class SelectedAndShownConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        values.All(v => v is true) ? Visibility.Visible : Visibility.Collapsed;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
