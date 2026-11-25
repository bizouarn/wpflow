using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wpflow.Core;

namespace Consumer;

public partial class TaskViewModel : ObservableObject, IMovable
{
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    public string Id { get; init; } = "";
}

public sealed class LinkViewModel
{
    public string Id { get; init; } = "";
    public string Source { get; init; } = "";
    public string Target { get; init; } = "";
}

public sealed class MainViewModel
{
    public MainViewModel() => ConnectCommand = new RelayCommand<Connection>(c =>
        Links.Add(new LinkViewModel { Id = $"{c!.Source}-{c.Target}", Source = c.Source, Target = c.Target }));

    public ObservableCollection<TaskViewModel> Nodes { get; } = [new() { Id = "a" }, new() { Id = "b", X = 300, Y = 120 }];
    public ObservableCollection<LinkViewModel> Links { get; } = [new() { Id = "a-b", Source = "a", Target = "b" }];
    public ICommand ConnectCommand { get; }
}
