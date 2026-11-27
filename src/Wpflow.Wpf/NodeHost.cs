using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Wpflow.Core;
using Rect = System.Windows.Rect;

namespace Wpflow.Wpf;

/// <summary>
///     Conteneur visuel d'un node. Le contenu est soit construit par défaut (selon <see cref="Node.Type" />),
///     soit issu d'un <c>DataTemplate</c> de clé <c>wpflow-node-{type}</c> (DataContext = le <see cref="Node" />).
/// </summary>
public sealed class NodeHost : ContentControl
{

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(NodeHost),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Affiche un contour autour des nodes à template personnalisé quand ils sont sélectionnés.</summary>
    public static readonly DependencyProperty ShowOutlineProperty = DependencyProperty.Register(
        nameof(ShowOutline), typeof(bool), typeof(NodeHost),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OutlineCornerRadiusProperty = DependencyProperty.Register(
        nameof(OutlineCornerRadius), typeof(CornerRadius), typeof(NodeHost),
        new FrameworkPropertyMetadata(new CornerRadius(0), FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Marque l'élément du template qui sert de poignée de déplacement ; la valeur doit égaler <see cref="Node.DragHandle" />.</summary>
    public static readonly DependencyProperty DragHandleProperty = DependencyProperty.RegisterAttached(
        "DragHandle", typeof(string), typeof(NodeHost), new PropertyMetadata(null));

    public static string? GetDragHandle(DependencyObject d) => (string?)d.GetValue(DragHandleProperty);

    public static void SetDragHandle(DependencyObject d, string? value) => d.SetValue(DragHandleProperty, value);

    private readonly FlowView _flow;
    private Border? _defaultBorder;
    private bool _measureQueued;
    private string? _contentKind;
    private string? _lastAria;

    // Le contour de sélection est dessiné ici plutôt que par un gabarit : pas d'élément visuel supplémentaire par node.
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (!IsSelected || !ShowOutline || ActualWidth <= 0) return;
        double gap = _flow.SelectionOutlineGap, thickness = _flow.SelectionOutlineThickness;
        var pen = _flow.OutlinePen(thickness);
        var inset = gap - thickness / 2;
        var radius = OutlineCornerRadius.TopLeft > 0 ? OutlineCornerRadius.TopLeft + gap : 0; // angles droits par défaut
        dc.DrawRoundedRectangle(null, pen, new Rect(-inset, -inset, ActualWidth + 2 * inset, ActualHeight + 2 * inset),
            radius, radius);
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

    /// <summary>Position absolue (coordonnées flow) actuellement appliquée ; diffère de <see cref="Node.Position" /> pour un enfant.</summary>
    internal XYPosition AbsolutePosition { get; private set; }

    /// <summary>Vrai si le node a été élevé parce qu'un de ses descendants est sélectionné (<see cref="FlowZIndexMode.Auto" />).</summary>
    internal bool Elevated { get; private set; }

    internal void SetNode(Node node, XYPosition absolute, int depth, bool elevated = false)
    {
        var typeChanged = Node?.Type != node.Type;
        Node = node;
        AbsolutePosition = absolute;
        Canvas.SetLeft(this, absolute.X);
        Canvas.SetTop(this, absolute.Y);
        // Un parent est sous ses enfants ; un node sélectionné passe devant (sauf les groupes, qui masqueraient leurs enfants).
        Elevated = elevated;
        Panel.SetZIndex(this, NodeZOrder.Compute(node, depth, _flow.ZIndexMode, _flow.ElevateNodesOnSelect, elevated));
        IsSelected = node.Selected;
        if (_rotation != node.Rotation)
        {
            _rotation = node.Rotation;
            RenderTransformOrigin = new Point(0.5, 0.5);
            RenderTransform = node.Rotation == 0 ? Transform.Identity : new RotateTransform(node.Rotation);
        }

        var aria = node.AriaLabel ?? _flow.NodeAriaLabelProvider?.Invoke(node);
        if (aria != _lastAria)
        {
            _lastAria = aria;
            if (aria != null) System.Windows.Automation.AutomationProperties.SetName(this, aria);
            else ClearValue(System.Windows.Automation.AutomationProperties.NameProperty);
        }

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
        InvalidateVisual();
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
            CornerRadius = new CornerRadius(0),
            Padding = new Thickness(10),
            Child = _defaultText
        };
        root.Children.Add(_defaultBorder);

        // Comme React Flow : "input" = source seule, "output" = cible seule, "default" = les deux.
        _defaultTarget = _defaultSource = null;
        if (node.Type != "input")
            root.Children.Add(_defaultTarget = new Handle { Type = HandleType.Target, Position = node.TargetPosition ?? Position.Top });
        if (node.Type != "output")
            root.Children.Add(_defaultSource = new Handle { Type = HandleType.Source, Position = node.SourcePosition ?? Position.Bottom });

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
            CornerRadius = new CornerRadius(0),
            MinWidth = 120,
            MinHeight = 80,
            Child = _defaultText
        };
        return _defaultBorder;
    }

    private double _rotation;
    private TextBlock? _defaultText;
    private Handle? _defaultTarget, _defaultSource; // handles du node par défaut : leur côté suit Node.Target/SourcePosition

    private void UpdateDefaultVisual(Node node)
    {
        if (_defaultBorder == null) return;
        if (_defaultTarget != null) _defaultTarget.Position = node.TargetPosition ?? Position.Top;
        if (_defaultSource != null) _defaultSource.Position = node.SourcePosition ?? Position.Bottom;
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
