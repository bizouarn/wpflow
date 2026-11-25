using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Wpflow.Wpf;

/// <summary>Boutons de zoom, de cadrage et de verrouillage (équivalent de <c>&lt;Controls&gt;</c>).</summary>
public sealed class FlowControls : Control
{
    private FlowView? _flow;
    private ToggleButton? _lock;
    private Button? _zoomIn, _zoomOut, _fit;
    private StackPanel? _panel;

    static FlowControls()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(FlowControls), new FrameworkPropertyMetadata(typeof(FlowControls)));
    }

    public FlowControls()
    {
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Bottom;
        Margin = new Thickness(15);
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    /// <summary>La <see cref="FlowView" /> pilotée ; par défaut, la vue qui contient ce contrôle. À renseigner pour un contrôle placé hors de la vue : <c>Flow="{Binding ElementName=Flow}"</c>.</summary>
    public static readonly DependencyProperty FlowProperty = DependencyProperty.Register(
        nameof(Flow), typeof(FlowView), typeof(FlowControls), new PropertyMetadata(null, (d, _) => ((FlowControls)d).Attach()));

    public static readonly DependencyProperty ShowZoomProperty = DependencyProperty.Register(
        nameof(ShowZoom), typeof(bool), typeof(FlowControls), new PropertyMetadata(true, (d, _) => ((FlowControls)d).ApplyOptions()));

    public static readonly DependencyProperty ShowFitViewProperty = DependencyProperty.Register(
        nameof(ShowFitView), typeof(bool), typeof(FlowControls), new PropertyMetadata(true, (d, _) => ((FlowControls)d).ApplyOptions()));

    /// <summary>Affiche le bouton de verrouillage (<c>showInteractive</c>).</summary>
    public static readonly DependencyProperty ShowInteractiveProperty = DependencyProperty.Register(
        nameof(ShowInteractive), typeof(bool), typeof(FlowControls), new PropertyMetadata(true, (d, _) => ((FlowControls)d).ApplyOptions()));

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(FlowControls),
        new PropertyMetadata(Orientation.Vertical, (d, _) => ((FlowControls)d).ApplyOptions()));

    /// <summary>Boutons supplémentaires (<c>ControlButton</c>), placés avant le verrou.</summary>
    public static readonly DependencyProperty ExtraContentProperty = DependencyProperty.Register(
        nameof(ExtraContent), typeof(object), typeof(FlowControls), new PropertyMetadata(null));

    /// <summary>Durée des transitions de zoom et de cadrage ; zéro = immédiat.</summary>
    public static readonly DependencyProperty AnimationDurationProperty = DependencyProperty.Register(
        nameof(AnimationDuration), typeof(TimeSpan), typeof(FlowControls), new PropertyMetadata(TimeSpan.Zero));

    /// <summary>Options du bouton de cadrage ; <c>null</c> = <see cref="FlowView.FitViewOptions" />.</summary>
    public static readonly DependencyProperty FitViewOptionsProperty = DependencyProperty.Register(
        nameof(FitViewOptions), typeof(FitViewOptions), typeof(FlowControls), new PropertyMetadata(null));

    public event EventHandler? ZoomedIn;
    public event EventHandler? ZoomedOut;
    public event EventHandler? FitViewClicked;

    /// <summary>Le verrou a été basculé ; l'argument est l'état « interactif » (vrai = déverrouillé).</summary>
    public event EventHandler<bool>? InteractiveChanged;

    public FlowView? Flow
    {
        get => (FlowView?)GetValue(FlowProperty);
        set => SetValue(FlowProperty, value);
    }

    public bool ShowZoom
    {
        get => (bool)GetValue(ShowZoomProperty);
        set => SetValue(ShowZoomProperty, value);
    }

    public bool ShowFitView
    {
        get => (bool)GetValue(ShowFitViewProperty);
        set => SetValue(ShowFitViewProperty, value);
    }

    public bool ShowInteractive
    {
        get => (bool)GetValue(ShowInteractiveProperty);
        set => SetValue(ShowInteractiveProperty, value);
    }

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public object? ExtraContent
    {
        get => GetValue(ExtraContentProperty);
        set => SetValue(ExtraContentProperty, value);
    }

    public TimeSpan AnimationDuration
    {
        get => (TimeSpan)GetValue(AnimationDurationProperty);
        set => SetValue(AnimationDurationProperty, value);
    }

    public FitViewOptions? FitViewOptions
    {
        get => (FitViewOptions?)GetValue(FitViewOptionsProperty);
        set => SetValue(FitViewOptionsProperty, value);
    }

    private void Attach()
    {
        Detach();
        _flow = Flow ?? (IsLoaded ? FlowView.FindAncestor<FlowView>(this) : null);
        if (_flow != null) _flow.StateChanged += OnFlowChanged;
        OnFlowChanged(null, EventArgs.Empty);
    }

    private void Detach()
    {
        if (_flow != null) _flow.StateChanged -= OnFlowChanged;
        _flow = null;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _panel = GetTemplateChild("PART_Panel") as StackPanel;
        _zoomIn = GetTemplateChild("PART_ZoomIn") as Button;
        _zoomOut = GetTemplateChild("PART_ZoomOut") as Button;
        _fit = GetTemplateChild("PART_Fit") as Button;
        _lock = GetTemplateChild("PART_Lock") as ToggleButton;
        if (_zoomIn != null)
            _zoomIn.Click += (_, _) =>
            {
                _flow?.ZoomIn(AnimationDuration);
                ZoomedIn?.Invoke(this, EventArgs.Empty);
            };
        if (_zoomOut != null)
            _zoomOut.Click += (_, _) =>
            {
                _flow?.ZoomOut(AnimationDuration);
                ZoomedOut?.Invoke(this, EventArgs.Empty);
            };
        if (_fit != null)
            _fit.Click += (_, _) =>
            {
                if (_flow != null)
                {
                    var options = FitViewOptions ?? _flow.FitViewOptions;
                    if (AnimationDuration > TimeSpan.Zero && options?.Duration == TimeSpan.Zero || options == null)
                        options = (options ?? new FitViewOptions()) with { Duration = AnimationDuration };
                    _flow.FitView(options);
                }

                FitViewClicked?.Invoke(this, EventArgs.Empty);
            };
        if (_lock != null)
        {
            _lock.Click += (_, _) =>
            {
                if (_flow != null) _flow.IsLocked = _lock.IsChecked == true;
                InteractiveChanged?.Invoke(this, _lock.IsChecked != true);
            };
        }

        ApplyOptions();
    }

    private void ApplyOptions()
    {
        if (_panel == null) return;
        _panel.Orientation = Orientation;
        var zoom = ShowZoom ? Visibility.Visible : Visibility.Collapsed;
        if (_zoomIn != null) _zoomIn.Visibility = zoom;
        if (_zoomOut != null) _zoomOut.Visibility = zoom;
        if (_fit != null) _fit.Visibility = ShowFitView ? Visibility.Visible : Visibility.Collapsed;
        if (_lock != null) _lock.Visibility = ShowInteractive ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnFlowChanged(object? sender, EventArgs e)
    {
        if (_lock != null && _flow != null) _lock.IsChecked = _flow.IsLocked;
    }
}
