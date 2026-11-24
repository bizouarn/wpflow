using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using Wpflow.Core;

namespace Wpflow.Wpf;

// Partie MVVM de FlowView : sources observables (NodesSource/EdgesSource) et sélection bindable.
public partial class FlowView
{
    #region Sources observables

    /// <summary>
    ///     Collection de ViewModels de nodes (idéalement <see cref="INotifyCollectionChanged" />, éléments
    ///     <see cref="System.ComponentModel.INotifyPropertyChanged" />) projetée dans <see cref="Nodes" /> selon <see cref="NodeMapping" />.
    ///     N'utilisez pas <see cref="Nodes" /> en même temps. 
    /// </summary>
    public static readonly DependencyProperty NodesSourceProperty = DependencyProperty.Register(
        nameof(NodesSource), typeof(IEnumerable), typeof(FlowView),
        new PropertyMetadata(null, (d, _) => ((FlowView)d).ResetNodeSource()));

    public static readonly DependencyProperty EdgesSourceProperty = DependencyProperty.Register(
        nameof(EdgesSource), typeof(IEnumerable), typeof(FlowView),
        new PropertyMetadata(null, (d, _) => ((FlowView)d).ResetEdgeSource()));

    /// <summary>Correspondance ViewModel → <see cref="Node" /> ; par défaut <c>Id</c>, <c>X</c>, <c>Y</c>. À fixer avant <see cref="NodesSource" />.</summary>
    public static readonly DependencyProperty NodeMappingProperty = DependencyProperty.Register(
        nameof(NodeMapping), typeof(NodeMapping), typeof(FlowView),
        new PropertyMetadata(null, (d, _) => ((FlowView)d).ResetNodeSource()));

    public static readonly DependencyProperty EdgeMappingProperty = DependencyProperty.Register(
        nameof(EdgeMapping), typeof(EdgeMapping), typeof(FlowView),
        new PropertyMetadata(null, (d, _) => ((FlowView)d).ResetEdgeSource()));

    public IEnumerable? NodesSource
    {
        get => (IEnumerable?)GetValue(NodesSourceProperty);
        set => SetValue(NodesSourceProperty, value);
    }

    public IEnumerable? EdgesSource
    {
        get => (IEnumerable?)GetValue(EdgesSourceProperty);
        set => SetValue(EdgesSourceProperty, value);
    }

    public NodeMapping? NodeMapping
    {
        get => (NodeMapping?)GetValue(NodeMappingProperty);
        set => SetValue(NodeMappingProperty, value);
    }

    public EdgeMapping? EdgeMapping
    {
        get => (EdgeMapping?)GetValue(EdgeMappingProperty);
        set => SetValue(EdgeMappingProperty, value);
    }

    /// <summary>Crée le ViewModel d'un node ajouté par la vue (dépôt depuis une palette…) ; il est ajouté à <see cref="NodesSource" />. Liable à une propriété du ViewModel.</summary>
    public static readonly DependencyProperty NodeFactoryProperty = DependencyProperty.Register(
        nameof(NodeFactory), typeof(Func<Node, object?>), typeof(FlowView), new PropertyMetadata(null));

    /// <summary>Crée le ViewModel d'un edge créé par la vue (connexion à la souris) ; il est ajouté à <see cref="EdgesSource" />. Liable à une propriété du ViewModel.</summary>
    public static readonly DependencyProperty EdgeFactoryProperty = DependencyProperty.Register(
        nameof(EdgeFactory), typeof(Func<Edge, object?>), typeof(FlowView), new PropertyMetadata(null));

    public Func<Node, object?>? NodeFactory
    {
        get => (Func<Node, object?>?)GetValue(NodeFactoryProperty);
        set => SetValue(NodeFactoryProperty, value);
    }

    public Func<Edge, object?>? EdgeFactory
    {
        get => (Func<Edge, object?>?)GetValue(EdgeFactoryProperty);
        set => SetValue(EdgeFactoryProperty, value);
    }

    private NodeSourceAdapter? _nodeSource;
    private EdgeSourceAdapter? _edgeSource;

    private void ResetNodeSource()
    {
        _nodeSource?.Dispose();
        _nodeSource = null;
        if (NodesSource is { } source) _nodeSource = new NodeSourceAdapter(this, source, NodeMapping ?? Wpflow.Wpf.NodeMapping.Default);
        if (_nodeSource != null) ClearHistory();
    }

    private void ResetEdgeSource()
    {
        _edgeSource?.Dispose();
        _edgeSource = null;
        if (EdgesSource is { } source) _edgeSource = new EdgeSourceAdapter(this, source, EdgeMapping ?? Wpflow.Wpf.EdgeMapping.Default);
        if (_edgeSource != null) ClearHistory();
    }

    private void ClearHistory()
    {
        _undo.Clear();
        _redo.Clear();
        UpdateHistoryState();
    }

    /// <summary>Le ViewModel (ou, sans source, le <see cref="Node" />) d'un id de node.</summary>
    private object? NodeElement(string id) =>
        _nodeSource?.ModelOf(id) ?? RawNode(id) ?? Nodes.FirstOrDefault(n => n.Id == id); // l'index n'existe qu'après le template

    private object? EdgeElement(string id) => _edgeSource?.ModelOf(id) ?? Edges.FirstOrDefault(e => e.Id == id);

    #endregion

    #region Sélection bindable

    public static readonly DependencyProperty SelectedNodesProperty = DependencyProperty.Register(
        nameof(SelectedNodes), typeof(IList), typeof(FlowView),
        new FrameworkPropertyMetadata(Array.Empty<object>(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, e) => ((FlowView)d).NodeSelection.OnAssigned((IList?)e.NewValue)));

    public static readonly DependencyProperty SelectedEdgesProperty = DependencyProperty.Register(
        nameof(SelectedEdges), typeof(IList), typeof(FlowView),
        new FrameworkPropertyMetadata(Array.Empty<object>(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, e) => ((FlowView)d).EdgeSelection.OnAssigned((IList?)e.NewValue)));

    public static readonly DependencyProperty SelectedNodeProperty = DependencyProperty.Register(
        nameof(SelectedNode), typeof(object), typeof(FlowView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, e) => ((FlowView)d).OnSelectedNodeAssigned(e.NewValue)));

    /// <summary>
    ///     Nodes sélectionnés : les ViewModels de <see cref="NodesSource" /> (ou des <see cref="Node" /> sans source).
    ///     Lue : un binding <c>OneWay</c> suffit. Écrite (<c>TwoWay</c>) : sélectionne ces éléments. Si la valeur liée est une
    ///     collection observable modifiable (<c>ObservableCollection</c>), elle est mise à jour sur place et ses modifications sont suivies.
    /// </summary>
    public IList SelectedNodes
    {
        get => (IList)GetValue(SelectedNodesProperty);
        set => SetValue(SelectedNodesProperty, value);
    }

    public IList SelectedEdges
    {
        get => (IList)GetValue(SelectedEdgesProperty);
        set => SetValue(SelectedEdgesProperty, value);
    }

    /// <summary>Premier node sélectionné (<c>null</c> si aucun) ; l'écrire le sélectionne seul.</summary>
    public object? SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    private SelectionMirror? _nodeSelection, _edgeSelection;
    private readonly List<string> _selectedIds = new(); // réutilisée : pas d'allocation à chaque mise à jour
    private bool _syncingSelectedNode;

    private SelectionMirror NodeSelection => _nodeSelection ??= new SelectionMirror(this, SelectedNodesProperty,
        NodeElement, item => item is Node n ? n.Id : _nodeSource?.IdOfKnown(item), ids => Select(ids, SelectedEdgeIds(), false));

    private SelectionMirror EdgeSelection => _edgeSelection ??= new SelectionMirror(this, SelectedEdgesProperty,
        EdgeElement, item => item is Edge e ? e.Id : _edgeSource?.IdOfKnown(item), ids => Select(SelectedNodeIds(), ids, false));

    private List<string> SelectedNodeIds() => Nodes.Where(n => n.Selected).Select(n => n.Id).ToList();

    private List<string> SelectedEdgeIds() => Edges.Where(e => e.Selected).Select(e => e.Id).ToList();

    /// <summary>À appeler quand <see cref="Nodes" /> change : met à jour la sélection exposée, sans rien allouer si elle n'a pas changé.</summary>
    private void UpdateSelectedNodes()
    {
        _selectedIds.Clear();
        foreach (var node in Nodes)
            if (node.Selected)
                _selectedIds.Add(node.Id);

        NodeSelection.Update(_selectedIds);

        var first = _selectedIds.Count > 0 ? NodeElement(_selectedIds[0]) : null;
        if (Equals(first, SelectedNode)) return;
        _syncingSelectedNode = true;
        try
        {
            SetCurrentValue(SelectedNodeProperty, first);
        }
        finally
        {
            _syncingSelectedNode = false;
        }
    }

    private void UpdateSelectedEdges()
    {
        _selectedIds.Clear();
        foreach (var edge in Edges)
            if (edge.Selected)
                _selectedIds.Add(edge.Id);

        EdgeSelection.Update(_selectedIds);
    }

    private void OnSelectedNodeAssigned(object? item)
    {
        if (_syncingSelectedNode) return;
        var id = item switch { null => null, Node n => n.Id, _ => _nodeSource?.IdOfKnown(item) };
        Select(id == null ? [] : [id], SelectedEdgeIds(), false);
    }

    #endregion

    #region JSON

    /// <summary>
    ///     Exporte le graphe au format React Flow. Avec une source de ViewModels, <c>Node.Data</c> n'est <b>pas</b> écrit par
    ///     défaut (un ViewModel n'est pas fait pour être sérialisé tel quel) : passez <paramref name="data" /> pour choisir ce qui
    ///     est écrit (un objet de transfert, ou <c>n =&gt; n.Data</c> pour le ViewModel lui-même).
    /// </summary>
    public string ToJson(Func<Node, object?>? data = null, bool indented = true) =>
        GraphSerializer.ToJson(Nodes, Edges, Viewport, indented, data ?? (_nodeSource != null ? _ => null : null));

    /// <summary>
    ///     Charge un graphe au format React Flow. Sans source : remplace <see cref="Nodes" /> et <see cref="Edges" />.
    ///     Avec une source : vide la collection et y ajoute les ViewModels créés par <see cref="NodeFactory" /> /
    ///     <see cref="EdgeFactory" /> (obligatoires), dont les propriétés mappées sont renseignées.
    /// </summary>
    public void LoadJson(string json)
    {
        var graph = GraphSerializer.FromJson(json);
        _nodeSource?.Load(graph.Nodes);
        _edgeSource?.Load(graph.Edges);
        if (_nodeSource == null) Nodes = graph.Nodes;
        if (_edgeSource == null) Edges = graph.Edges;
        ClearHistory();
        Viewport = graph.Viewport;
    }

    #endregion

    /// <summary>Reporte dans les ViewModels les changes que la vue vient d'appliquer.</summary>
    private void WriteBackNodes(IReadOnlyList<NodeChange> changes) => _nodeSource?.WriteBack(changes);

    private void WriteBackEdges(IReadOnlyList<EdgeChange> changes) => _edgeSource?.WriteBack(changes);
}

/// <summary>
///     Garde une propriété de sélection (<see cref="FlowView.SelectedNodes" />…) synchronisée avec la sélection réelle,
///     dans les deux sens : la vue la met à jour quand la sélection change, et sélectionne quand on l'écrit.
/// </summary>
internal sealed class SelectionMirror
{
    private readonly FlowView _flow;
    private readonly DependencyProperty _property;
    private readonly Func<string, object?> _elementOf;
    private readonly Func<object, string?> _idOf;
    private readonly Action<List<string>> _apply;
    private List<string> _ids = new();
    private IList? _owned; // la liste que nous avons créée (jamais modifiée sur place : un binding OneWay n'en serait pas notifié)
    private INotifyCollectionChanged? _watched;
    private bool _syncing, _inCollectionEvent;

    public SelectionMirror(FlowView flow, DependencyProperty property, Func<string, object?> elementOf,
        Func<object, string?> idOf, Action<List<string>> apply)
    {
        _flow = flow;
        _property = property;
        _elementOf = elementOf;
        _idOf = idOf;
        _apply = apply;
    }

    /// <summary>La sélection réelle est <paramref name="ids" /> : met à jour la propriété si elle diffère.</summary>
    public void Update(List<string> ids)
    {
        if (_ids.SequenceEqual(ids)) return;
        _ids = ids.ToList();
        CommandManager.InvalidateRequerySuggested(); // Supprimer / Désélectionner dépendent de la sélection
        var elements = _ids.Select(_elementOf).Where(e => e != null).Cast<object>().ToList();
        _syncing = true;
        try
        {
            if (_flow.GetValue(_property) is IList { IsReadOnly: false, IsFixedSize: false } external &&
                !ReferenceEquals(external, _owned) && external is INotifyCollectionChanged)
            {
                if (SameContent(external, elements)) return; // déjà à jour (cas de la modification qui a provoqué cette sélection)
                if (_inCollectionEvent)
                {
                    // Une ObservableCollection refuse d'être modifiée pendant son propre événement : on reporte.
                    var wanted = elements;
                    _flow.Dispatcher.BeginInvoke(() =>
                    {
                        if (!_ids.SequenceEqual(wanted.Select(_idOf))) return; // la sélection a encore changé entre-temps
                        _syncing = true;
                        try
                        {
                            external.Clear();
                            foreach (var element in wanted) external.Add(element);
                        }
                        finally
                        {
                            _syncing = false;
                        }
                    });
                    return;
                }

                external.Clear(); // collection du ViewModel : mise à jour sur place
                foreach (var element in elements) external.Add(element);
            }
            else
            {
                _owned = elements;
                _flow.SetCurrentValue(_property, elements);
                Watch(null);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>La propriété a reçu une valeur de l'extérieur : sélectionne ces éléments.</summary>
    public void OnAssigned(IList? value)
    {
        if (_syncing) return;
        Watch(ReferenceEquals(value, _owned) ? null : value as INotifyCollectionChanged);
        Apply(value);
    }

    private void Watch(INotifyCollectionChanged? collection)
    {
        if (ReferenceEquals(collection, _watched)) return;
        if (_watched != null) _watched.CollectionChanged -= OnCollectionChanged;
        _watched = collection;
        if (_watched != null) _watched.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_syncing) return;
        _inCollectionEvent = true;
        try
        {
            Apply(_flow.GetValue(_property) as IList);
        }
        finally
        {
            _inCollectionEvent = false;
        }
    }

    private static bool SameContent(IList list, List<object> elements)
    {
        if (list.Count != elements.Count) return false;
        for (var i = 0; i < elements.Count; i++)
            if (!ReferenceEquals(list[i], elements[i]))
                return false;
        return true;
    }

    private void Apply(IList? value) =>
        _apply(value?.Cast<object>().Select(_idOf).OfType<string>().ToList() ?? []);
}
