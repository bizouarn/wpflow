using System.Collections.ObjectModel;
using System.ComponentModel;
using Wpflow.Core;
using Wpflow.Wpf;

namespace Wpflow.Wpf.Tests;

/// <summary>Les données et les marqueurs d'un edge viennent aussi du ViewModel.</summary>
public class MvvmEdgeMappingTests
{
    private sealed class LinkVm : INotifyPropertyChanged
    {
        private string _kind = "one";
        private EdgeMarker _end = EdgeMarker.ArrowClosed;

        public string Id => "e";
        public string Source => "a";
        public string Target => "b";

        public string Kind
        {
            get => _kind;
            set => Set(ref _kind, value, nameof(Kind));
        }

        public EdgeMarker End
        {
            get => _end;
            set => Set(ref _end, value, nameof(End));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Set<T>(ref T field, T value, string name)
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    private static (FlowView Flow, LinkVm Link) Make(EdgeMapping mapping)
    {
        var link = new LinkVm();
        var flow = new FlowView { EdgeMapping = mapping };
        flow.EdgesSource = new ObservableCollection<LinkVm> { link };
        return (flow, link);
    }

    [Fact]
    public void Data_and_markers_follow_the_view_model() => Sta.Run(() =>
    {
        var (flow, link) = Make(new EdgeMapping { DataPath = "Kind", MarkerEndPath = "End" });
        Assert.Equal("one", flow.Edges[0].Data);
        Assert.Equal(EdgeMarker.ArrowClosed, flow.Edges[0].MarkerEnd);

        link.Kind = "many";
        link.End = EdgeMarker.None;

        Assert.Equal("many", flow.Edges[0].Data);
        Assert.Equal(EdgeMarker.None, flow.Edges[0].MarkerEnd);
    });

    [Fact]
    public void An_updated_edge_writes_its_data_and_markers_back() => Sta.Run(() =>
    {
        var (flow, link) = Make(new EdgeMapping { DataPath = "Kind", MarkerEndPath = "End" });

        flow.UpdateEdge("e", e => e with { Data = "many", MarkerEnd = EdgeMarker.Arrow });

        Assert.Equal("many", link.Kind);
        Assert.Equal(EdgeMarker.Arrow, link.End);
    });

    [Fact]
    public void Without_a_data_path_the_view_model_is_not_the_data() => Sta.Run(() =>
    {
        var (flow, _) = Make(new EdgeMapping());

        Assert.Null(flow.Edges[0].Data);
    });
}
