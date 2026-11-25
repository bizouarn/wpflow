using System.Collections;
using Microsoft.AspNetCore.Components;

namespace Wpflow.Blazor;

// Partie MVVM de FlowView : sources observables (NodesSource/EdgesSource), sélection bindable, contrôleur, historique de
// l'application. La logique de projection et de réécriture est commune avec WPF (Wpflow.Core, SourceAdapter). Les actions
// que WPF expose en commandes passent ici par les EventCallback.
public partial class FlowView
{
    // ---------- Sources ----------

    /// <summary>
    ///     Collection de ViewModels de nodes (idéalement <see cref="System.Collections.Specialized.INotifyCollectionChanged" />,
    ///     éléments <see cref="System.ComponentModel.INotifyPropertyChanged" />) projetée selon <see cref="NodeMapping" />.
    ///     N'utilisez pas <see cref="Nodes" /> en même temps. Modifiez-la sur le contexte de rendu du composant
    ///     (<c>InvokeAsync</c> depuis une tâche de fond).
    /// </summary>
    [Parameter] public IEnumerable? NodesSource { get; set; }

    [Parameter] public IEnumerable? EdgesSource { get; set; }

    /// <summary>Correspondance ViewModel → <see cref="Node" /> ; par défaut <c>Id</c>, <c>X</c>, <c>Y</c>.</summary>
    [Parameter] public NodeMapping? NodeMapping { get; set; }

    [Parameter] public EdgeMapping? EdgeMapping { get; set; }

    /// <summary>Crée le ViewModel d'un node ajouté par la vue (collage, <see cref="LoadJson" />).</summary>
    [Parameter] public Func<Node, object?>? NodeFactory { get; set; }

    /// <summary>Crée le ViewModel d'un edge créé par la vue (connexion à la souris).</summary>
    [Parameter] public Func<Edge, object?>? EdgeFactory { get; set; }

    private NodeSourceAdapter? _nodeSource;
    private EdgeSourceAdapter? _edgeSource;
    private (IEnumerable?, NodeMapping?) _nodeSourceKey;
    private (IEnumerable?, EdgeMapping?) _edgeSourceKey;
    private bool _configuringSources;

    private void ConfigureSources()
    {
        _configuringSources = true;
        try
        {
            ConfigureNodeSource();
            ConfigureEdgeSource();
        }
        finally
        {
            _configuringSources = false;
        }
    }

    private void ConfigureNodeSource()
    {
        // Un mappage écrit dans le balisage serait recréé à chaque rendu : gardez-le dans un champ.
        var key = (NodesSource, NodeMapping);
        if (_nodeSourceKey == key) return;
        _nodeSourceKey = key;
        _nodeSource?.Dispose();
        _nodeSource = null;
        if (NodesSource is not { } source) return;
        Nodes = Array.Empty<Node>();
        var adapter = new NodeSourceAdapter(source, NodeMapping ?? Wpflow.Blazor.NodeMapping.Default, () => Nodes, PushNodes,
            () => NodeFactory);
        adapter.ChangedBySource += (before, after) => _history?.Rewrite(s => s with
        {
            Nodes = s.Nodes.Select(n => n.Id == before.Id ? adapter.Rebase(n, before, after) : n).ToList()
        });
        // Les modèles de nodes sont mémorisés : un record recopié (égal, mais nouveau) fait redessiner ce node seul.
        adapter.Notified += node => PushNodes(Nodes.Select(n => ReferenceEquals(n, node) ? n with { } : n).ToList());
        _nodeSource = adapter;
        ClearHistory();
    }

    private void ConfigureEdgeSource()
    {
        var key = (EdgesSource, EdgeMapping);
        if (_edgeSourceKey == key) return;
        _edgeSourceKey = key;
        _edgeSource?.Dispose();
        _edgeSource = null;
        if (EdgesSource is not { } source) return;
        Edges = Array.Empty<Edge>();
        var adapter = new EdgeSourceAdapter(source, EdgeMapping ?? Wpflow.Blazor.EdgeMapping.Default, () => Edges, PushEdges,
            () => EdgeFactory);
        adapter.ChangedBySource += (before, after) => _history?.Rewrite(s => s with
        {
            Edges = s.Edges.Select(e => e.Id == before.Id ? adapter.Rebase(e, before, after) : e).ToList()
        });
        adapter.Notified += edge =>
        {
            _edgeCache.Clear();
            PushEdges(Edges.Select(e => ReferenceEquals(e, edge) ? e with { } : e).ToList());
        };
        _edgeSource = adapter;
        ClearHistory();
    }

    private void PushNodes(IReadOnlyList<Node> nodes)
    {
        Nodes = nodes;
        RebuildIndex();
        OnSourcePushed();
    }

    private void PushEdges(IReadOnlyList<Edge> edges)
    {
        Edges = edges;
        OnSourcePushed();
    }

    private bool _renderQueued;

    // Une rafale de changements (200 ViewModels ajoutés un par un) ne donne qu'un rendu : il attend la fin du travail en cours.
    private void OnSourcePushed()
    {
        _forceLayer = true;
        if (_configuringSources || _renderQueued) return; // pendant la lecture des paramètres, le rendu suit de toute façon
        _renderQueued = true;
        _ = InvokeAsync(async () =>
        {
            await Task.Yield();
            _renderQueued = false;
            _forceLayer = true;
            StateHasChanged();
            await AfterSelectionMaybeChanged();
        });
    }

    /// <summary>Le ViewModel d'un node de <see cref="NodesSource" /> (sans source : le <see cref="Node" />), ou <c>null</c>.</summary>
    public object? NodeModel(string id) => _nodeSource != null ? _nodeSource.ModelOf(id) : _nodeIndex.GetValueOrDefault(id);

    public object? EdgeModel(string id) => _edgeSource != null ? _edgeSource.ModelOf(id) : Edges.FirstOrDefault(e => e.Id == id);

    private string? NodeIdOf(object model) => model is Node n ? n.Id : _nodeSource?.IdOfKnown(model);

    private string? EdgeIdOf(object model) => model is Edge e ? e.Id : _edgeSource?.IdOfKnown(model);

    // ---------- Sélection bindable ----------

    /// <summary>
    ///     Les éléments sélectionnés (ViewModels, ou <see cref="Node" /> sans source). Lié (<c>@bind-SelectedNodeItems</c>), il
    ///     suit la sélection ; une nouvelle valeur donnée par l'application sélectionne ces éléments.
    /// </summary>
    [Parameter] public IReadOnlyList<object>? SelectedNodeItems { get; set; }

    [Parameter] public EventCallback<IReadOnlyList<object>> SelectedNodeItemsChanged { get; set; }

    [Parameter] public IReadOnlyList<object>? SelectedEdgeItems { get; set; }
    [Parameter] public EventCallback<IReadOnlyList<object>> SelectedEdgeItemsChanged { get; set; }

    /// <summary>Le premier node sélectionné (<c>null</c> si aucun) ; une nouvelle valeur le sélectionne seul.</summary>
    [Parameter] public object? SelectedNode { get; set; }

    [Parameter] public EventCallback<object?> SelectedNodeChanged { get; set; }

    // La dernière valeur REÇUE de l'application : seule une valeur différente est un ordre. Sans liaison, l'application
    // redonne la même à chaque rendu ; elle ne doit pas défaire la sélection faite à la souris.
    private List<object> _receivedNodeItems = [], _receivedEdgeItems = [];
    private object? _receivedNode;
    private List<object> _reportedNodeItems = [], _reportedEdgeItems = [];
    private object? _reportedNode;

    private static bool Same(IReadOnlyList<object> a, IReadOnlyList<object> b) =>
        a.Count == b.Count && a.Zip(b).All(p => ReferenceEquals(p.First, p.Second));

    private async Task ApplyBoundSelection()
    {
        List<string>? nodes = null, edges = null;
        if (SelectedNodeItems != null && !Same(SelectedNodeItems, _receivedNodeItems))
        {
            _receivedNodeItems = SelectedNodeItems.ToList();
            nodes = _receivedNodeItems.Select(NodeIdOf).OfType<string>().ToList();
        }

        if (!ReferenceEquals(SelectedNode, _receivedNode))
        {
            _receivedNode = SelectedNode;
            if (!ReferenceEquals(SelectedNode, _reportedNode))
                nodes = SelectedNode is { } one && NodeIdOf(one) is { } id ? [id] : [];
        }

        if (SelectedEdgeItems != null && !Same(SelectedEdgeItems, _receivedEdgeItems))
        {
            _receivedEdgeItems = SelectedEdgeItems.ToList();
            edges = _receivedEdgeItems.Select(EdgeIdOf).OfType<string>().ToList();
        }

        if (nodes == null && edges == null) return;
        var (nodeChanges, edgeChanges) = SelectionOps.Select(Nodes, Edges,
            nodes ?? Nodes.Where(n => n.Selected).Select(n => n.Id), edges ?? Edges.Where(e => e.Selected).Select(e => e.Id),
            false, ElementsSelectable);
        await ApplyNodeChanges(nodeChanges);
        await ApplyEdgeChanges(edgeChanges);
    }

    private async Task ReportBoundSelection()
    {
        var nodes = Nodes.Where(n => n.Selected).Select(n => NodeModel(n.Id)).OfType<object>().ToList();
        var edges = Edges.Where(e => e.Selected).Select(e => EdgeModel(e.Id)).OfType<object>().ToList();
        var first = nodes.FirstOrDefault();
        if (!Same(nodes, _reportedNodeItems))
        {
            _reportedNodeItems = nodes;
            await SelectedNodeItemsChanged.InvokeAsync(nodes);
        }

        if (!Same(edges, _reportedEdgeItems))
        {
            _reportedEdgeItems = edges;
            await SelectedEdgeItemsChanged.InvokeAsync(edges);
        }

        if (!ReferenceEquals(first, _reportedNode))
        {
            _reportedNode = first;
            await SelectedNodeChanged.InvokeAsync(first);
        }
    }

    // ---------- Historique de l'application et contrôleur ----------

    /// <summary>Remplace l'historique de la vue (voir <see cref="IFlowHistory" />).</summary>
    [Parameter] public IFlowHistory? History { get; set; }

    /// <summary>Remet à l'application de quoi piloter la vue, une fois : <c>ControllerChanged="c => vm.Flow = c"</c>.</summary>
    [Parameter] public EventCallback<IFlowController> ControllerChanged { get; set; }

    private IFlowController? _controller;

    /// <summary>Le contrôleur de cette vue : il ne laisse voir que <see cref="IFlowController" />.</summary>
    [Parameter] public IFlowController? Controller { get; set; }

    private async Task PublishController()
    {
        if (!ControllerChanged.HasDelegate || _controller != null) return;
        _controller = new FlowController(this);
        await ControllerChanged.InvokeAsync(_controller);
    }

    private sealed class FlowController(FlowView flow) : IFlowController
    {
        private Task<T> On<T>(Func<Task<T>> action)
        {
            var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = flow.InvokeAsync(async () =>
            {
                try
                {
                    done.SetResult(await action());
                    flow.Refresh();
                }
                catch (Exception e)
                {
                    done.SetException(e);
                }
            });
            return done.Task;
        }

        private Task On(Func<Task> action) => On(async () =>
        {
            await action();
            return true;
        });

        public Task<bool> FitView(FitViewOptions? options = null) => On(() => flow.FitView(options));
        public Task ZoomIn() => On(() => flow.ZoomIn());
        public Task ZoomOut() => On(() => flow.ZoomOut());
        public Task ZoomTo(double zoom) => On(() => flow.ZoomTo(zoom));

        public Task<bool> CenterOn(string nodeId) => On(async () =>
        {
            if (!flow._absNodes.TryGetValue(nodeId, out var n) || n.Width is not { } w || n.Height is not { } h) return false;
            await flow.SetCenter(n.Position.X + w / 2, n.Position.Y + h / 2);
            return true;
        });

        public Task SetCenter(double x, double y, double? zoom = null, TimeSpan duration = default) =>
            On(() => flow.SetCenter(x, y, zoom, duration));

        public Task SelectAll() => On(flow.SelectAll);
        public Task ClearSelection() => On(flow.ClearSelection);
        public Task<bool> DeleteSelection() => On(flow.DeleteSelected);
        public bool Copy() => flow.Copy();
        public Task<bool> Cut() => On(flow.Cut);
        public Task<bool> Paste() => On(flow.Paste);
        public Task<bool> Undo() => On(flow.Undo);
        public Task<bool> Redo() => On(flow.Redo);
        public XYPosition ScreenToFlow(XYPosition container) => flow.ScreenToFlow(container);
        public XYPosition FlowToScreen(XYPosition position) => flow.FlowToScreen(position);
    }

    private void DisposeSources()
    {
        _nodeSource?.Dispose();
        _edgeSource?.Dispose();
    }
}
