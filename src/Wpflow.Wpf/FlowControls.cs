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
        Loaded += (_, _) =>
        {
            _flow = FlowView.FindAncestor<FlowView>(this);
            if (_flow != null) _flow.StateChanged += OnFlowChanged;
            OnFlowChanged(null, EventArgs.Empty);
        };
        Unloaded += (_, _) =>
        {
            if (_flow != null) _flow.StateChanged -= OnFlowChanged;
        };
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
