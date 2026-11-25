using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Wpflow.Core;
using Wpflow.Wpf;

namespace Wpflow.Wpf.Tests;

/// <summary>Ctrl+Z avec des sources : tout ce que la vue annule doit revenir aussi dans les ViewModels.</summary>
public class MvvmUndoTests
{
    private abstract class Vm : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    private sealed class BoxVm : Vm
    {
        private double _x, _y, _width = double.NaN, _height = double.NaN;
        private bool _hidden;
        private string _kind = "default";

        public string Id => "a";
        public double X { get => _x; set => Set(ref _x, value); }
        public double Y { get => _y; set => Set(ref _y, value); }
        public double Width { get => _width; set => Set(ref _width, value); }
        public double Height { get => _height; set => Set(ref _height, value); }
        public bool Hidden { get => _hidden; set => Set(ref _hidden, value); }
        public string Kind { get => _kind; set => Set(ref _kind, value); }
    }

    private sealed class LineVm : Vm
    {
        private string _label = "avant";

        public string Id => "e";
        public string Source => "a";
        public string Target => "b";
        public string Label { get => _label; set => Set(ref _label, value); }
    }

    private static (FlowView Flow, BoxVm Box) Nodes()
    {
        var box = new BoxVm();
        var flow = new FlowView
        {
            NodeMapping = new NodeMapping { WidthPath = "Width", HeightPath = "Height", HiddenPath = "Hidden", TypePath = "Kind" }
        };
        flow.NodesSource = new ObservableCollection<BoxVm> { box };
        return (flow, box);
    }

    [Fact]
    public void Undoing_the_first_resize_gives_back_the_automatic_size() => Sta.Run(() =>
    {
        var (flow, box) = Nodes();
        flow.PushHistory(); // ce que fait la poignée au début du geste
        flow.ApplyNodeChanges([new NodeResizeChange("a", 200, 100)]);

        flow.Undo();

        Assert.True(double.IsNaN(box.Width), $"Width = {box.Width}");
        Assert.True(double.IsNaN(box.Height), $"Height = {box.Height}");
        Assert.Null(flow.Nodes[0].FixedWidth);
    });

    [Fact]
    public void Undo_writes_back_more_than_position_and_selection() => Sta.Run(() =>
    {
        var (flow, box) = Nodes();
        flow.PushHistory();
        flow.UpdateNode("a", n => n with { Hidden = true, Type = "input" });

        flow.Undo();

        Assert.False(box.Hidden);
        Assert.Equal("default", box.Kind);

        flow.Redo();

        Assert.True(box.Hidden);
        Assert.Equal("input", box.Kind);
    });

    [Fact]
    public void A_change_made_by_the_application_survives_an_undo() => Sta.Run(() =>
    {
        var (flow, box) = Nodes();
        flow.PushHistory();
        flow.ApplyNodeChanges([new NodePositionChange("a", new XYPosition(50, 60))]);
        box.Kind = "output"; // une tâche de fond, pas un geste

        flow.Undo();

        Assert.Equal(0, box.X);
        Assert.Equal("output", box.Kind);
        Assert.Equal("output", flow.Nodes[0].Type);

        flow.Redo();

        Assert.Equal(50, box.X);
        Assert.Equal("output", box.Kind);
    });

    [Fact]
    public void Undo_writes_back_the_properties_of_an_edge() => Sta.Run(() =>
    {
        var line = new LineVm();
        var flow = new FlowView { EdgeMapping = new EdgeMapping { LabelPath = "Label" } };
        flow.EdgesSource = new ObservableCollection<LineVm> { line };
        flow.PushHistory();
        flow.UpdateEdge("e", e => e with { Label = "après" });

        flow.Undo();

        Assert.Equal("avant", line.Label);
    });
}
