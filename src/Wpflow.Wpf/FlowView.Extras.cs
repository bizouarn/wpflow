using System.Windows;
using System.Windows.Controls;
using Wpflow.Core;

namespace Wpflow.Wpf;

// Fond (réglages avancés) et couche de portail (contenu positionné en coordonnées flow).
public partial class FlowView
{
    /// <summary>Rayon d'un point ou demi-longueur d'une croix du fond ; <c>null</c> = valeur par défaut (<c>size</c>).</summary>
    public static readonly DependencyProperty BackgroundSizeProperty = DependencyProperty.Register(
        nameof(BackgroundSize), typeof(double?), typeof(FlowView),
        new PropertyMetadata(null, (d, _) => ((FlowView)d).ConfigureBackground()));

    /// <summary>Épaisseur des lignes et des croix du fond (<c>lineWidth</c>).</summary>
    public static readonly DependencyProperty BackgroundLineWidthProperty = DependencyProperty.Register(
        nameof(BackgroundLineWidth), typeof(double), typeof(FlowView),
        new PropertyMetadata(1.0, (d, _) => ((FlowView)d).ConfigureBackground()));

    /// <summary>Décalage du fond, en unités flow (<c>offset</c>).</summary>
    public static readonly DependencyProperty BackgroundOffsetProperty = DependencyProperty.Register(
        nameof(BackgroundOffset), typeof(double), typeof(FlowView),
        new PropertyMetadata(0.0, (d, _) => ((FlowView)d).ConfigureBackground()));

    /// <summary>Motifs superposés au fond principal (plusieurs <c>&lt;Background id="…"&gt;</c>).</summary>
    public static readonly DependencyProperty AdditionalBackgroundsProperty = DependencyProperty.Register(
        nameof(AdditionalBackgrounds), typeof(IReadOnlyList<BackgroundLayer>), typeof(FlowView),
        new PropertyMetadata(null, (d, _) => ((FlowView)d).ConfigureBackground()));

    public double? BackgroundSize
    {
        get => (double?)GetValue(BackgroundSizeProperty);
        set => SetValue(BackgroundSizeProperty, value);
    }

    public double BackgroundLineWidth
    {
        get => (double)GetValue(BackgroundLineWidthProperty);
        set => SetValue(BackgroundLineWidthProperty, value);
    }

    public double BackgroundOffset
    {
        get => (double)GetValue(BackgroundOffsetProperty);
        set => SetValue(BackgroundOffsetProperty, value);
    }

    public IReadOnlyList<BackgroundLayer>? AdditionalBackgrounds
    {
        get => (IReadOnlyList<BackgroundLayer>?)GetValue(AdditionalBackgroundsProperty);
        set => SetValue(AdditionalBackgroundsProperty, value);
    }

    private void ConfigureBackground()
    {
        if (_background == null) return;
        var themeDots = ThemeBrush("Flow.Dots");
        var layers = new List<(BackgroundLayer, System.Windows.Media.Brush)>
        {
            (new BackgroundLayer(BackgroundVariant, BackgroundGap, null, BackgroundSize, BackgroundLineWidth, BackgroundOffset),
                BackgroundColor ?? themeDots)
        };
        if (AdditionalBackgrounds is { } extra) layers.AddRange(extra.Select(l => (l, l.Color ?? themeDots)));
        _background.Configure(layers);
    }

    /// <summary>Texte UI Automation des nodes sans <see cref="Node.AriaLabel" /> (<c>ariaLabelConfig</c>) ; <c>null</c> = nom par défaut.</summary>
    public static readonly DependencyProperty NodeAriaLabelProviderProperty = DependencyProperty.Register(
        nameof(NodeAriaLabelProvider), typeof(Func<Node, string?>), typeof(FlowView),
        new PropertyMetadata(null, (d, _) => ((FlowView)d).OnNodesChanged()));

    public Func<Node, string?>? NodeAriaLabelProvider
    {
        get => (Func<Node, string?>?)GetValue(NodeAriaLabelProviderProperty);
        set => SetValue(NodeAriaLabelProviderProperty, value);
    }

    /// <summary>Texte annoncé par les lecteurs d'écran (notification UI Automation) ; sans effet s'il n'y en a pas.</summary>
    internal void Announce(string text) =>
        System.Windows.Automation.Peers.UIElementAutomationPeer.FromElement(this)?.RaiseNotificationEvent(
            System.Windows.Automation.AutomationNotificationKind.Other,
            System.Windows.Automation.AutomationNotificationProcessing.MostRecent, text, "wpflow.selection");

    #region Portail

    private Canvas? _portal;

    /// <summary>
    ///     Couche dont les enfants suivent le viewport, au-dessus des nodes (<c>ViewportPortal</c>) : placez un élément avec
    ///     <c>Canvas.Left</c> / <c>Canvas.Top</c> en coordonnées flow, ou utilisez <see cref="AddToPortal" />.
    ///     <c>null</c> tant que le gabarit n'est pas appliqué.
    /// </summary>
    public Canvas? PortalLayer => _portal;

    /// <summary>Place <paramref name="element" /> aux coordonnées flow données ; il se déplace et se zoome avec le graphe.</summary>
    public void AddToPortal(UIElement element, double x, double y)
    {
        if (_portal == null) return;
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        if (!_portal.Children.Contains(element)) _portal.Children.Add(element);
    }

    public void RemoveFromPortal(UIElement element) => _portal?.Children.Remove(element);

    #endregion
}
