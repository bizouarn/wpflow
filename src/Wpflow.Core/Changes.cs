namespace Wpflow.Core;

/// <summary>Modification atomique d'un node (équivalent de <c>NodeChange</c> dans xyflow).</summary>
public abstract record NodeChange;

public sealed record NodePositionChange(string Id, XYPosition Position) : NodeChange;

public sealed record NodeDimensionsChange(string Id, double Width, double Height) : NodeChange;

/// <summary>Redimensionnement : fixe la taille explicite du node (et sa position si le bord gauche/haut a bougé).</summary>
public sealed record NodeResizeChange(string Id, double Width, double Height, XYPosition? Position = null) : NodeChange;

public sealed record NodeSelectChange(string Id, bool Selected) : NodeChange;

/// <summary>Un déplacement ou un redimensionnement commence ou se termine (<c>node.dragging</c>, <c>node.resizing</c>) ; <c>null</c> laisse la valeur.</summary>
public sealed record NodeInteractionChange(string Id, bool? Dragging = null, bool? Resizing = null) : NodeChange;

public sealed record NodeRemoveChange(string Id) : NodeChange;

public sealed record NodeAddChange(Node Item) : NodeChange;

public sealed record NodeReplaceChange(string Id, Node Item) : NodeChange;

/// <summary>Modification atomique d'un edge.</summary>
public abstract record EdgeChange;

public sealed record EdgeSelectChange(string Id, bool Selected) : EdgeChange;

public sealed record EdgeRemoveChange(string Id) : EdgeChange;

public sealed record EdgeAddChange(Edge Item) : EdgeChange;

public sealed record EdgeReplaceChange(string Id, Edge Item) : EdgeChange;

/// <summary>Applique des changes à une liste immuable (équivalent de <c>applyNodeChanges</c> / <c>applyEdgeChanges</c>).</summary>
public static class Changes
{
    // Au-delà de ce nombre de changes, on construit un index id -> position : sinon chaque change coûte O(n)
    // (le chargement de N nodes émet N NodeDimensionsChange d'un coup).
    private const int IndexThreshold = 8;

    public static IReadOnlyList<Node> Apply(IEnumerable<NodeChange> changes, IReadOnlyList<Node> nodes)
    {
        var list = changes as IReadOnlyList<NodeChange> ?? changes.ToList();
        if (list.Count == 0) return nodes;

        var items = new IdList<Node>(nodes, n => n.Id, list.Count >= IndexThreshold);
        foreach (var change in list)
        {
            switch (change)
            {
                case NodeAddChange add:
                    items.Add(add.Item);
                    break;
                case NodeRemoveChange remove:
                    items.Remove(remove.Id);
                    break;
                case NodeReplaceChange replace:
                    items.Update(replace.Id, _ => replace.Item);
                    break;
                case NodePositionChange pos:
                    items.Update(pos.Id, n => n with { Position = pos.Position });
                    break;
                case NodeDimensionsChange dim:
                    items.Update(dim.Id, n => n with { Width = dim.Width, Height = dim.Height });
                    break;
                case NodeResizeChange resize:
                    items.Update(resize.Id, n => n with
                    {
                        FixedWidth = resize.Width, FixedHeight = resize.Height,
                        Width = resize.Width, Height = resize.Height,
                        Position = resize.Position ?? n.Position
                    });
                    break;
                case NodeInteractionChange act:
                    items.Update(act.Id, n => n with { Dragging = act.Dragging ?? n.Dragging, Resizing = act.Resizing ?? n.Resizing });
                    break;
                case NodeSelectChange sel:
                    items.Update(sel.Id, n => n with { Selected = sel.Selected });
                    break;
            }
        }

        return items.Items;
    }

    public static IReadOnlyList<Edge> Apply(IEnumerable<EdgeChange> changes, IReadOnlyList<Edge> edges)
    {
        var list = changes as IReadOnlyList<EdgeChange> ?? changes.ToList();
        if (list.Count == 0) return edges;

        var items = new IdList<Edge>(edges, e => e.Id, list.Count >= IndexThreshold);
        foreach (var change in list)
        {
            switch (change)
            {
                case EdgeAddChange add:
                    items.Add(add.Item);
                    break;
                case EdgeRemoveChange remove:
                    items.Remove(remove.Id);
                    break;
                case EdgeReplaceChange replace:
                    items.Update(replace.Id, _ => replace.Item);
                    break;
                case EdgeSelectChange sel:
                    items.Update(sel.Id, e => e with { Selected = sel.Selected });
                    break;
            }
        }

        return items.Items;
    }

    /// <summary>Copie modifiable d'une liste, avec recherche par id (linéaire, ou indexée pour les gros lots).</summary>
    private sealed class IdList<T>(IReadOnlyList<T> source, Func<T, string> idOf, bool indexed) where T : class
    {
        private Dictionary<string, int>? _index;

        public List<T> Items { get; } = source.ToList();

        private int Find(string id)
        {
            if (!indexed) return Items.FindIndex(item => idOf(item) == id);
            if (_index == null)
            {
                // Premier élément gagnant en cas de doublon, comme FindIndex.
                _index = new Dictionary<string, int>(Items.Count);
                for (var i = 0; i < Items.Count; i++) _index.TryAdd(idOf(Items[i]), i);
            }

            return _index.TryGetValue(id, out var found) ? found : -1;
        }

        public void Add(T item)
        {
            Items.Add(item);
            _index?.TryAdd(idOf(item), Items.Count - 1);
        }

        public void Remove(string id)
        {
            if (Items.RemoveAll(item => idOf(item) == id) > 0) _index = null; // les positions ont changé
        }

        public void Update(string id, Func<T, T> update)
        {
            var i = Find(id);
            if (i < 0) return;
            var updated = update(Items[i]);
            if (_index != null && idOf(updated) != id) _index = null; // un remplacement peut changer l'id
            Items[i] = updated;
        }
    }
}
