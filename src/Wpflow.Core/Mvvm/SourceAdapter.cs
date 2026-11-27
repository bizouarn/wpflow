using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Wpflow.Core;

/// <summary>
///     Projette une collection de ViewModels (<c>FlowView.NodesSource</c> / <c>EdgesSource</c>) vers les listes de
///     <c>record</c> immuables d'une vue, et écrit dans le ViewModel les changes appliqués par la vue (position, taille,
///     sélection, suppression…). Les records qui n'ont pas changé gardent leur référence : la vue ne retraite que ce qui a
///     bougé. Commun aux vues WPF et Blazor ; à utiliser sur le thread (ou le contexte de rendu) de la vue.
///     La source ne retient pas l'adaptateur : gardez-en une référence tant qu'il doit la suivre.
/// </summary>
public abstract class SourceAdapter<T> : IDisposable where T : class
{
    private readonly Dictionary<object, string> _idByModel = new(ReferenceEqualityComparer.Instance);
    private readonly IEnumerable _source;
    private readonly HashSet<string> _paths;
    private readonly Func<IReadOnlyList<T>> _current;
    private readonly Action<IReadOnlyList<T>> _push;
    private readonly WeakListener _listener;
    private readonly Dictionary<string, object> _modelById = new();
    private readonly Dictionary<string, object> _removed = new(); // ViewModels retirés par la vue : un Undo les remet tels quels
    private int _writing; // > 0 pendant l'écriture vers le ViewModel : ses PropertyChanged ne sont pas des changements à relire
    private HashSet<object>? _notified; // ViewModels qui ont notifié pendant la réécriture d'un change : à relire ensuite
    private List<ModelBinding<T>> _bindings = [];
    private string _idPath = "Id";
    private string? _dataPath;
    private HashSet<string?>? _gesturePaths;

    /// <summary>
    ///     Un ViewModel a notifié sans que son record change (propriété non mappée : un titre que le modèle affiche). WPF
    ///     suit seul ses bindings ; une vue qui mémorise le rendu de ses éléments s'en sert pour redessiner celui-là.
    /// </summary>
    public event Action<T>? Notified;

    private protected SourceAdapter(IEnumerable source, Func<IReadOnlyList<T>> current, Action<IReadOnlyList<T>> push)
    {
        _source = source;
        _current = current;
        _push = push;
        _paths = [];
        _listener = new WeakListener(this);
    }

    /// <summary>
    ///     Un ViewModel a changé de lui-même (pas à cause de la vue) : le record d'avant et celui d'après. La vue s'en sert
    ///     pour que l'annulation d'un geste ne défasse pas ce changement.
    /// </summary>
    public event Action<T, T>? ChangedBySource;

    /// <summary>Les propriétés que les gestes modifient : l'annulation les restaure, même si la source les a ajustées depuis.</summary>
    private protected abstract IEnumerable<string?> GesturePaths { get; }

    private protected abstract string IdOfRecord(T item);
    private protected abstract T NewRecord(string id);
    private protected abstract object? DataOf(T item);
    private protected abstract T WithData(T item, object? data);

    /// <summary>La donnée d'un record quand aucune propriété n'est désignée par <c>DataPath</c>.</summary>
    private protected abstract object? DefaultData(object model, T previous);

    private protected void Start(string idPath, string? dataPath, List<ModelBinding<T>> bindings)
    {
        _idPath = idPath;
        _dataPath = dataPath;
        _bindings = bindings;
        _paths.Add(idPath);
        if (dataPath != null) _paths.Add(dataPath);
        foreach (var binding in bindings) _paths.Add(binding.Path);
        if (_source is INotifyCollectionChanged changed) changed.CollectionChanged += _listener.CollectionChanged;
        Rebuild();
    }

    public void Dispose()
    {
        if (_source is INotifyCollectionChanged changed) changed.CollectionChanged -= _listener.CollectionChanged;
        foreach (var model in _idByModel.Keys)
            if (model is INotifyPropertyChanged inpc)
                inpc.PropertyChanged -= _listener.ItemChanged;
        _idByModel.Clear();
        _modelById.Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>Le ViewModel correspondant à l'id d'un node/edge, ou <c>null</c>.</summary>
    public object? ModelOf(string id) => _modelById.GetValueOrDefault(id);

    /// <summary>L'id d'un ViewModel (ou <c>null</c> s'il n'appartient pas à la source).</summary>
    public string? IdOfKnown(object model) => _idByModel.GetValueOrDefault(model);

    private string IdOf(object model) => Reflect.Text(model, _idPath) ?? "";

    private T Project(object model, T? previous)
    {
        var id = IdOf(model);
        var record = previous != null && IdOfRecord(previous) == id ? previous : NewRecord(id);
        var before = record;
        foreach (var binding in _bindings) record = binding.Read(record, Reflect.Get(model, binding.Path));
        record = WithData(record, _dataPath == null ? DefaultData(model, before) : Reflect.Get(model, _dataPath));
        return record;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_writing == 0) Rebuild();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == null) return;
        if (!string.IsNullOrEmpty(e.PropertyName) && !_paths.Contains(e.PropertyName))
        {
            if (_writing == 0 && Notified != null && RecordOf(sender) is { } shown) Notified(shown);
            return;
        }

        if (_writing > 0)
        {
            _notified?.Add(sender);
            return;
        }

        if (!_idByModel.TryGetValue(sender, out var oldId) || IdOf(sender) != oldId)
        {
            Rebuild(); // l'identité a changé : tout est à recalculer
            return;
        }

        var current = _current();
        var index = -1;
        for (var i = 0; i < current.Count; i++)
            if (IdOfRecord(current[i]) == oldId)
            {
                index = i;
                break;
            }

        if (index < 0) return;
        var before = current[index];
        var updated = Project(sender, before);
        if (updated.Equals(before))
        {
            Notified?.Invoke(before);
            return;
        }

        var list = current.ToList();
        list[index] = updated;
        ChangedBySource?.Invoke(before, updated);
        _push(list);
    }

    /// <summary>
    ///     L'abonnement à la source et aux ViewModels, qui vivent souvent plus longtemps que la vue (document rouvert) : il ne
    ///     retient pas l'adaptateur, donc ni la vue qui le tient. Un abonnement devenu inutile se retire à la notification
    ///     suivante.
    /// </summary>
    private sealed class WeakListener(SourceAdapter<T> adapter)
    {
        private readonly WeakReference<SourceAdapter<T>> _adapter = new(adapter);

        public void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_adapter.TryGetTarget(out var target)) target.OnCollectionChanged(sender, e);
            else if (sender is INotifyCollectionChanged source) source.CollectionChanged -= CollectionChanged;
        }

        public void ItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_adapter.TryGetTarget(out var target)) target.OnItemChanged(sender, e);
            else if (sender is INotifyPropertyChanged item) item.PropertyChanged -= ItemChanged;
        }
    }

    /// <summary>Les éléments par id ; en cas de doublon, le premier gagne, comme dans la projection de la source.</summary>
    private protected static Dictionary<string, T> ById(IReadOnlyList<T> items, Func<T, string> id)
    {
        var byId = new Dictionary<string, T>(items.Count);
        foreach (var item in items)
            if (!byId.ContainsKey(id(item)))
                byId.Add(id(item), item);
        return byId;
    }

    private T? RecordOf(object model) =>
        _idByModel.TryGetValue(model, out var id) ? _current().FirstOrDefault(r => IdOfRecord(r) == id) : null;

    /// <summary>
    ///     Reporte sur <paramref name="target" /> les propriétés mappées qui diffèrent entre <paramref name="before" /> et
    ///     <paramref name="after" /> : un état gardé par l'historique reste ainsi à jour de ce que la source a changé. Les
    ///     propriétés que les gestes modifient (position, taille, sélection, rotation, extrémités d'un edge) sont laissées :
    ///     une application qui les ajuste après un geste (alignement en fin de glisser) doit pouvoir être annulée avec lui.
    /// </summary>
    public T Rebase(T target, T before, T after)
    {
        var gestures = _gesturePaths ??= GesturePaths.ToHashSet();
        foreach (var binding in _bindings)
            if (!gestures.Contains(binding.Path) && !Equals(binding.Value(before), binding.Value(after)))
                target = binding.Copy(target, after);
        if (!Equals(DataOf(before), DataOf(after))) target = WithData(target, DataOf(after));
        return target;
    }

    /// <summary>Recalcule la liste complète en réutilisant les records inchangés ; abonne les nouveaux ViewModels.</summary>
    private protected void Rebuild()
    {
        var current = _current();
        var previous = new Dictionary<string, T>(current.Count);
        foreach (var item in current) previous.TryAdd(IdOfRecord(item), item);

        var models = new List<object>();
        foreach (var model in _source)
            if (model != null)
                models.Add(model);

        var known = new HashSet<object>(models, ReferenceEqualityComparer.Instance);
        foreach (var gone in _idByModel.Keys.Where(m => !known.Contains(m)).ToList())
        {
            if (gone is INotifyPropertyChanged inpc) inpc.PropertyChanged -= _listener.ItemChanged;
            _idByModel.Remove(gone);
        }

        _modelById.Clear();
        var list = new List<T>(models.Count);
        var seen = new HashSet<string>();
        foreach (var model in models)
        {
            var id = IdOf(model);
            if (!seen.Add(id)) continue; // id en double : le premier gagne, comme dans Changes.Apply

            if (!_idByModel.ContainsKey(model) && model is INotifyPropertyChanged inpc) inpc.PropertyChanged += _listener.ItemChanged;
            _idByModel[model] = id;
            _modelById[id] = model;

            previous.TryGetValue(id, out var before);
            var projected = Project(model, before);
            list.Add(before != null && projected.Equals(before) ? before : projected);
        }

        if (list.Count == current.Count && list.Zip(current).All(p => ReferenceEquals(p.First, p.Second))) return;
        _push(list);
    }

    /// <summary>Exécute <paramref name="write" /> (écritures dans les ViewModels) sans que leurs notifications ne reviennent ici.</summary>
    private void Write(Action write)
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

    /// <summary>
    ///     Comme <see cref="Write" />, pour la réécriture des changes appliqués par la vue : un ViewModel qui notifie pendant
    ///     ce temps est relu ensuite, car il a pu ajuster ou refuser la valeur (setter qui borne, node verrouillé…). La vue
    ///     suit alors le ViewModel. Un ViewModel qui accepte donne le même record : rien n'est renvoyé à la vue.
    /// </summary>
    private protected void WriteBackAndRead(Action write)
    {
        var outer = _notified;
        var notified = _notified = new HashSet<object>(ReferenceEqualityComparer.Instance);
        try
        {
            Write(write);
        }
        finally
        {
            _notified = outer;
        }

        if (notified.Count == 0) return;
        var current = _current();
        var index = new Dictionary<string, int>(current.Count);
        for (var i = 0; i < current.Count; i++) index.TryAdd(IdOfRecord(current[i]), i);
        List<T>? list = null;
        foreach (var model in notified)
        {
            if (!_idByModel.TryGetValue(model, out var id) || !index.TryGetValue(id, out var at)) continue;
            var read = Project(model, current[at]);
            if (read.Equals(current[at])) continue;
            list ??= current.ToList();
            list[at] = read;
        }

        if (list != null) _push(list);
    }

    /// <summary>
    ///     Écrit dans un ViewModel les propriétés mappées d'un record. <paramref name="everything" /> faux (élément ajouté par
    ///     la vue) : les réglages restés à leur valeur par défaut ne sont pas écrits, pour ne pas écraser ceux du ViewModel.
    /// </summary>
    private protected void Align(object model, T item, bool everything)
    {
        var blank = everything ? null : NewRecord(IdOfRecord(item));
        foreach (var binding in _bindings)
            if (blank == null || binding.Always || !Equals(binding.Value(item), binding.Value(blank)))
                Reflect.Set(model, binding.Path, binding.Value(item));
    }

    /// <summary>Remplacement d'un record (<c>UpdateNode</c>, reconnexion, annulation) : toutes les propriétés, données comprises.</summary>
    private protected void Replace(object model, T item)
    {
        Align(model, item, everything: true);
        if (_dataPath != null) Reflect.Set(model, _dataPath, DataOf(item));
    }

    /// <summary>Retire ces ViewModels de la collection source (si elle est modifiable), puis resynchronise une seule fois.</summary>
    private protected void RemoveModels(IEnumerable<string> ids)
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

    /// <summary>Remplace tout le contenu de la source (chargement d'un graphe) par les ViewModels de <paramref name="create" />.</summary>
    private protected void ReplaceAll(IEnumerable<T> items, Func<T, object?> create)
    {
        if (_source is not IList { IsReadOnly: false, IsFixedSize: false } list) return;
        Write(() =>
        {
            list.Clear();
            foreach (var item in items)
                if (create(item) is { } model)
                {
                    Align(model, item, everything: false);
                    list.Add(model);
                }
        });
        _removed.Clear();
        Rebuild();
    }

    /// <summary>
    ///     Ajoute à la source le ViewModel d'un élément apparu côté vue : celui qu'elle avait retiré (Undo), sinon
    ///     <paramref name="create" /> (la fabrique).
    /// </summary>
    private protected void AddModel(T item, Func<object?> create)
    {
        if (_source is not IList { IsReadOnly: false, IsFixedSize: false } list) return;
        var restored = _removed.Remove(IdOfRecord(item), out var kept);
        var model = restored ? kept : create();
        if (model == null) return;
        Write(() =>
        {
            Align(model, item, everything: restored);
            list.Add(model);
        });
        Rebuild();
    }
}

/// <summary>Adaptateur des nodes : <see cref="NodeMappingBase" /> dans les deux sens.</summary>
public sealed class NodeSourceAdapter : SourceAdapter<Node>
{
    private readonly Func<Func<Node, object?>?> _factory;
    private readonly NodeMappingBase _mapping;

    /// <param name="source">La collection de ViewModels.</param>
    /// <param name="mapping">Les noms de leurs propriétés.</param>
    /// <param name="current">Les nodes affichés par la vue.</param>
    /// <param name="push">Remplace les nodes de la vue.</param>
    /// <param name="factory">La fabrique de ViewModels de la vue, ou <c>null</c>.</param>
    public NodeSourceAdapter(IEnumerable source, NodeMappingBase mapping, Func<IReadOnlyList<Node>> current,
        Action<IReadOnlyList<Node>> push, Func<Func<Node, object?>?> factory) : base(source, current, push)
    {
        _factory = factory;
        _mapping = mapping;
        Start(mapping.IdPath, mapping.DataPath, ModelBindings.For(mapping));
    }

    private protected override IEnumerable<string?> GesturePaths =>
        [_mapping.XPath, _mapping.YPath, _mapping.WidthPath, _mapping.HeightPath, _mapping.SelectedPath, _mapping.RotationPath];

    private protected override string IdOfRecord(Node item) => item.Id;
    private protected override Node NewRecord(string id) => new(id, XYPosition.Zero);
    private protected override object? DataOf(Node item) => item.Data;
    private protected override Node WithData(Node item, object? data) => item with { Data = data };
    private protected override object? DefaultData(object model, Node previous) => model;

    /// <summary>Reporte dans les ViewModels ce que la vue vient d'appliquer.</summary>
    public void WriteBack(IReadOnlyList<NodeChange> changes)
    {
        List<string>? removed = null;
        List<Node>? added = null;
        WriteBackAndRead(() =>
        {
            foreach (var change in changes)
                switch (change)
                {
                    case NodePositionChange p when ModelOf(p.Id) is { } m:
                        Reflect.Set(m, _mapping.XPath, p.Position.X);
                        Reflect.Set(m, _mapping.YPath, p.Position.Y);
                        break;
                    case NodeResizeChange r when ModelOf(r.Id) is { } m:
                        Reflect.Set(m, _mapping.WidthPath, r.Width);
                        Reflect.Set(m, _mapping.HeightPath, r.Height);
                        if (r.Position is { } pos)
                        {
                            Reflect.Set(m, _mapping.XPath, pos.X);
                            Reflect.Set(m, _mapping.YPath, pos.Y);
                        }

                        break;
                    case NodeSelectChange s when ModelOf(s.Id) is { } m:
                        Reflect.Set(m, _mapping.SelectedPath, s.Selected);
                        break;
                    case NodeReplaceChange r when ModelOf(r.Id) is { } m:
                        Replace(m, r.Item);
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
        foreach (var node in added) AddModel(node, () => _factory()?.Invoke(node));
    }

    /// <summary>Remplace tous les ViewModels par ceux que la fabrique crée pour ces nodes.</summary>
    public void Load(IReadOnlyList<Node> nodes)
    {
        if (_factory() is not { } factory)
            throw new InvalidOperationException("Charger un graphe dans NodesSource demande une NodeFactory.");
        ReplaceAll(nodes, n => factory(n));
    }

    /// <summary>Les changes qui font passer <paramref name="from" /> à <paramref name="to" /> (restauration d'un état d'Undo/Redo).</summary>
    public static List<NodeChange> Diff(IReadOnlyList<Node> from, IReadOnlyList<Node> to) =>
        Changesets.Walk<Node, NodeChange>(from, to, ById(from, n => n.Id), n => n.Id, id => new NodeRemoveChange(id),
            n => new NodeAddChange(n), Changed);

    // Une modification est détaillée autant que possible : un ViewModel ne reçoit que les propriétés concernées.
    private static void Changed(Node o, Node n, List<NodeChange> changes)
    {
        var resized = o.FixedWidth != n.FixedWidth || o.FixedHeight != n.FixedHeight;
        // Ce qui diffère en dehors de la position, de la sélection, de la taille et des états de la vue.
        var others = o with
        {
            Position = n.Position, Selected = n.Selected, FixedWidth = n.FixedWidth, FixedHeight = n.FixedHeight,
            Width = n.Width, Height = n.Height, Dragging = n.Dragging, Resizing = n.Resizing
        } != n;
        // Une taille redevenue automatique ne s'exprime pas par un redimensionnement.
        if (others || (resized && (n.FixedWidth == null || n.FixedHeight == null)))
        {
            changes.Add(new NodeReplaceChange(n.Id, n));
            return;
        }

        if (o.Position != n.Position) changes.Add(new NodePositionChange(n.Id, n.Position));
        if (o.Selected != n.Selected) changes.Add(new NodeSelectChange(n.Id, n.Selected));
        if (resized) changes.Add(new NodeResizeChange(n.Id, n.FixedWidth!.Value, n.FixedHeight!.Value));
    }
}

/// <summary>Adaptateur des edges : <see cref="EdgeMappingBase" /> dans les deux sens.</summary>
public sealed class EdgeSourceAdapter : SourceAdapter<Edge>
{
    private readonly Func<Func<Edge, object?>?> _factory;
    private readonly EdgeMappingBase _mapping;

    /// <inheritdoc cref="NodeSourceAdapter(IEnumerable,NodeMappingBase,Func{IReadOnlyList{Node}},Action{IReadOnlyList{Node}},Func{Func{Node,object?}?})" />
    public EdgeSourceAdapter(IEnumerable source, EdgeMappingBase mapping, Func<IReadOnlyList<Edge>> current,
        Action<IReadOnlyList<Edge>> push, Func<Func<Edge, object?>?> factory) : base(source, current, push)
    {
        _factory = factory;
        _mapping = mapping;
        Start(mapping.IdPath, mapping.DataPath, ModelBindings.For(mapping));
    }

    private protected override IEnumerable<string?> GesturePaths =>
        [_mapping.SourcePath, _mapping.TargetPath, _mapping.SourceHandlePath, _mapping.TargetHandlePath, _mapping.SelectedPath];

    private protected override string IdOfRecord(Edge item) => item.Id;
    private protected override Edge NewRecord(string id) => new(id, "", "");
    private protected override object? DataOf(Edge item) => item.Data;
    private protected override Edge WithData(Edge item, object? data) => item with { Data = data };
    private protected override object? DefaultData(object model, Edge previous) => previous.Data;

    /// <summary>Reporte dans les ViewModels ce que la vue vient d'appliquer.</summary>
    public void WriteBack(IReadOnlyList<EdgeChange> changes)
    {
        List<string>? removed = null;
        List<Edge>? added = null;
        WriteBackAndRead(() =>
        {
            foreach (var change in changes)
                switch (change)
                {
                    case EdgeSelectChange s when ModelOf(s.Id) is { } m:
                        Reflect.Set(m, _mapping.SelectedPath, s.Selected);
                        break;
                    case EdgeReplaceChange r when ModelOf(r.Id) is { } m:
                        Replace(m, r.Item);
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
        foreach (var edge in added) AddModel(edge, () => _factory()?.Invoke(edge));
    }

    /// <summary>Remplace tous les ViewModels par ceux que la fabrique crée pour ces edges.</summary>
    public void Load(IReadOnlyList<Edge> edges)
    {
        if (_factory() is not { } factory)
            throw new InvalidOperationException("Charger un graphe dans EdgesSource demande une EdgeFactory.");
        ReplaceAll(edges, e => factory(e));
    }

    /// <inheritdoc cref="NodeSourceAdapter.Diff" />
    public static List<EdgeChange> Diff(IReadOnlyList<Edge> from, IReadOnlyList<Edge> to) =>
        Changesets.Walk<Edge, EdgeChange>(from, to, ById(from, e => e.Id), e => e.Id, id => new EdgeRemoveChange(id),
            e => new EdgeAddChange(e), (o, e, changes) => changes.Add(o with { Selected = e.Selected } != e
                ? new EdgeReplaceChange(e.Id, e)
                : new EdgeSelectChange(e.Id, e.Selected)));
}
