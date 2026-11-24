using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Wpflow.Core;

namespace Wpflow.Wpf;

/// <summary>
///     Barre d'outils attachée à un node (équivalent de <c>&lt;NodeToolbar&gt;</c>) : apparaît à côté du node quand
///     il est sélectionné. À placer dans le template du node (dans le <c>Grid</c> racine).
/// </summary>
public sealed class NodeToolbar : ContentControl
{
    public static readonly DependencyProperty ToolbarPositionProperty = DependencyProperty.Register(
        nameof(ToolbarPosition), typeof(Position), typeof(NodeToolbar),
        new PropertyMetadata(Position.Top, (d, _) => ((NodeToolbar)d).UpdatePlacement()));

    /// <summary>Distance entre le node et la barre.</summary>
    public static readonly DependencyProperty OffsetProperty = DependencyProperty.Register(
        nameof(Offset), typeof(double), typeof(NodeToolbar),
        new PropertyMetadata(10.0, (d, _) => ((NodeToolbar)d).UpdatePlacement()));

    static NodeToolbar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NodeToolbar), new FrameworkPropertyMetadata(typeof(NodeToolbar)));
    }

    public NodeToolbar()
    {
        Focusable = false;
        SetBinding(VisibilityProperty, new Binding
        {
            Path = new PropertyPath(NodeHost.IsSelectedProperty),
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(NodeHost), 1),
            Converter = new BooleanToVisibilityConverter()
        });
        SizeChanged += (_, _) => UpdatePlacement();
        UpdatePlacement();
    }

    public Position ToolbarPosition
    {
        get => (Position)GetValue(ToolbarPositionProperty);
        set => SetValue(ToolbarPositionProperty, value);
    }

    public double Offset
    {
        get => (double)GetValue(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    private void UpdatePlacement()
    {
        var gap = Offset;
        switch (ToolbarPosition)
        {
            case Position.Top:
                HorizontalAlignment = HorizontalAlignment.Center;
                VerticalAlignment = VerticalAlignment.Top;
                RenderTransform = new TranslateTransform(0, -(ActualHeight + gap));
                break;
            case Position.Bottom:
                HorizontalAlignment = HorizontalAlignment.Center;
                VerticalAlignment = VerticalAlignment.Bottom;
                RenderTransform = new TranslateTransform(0, ActualHeight + gap);
                break;
            case Position.Left:
                HorizontalAlignment = HorizontalAlignment.Left;
                VerticalAlignment = VerticalAlignment.Center;
                RenderTransform = new TranslateTransform(-(ActualWidth + gap), 0);
                break;
            default:
                HorizontalAlignment = HorizontalAlignment.Right;
                VerticalAlignment = VerticalAlignment.Center;
                RenderTransform = new TranslateTransform(ActualWidth + gap, 0);
                break;
        }
    }
}

public enum PanelPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    CenterLeft,
    CenterRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

/// <summary>Ancre son contenu à un coin ou un bord de la surface (équivalent de <c>&lt;Panel position="…"&gt;</c>), dans l'overlay d'un <see cref="FlowView" />.</summary>
public sealed class FlowPanel : ContentControl
{
    public static readonly DependencyProperty PanelPositionProperty = DependencyProperty.Register(
        nameof(PanelPosition), typeof(PanelPosition), typeof(FlowPanel),
        new PropertyMetadata(PanelPosition.TopLeft, (d, _) => ((FlowPanel)d).Apply()));

    public FlowPanel()
    {
        Margin = new Thickness(15);
        Focusable = false;
        Apply();
    }

    public PanelPosition PanelPosition
    {
        get => (PanelPosition)GetValue(PanelPositionProperty);
        set => SetValue(PanelPositionProperty, value);
    }

    private void Apply()
    {
        (HorizontalAlignment, VerticalAlignment) = PanelPosition switch
        {
            PanelPosition.TopLeft => (HorizontalAlignment.Left, VerticalAlignment.Top),
            PanelPosition.TopCenter => (HorizontalAlignment.Center, VerticalAlignment.Top),
            PanelPosition.TopRight => (HorizontalAlignment.Right, VerticalAlignment.Top),
            PanelPosition.CenterLeft => (HorizontalAlignment.Left, VerticalAlignment.Center),
            PanelPosition.CenterRight => (HorizontalAlignment.Right, VerticalAlignment.Center),
            PanelPosition.BottomLeft => (HorizontalAlignment.Left, VerticalAlignment.Bottom),
            PanelPosition.BottomCenter => (HorizontalAlignment.Center, VerticalAlignment.Bottom),
            _ => (HorizontalAlignment.Right, VerticalAlignment.Bottom)
        };
    }
}
