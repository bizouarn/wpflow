using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Wpflow.Core;

namespace Wpflow.Wpf;

/// <summary>
///     Poignée de rotation (exemple « Rotatable Node ») : un rond au-dessus du node, visible quand il est sélectionné ;
///     le glisser fait tourner le node autour de son centre (<see cref="Node.Rotation" />, en degrés). Maj pendant le glisser
///     arrondit à <see cref="SnapAngle" />. À placer dans le <c>Grid</c> racine du template du node. Les handles et les edges
///     restent attachés aux positions non tournées, comme dans React Flow.
/// </summary>
public sealed class NodeRotator : Thumb
{
    public static readonly DependencyProperty SnapAngleProperty = DependencyProperty.Register(
        nameof(SnapAngle), typeof(double), typeof(NodeRotator), new PropertyMetadata(15.0));

    /// <summary>Pas d'arrondi (degrés) quand Maj est maintenue.</summary>
    public double SnapAngle
    {
        get => (double)GetValue(SnapAngleProperty);
        set => SetValue(SnapAngleProperty, value);
    }

    public NodeRotator()
    {
        Width = Height = 14;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(0, -32, 0, 0);
        Cursor = Cursors.Hand;
        Focusable = false;
        Template = new ControlTemplate(typeof(Thumb))
        {
            VisualTree = Ellipse()
        };
        SetBinding(VisibilityProperty, new Binding
        {
            Path = new PropertyPath(NodeHost.IsSelectedProperty),
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(NodeHost), 1),
            Converter = new BooleanToVisibilityConverter()
        });
        DragStarted += (_, _) =>
        {
            if (Context() is { } c) c.Flow.PushHistory();
        };
        DragDelta += (_, _) => Rotate();
    }

    private static FrameworkElementFactory Ellipse()
    {
        var e = new FrameworkElementFactory(typeof(System.Windows.Shapes.Ellipse));
        e.SetValue(System.Windows.Shapes.Shape.FillProperty, Brushes.White);
        e.SetValue(System.Windows.Shapes.Shape.StrokeProperty, new SolidColorBrush(Color.FromRgb(0x33, 0x67, 0xd9)));
        e.SetValue(System.Windows.Shapes.Shape.StrokeThicknessProperty, 1.5);
        return e;
    }

    private (FlowView Flow, NodeHost Host)? Context() =>
        FlowView.FindAncestor<NodeHost>(this) is { } host && FlowView.FindAncestor<FlowView>(host) is { } flow ? (flow, host) : null;

    /// <summary>Angle (degrés, 0 = poignée en haut) du point flow <paramref name="p" /> vu du centre du node.</summary>
    internal static double AngleTo(XYPosition center, XYPosition p) => NodeRotation.AngleTo(center, p);

    private void Rotate()
    {
        if (Context() is not { } c || c.Host.Node is not { } node || c.Flow.GetInternalNode(node.Id)?.Rect is not { } rect) return;
        var angle = AngleTo(rect.Center, c.Flow.ScreenToFlow(Mouse.GetPosition(c.Flow)));
        angle = NodeRotation.Resolve(angle, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), SnapAngle);
        c.Flow.UpdateNode(node.Id, n => n with { Rotation = angle });
    }
}
