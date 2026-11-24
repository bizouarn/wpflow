namespace Wpflow.Core;

/// <summary>Modification atomique d'un node (équivalent de <c>NodeChange</c> dans xyflow).</summary>
public abstract record NodeChange;

public sealed record NodePositionChange(string Id, XYPosition Position) : NodeChange;

public sealed record NodeDimensionsChange(string Id, double Width, double Height) : NodeChange;

/// <summary>Redimensionnement : fixe la taille explicite du node (et sa position si le bord gauche/haut a bougé).</summary>
public sealed record NodeResizeChange(string Id, double Width, double Height, XYPosition? Position = null) : NodeChange;

public sealed record NodeSelectChange(string Id, bool Selected) : NodeChange;

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
    public static IReadOnlyList<Node> Apply(IEnumerable<NodeChange> changes, IReadOnlyList<Node> nodes)
    {
        var result = nodes.ToList();
        foreach (var change in changes)
        {
            switch (change)
            {
                case NodeAddChange add:
                    result.Add(add.Item);
                    break;
                case NodeRemoveChange remove:
                    result.RemoveAll(n => n.Id == remove.Id);
                    break;
                case NodeReplaceChange replace:
                    Update(result, replace.Id, _ => replace.Item);
                    break;
                case NodePositionChange pos:
                    Update(result, pos.Id, n => n with { Position = pos.Position });
                    break;
                case NodeDimensionsChange dim:
                    Update(result, dim.Id, n => n with { Width = dim.Width, Height = dim.Height });
                    break;
                case NodeResizeChange resize:
                    Update(result, resize.Id, n => n with
                    {
                        FixedWidth = resize.Width, FixedHeight = resize.Height,
                        Width = resize.Width, Height = resize.Height,
                        Position = resize.Position ?? n.Position
                    });
                    break;
                case NodeSelectChange sel:
                    Update(result, sel.Id, n => n with { Selected = sel.Selected });
                    break;
            }
        }

        return result;
    }

    public static IReadOnlyList<Edge> Apply(IEnumerable<EdgeChange> changes, IReadOnlyList<Edge> edges)
    {
        var result = edges.ToList();
        foreach (var change in changes)
        {
            switch (change)
            {
                case EdgeAddChange add:
                    result.Add(add.Item);
                    break;
                case EdgeRemoveChange remove:
                    result.RemoveAll(e => e.Id == remove.Id);
                    break;
                case EdgeReplaceChange replace:
                    Update(result, replace.Id, _ => replace.Item);
                    break;
                case EdgeSelectChange sel:
                    Update(result, sel.Id, e => e with { Selected = sel.Selected });
                    break;
            }
        }

        return result;
    }

    private static void Update<T>(List<T> list, string id, Func<T, T> update) where T : class
    {
        var index = list.FindIndex(item => IdOf(item) == id);
        if (index >= 0) list[index] = update(list[index]);
    }

    private static string IdOf<T>(T item) => item switch
    {
        Node n => n.Id,
        Edge e => e.Id,
        _ => throw new ArgumentException("Type non supporté.", nameof(item))
    };
}
