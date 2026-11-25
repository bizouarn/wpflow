using System.Collections.ObjectModel;
using System.ComponentModel;
using Wpflow.Core;
using Wpflow.Wpf;

namespace Wpflow.Wpf.Tests;

/// <summary>Un ViewModel qui refuse ou ajuste ce que la vue lui écrit : la vue doit suivre le ViewModel, pas l'inverse.</summary>
public class MvvmRefusalTests
{
    private sealed class GuardedVm : INotifyPropertyChanged
    {
        private double _x = 10;

        public string Id { get; init; } = "a";

        /// <summary>Jamais négatif : une valeur refusée est ramenée à 0.</summary>
        public double X
        {
            get => _x;
            set
            {
                _x = Math.Max(0, value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(X)));
            }
        }

        public double Y { get; set; }

        /// <summary>Verrouillé : la sélection est refusée, et le ViewModel le signale.</summary>
        public bool IsSelected
        {
            get => false;
            set => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private static (FlowView Flow, GuardedVm Model) Make()
    {
        var model = new GuardedVm();
        var flow = new FlowView { NodeMapping = new NodeMapping { SelectedPath = "IsSelected" } };
        flow.NodesSource = new ObservableCollection<GuardedVm> { model };
        return (flow, model);
    }

    [Fact]
    public void A_value_the_view_model_adjusts_is_shown_as_adjusted() => Sta.Run(() =>
    {
        var (flow, model) = Make();

        flow.ApplyNodeChanges([new NodePositionChange("a", new XYPosition(-50, 20))]);

        Assert.Equal(0, model.X);
        Assert.Equal(new XYPosition(0, 20), flow.Nodes[0].Position);
    });

    [Fact]
    public void A_change_the_view_model_refuses_is_not_kept_by_the_view() => Sta.Run(() =>
    {
        var (flow, _) = Make();

        flow.ApplyNodeChanges([new NodeSelectChange("a", true)]);

        Assert.False(flow.Nodes[0].Selected);
        Assert.Empty(flow.SelectedNodes);
    });

    [Fact]
    public void An_accepted_change_keeps_the_record_applied_by_the_view() => Sta.Run(() =>
    {
        var (flow, _) = Make();
        var pushes = 0;
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(FlowView.NodesProperty, typeof(FlowView))
            .AddValueChanged(flow, (_, _) => pushes++);

        flow.ApplyNodeChanges([new NodePositionChange("a", new XYPosition(30, 20))]);

        Assert.Equal(new XYPosition(30, 20), flow.Nodes[0].Position);
        Assert.Equal(1, pushes); // pas de seconde mise à jour quand rien n'a été ajusté
    });
}
