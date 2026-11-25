using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpflow.Core;

namespace Wpflow.Wpf;

/// <summary>
///     Barre d'outils attachée à un edge (équivalent de <c>&lt;EdgeToolbar&gt;</c>) : visible quand l'edge est sélectionné,
///     centrée sur son point de label. À placer dans un template de label (<c>EdgeLabelTemplate</c>,
///     <c>wpflow-edge-label-{type}</c>) ou d'edge (<c>wpflow-edge-template-{type}</c>) : le <c>DataContext</c> est l'
///     <see cref="Edge" /> (ou un <see cref="EdgeRenderContext" />).
/// </summary>
public sealed class EdgeToolbar : ContentControl
{
    /// <summary>Force la visibilité ; <c>null</c> = visible quand l'edge est sélectionné.</summary>
    public static readonly DependencyProperty IsToolbarVisibleProperty = DependencyProperty.Register(
        nameof(IsToolbarVisible), typeof(bool?), typeof(EdgeToolbar),
        new PropertyMetadata(null, (d, _) => ((EdgeToolbar)d).UpdateVisibility()));

    public static readonly DependencyProperty ToolbarPositionProperty = DependencyProperty.Register(
        nameof(ToolbarPosition), typeof(Position), typeof(EdgeToolbar),
        new PropertyMetadata(Position.Top, (d, _) => ((EdgeToolbar)d).UpdatePlacement()));

    /// <summary>Distance entre le point du label et la barre.</summary>
    public static readonly DependencyProperty OffsetProperty = DependencyProperty.Register(
        nameof(Offset), typeof(double), typeof(EdgeToolbar),
        new PropertyMetadata(10.0, (d, _) => ((EdgeToolbar)d).UpdatePlacement()));

    static EdgeToolbar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(EdgeToolbar), new FrameworkPropertyMetadata(typeof(EdgeToolbar)));
    }

    public EdgeToolbar()
    {
        Focusable = false;
        Panel.SetZIndex(this, 10);
        DataContextChanged += (_, _) => UpdateVisibility();
        SizeChanged += (_, _) => UpdatePlacement();
        UpdateVisibility();
        UpdatePlacement();
    }

    public bool? IsToolbarVisible
    {
        get => (bool?)GetValue(IsToolbarVisibleProperty);
        set => SetValue(IsToolbarVisibleProperty, value);
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

    private void UpdateVisibility()
    {
        var selected = DataContext switch
        {
            Edge e => e.Selected,
            EdgeRenderContext c => c.IsSelected,
            _ => false
        };
        Visibility = IsToolbarVisible ?? selected ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdatePlacement()
    {
        var gap = Offset;
        (HorizontalAlignment, VerticalAlignment, RenderTransform) = ToolbarPosition switch
        {
            Position.Top => (HorizontalAlignment.Center, VerticalAlignment.Top, (Transform)new TranslateTransform(0, -(ActualHeight + gap))),
            Position.Bottom => (HorizontalAlignment.Center, VerticalAlignment.Bottom, new TranslateTransform(0, ActualHeight + gap)),
            Position.Left => (HorizontalAlignment.Left, VerticalAlignment.Center, new TranslateTransform(-(ActualWidth + gap), 0)),
            _ => (HorizontalAlignment.Right, VerticalAlignment.Center, new TranslateTransform(ActualWidth + gap, 0))
        };
    }
}
