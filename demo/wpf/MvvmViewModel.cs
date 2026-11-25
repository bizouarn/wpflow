using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Wpflow.Core;
using Wpflow.Wpf;

namespace demo;

/// <summary>Base minimale d'un objet observable.</summary>
public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>ViewModel d'un node : le graphe lit et écrit ces propriétés, sans code-behind.</summary>
public sealed class TaskVm : Observable
{
    private string _title = "";
    private double _x, _y;
    private bool _done, _selected;

    public string Id { get; init; } = "";

    /// <summary>Type de node : choisit le template <c>wpflow-node-task</c>.</summary>
    public string Kind => "task";

    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    public double X
    {
        get => _x;
        set => Set(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => Set(ref _y, value);
    }

    public bool Done
    {
        get => _done;
        set => Set(ref _done, value);
    }

    public bool IsSelected
    {
        get => _selected;
        set => Set(ref _selected, value);
    }
}

public sealed class LinkVm
{
    public string Id { get; init; } = "";
    public string Source { get; init; } = "";
    public string Target { get; init; } = "";
}

/// <summary>
///     ViewModel de l'onglet MVVM : la vue (<c>MvvmExample.xaml</c>) n'a aucun code-behind. Tout passe par des collections,
///     des commandes et le <see cref="IFlowController" /> que le contrôle remet par liaison.
/// </summary>
public sealed class MvvmViewModel : Observable
{
    private int _next = 4;
    private string _summary = "", _hint = "Cliquez un node ou le fond.";
    private double _zoom = 1;
    private IFlowController? _flow;

    public MvvmViewModel()
    {
        Tasks =
        [
            new TaskVm { Id = "1", Title = "Écrire le ViewModel", X = 0, Y = 40 },
            new TaskVm { Id = "2", Title = "Binder NodesSource", X = 280, Y = 0 },
            new TaskVm { Id = "3", Title = "Glisser les nodes", X = 280, Y = 120 }
        ];
        Links =
        [
            new LinkVm { Id = "l1", Source = "1", Target = "2" },
            new LinkVm { Id = "l2", Source = "1", Target = "3" }
        ];
        Selection.CollectionChanged += (_, _) => UpdateSummary();
        Tasks.CollectionChanged += (_, _) => UpdateSummary();
        UpdateSummary();

        AddCommand = new ParamCommand(_ => AddTask(40 * (_next % 6), 60 + 40 * (_next % 5)));
        ShuffleCommand = new ParamCommand(_ =>
        {
            var i = 0;
            foreach (var task in Tasks)
            {
                task.X = 60 * i;
                task.Y = 50 * (i++ % 3);
            }
        });
        FitCommand = new ParamCommand(_ => Flow?.FitView());
        CenterLastCommand = new ParamCommand(_ => Flow?.CenterOn(Tasks.LastOrDefault()?.Id ?? ""), _ => Tasks.Count > 0);

        // Commandes que le contrôle exécute à notre place (voir MvvmExample.xaml).
        ConnectCommand = new ParamCommand(p => AddLink((Connection)p!), CanConnect);
        NodesDeleteCommand = new ParamCommand(p => DeleteTasks((IReadOnlyList<object>)p!));
        NodeClickCommand = new ParamCommand(p => Hint = $"clic sur « {((TaskVm)p!).Title} »");
        PaneClickCommand = new ParamCommand(p => Hint = $"clic sur le fond en {(XYPosition)p!}");
        NodeDragStopCommand = new ParamCommand(p => Hint = $"{((IReadOnlyList<object>)p!).Count} node(s) déplacé(s)");
        LinkFactory = edge => new LinkVm { Id = edge.Id, Source = edge.Source, Target = edge.Target };
    }

    public ObservableCollection<TaskVm> Tasks { get; }
    public ObservableCollection<LinkVm> Links { get; }

    /// <summary>Mise à jour par la vue (nodes sélectionnés) ; le ViewModel peut aussi y ajouter des éléments pour sélectionner.</summary>
    public ObservableCollection<TaskVm> Selection { get; } = new();

    /// <summary>Remis par le contrôle (<c>Controller</c>, liaison <c>OneWayToSource</c>) : permet de piloter la vue sans la connaître.</summary>
    public IFlowController? Flow
    {
        get => _flow;
        set => Set(ref _flow, value);
    }

    /// <summary>Zoom de la vue, lié en <c>TwoWay</c> (le curseur de la barre d'outils et la molette se suivent).</summary>
    public double Zoom
    {
        get => _zoom;
        set => Set(ref _zoom, value);
    }

    public string Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
    }

    /// <summary>Dernier événement signalé par les commandes de clic, de déplacement et de connexion.</summary>
    public string Hint
    {
        get => _hint;
        private set => Set(ref _hint, value);
    }

    public ICommand AddCommand { get; }
    public ICommand ShuffleCommand { get; }
    public ICommand FitCommand { get; }
    public ICommand CenterLastCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand NodesDeleteCommand { get; }
    public ICommand NodeClickCommand { get; }
    public ICommand PaneClickCommand { get; }
    public ICommand NodeDragStopCommand { get; }
    public Func<Edge, object?> LinkFactory { get; }

    private void AddTask(double x, double y)
    {
        var n = _next++;
        Tasks.Add(new TaskVm { Id = n.ToString(), Title = $"Tâche {n}", X = x, Y = y });
    }

    // Règle métier : on ne se connecte pas à la première tâche, et pas deux fois le même lien. Le contrôle refuse la
    // connexion quand CanExecute est faux : c'est le validateur côté ViewModel.
    private bool CanConnect(object? p) =>
        p is Connection c && c.Target != "1" && !Links.Any(l => l.Source == c.Source && l.Target == c.Target);

    private void AddLink(Connection c)
    {
        Links.Add(new LinkVm { Id = $"l{Links.Count + 1}-{c.Source}-{c.Target}", Source = c.Source, Target = c.Target });
        Hint = $"lien {c.Source} → {c.Target} créé";
    }

    private void DeleteTasks(IReadOnlyList<object> tasks)
    {
        foreach (var task in tasks.Cast<TaskVm>())
        {
            foreach (var link in Links.Where(l => l.Source == task.Id || l.Target == task.Id).ToList()) Links.Remove(link);
            Tasks.Remove(task);
        }

        Hint = $"{tasks.Count} node(s) supprimé(s)";
    }

    private void UpdateSummary() =>
        Summary = $"{Tasks.Count} nodes, {Selection.Count} sélectionné(s)" +
                  (Selection.Count > 0 ? " : " + string.Join(", ", Selection.Select(t => t.Title)) : "");
}
