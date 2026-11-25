using System.Windows;
using System.Windows.Controls;
using Wpflow.Core;

namespace Wpflow.Wpf;

/// <summary>
///     Barre d'outils d'une sélection multiple (exemple « Multi Select toolbar ») : apparaît au-dessus du rectangle qui
///     englobe les nodes sélectionnés dès que <see cref="MinSelected" /> nodes le sont. À placer dans le contenu (overlay)
///     d'un <see cref="FlowView" />, ou ailleurs avec <c>Flow="{Binding ElementName=Flow}"</c>.
/// </summary>
public sealed class SelectionToolbar : ContentControl
{
    private FlowView? _flow;

    public static readonly DependencyProperty FlowProperty = DependencyProperty.Register(
        nameof(Flow), typeof(FlowView), typeof(SelectionToolbar), new PropertyMetadata(null, (d, _) => ((SelectionToolbar)d).Attach()));

    /// <summary>Nombre minimal de nodes sélectionnés pour afficher la barre.</summary>
    public static readonly DependencyProperty MinSelectedProperty = DependencyProperty.Register(
        nameof(MinSelected), typeof(int), typeof(SelectionToolbar), new PropertyMetadata(2, (d, _) => ((SelectionToolbar)d).Update()));

    /// <summary>Distance entre le haut de la sélection et la barre.</summary>
    public static readonly DependencyProperty OffsetProperty = DependencyProperty.Register(
        nameof(Offset), typeof(double), typeof(SelectionToolbar), new PropertyMetadata(10.0, (d, _) => ((SelectionToolbar)d).Update()));

    /// <summary>Nodes concernés (<c>nodeId</c> en tableau) ; <c>null</c> = les nodes sélectionnés. Avec une liste, la barre s'affiche dès que l'un d'eux est sélectionné.</summary>
    public static readonly DependencyProperty NodeIdsProperty = DependencyProperty.Register(
        nameof(NodeIds), typeof(IEnumerable<string>), typeof(SelectionToolbar), new PropertyMetadata(null, (d, _) => ((SelectionToolbar)d).Update()));

    /// <summary>Force la visibilité ; <c>null</c> = automatique.</summary>
    public static readonly DependencyProperty IsToolbarVisibleProperty = DependencyProperty.Register(
        nameof(IsToolbarVisible), typeof(bool?), typeof(SelectionToolbar), new PropertyMetadata(null, (d, _) => ((SelectionToolbar)d).Update()));

    static SelectionToolbar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SelectionToolbar), new FrameworkPropertyMetadata(typeof(SelectionToolbar)));
    }

    public SelectionToolbar()
    {
        Focusable = false;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Visibility = Visibility.Collapsed;
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        SizeChanged += (_, _) => Update();
    }

    public FlowView? Flow
    {
        get => (FlowView?)GetValue(FlowProperty);
        set => SetValue(FlowProperty, value);
    }

    public IEnumerable<string>? NodeIds
    {
        get => (IEnumerable<string>?)GetValue(NodeIdsProperty);
        set => SetValue(NodeIdsProperty, value);
    }

    public bool? IsToolbarVisible
    {
        get => (bool?)GetValue(IsToolbarVisibleProperty);
        set => SetValue(IsToolbarVisibleProperty, value);
    }

    public int MinSelected
    {
        get => (int)GetValue(MinSelectedProperty);
        set => SetValue(MinSelectedProperty, value);
    }

    public double Offset
    {
        get => (double)GetValue(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    private void Attach()
    {
        Detach();
        _flow = Flow ?? (IsLoaded ? FlowView.FindAncestor<FlowView>(this) : null);
        if (_flow != null) _flow.StateChanged += OnFlowChanged;
        Update();
    }

    private void Detach()
    {
        if (_flow != null) _flow.StateChanged -= OnFlowChanged;
        _flow = null;
    }

    private void OnFlowChanged(object? sender, EventArgs e) => Update();

    private void Update()
    {
        if (_flow == null) return;
        var ids = NodeIds?.ToHashSet();
        var nodes = (ids == null ? _flow.AbsoluteNodes.Where(n => n.Selected) : _flow.AbsoluteNodes.Where(n => ids.Contains(n.Id))).ToList();
        var show = IsToolbarVisible ?? (ids == null ? nodes.Count >= MinSelected : nodes.Any(n => n.Selected));
        if (!show || GraphUtils.GetNodesBounds(nodes) is not { } bounds)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        var topCenter = ViewportMath.FlowToScreen(_flow.Viewport, new XYPosition(bounds.X + bounds.Width / 2, bounds.Y));
        Margin = new Thickness(topCenter.X - ActualWidth / 2, Math.Max(0, topCenter.Y - ActualHeight - Offset), 0, 0);
        Visibility = Visibility.Visible;
    }
}
