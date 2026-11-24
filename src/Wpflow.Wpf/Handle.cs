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

    private static readonly Brush DefaultFill = Freeze(new SolidColorBrush(Color.FromRgb(0x9c, 0xa3, 0xaf)));

    static Handle()
    {
        // Sans gabarit, le handle se dessine lui-même dans OnRender (un seul élément visuel au lieu de quatre) ; un Template le remplace.
        // Taille par défaut : une valeur par défaut (et non locale), pour qu'un Style ou un gabarit puisse la changer.
        WidthProperty.OverrideMetadata(typeof(Handle), new FrameworkPropertyMetadata(Size));
        HeightProperty.OverrideMetadata(typeof(Handle), new FrameworkPropertyMetadata(Size));
        BackgroundProperty.OverrideMetadata(typeof(Handle), new FrameworkPropertyMetadata(DefaultFill));
        BorderBrushProperty.OverrideMetadata(typeof(Handle), new FrameworkPropertyMetadata(Brushes.White));
        BorderThicknessProperty.OverrideMetadata(typeof(Handle), new FrameworkPropertyMetadata(new Thickness(1)));
    }

    private static Brush Freeze(Brush b)
    {
        b.Freeze();
        return b;
    }

    // Zone de clic élargie autour du point visible, sans effet sur la mesure ni sur la mise en page.
    private const double HitMargin = 6;

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null,
            new Rect(-HitMargin, -HitMargin, ActualWidth + 2 * HitMargin, ActualHeight + 2 * HitMargin));
        if (Template != null) return; // dessiné par le gabarit : seule la zone de clic ci-dessus reste
        var thickness = BorderThickness.Left;
        var pen = BorderBrush != null && thickness > 0 ? new Pen(BorderBrush, thickness) : null;
        dc.DrawEllipse(Background, pen, new Point(ActualWidth / 2, ActualHeight / 2),
            (ActualWidth - thickness) / 2, (ActualHeight - thickness) / 2);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == WidthProperty || e.Property == HeightProperty) ApplyAlignment();
        else if (e.Property == TemplateProperty || e.Property == BackgroundProperty || e.Property == BorderBrushProperty || e.Property == BorderThicknessProperty)
            InvalidateVisual();
    }

    public Handle()
    {
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
        var w = double.IsNaN(Width) ? Size : Width;
        var h = double.IsNaN(Height) ? Size : Height;
        var hasOffset = !double.IsNaN(Offset);
        switch (Position)
        {
            case Position.Left:
            case Position.Right:
                HorizontalAlignment = Position == Position.Left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
                VerticalAlignment = hasOffset ? VerticalAlignment.Top : VerticalAlignment.Center;
                Margin = new Thickness(Position == Position.Left ? -w / 2 : 0, hasOffset ? Offset - h / 2 : 0,
                    Position == Position.Right ? -w / 2 : 0, 0);
                break;
            default:
                VerticalAlignment = Position == Position.Top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
                HorizontalAlignment = hasOffset ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                Margin = new Thickness(hasOffset ? Offset - w / 2 : 0, Position == Position.Top ? -h / 2 : 0, 0,
                    Position == Position.Bottom ? -h / 2 : 0);
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
