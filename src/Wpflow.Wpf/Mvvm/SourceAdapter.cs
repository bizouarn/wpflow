using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Wpflow.Core;

namespace Wpflow.Wpf;

/// <summary>
///     Projette une collection de ViewModels (<see cref="FlowView.NodesSource" /> / <see cref="FlowView.EdgesSource" />)
///     vers les listes de <c>record</c> immuables de <see cref="FlowView" />, et écrit dans le ViewModel les changes
///     appliqués par la vue (position, taille, sélection, suppression…).
///     Les records qui n'ont pas changé gardent leur référence : la vue ne retraite que ce qui a bougé.
/// </summary>
internal abstract class SourceAdapter<T> : IDisposable where T : class
{
    private readonly Dictionary<object, string> _idByModel = new(ReferenceEqualityComparer.Instance);
    private readonly IEnumerable _source;
    private readonly HashSet<string> _paths;
    private readonly Dictionary<string, object> _modelById = new();
    private readonly Dictionary<string, object> _removed = new(); // ViewModels retirés par la vue : un Undo les remet tels quels
    private int _writing; // > 0 pendant l'écriture vers le ViewModel : ses PropertyChanged ne sont pas des changements à relire

    protected SourceAdapter(IEnumerable source, HashSet<string> paths)
    {
        _source = source;
        _paths = paths;
    }

    protected abstract IReadOnlyList<T> Current { get; }
    protected abstract void Push(IReadOnlyList<T> items);
    protected abstract string IdOfModel(T item);
    protected abstract string IdOf(object model);
    protected abstract T Project(object model, T? previous);

    protected bool IsWriting => _writing > 0;

    /// <summary>Le ViewModel correspondant à l'id d'un node/edge, ou <c>null</c>.</summary>
    public object? ModelOf(string id) => _modelById.GetValueOrDefault(id);

    /// <summary>L'id d'un ViewModel (ou <c>null</c> s'il n'appartient pas à la source).</summary>
    public string? IdOfKnown(object model) => _idByModel.GetValueOrDefault(model);

    protected void Start()
    {
        if (_source is INotifyCollectionChanged changed) changed.CollectionChanged += OnCollectionChanged;
        Rebuild();
    }

    public void Dispose()
    {
        if (_source is INotifyCollectionChanged changed) changed.CollectionChanged -= OnCollectionChanged;
        foreach (var model in _idByModel.Keys)
            if (model is INotifyPropertyChanged inpc)
                inpc.PropertyChanged -= OnItemChanged;
        _idByModel.Clear();
        _modelById.Clear();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!IsWriting) Rebuild();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsWriting || sender == null) return;
        if (!string.IsNullOrEmpty(e.PropertyName) && !_paths.Contains(e.PropertyName)) return;
        if (!_idByModel.TryGetValue(sender, out var oldId) || IdOf(sender) != oldId)
        {
            Rebuild(); // l'identité a changé : tout est à recalculer
            return;
        }

        var current = Current;
        var index = -1;
        for (var i = 0; i < current.Count; i++)
            if (IdOfModel(current[i]) == oldId)
            {
                index = i;
                break;
            }

        if (index < 0) return;
        var updated = Project(sender, current[index]);
        if (updated.Equals(current[index])) return;
        var list = current.ToList();
        list[index] = updated;
        Push(list);
    }

    /// <summary>Recalcule la liste complète en réutilisant les records inchangés ; abonne les nouveaux ViewModels.</summary>
    protected void Rebuild()
    {
        var current = Current;
        var previous = new Dictionary<string, T>(current.Count);
        foreach (var item in current) previous.TryAdd(IdOfModel(item), item);

        var models = new List<object>();
        foreach (var model in _source)
            if (model != null)
                models.Add(model);

        var known = new HashSet<object>(models, ReferenceEqualityComparer.Instance);
        foreach (var gone in _idByModel.Keys.Where(m => !known.Contains(m)).ToList())
        {
            if (gone is INotifyPropertyChanged inpc) inpc.PropertyChanged -= OnItemChanged;
            _idByModel.Remove(gone);
        }

        _modelById.Clear();
        var list = new List<T>(models.Count);
        var seen = new HashSet<string>();
        foreach (var model in models)
        {
            var id = IdOf(model);
            if (!seen.Add(id)) continue; // id en double : le premier gagne, comme dans Changes.Apply

            if (!_idByModel.ContainsKey(model) && model is INotifyPropertyChanged inpc) inpc.PropertyChanged += OnItemChanged;
            _idByModel[model] = id;
            _modelById[id] = model;

            previous.TryGetValue(id, out var before);
            var projected = Project(model, before);
            list.Add(before != null && projected.Equals(before) ? before : projected);
        }

        if (list.Count == current.Count && list.Zip(current).All(p => ReferenceEquals(p.First, p.Second))) return;
        Push(list);
    }

    /// <summary>Exécute <paramref name="write" /> (écritures dans les ViewModels) sans que leurs notifications ne reviennent ici.</summary>
    protected void Write(Action write)
    {
        _writing++;
        try
        {
            write();
        }
        finally
        {
            _writing--;
        }
    }

    /// <summary>Retire ces ViewModels de la collection source (si elle est modifiable), puis resynchronise une seule fois.</summary>
    protected void RemoveModels(IEnumerable<string> ids)
    {
        if (_source is not IList { IsReadOnly: false, IsFixedSize: false } list) return;
        var any = false;
        Write(() =>
        {
            foreach (var id in ids)
                if (_modelById.TryGetValue(id, out var model))
                {
                    list.Remove(model);
                    _removed[id] = model;
                    if (_removed.Count > 500) _removed.Remove(_removed.Keys.First());
                    any = true;
                }
        });
        if (any) Rebuild();
    }


    /// <summary>Remplace tout le contenu de la source (chargement d'un graphe) ; les ViewModels sont créés par <paramref name="create" /> puis alignés par <paramref name="init" />.</summary>
    protected void ReplaceAll<TItem>(IEnumerable<TItem> items, Func<TItem, object?> create, Action<object, TItem> init)
    {
        if (_source is not IList { IsReadOnly: false, IsFixedSize: false } list) return;
        Write(() =>
        {
            list.Clear();
            foreach (var item in items)
                if (create(item) is { } model)
                {
                    init(model, item);
                    list.Add(model);
                }
        });
        _removed.Clear();
        Rebuild();
    }
    /// <summary>
    ///     Ajoute à la source le ViewModel d'un élément apparu côté vue : celui qu'elle avait retiré (Undo), sinon
    ///     <paramref name="create" /> (la fabrique). <paramref name="init" /> aligne ses propriétés sur le record avant l'ajout.
    /// </summary>
    protected void AddModel(string id, Func<object?> create, Action<object> init)
    {
        if (_source is not IList { IsReadOnly: false, IsFixedSize: false } list) return;
        var model = _removed.Remove(id, out var restored) ? restored : create();
        if (model == null) return;
        Write(() =>
        {
            init(model);
            list.Add(model);
        });
        Rebuild();
    }
}

/// <summary>Adaptateur des nodes : <see cref="NodeMapping" /> dans les deux sens.</summary>
internal sealed class NodeSourceAdapter : SourceAdapter<Node>
{
    private readonly FlowView _flow;
    private readonly NodeMapping _map;

    public NodeSourceAdapter(FlowView flow, IEnumerable source, NodeMapping mapping) : base(source, mapping.Paths())
    {
        _flow = flow;
        _map = mapping;
        Start();
    }

    protected override IReadOnlyList<Node> Current => _flow.Nodes;
    protected override void Push(IReadOnlyList<Node> items) => _flow.SetCurrentValue(FlowView.NodesProperty, items);
    protected override string IdOfModel(Node item) => item.Id;
    protected override string IdOf(object model) => Reflect.Text(model, _map.IdPath) ?? "";

    protected override Node Project(object model, Node? previous)
    {
        var id = IdOf(model);
        var node = previous ?? new Node(id, XYPosition.Zero);
        var x = Reflect.Double(model, _map.XPath) ?? node.Position.X;
        var y = Reflect.Double(model, _map.YPath) ?? node.Position.Y;
        return node with
        {
            Id = id,
            Position = new XYPosition(x, y),
            Type = _map.TypePath == null ? node.Type : Reflect.Text(model, _map.TypePath) ?? "default",
            Data = _map.DataPath == null ? model : Reflect.Get(model, _map.DataPath),
            FixedWidth = _map.WidthPath == null ? node.FixedWidth : Reflect.Double(model, _map.WidthPath),
            FixedHeight = _map.HeightPath == null ? node.FixedHeight : Reflect.Double(model, _map.HeightPath),
            Selected = Reflect.Bool(model, _map.SelectedPath) ?? node.Selected,
            Draggable = Reflect.Bool(model, _map.DraggablePath) ?? node.Draggable,
            Selectable = Reflect.Bool(model, _map.SelectablePath) ?? node.Selectable,
            ParentId = _map.ParentIdPath == null ? node.ParentId : Reflect.Text(model, _map.ParentIdPath)
        };
    }

    /// <summary>Reporte dans les ViewModels ce que la vue vient d'appliquer.</summary>
    public void WriteBack(IReadOnlyList<NodeChange> changes)
    {
        List<string>? removed = null;
        List<Node>? added = null;
        Write(() =>
        {
            foreach (var change in changes)
                switch (change)
                {
                    case NodePositionChange p when ModelOf(p.Id) is { } m:
                        Reflect.Set(m, _map.XPath, p.Position.X);
                        Reflect.Set(m, _map.YPath, p.Position.Y);
                        break;
                    case NodeResizeChange r when ModelOf(r.Id) is { } m:
                        Reflect.Set(m, _map.WidthPath, r.Width);
                        Reflect.Set(m, _map.HeightPath, r.Height);
                        if (r.Position is { } pos)
                        {
                            Reflect.Set(m, _map.XPath, pos.X);
                            Reflect.Set(m, _map.YPath, pos.Y);
                        }

                        break;
                    case NodeSelectChange s when ModelOf(s.Id) is { } m:
                        Reflect.Set(m, _map.SelectedPath, s.Selected);
                        break;
                    case NodeRemoveChange r:
                        (removed ??= []).Add(r.Id);
                        break;
                    case NodeAddChange a:
                        (added ??= []).Add(a.Item);
                        break;
                }
        });

        if (removed != null) RemoveModels(removed);
        if (added == null) return;
        foreach (var node in added)
            AddModel(node.Id, () => _flow.NodeFactory?.Invoke(node), model => Align(model, node));
    }

    /// <summary>Écrit dans un ViewModel les valeurs d'un node (propriétés mappées uniquement).</summary>
    private void Align(object model, Node node)
    {
        Reflect.Set(model, _map.XPath, node.Position.X);
        Reflect.Set(model, _map.YPath, node.Position.Y);
        Reflect.Set(model, _map.SelectedPath, node.Selected);
        Reflect.Set(model, _map.WidthPath, node.FixedWidth);
        Reflect.Set(model, _map.HeightPath, node.FixedHeight);
        Reflect.Set(model, _map.TypePath, node.Type);
        Reflect.Set(model, _map.ParentIdPath, node.ParentId);
    }

    /// <summary>Remplace tous les ViewModels par ceux que <see cref="FlowView.NodeFactory" /> crée pour ces nodes.</summary>
    public void Load(IReadOnlyList<Node> nodes)
    {
        if (_flow.NodeFactory is not { } factory)
            throw new InvalidOperationException("Charger un graphe dans NodesSource demande une NodeFactory.");
        ReplaceAll(nodes, n => factory(n), Align);
    }

    /// <summary>Les changes qui font passer <paramref name="from" /> à <paramref name="to" /> (restauration d'un état d'Undo/Redo).</summary>
    public static List<NodeChange> Diff(IReadOnlyList<Node> from, IReadOnlyList<Node> to)
    {
        var changes = new List<NodeChange>();
        var before = new Dictionary<string, Node>(from.Count);
        foreach (var n in from) before.TryAdd(n.Id, n);
        var after = new HashSet<string>(to.Select(n => n.Id));
        changes.AddRange(from.Where(n => !after.Contains(n.Id)).Select(n => (NodeChange)new NodeRemoveChange(n.Id)));
        foreach (var n in to)
        {
            if (!before.TryGetValue(n.Id, out var o))
            {
                changes.Add(new NodeAddChange(n));
                continue;
            }

            if (o.Position != n.Position) changes.Add(new NodePositionChange(n.Id, n.Position));
            if (o.Selected != n.Selected) changes.Add(new NodeSelectChange(n.Id, n.Selected));
            if ((o.FixedWidth != n.FixedWidth || o.FixedHeight != n.FixedHeight) &&
                n.FixedWidth is { } w && n.FixedHeight is { } h)
                changes.Add(new NodeResizeChange(n.Id, w, h));
        }

        return changes;
    }
}

/// <summary>Adaptateur des edges : <see cref="EdgeMapping" /> dans les deux sens.</summary>
internal sealed class EdgeSourceAdapter : SourceAdapter<Edge>
{
    private readonly FlowView _flow;
    private readonly EdgeMapping _map;

    public EdgeSourceAdapter(FlowView flow, IEnumerable source, EdgeMapping mapping) : base(source, mapping.Paths())
    {
        _flow = flow;
        _map = mapping;
        Start();
    }

    protected override IReadOnlyList<Edge> Current => _flow.Edges;
    protected override void Push(IReadOnlyList<Edge> items) => _flow.SetCurrentValue(FlowView.EdgesProperty, items);
    protected override string IdOfModel(Edge item) => item.Id;
    protected override string IdOf(object model) => Reflect.Text(model, _map.IdPath) ?? "";

    protected override Edge Project(object model, Edge? previous)
    {
        var id = IdOf(model);
        var edge = previous ?? new Edge(id, "", "");
        return edge with
        {
            Id = id,
            Source = Reflect.Text(model, _map.SourcePath) ?? "",
            Target = Reflect.Text(model, _map.TargetPath) ?? "",
            SourceHandle = _map.SourceHandlePath == null ? edge.SourceHandle : Reflect.Text(model, _map.SourceHandlePath),
            TargetHandle = _map.TargetHandlePath == null ? edge.TargetHandle : Reflect.Text(model, _map.TargetHandlePath),
            Type = _map.TypePath == null ? edge.Type : Reflect.Text(model, _map.TypePath) ?? "default",
            Label = _map.LabelPath == null ? edge.Label : Reflect.Text(model, _map.LabelPath),
            Animated = Reflect.Bool(model, _map.AnimatedPath) ?? edge.Animated,
            Selected = Reflect.Bool(model, _map.SelectedPath) ?? edge.Selected
        };
    }

    public void WriteBack(IReadOnlyList<EdgeChange> changes)
    {
        List<string>? removed = null;
        List<Edge>? added = null;
        Write(() =>
        {
            foreach (var change in changes)
                switch (change)
                {
                    case EdgeSelectChange s when ModelOf(s.Id) is { } m:
                        Reflect.Set(m, _map.SelectedPath, s.Selected);
                        break;
                    case EdgeReplaceChange r when ModelOf(r.Id) is { } m: // reconnexion
                        Reflect.Set(m, _map.SourcePath, r.Item.Source);
                        Reflect.Set(m, _map.TargetPath, r.Item.Target);
                        Reflect.Set(m, _map.SourceHandlePath, r.Item.SourceHandle);
                        Reflect.Set(m, _map.TargetHandlePath, r.Item.TargetHandle);
                        break;
                    case EdgeRemoveChange r:
                        (removed ??= []).Add(r.Id);
                        break;
                    case EdgeAddChange a:
                        (added ??= []).Add(a.Item);
                        break;
                }
        });

        if (removed != null) RemoveModels(removed);
        if (added == null) return;
        foreach (var edge in added)
            AddModel(edge.Id, () => _flow.EdgeFactory?.Invoke(edge), model => Align(model, edge));
    }

    /// <summary>Écrit dans un ViewModel les valeurs d'un edge (propriétés mappées uniquement).</summary>
    private void Align(object model, Edge edge)
    {
        Reflect.Set(model, _map.SourcePath, edge.Source);
        Reflect.Set(model, _map.TargetPath, edge.Target);
        Reflect.Set(model, _map.SourceHandlePath, edge.SourceHandle);
        Reflect.Set(model, _map.TargetHandlePath, edge.TargetHandle);
        Reflect.Set(model, _map.TypePath, edge.Type);
        Reflect.Set(model, _map.LabelPath, edge.Label);
        Reflect.Set(model, _map.AnimatedPath, edge.Animated);
        Reflect.Set(model, _map.SelectedPath, edge.Selected);
    }

    /// <summary>Remplace tous les ViewModels par ceux que <see cref="FlowView.EdgeFactory" /> crée pour ces edges.</summary>
    public void Load(IReadOnlyList<Edge> edges)
    {
        if (_flow.EdgeFactory is not { } factory)
            throw new InvalidOperationException("Charger un graphe dans EdgesSource demande une EdgeFactory.");
        ReplaceAll(edges, e => factory(e), Align);
    }

    public static List<EdgeChange> Diff(IReadOnlyList<Edge> from, IReadOnlyList<Edge> to)
    {
        var changes = new List<EdgeChange>();
        var before = new Dictionary<string, Edge>(from.Count);
        foreach (var e in from) before.TryAdd(e.Id, e);
        var after = new HashSet<string>(to.Select(e => e.Id));
        changes.AddRange(from.Where(e => !after.Contains(e.Id)).Select(e => (EdgeChange)new EdgeRemoveChange(e.Id)));
        foreach (var e in to)
        {
            if (!before.TryGetValue(e.Id, out var o))
            {
                changes.Add(new EdgeAddChange(e));
                continue;
            }

            if (o.Source != e.Source || o.Target != e.Target || o.SourceHandle != e.SourceHandle || o.TargetHandle != e.TargetHandle)
                changes.Add(new EdgeReplaceChange(e.Id, e));
            if (o.Selected != e.Selected) changes.Add(new EdgeSelectChange(e.Id, e.Selected));
        }

        return changes;
    }
}
