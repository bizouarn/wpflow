using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Wpflow.Core;

namespace Wpflow.Core.Tests;

/// <summary>Les ViewModels vivent souvent plus longtemps que la vue : ils ne doivent pas la retenir (ROADMAP-TESTS, lot 4).</summary>
public class SourceAdapterLifetimeTests
{
    private sealed class Model : INotifyPropertyChanged
    {
        private double _x;
        public string Id { get; init; } = "a";
        public double Y { get; set; }

        public double X
        {
            get => _x;
            set
            {
                _x = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(X)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public int Subscribers => PropertyChanged?.GetInvocationList().Length ?? 0;
    }

    private sealed class View
    {
        public IReadOnlyList<Node> Nodes = [];
        public NodeSourceAdapter? Adapter;

        public View(ObservableCollection<Model> source) =>
            Adapter = new NodeSourceAdapter(source, new NodeMappingBase(), () => Nodes, items => Nodes = items, () => null);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Abandoned(ObservableCollection<Model> source) => new(new View(source));

    [Fact]
    public void A_view_left_without_dispose_is_not_held_by_its_source()
    {
        var source = new ObservableCollection<Model> { new() };
        var view = Abandoned(source);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(view.IsAlive);
        // L'abonnement devenu inutile part à la notification suivante.
        source[0].X = 5;
        Assert.Equal(0, source[0].Subscribers);
        source.Add(new Model { Id = "b" });
    }

    [Fact]
    public void A_live_view_keeps_following_its_source_until_disposed()
    {
        var source = new ObservableCollection<Model> { new() };
        var view = new View(source);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        source[0].X = 12;
        source.Add(new Model { Id = "b" });

        Assert.Equal(12, view.Nodes[0].Position.X);
        Assert.Equal(2, view.Nodes.Count);

        view.Adapter!.Dispose();
        source[0].X = 40;

        Assert.Equal(12, view.Nodes[0].Position.X);
        Assert.Equal(0, source[0].Subscribers);
    }
}
