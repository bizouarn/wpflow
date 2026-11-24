using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpflow.Core;
using Rect = System.Windows.Rect;

namespace Wpflow.Wpf;

/// <summary>
///     Point de connexion d'un node (équivalent de <c>&lt;Handle&gt;</c>). À placer dans le template d'un node :
///     <c>&lt;flow:Handle Type="Source" Position="Right" /&gt;</c>.
/// </summary>
public class Handle : Control
{
    public static readonly DependencyProperty TypeProperty = DependencyProperty.Register(
        nameof(Type), typeof(HandleType), typeof(Handle), new PropertyMetadata(HandleType.Source, OnLayoutChanged));

    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
        nameof(Position), typeof(Position), typeof(Handle), new PropertyMetadata(Position.Top, OnLayoutChanged));

    public static readonly DependencyProperty HandleIdProperty = DependencyProperty.Register(
        nameof(HandleId), typeof(string), typeof(Handle), new PropertyMetadata(null, OnLayoutChanged));

    /// <summary>Décalage le long du côté (depuis le haut ou la gauche). <c>NaN</c> = centré.</summary>
    public static readonly DependencyProperty OffsetProperty = DependencyProperty.Register(
        nameof(Offset), typeof(double), typeof(Handle), new PropertyMetadata(double.NaN, OnLayoutChanged));

    internal const double Size = 8;

    static Handle()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Handle), new FrameworkPropertyMetadata(typeof(Handle)));
    }

    public Handle()
    {
        Width = Size;
        Height = Size;
        Cursor = System.Windows.Input.Cursors.Cross;
        ApplyAlignment();
        Loaded += (_, _) => NodeHost?.RequestMeasure();
        Unloaded += (_, _) => NodeHost?.RequestMeasure();
    }

    public HandleType Type
    {
        get => (HandleType)GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }

    public Position Position
    {
        get => (Position)GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public string? HandleId
    {
        get => (string?)GetValue(HandleIdProperty);
        set => SetValue(HandleIdProperty, value);
    }

    public double Offset
    {
        get => (double)GetValue(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    internal NodeHost? NodeHost => FlowView.FindAncestor<NodeHost>(this);

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var h = (Handle)d;
        h.ApplyAlignment();
        h.NodeHost?.RequestMeasure();
    }

    private void ApplyAlignment()
    {
        var half = Size / 2;
        var hasOffset = !double.IsNaN(Offset);
        switch (Position)
        {
            case Position.Left:
            case Position.Right:
                HorizontalAlignment = Position == Position.Left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
                VerticalAlignment = hasOffset ? VerticalAlignment.Top : VerticalAlignment.Center;
                Margin = new Thickness(Position == Position.Left ? -half : 0, hasOffset ? Offset - half : 0,
                    Position == Position.Right ? -half : 0, 0);
                break;
            default:
                VerticalAlignment = Position == Position.Top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
                HorizontalAlignment = hasOffset ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                Margin = new Thickness(hasOffset ? Offset - half : 0, Position == Position.Top ? -half : 0, 0,
                    Position == Position.Bottom ? -half : 0);
                break;
        }
    }

    /// <summary>Informations de handle relatives au node hôte (coordonnées locales du node).</summary>
    internal HandleInfo? Measure(NodeHost host)
    {
        if (!IsLoaded || !host.IsLoaded) return null;
        try
        {
            var b = TransformToAncestor(host).TransformBounds(new Rect(0, 0, ActualWidth, ActualHeight));
            return new HandleInfo(HandleId, Type, Position, b.X, b.Y, b.Width, b.Height);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
