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

    /// <summary>Le handle accepte des connexions (début ou fin) ; en plus de <see cref="Node.Connectable" />.</summary>
    public static readonly DependencyProperty IsConnectableProperty = DependencyProperty.Register(
        nameof(IsConnectable), typeof(bool), typeof(Handle), new PropertyMetadata(true, OnLayoutChanged));

    /// <summary>Une connexion peut partir de ce handle.</summary>
    public static readonly DependencyProperty IsConnectableStartProperty = DependencyProperty.Register(
        nameof(IsConnectableStart), typeof(bool), typeof(Handle), new PropertyMetadata(true, OnLayoutChanged));

    /// <summary>Une connexion peut aboutir à ce handle.</summary>
    public static readonly DependencyProperty IsConnectableEndProperty = DependencyProperty.Register(
        nameof(IsConnectableEnd), typeof(bool), typeof(Handle), new PropertyMetadata(true, OnLayoutChanged));

    /// <summary>Validateur propre à ce handle, appliqué quand il est l'une des extrémités (en plus de celui de la vue).</summary>
    public static readonly DependencyProperty IsValidConnectionProperty = DependencyProperty.Register(
        nameof(IsValidConnection), typeof(Func<Connection, bool>), typeof(Handle), new PropertyMetadata(null, OnLayoutChanged));

    /// <summary>État pendant qu'un fil est tiré (origine, cible valide ou refusée) ; piloté par la vue, utilisable dans un Template.</summary>
    public static readonly DependencyProperty ConnectionStatusProperty = DependencyProperty.Register(
        nameof(ConnectionStatus), typeof(HandleConnectionStatus), typeof(Handle),
        new FrameworkPropertyMetadata(HandleConnectionStatus.None, FrameworkPropertyMetadataOptions.AffectsRender));

    internal const double Size = 8;

    private static readonly Brush DefaultFill = Freeze(new SolidColorBrush(Color.FromRgb(0x9c, 0xa3, 0xaf)));
    private static readonly Brush ValidFill = Freeze(new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e)));
    private static readonly Brush InvalidFill = Freeze(new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44)));
    private static readonly Brush OriginFill = Freeze(new SolidColorBrush(Color.FromRgb(0x33, 0x67, 0xd9)));

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
    private Pen? _borderPen;

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null,
            new Rect(-HitMargin, -HitMargin, ActualWidth + 2 * HitMargin, ActualHeight + 2 * HitMargin));
        if (Template != null) return; // dessiné par le gabarit : seule la zone de clic ci-dessus reste
        var thickness = BorderThickness.Left;
        Pen? pen = null;
        if (BorderBrush != null && thickness > 0)
        {
            pen = _borderPen;
            if (pen == null)
            {
                pen = new Pen(BorderBrush, thickness);
                if (pen.CanFreeze) pen.Freeze();
                _borderPen = pen;
            }
        }
        var fill = ConnectionStatus switch
        {
            HandleConnectionStatus.Valid => ValidFill,
            HandleConnectionStatus.Invalid => InvalidFill,
            HandleConnectionStatus.Origin => OriginFill,
            _ => Background
        };
        dc.DrawEllipse(fill, pen, new Point(ActualWidth / 2, ActualHeight / 2),
            (ActualWidth - thickness) / 2, (ActualHeight - thickness) / 2);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == WidthProperty || e.Property == HeightProperty) ApplyAlignment();
        else if (e.Property == BorderBrushProperty || e.Property == BorderThicknessProperty)
        {
            _borderPen = null;
            InvalidateVisual();
        }
        else if (e.Property == TemplateProperty || e.Property == BackgroundProperty)
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

    public HandleConnectionStatus ConnectionStatus
    {
        get => (HandleConnectionStatus)GetValue(ConnectionStatusProperty);
        set => SetValue(ConnectionStatusProperty, value);
    }

    /// <summary>Une connexion vient d aboutir à ce handle ou d'en partir (<c>onConnect</c> d'un handle).</summary>
    public event EventHandler<Connection>? Connected;

    internal void RaiseConnected(Connection connection) => Connected?.Invoke(this, connection);

    public bool IsConnectable
    {
        get => (bool)GetValue(IsConnectableProperty);
        set => SetValue(IsConnectableProperty, value);
    }

    public bool IsConnectableStart
    {
        get => (bool)GetValue(IsConnectableStartProperty);
        set => SetValue(IsConnectableStartProperty, value);
    }

    public bool IsConnectableEnd
    {
        get => (bool)GetValue(IsConnectableEndProperty);
        set => SetValue(IsConnectableEndProperty, value);
    }

    public Func<Connection, bool>? IsValidConnection
    {
        get => (Func<Connection, bool>?)GetValue(IsValidConnectionProperty);
        set => SetValue(IsValidConnectionProperty, value);
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
            return new HandleInfo(HandleId, Type, Position, b.X, b.Y, b.Width, b.Height, IsConnectable,
                IsConnectableStart, IsConnectableEnd, IsValidConnection);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
