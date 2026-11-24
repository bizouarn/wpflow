using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Wpflow.Wpf;

/// <summary>Boutons de zoom, de cadrage et de verrouillage (équivalent de <c>&lt;Controls&gt;</c>).</summary>
public sealed class FlowControls : Control
{
    private FlowView? _flow;
    private ToggleButton? _lock;

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

    public FlowView? Flow
    {
        get => (FlowView?)GetValue(FlowProperty);
        set => SetValue(FlowProperty, value);
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
        if (GetTemplateChild("PART_ZoomIn") is Button zin) zin.Click += (_, _) => _flow?.ZoomIn();
        if (GetTemplateChild("PART_ZoomOut") is Button zout) zout.Click += (_, _) => _flow?.ZoomOut();
        if (GetTemplateChild("PART_Fit") is Button fit) fit.Click += (_, _) => _flow?.FitView();
        _lock = GetTemplateChild("PART_Lock") as ToggleButton;
        if (_lock != null)
        {
            _lock.Click += (_, _) =>
            {
                if (_flow != null) _flow.IsLocked = _lock.IsChecked == true;
            };
        }
    }

    private void OnFlowChanged(object? sender, EventArgs e)
    {
        if (_lock != null && _flow != null) _lock.IsChecked = _flow.IsLocked;
    }
}
