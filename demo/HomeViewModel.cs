using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Wpflow.Core;

namespace demo;

/// <summary>ViewModel d'un node de la page d'accueil. <see cref="State" /> (partagé) devient le <c>Node.Data</c> des templates.</summary>
public sealed class HomeNodeVm : Observable
{
    private double _x, _y;
    private double _width = double.NaN, _height = double.NaN;

    public string Id { get; init; } = "";

    /// <summary>Choisit le template <c>wpflow-node-{Kind}</c>.</summary>
    public string Kind { get; init; } = "";

    public DemoState State { get; init; } = null!;

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

    /// <summary>Taille explicite (NaN = automatique) ; le <c>NodeResizer</c> la modifie.</summary>
    public double Width
    {
        get => _width;
        set => Set(ref _width, value);
    }

    public double Height
    {
        get => _height;
        set => Set(ref _height, value);
    }
}

/// <summary>ViewModel d'un edge (un « fil » entre un node d'entrée et le node de sortie).</summary>
public sealed class HomeLinkVm : Observable
{
    private string _target = "", _targetHandle = "";

    public string Id { get; init; } = "";
    public string Source { get; init; } = "";

    public string Target
    {
        get => _target;
        set => Set(ref _target, value);
    }

    /// <summary>Entrée du node de sortie (<c>color</c>, <c>shape</c> ou <c>zoom</c>) ; change si l'edge est reconnecté.</summary>
    public string TargetHandle
    {
        get => _targetHandle;
        set => Set(ref _targetHandle, value);
    }

    public bool Animated { get; init; } = true;
}

/// <summary>
///     Toute la logique de la page d'accueil : les nodes et les edges sont des collections, le viewport est lié, et
///     le node de sortie n'utilise couleur, forme et zoom que si le lien correspondant existe — la vue n'a aucun code pour cela.
/// </summary>
public sealed class HomeViewModel : Observable
{
    private Viewport _viewport = new(560, 150, 1.1);

    public HomeViewModel()
    {
        Nodes =
        [
            new HomeNodeVm { Id = "color", Kind = "color", State = State, X = 0, Y = 0 },
            new HomeNodeVm { Id = "shape", Kind = "shape", State = State, X = -35, Y = 85 },
            new HomeNodeVm { Id = "zoom", Kind = "zoom", State = State, X = -10, Y = 190 },
            new HomeNodeVm { Id = "output", Kind = "output", State = State, X = 215, Y = 25, Width = 300, Height = 250 }
        ];
        Links =
        [
            new HomeLinkVm { Id = "e-color", Source = "color", Target = "output", TargetHandle = "color" },
            new HomeLinkVm { Id = "e-shape", Source = "shape", Target = "output", TargetHandle = "shape" },
            new HomeLinkVm { Id = "e-zoom", Source = "zoom", Target = "output", TargetHandle = "zoom" }
        ];
        Links.CollectionChanged += OnLinksChanged;
        foreach (var link in Links) link.PropertyChanged += OnLinkChanged;
        UpdateConnections();

        LinkFactory = edge => new HomeLinkVm
            { Id = edge.Id, Source = edge.Source, Target = edge.Target, TargetHandle = edge.TargetHandle ?? "" };
        OpenExamplesCommand = new ParamCommand(_ => ExamplesRequested?.Invoke());
    }

    public DemoState State { get; } = new();
    public ObservableCollection<HomeNodeVm> Nodes { get; }
    public ObservableCollection<HomeLinkVm> Links { get; }

    /// <summary>Position et zoom de la vue (lié en <c>TwoWay</c> : molette et pan l'écrivent ici).</summary>
    public Viewport Viewport
    {
        get => _viewport;
        set => Set(ref _viewport, value);
    }

    /// <summary>Crée le ViewModel d'un edge tracé à la souris (lié à <c>FlowView.EdgeFactory</c>).</summary>
    public Func<Edge, object?> LinkFactory { get; }

    public ICommand OpenExamplesCommand { get; }

    /// <summary>Demande l'ouverture de la fenêtre d'exemples (la vue s'en charge : un ViewModel ne crée pas de fenêtre).</summary>
    public event Action? ExamplesRequested;

    private void OnLinksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (HomeLinkVm link in e.OldItems)
                link.PropertyChanged -= OnLinkChanged;
        if (e.NewItems != null)
            foreach (HomeLinkVm link in e.NewItems)
                link.PropertyChanged += OnLinkChanged;
        UpdateConnections();
    }

    private void OnLinkChanged(object? sender, PropertyChangedEventArgs e) => UpdateConnections();

    // « Relier » : le node de sortie n'utilise couleur, forme et zoom que si le node correspondant lui est connecté.
    private void UpdateConnections()
    {
        bool Linked(string handle) => Links.Any(l => l.Target == "output" && l.TargetHandle == handle);
        State.UseColor = Linked("color");
        State.UseShape = Linked("shape");
        State.UseZoom = Linked("zoom");
    }
}
