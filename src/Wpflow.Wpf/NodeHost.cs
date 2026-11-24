using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Wpflow.Core;

namespace Wpflow.Wpf;

/// <summary>
///     Conteneur visuel d'un node. Le contenu est soit construit par défaut (selon <see cref="Node.Type" />),
///     soit issu d'un <c>DataTemplate</c> de clé <c>wpflow-node-{type}</c> (DataContext = le <see cref="Node" />).
/// </summary>
public sealed class NodeHost : ContentControl
{

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(NodeHost), new PropertyMetadata(false));

    /// <summary>Affiche un contour autour des nodes à template personnalisé quand ils sont sélectionnés.</summary>
    public static readonly DependencyProperty ShowOutlineProperty = DependencyProperty.Register(
        nameof(ShowOutline), typeof(bool), typeof(NodeHost), new PropertyMetadata(false));

    public static readonly DependencyProperty OutlineCornerRadiusProperty = DependencyProperty.Register(
        nameof(OutlineCornerRadius), typeof(CornerRadius), typeof(NodeHost), new PropertyMetadata(new CornerRadius(8)));

    private readonly FlowView _flow;
    private Border? _defaultBorder;
    private bool _measureQueued;
    private string? _contentKind;

    static NodeHost()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NodeHost), new FrameworkPropertyMetadata(typeof(NodeHost)));
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        private set => SetValue(IsSelectedProperty, value);
    }

    public bool ShowOutline
    {
        get => (bool)GetValue(ShowOutlineProperty);
        set => SetValue(ShowOutlineProperty, value);
    }

    public CornerRadius OutlineCornerRadius
    {
        get => (CornerRadius)GetValue(OutlineCornerRadiusProperty);
        set => SetValue(OutlineCornerRadiusProperty, value);
    }

    internal NodeHost(FlowView flow)
    {
        _flow = flow;
        Focusable = false;
        SizeChanged += (_, _) => RequestMeasure();
        Loaded += (_, _) => RequestMeasure();
    }

    public Node? Node { get; private set; }

    /// <summary>Handles mesurés, relatifs au coin du node.</summary>
    internal IReadOnlyList<HandleInfo> Handles { get; private set; } = Array.Empty<HandleInfo>();

    private static T Frozen<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }

    /// <summary>Position absolue (coordonnées flow) actuellement appliquée ; diffère de <see cref="Node.Position" /> pour un enfant.</summary>
    internal XYPosition AbsolutePosition { get; private set; }

    internal void SetNode(Node node, XYPosition absolute, int depth)
    {
        var typeChanged = Node?.Type != node.Type;
        Node = node;
        AbsolutePosition = absolute;
        Canvas.SetLeft(this, absolute.X);
        Canvas.SetTop(this, absolute.Y);
        // Un parent est sous ses enfants ; un node sélectionné passe devant (sauf les groupes, qui masqueraient leurs enfants).
        Panel.SetZIndex(this, depth + (node.Selected && node.Type != "group" ? 1000 : 0));
        IsSelected = node.Selected;

        // Taille explicite (redimensionnement) : null => taille naturelle du contenu.
        Width = node.FixedWidth ?? double.NaN;
        Height = node.FixedHeight ?? double.NaN;

        if (typeChanged || _contentKind == null) BuildContent(node);
        else if (_contentKind == "template") Content = node; // met à jour le DataContext
        else UpdateDefaultVisual(node);
        ShowOutline = _contentKind == "template";
    }

    /// <summary>Réapplique les couleurs du thème (changement de <see cref="ColorMode" />).</summary>
    internal void RefreshTheme()
    {
        if (Node != null) UpdateDefaultVisual(Node);
    }

    private void BuildContent(Node node)
    {
        if (_flow.TryFindResource("wpflow-node-" + node.Type) is DataTemplate template)
        {
            _contentKind = "template";
            ContentTemplate = template;
            Content = node;
            return;
        }

        _contentKind = "default";
        ContentTemplate = null;
        Content = node.Type == "group" ? BuildGroup(node) : BuildDefault(node);
        UpdateDefaultVisual(node);
    }

    private UIElement BuildDefault(Node node)
    {
        // Les handles sont des frères de la bordure (et non ses enfants) : sinon le padding les décalerait vers l'intérieur.
        var root = new Grid { Width = 150 };
        _defaultText = new TextBlock
        {
            Text = node.Data?.ToString() ?? node.Id,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        };
        _defaultBorder = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(10),
            Child = _defaultText
        };
        root.Children.Add(_defaultBorder);

        // Comme React Flow : "input" = source seule, "output" = cible seule, "default" = les deux.
        if (node.Type != "input")
            root.Children.Add(new Handle { Type = HandleType.Target, Position = Position.Top });
        if (node.Type != "output")
            root.Children.Add(new Handle { Type = HandleType.Source, Position = Position.Bottom });

        return root;
    }

    /// <summary>Groupe (<c>type: "group"</c>) : zone translucide à bord fin, sans handles, qui contient d'autres nodes.</summary>
    private UIElement BuildGroup(Node node)
    {
        _defaultText = new TextBlock
        {
            Text = node.Data?.ToString() ?? "",
            FontSize = 11,
            Margin = new Thickness(8, 4, 8, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        _defaultBorder = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            MinWidth = 120,
            MinHeight = 80,
            Child = _defaultText
        };
        return _defaultBorder;
    }

    private TextBlock? _defaultText;

    private void UpdateDefaultVisual(Node node)
    {
        if (_defaultBorder == null) return;
        var group = node.Type == "group";
        _defaultBorder.Background = group
            ? _flow.ThemeBrush("Flow.GroupBackground")
            : _flow.ThemeBrush("Flow.NodeBackground");
        _defaultBorder.BorderBrush = node.Selected
            ? _flow.ThemeBrush("Flow.NodeBorderSelected")
            : _flow.ThemeBrush("Flow.NodeBorder");
        if (_defaultText != null) _defaultText.Foreground = _flow.ThemeBrush("Flow.NodeText");
    }

    internal void RequestSelect(bool additive)
    {
        if (Node != null) _flow.SelectNode(Node.Id, additive);
    }

    internal void RequestDeselect()
    {
        if (Node != null) _flow.ApplyNodeChanges([new NodeSelectChange(Node.Id, false)]);
    }

    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        new NodeHostAutomationPeer(this);

    /// <summary>Planifie (une seule fois par passe de layout) la mesure du node et de ses handles.</summary>
    internal void RequestMeasure()
    {
        if (_measureQueued) return;
        _measureQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            _measureQueued = false;
            MeasureNow();
        });
    }

    private void MeasureNow()
    {
        if (Node == null || !IsLoaded || ActualWidth <= 0) return;

        var handles = new List<HandleInfo>();
        foreach (var h in FlowView.FindDescendants<Handle>(this))
            if (h.Measure(this) is { } info)
                handles.Add(info);
        Handles = handles;
        _flow.OnNodeMeasured(this, ActualWidth, ActualHeight);
    }
}
