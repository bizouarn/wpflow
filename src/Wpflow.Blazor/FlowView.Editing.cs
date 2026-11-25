using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Wpflow.Blazor;

/// <summary>Changes, sélection, suppression, presse-papier, historique et clavier.</summary>
public partial class FlowView
{
    private GraphHistory? _history;
    private ClipboardData? _clipboard;
    private int _pasteCount;
    private string? _focusedNode;
    private string? _focusedEdge;
    private readonly Dictionary<string, EdgeRenderContext> _edgeContexts = new();

    // ---------- Changes ----------

    public async Task ApplyNodeChanges(IReadOnlyList<NodeChange> changes)
    {
        if (changes.Count == 0) return;
        if (Debug) Console.WriteLine($"[wpflow] {changes.Count} node change(s): {string.Join(", ", changes.Select(c => c.GetType().Name).Distinct())}");
        await OnNodesChange.InvokeAsync(changes);
        if (!AutoApplyChanges) return;
        _forceLayer = true;
        Nodes = Changes.Apply(changes, Nodes);
        RebuildIndex();
        await NodesChanged.InvokeAsync(Nodes);
        if (changes.Any(c => c is NodeSelectChange or NodeRemoveChange or NodeAddChange or NodeReplaceChange)) await AfterSelectionMaybeChanged();
    }

    public async Task ApplyEdgeChanges(IReadOnlyList<EdgeChange> changes)
    {
        if (changes.Count == 0) return;
        if (Debug) Console.WriteLine($"[wpflow] {changes.Count} edge change(s): {string.Join(", ", changes.Select(c => c.GetType().Name).Distinct())}");
        await OnEdgesChange.InvokeAsync(changes);
        if (!AutoApplyChanges) return;
        _forceLayer = true;
        Edges = Changes.Apply(changes, Edges);
        await EdgesChanged.InvokeAsync(Edges);
        if (changes.Any(c => c is EdgeSelectChange or EdgeRemoveChange or EdgeAddChange or EdgeReplaceChange)) await AfterSelectionMaybeChanged();
    }

    // ---------- Sélection ----------

    public IReadOnlyList<Node> SelectedNodes => Nodes.Where(n => n.Selected).ToList();

    public IReadOnlyList<Edge> SelectedEdges => Edges.Where(e => e.Selected).ToList();

    private async Task AfterSelectionMaybeChanged()
    {
        var nodes = SelectedNodes;
        var edges = SelectedEdges;
        var signature = string.Join(",", nodes.Select(n => n.Id)) + "|" + string.Join(",", edges.Select(e => e.Id));
        if (signature == _selectionSignature) return;
        var hadSelection = _selectionSignature is { Length: > 1 };
        _selectionSignature = signature;
        if (AnnounceSelection)
            _announce = (nodes.Count, edges.Count) switch
            {
                (0, 0) => hadSelection ? "Sélection effacée" : null,
                (1, 0) => SelectionOps.AnnounceNode(nodes[0], NodeAriaLabelProvider),
                (0, 1) => SelectionOps.AnnounceEdge(edges[0], EdgeAriaLabelProvider),
                _ => $"{nodes.Count + edges.Count} éléments sélectionnés"
            };
        await OnSelectionChange.InvokeAsync(new SelectionChangeArgs(nodes, edges));
    }

    /// <summary>Sélectionne uniquement ce node et/ou cet edge ; <c>null</c>, <c>null</c> efface la sélection.</summary>
    public async Task SelectOnly(string? nodeId, string? edgeId)
    {
        var (nodeChanges, edgeChanges) = SelectionOps.Select(Nodes, Edges, nodeId == null ? [] : [nodeId], edgeId == null ? [] : [edgeId], false);
        await ApplyNodeChanges(nodeChanges);
        await ApplyEdgeChanges(edgeChanges);
    }

    public Task ClearSelection() => SelectOnly(null, null);

    public async Task SelectAll()
    {
        if (!CanSelect) return;
        await ApplyNodeChanges(Nodes.Where(n => !n.Selected && n.Selectable && !n.Hidden).Select(n => (NodeChange)new NodeSelectChange(n.Id, true)).ToList());
    }

    // ---------- Suppression ----------

    /// <summary>Supprime les nodes et edges sélectionnés (et les edges reliés aux nodes supprimés).</summary>
    public Task<bool> DeleteSelected() =>
        DeleteElements(Nodes.Where(n => n.Selected).Select(n => n.Id), Edges.Where(e => e.Selected).Select(e => e.Id));

    /// <summary>
    ///     Supprime des éléments (<c>deleteElements</c>) : les nodes non supprimables sont ignorés, les descendants et les
    ///     edges reliés suivent. <see cref="OnBeforeDelete" /> peut refuser. Renvoie vrai si quelque chose a été supprimé.
    /// </summary>
    public async Task<bool> DeleteElements(IEnumerable<string>? nodeIds, IEnumerable<string>? edgeIds = null)
    {
        if (GraphDeletion.Plan(Nodes, Edges, nodeIds, edgeIds) is not { } plan) return false;
        var nodeSet = plan.Nodes;
        var edges = Edges.Where(e => plan.Edges.Contains(e.Id) || plan.EdgesOfNodes.Contains(e.Id)).ToList();
        var request = new DeleteRequest(Nodes.Where(n => nodeSet.Contains(n.Id)).ToList(), edges);
        if (OnBeforeDelete != null && !await OnBeforeDelete(request)) return false;

        PushHistory();
        await ApplyEdgeChanges(edges.Select(e => (EdgeChange)new EdgeRemoveChange(e.Id)).ToList());
        await ApplyNodeChanges(nodeSet.Select(id => (NodeChange)new NodeRemoveChange(id)).ToList());
        await OnDelete.InvokeAsync(request);
        return true;
    }

    // ---------- Ajout et mise à jour ----------

    public Task AddNodes(IEnumerable<Node> nodes) => ApplyNodeChanges(nodes.Select(n => (NodeChange)new NodeAddChange(n)).ToList());

    public Task AddEdges(IEnumerable<Edge> edges) => ApplyEdgeChanges(edges.Select(e => (EdgeChange)new EdgeAddChange(e)).ToList());

    public async Task UpdateNode(string id, Func<Node, Node> update)
    {
        if (_nodeIndex.TryGetValue(id, out var n)) await ApplyNodeChanges([new NodeReplaceChange(id, update(n))]);
    }

    public Task UpdateNodeData(string id, object? data) => UpdateNode(id, n => n with { Data = data });

    public Task UpdateNodeData(string id, Func<object?, object?> update) => UpdateNode(id, n => n with { Data = update(n.Data) });

    public async Task UpdateEdge(string id, Func<Edge, Edge> update)
    {
        var e = Edges.FirstOrDefault(x => x.Id == id);
        if (e != null) await ApplyEdgeChanges([new EdgeReplaceChange(id, update(e))]);
    }

    public Task UpdateEdgeData(string id, object? data) => UpdateEdge(id, e => e with { Data = data });

    public Task UpdateEdgeData(string id, Func<object?, object?> update) => UpdateEdge(id, e => e with { Data = update(e.Data) });

    // ---------- Presse-papier ----------

    /// <summary>Copie la sélection (nodes, descendants, edges internes) dans le presse-papier du composant.</summary>
    public bool Copy()
    {
        _clipboard = GraphClipboard.Copy(Nodes, Edges);
        _pasteCount = 0;
        return _clipboard != null;
    }

    public async Task<bool> Cut()
    {
        if (!Copy()) return false;
        return await DeleteSelected();
    }

    /// <summary>Colle le presse-papier : copies sélectionnées, décalées de 20 px à chaque collage.</summary>
    public async Task<bool> Paste()
    {
        if (_clipboard == null || IsLocked) return false;
        _pasteCount++;
        var offset = GraphClipboard.PasteOffset(_pasteCount);
        var pasted = GraphClipboard.Paste(_clipboard, Nodes, Edges, offset);
        PushHistory();
        var deselect = Nodes.Where(n => n.Selected).Select(n => (NodeChange)new NodeSelectChange(n.Id, false));
        await ApplyNodeChanges(deselect.Concat(pasted.Nodes.Select(n => (NodeChange)new NodeAddChange(n))).ToList());
        await ApplyEdgeChanges(Edges.Where(e => e.Selected).Select(e => (EdgeChange)new EdgeSelectChange(e.Id, false))
            .Concat(pasted.Edges.Select(e => (EdgeChange)new EdgeAddChange(e))).ToList());
        return true;
    }

    // ---------- Historique ----------

    public bool CanUndo => _history?.CanUndo == true;

    public bool CanRedo => _history?.CanRedo == true;

    /// <summary>Enregistre l'état courant avant une modification annulable.</summary>
    private void PushHistory()
    {
        if (!HistoryEnabled) return;
        (_history ??= new GraphHistory(HistoryLimit)).Push(Nodes, Edges);
    }

    public async Task<bool> Undo()
    {
        if (_history == null || !_history.TryUndo(Nodes, Edges, out var snapshot)) return false;
        await Restore(snapshot);
        return true;
    }

    public async Task<bool> Redo()
    {
        if (_history == null || !_history.TryRedo(Nodes, Edges, out var snapshot)) return false;
        await Restore(snapshot);
        return true;
    }

    private async Task Restore(GraphHistory.Snapshot snapshot)
    {
        var edges = Changesets.Diff(Edges, snapshot.Edges);
        var nodes = Changesets.Diff(Nodes, snapshot.Nodes);
        await ApplyEdgeChanges(edges);
        await ApplyNodeChanges(nodes);
        StateHasChanged();
    }

    /// <summary>Vide l'historique (après un chargement, par exemple).</summary>
    public void ClearHistory()
    {
        _history?.Clear();
    }

    // ---------- Clavier ----------

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == " " && PanActivationKey == "Space") _space = true;
        var ctrl = e.CtrlKey || e.MetaKey;
        var key = e.Key;

        if (ctrl && !e.AltKey)
        {
            switch (key.ToLowerInvariant())
            {
                case "a" when !DisableKeyboardA11y:
                    await SelectAll();
                    return;
                case "c":
                    Copy();
                    return;
                case "x":
                    await Cut();
                    return;
                case "v":
                    await Paste();
                    return;
                case "z" when e.ShiftKey:
                case "y":
                    await Redo();
                    return;
                case "z":
                    await Undo();
                    return;
            }

            return;
        }

        if (DeleteKeyEnabled && DeleteKeys.Contains(key))
        {
            if (CanSelect) await DeleteSelected();
            return;
        }

        if (DisableKeyboardA11y) return;
        switch (key)
        {
            case "Escape":
                if (_clickMode) await CancelClickConnect();
                else await ClearSelection();
                break;
            case "Enter" or " " when _focusedNode != null && _nodeIndex.TryGetValue(_focusedNode, out var fn) && CanSelect && fn.Selectable:
                if (e.ShiftKey || ctrl) await ApplyNodeChanges([new NodeSelectChange(fn.Id, !fn.Selected)]);
                else await SelectOnly(fn.Id, null);
                break;
            case "Enter" or " " when _focusedEdge != null && CanSelect:
                await SelectOnly(null, _focusedEdge);
                break;
            case "ArrowLeft" or "ArrowRight" or "ArrowUp" or "ArrowDown":
                await MoveSelectedByKey(key, e.ShiftKey);
                break;
        }
    }

    private Task OnKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == " ") _space = false;
        return Task.CompletedTask;
    }

    private async Task MoveSelectedByKey(string key, bool fast)
    {
        if (!CanDrag) return;
        var (dx, dy) = KeyboardMove.Delta(key switch
        {
            "ArrowLeft" => KeyboardMove.Arrow.Left,
            "ArrowRight" => KeyboardMove.Arrow.Right,
            "ArrowUp" => KeyboardMove.Arrow.Up,
            _ => KeyboardMove.Arrow.Down
        }, fast);
        var start = DragSelection.CollectStart(Nodes, CanDrag, CanSelect, SelectNodesOnDrag);
        if (start.Count == 0) return;
        PushHistory();
        await ApplyNodeChanges(start.Select(kv => (NodeChange)new NodePositionChange(kv.Key,
            ConstrainPosition(_nodeIndex[kv.Key], new XYPosition(kv.Value.X + dx, kv.Value.Y + dy)))).ToList());
    }

    internal void NodeFocused(Node n) => _focusedNode = n.Id;

    private void EdgeFocused(Edge e) => _focusedEdge = e.Id;

    // ---------- Double-clic, menu contextuel, survol, dépôt ----------

    internal async Task NodeDoubleClick(Node n, MouseEventArgs e) =>
        await OnNodeDoubleClick.InvokeAsync(NodeArgs(n, MakePointer(Rel(e.ClientX, e.ClientY), e.ShiftKey, e.CtrlKey, e.AltKey, e.MetaKey)));

    private async Task EdgeDoubleClick(Edge edge, MouseEventArgs e) =>
        await OnEdgeDoubleClick.InvokeAsync(new EdgeEventArgs(edge, MakePointer(Rel(e.ClientX, e.ClientY), e.ShiftKey, e.CtrlKey, e.AltKey, e.MetaKey)));

    private async Task PaneDoubleClick(MouseEventArgs e)
    {
        if (!ZoomOnDoubleClick) return;
        var p = Rel(e.ClientX, e.ClientY);
        await AnimateTo(ViewportMath.ZoomAt(Viewport, p, Viewport.Zoom * 2, MinZoom, MaxZoom), TimeSpan.FromMilliseconds(200));
    }

    internal async Task NodeContextMenu(Node n, MouseEventArgs e)
    {
        var pointer = MakePointer(Rel(e.ClientX, e.ClientY), e.ShiftKey, e.CtrlKey, e.AltKey, e.MetaKey);
        if (n.Selected && Nodes.Count(x => x.Selected) > 1 && OnSelectionContextMenu.HasDelegate)
            await OnSelectionContextMenu.InvokeAsync(new PaneEventArgs(pointer));
        else await OnNodeContextMenu.InvokeAsync(NodeArgs(n, pointer));
    }

    private async Task EdgeContextMenu(Edge edge, MouseEventArgs e) =>
        await OnEdgeContextMenu.InvokeAsync(new EdgeEventArgs(edge, MakePointer(Rel(e.ClientX, e.ClientY), e.ShiftKey, e.CtrlKey, e.AltKey, e.MetaKey)));

    private async Task PaneContextMenu(MouseEventArgs e) =>
        await OnPaneContextMenu.InvokeAsync(new PaneEventArgs(MakePointer(Rel(e.ClientX, e.ClientY), e.ShiftKey, e.CtrlKey, e.AltKey, e.MetaKey)));

    internal EventCallback<PointerEventArgs> NodeEnterHandler(Node n) =>
        OnNodeMouseEnter.HasDelegate ? Cb(e => OnNodeMouseEnter.InvokeAsync(new NodeEventArgs(n, MakePointer(e)))) : default;

    internal EventCallback<PointerEventArgs> NodeLeaveHandler(Node n) =>
        OnNodeMouseLeave.HasDelegate ? Cb(e => OnNodeMouseLeave.InvokeAsync(new NodeEventArgs(n, MakePointer(e)))) : default;

    private EventCallback<PointerEventArgs> EdgeEnterHandler(Edge edge) =>
        OnEdgeMouseEnter.HasDelegate ? Cb(e => OnEdgeMouseEnter.InvokeAsync(new EdgeEventArgs(edge, MakePointer(e)))) : default;

    private EventCallback<PointerEventArgs> EdgeLeaveHandler(Edge edge) =>
        OnEdgeMouseLeave.HasDelegate ? Cb(e => OnEdgeMouseLeave.InvokeAsync(new EdgeEventArgs(edge, MakePointer(e)))) : default;

    private EventCallback<PointerEventArgs> PaneEnterHandler =>
        OnPaneMouseEnter.HasDelegate ? Cb(e => OnPaneMouseEnter.InvokeAsync(new PaneEventArgs(MakePointer(e)))) : default;

    private EventCallback<PointerEventArgs> PaneLeaveHandler =>
        OnPaneMouseLeave.HasDelegate ? Cb(e => OnPaneMouseLeave.InvokeAsync(new PaneEventArgs(MakePointer(e)))) : default;

    private EventCallback<PointerEventArgs> Cb(Func<PointerEventArgs, Task> f) => EventCallback.Factory.Create(this, f);

    private async Task OnDropped(DragEventArgs e) =>
        await OnDrop.InvokeAsync(new FlowDropEventArgs(ClientToFlow(e.ClientX, e.ClientY), e));
}
