using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Windows.Input;
using ReactiveUI;
using Wpflow.Core;

namespace Consumer;

public class TaskViewModel : ReactiveObject, IMovable
{
    private double _x, _y;
    public string Id { get; init; } = "";
    public double X { get => _x; set => this.RaiseAndSetIfChanged(ref _x, value); }
    public double Y { get => _y; set => this.RaiseAndSetIfChanged(ref _y, value); }
}

public sealed class LinkViewModel
{
    public string Id { get; init; } = "";
    public string Source { get; init; } = "";
    public string Target { get; init; } = "";
}

public sealed class MainViewModel
{
    public MainViewModel() => ConnectCommand = ReactiveCommand.Create<Connection>(c =>
            Links.Add(new LinkViewModel { Id = $"{c.Source}-{c.Target}", Source = c.Source, Target = c.Target }),
        outputScheduler: ImmediateScheduler.Instance);

    public ObservableCollection<TaskViewModel> Nodes { get; } = [new() { Id = "a" }, new() { Id = "b", X = 300, Y = 120 }];
    public ObservableCollection<LinkViewModel> Links { get; } = [new() { Id = "a-b", Source = "a", Target = "b" }];
    public ICommand ConnectCommand { get; }
}
