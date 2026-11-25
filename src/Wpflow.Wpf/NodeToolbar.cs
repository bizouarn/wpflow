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
/// <summary>Alignement de la barre le long du côté du node (<c>align</c>).</summary>
public enum ToolbarAlign
{
    Start,
    Center,
    End
}

public sealed class NodeToolbar : ContentControl
{
    public static readonly DependencyProperty ToolbarPositionProperty = DependencyProperty.Register(
        nameof(ToolbarPosition), typeof(Position), typeof(NodeToolbar),
        new PropertyMetadata(Position.Top, (d, _) => ((NodeToolbar)d).UpdatePlacement()));

    /// <summary>Distance entre le node et la barre.</summary>
    public static readonly DependencyProperty OffsetProperty = DependencyProperty.Register(
        nameof(Offset), typeof(double), typeof(NodeToolbar),
        new PropertyMetadata(10.0, (d, _) => ((NodeToolbar)d).UpdatePlacement()));

    public static readonly DependencyProperty AlignProperty = DependencyProperty.Register(
        nameof(Align), typeof(ToolbarAlign), typeof(NodeToolbar),
        new PropertyMetadata(ToolbarAlign.Center, (d, _) => ((NodeToolbar)d).UpdatePlacement()));

    /// <summary>Force la visibilité (<c>isVisible</c>) ; <c>null</c> = visible quand le node est sélectionné.</summary>
    public static readonly DependencyProperty IsToolbarVisibleProperty = DependencyProperty.Register(
        nameof(IsToolbarVisible), typeof(bool?), typeof(NodeToolbar),
        new PropertyMetadata(null, (d, _) => ((NodeToolbar)d).UpdateVisibility()));

    static NodeToolbar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NodeToolbar), new FrameworkPropertyMetadata(typeof(NodeToolbar)));
    }

    public NodeToolbar()
    {
        Focusable = false;
        UpdateVisibility();
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

    public ToolbarAlign Align
    {
        get => (ToolbarAlign)GetValue(AlignProperty);
        set => SetValue(AlignProperty, value);
    }

    public bool? IsToolbarVisible
    {
        get => (bool?)GetValue(IsToolbarVisibleProperty);
        set => SetValue(IsToolbarVisibleProperty, value);
    }

    // Visible quand le node est sélectionné, sauf si IsToolbarVisible la force.
    private void UpdateVisibility()
    {
        if (IsToolbarVisible is { } forced)
        {
            BindingOperations.ClearBinding(this, VisibilityProperty);
            Visibility = forced ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        SetBinding(VisibilityProperty, new Binding
        {
            Path = new PropertyPath(NodeHost.IsSelectedProperty),
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(NodeHost), 1),
            Converter = new BooleanToVisibilityConverter()
        });
    }

    private void UpdatePlacement()
    {
        var gap = Offset;
        var h = Align switch
        {
            ToolbarAlign.Start => HorizontalAlignment.Left,
            ToolbarAlign.End => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Center
        };
        var v = Align switch
        {
            ToolbarAlign.Start => VerticalAlignment.Top,
            ToolbarAlign.End => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Center
        };
        switch (ToolbarPosition)
        {
            case Position.Top:
                (HorizontalAlignment, VerticalAlignment) = (h, VerticalAlignment.Top);
                RenderTransform = new TranslateTransform(0, -(ActualHeight + gap));
                break;
            case Position.Bottom:
                (HorizontalAlignment, VerticalAlignment) = (h, VerticalAlignment.Bottom);
                RenderTransform = new TranslateTransform(0, ActualHeight + gap);
                break;
            case Position.Left:
                (HorizontalAlignment, VerticalAlignment) = (HorizontalAlignment.Left, v);
                RenderTransform = new TranslateTransform(-(ActualWidth + gap), 0);
                break;
            default:
                (HorizontalAlignment, VerticalAlignment) = (HorizontalAlignment.Right, v);
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
