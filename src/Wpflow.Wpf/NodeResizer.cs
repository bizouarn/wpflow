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

    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Brush), typeof(NodeResizer),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x33, 0x67, 0xd9))));

    private static readonly ControlTemplate ThumbTemplate = BuildThumbTemplate();

    private readonly Dictionary<ResizeDirection, Thumb> _thumbs = new();
    private Core.Rect _startRect;
    private XYPosition _startMouse;

    public NodeResizer()
    {
        // Visible uniquement quand le node hôte est sélectionné.
        SetBinding(VisibilityProperty, new Binding
        {
            Path = new PropertyPath(NodeHost.IsSelectedProperty),
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(NodeHost), 1),
            Converter = new BooleanToVisibilityConverter()
        });

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

        thumb.DragStarted += (_, _) => OnDragStarted();
        thumb.DragDelta += (_, _) => OnDragDelta(dir);
        _thumbs[dir] = thumb;
        Children.Add(thumb);
    }

    private (FlowView Flow, NodeHost Host)? Context() =>
        FlowView.FindAncestor<NodeHost>(this) is { } host && FlowView.FindAncestor<FlowView>(host) is { } flow
            ? (flow, host)
            : null;

    private XYPosition MouseInFlow(FlowView flow) => flow.ScreenToFlow(Mouse.GetPosition(flow));

    private void OnDragStarted()
    {
        if (Context() is not { } c || c.Host.Node?.Rect is not { } rect) return;
        _startRect = rect;
        _startMouse = MouseInFlow(c.Flow);
        c.Flow.PushHistory();
    }

    private void OnDragDelta(ResizeDirection dir)
    {
        if (Context() is not { } c || c.Host.Node is not { } node) return;
        var m = MouseInFlow(c.Flow);
        var r = NodeResize.Resize(dir, _startRect, m.X - _startMouse.X, m.Y - _startMouse.Y,
            NodeMinWidth, NodeMinHeight, NodeMaxWidth, NodeMaxHeight);
        c.Flow.ApplyNodeChanges([new NodeResizeChange(node.Id, r.Width, r.Height, new XYPosition(r.X, r.Y))]);
    }
}
