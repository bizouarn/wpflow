using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

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
        _nodeSource?.WriteBack(changes); // un ViewModel qui ajuste ou refuse une valeur est relu
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
        _edgeSource?.WriteBack(changes);
        await EdgesChanged.InvokeAsync(Edges);
        if (changes.Any(c => c is EdgeSelectChange or EdgeRemoveChange or EdgeAddChange or EdgeReplaceChange)) await AfterSelectionMaybeChanged();
    }

    // ---------- Sélection ----------

    public IReadOnlyList<Node> SelectedNodes => Nodes.Where(n => n.Selected).ToList();

    public IReadOnlyList<Edge> SelectedEdges => Edges.Where(e => e.Selected).ToList();

    private async Task AfterSelectionMaybeChanged()
    {
        var hadSelection = _selectionTracker.HasSelection;
        if (!_selectionTracker.Update(Nodes, Edges)) return;
        var nodes = SelectedNodes;
        var edges = SelectedEdges;
        if (AnnounceSelection)
            _announce = (nodes.Count, edges.Count) switch
            {
                (0, 0) => hadSelection ? "Sélection effacée" : null,
                (1, 0) => SelectionOps.AnnounceNode(nodes[0], NodeAriaLabelProvider),
                (0, 1) => SelectionOps.AnnounceEdge(edges[0], EdgeAriaLabelProvider),
                _ => $"{nodes.Count + edges.Count} éléments sélectionnés"
            };
        await OnSelectionChange.InvokeAsync(new SelectionChangeArgs(nodes, edges));
        await ReportBoundSelection();
    }

    /// <summary>
    ///     Sélectionne uniquement ce node et/ou cet edge ; <c>null</c>, <c>null</c> efface la sélection. Sans
    ///     <see cref="ElementsSelectable" />, on ne peut que désélectionner.
    /// </summary>
    public Task SelectOnly(string? nodeId, string? edgeId) =>
        SelectElements(nodeId == null ? [] : [nodeId], edgeId == null ? [] : [edgeId], false);

    public Task ClearSelection() => SelectOnly(null, null);

    /// <summary>Sélectionne tous les nodes et tous les edges, comme en WPF.</summary>
    public Task SelectAll() =>
        CanSelect ? SelectElements(Nodes.Select(n => n.Id), Edges.Select(e => e.Id), false) : Task.CompletedTask;

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
        // Mode contrôlé : pendant un évènement du navigateur, Blazor diffère le rendu du parent ; on le laisse arriver,
        // pour que la nouvelle sélection soit annoncée avant OnDelete, comme en WPF.
        if (!AutoApplyChanges) await Task.Yield();
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

    public bool CanUndo => History?.CanUndo ?? _history?.CanUndo == true;

    public bool CanRedo => History?.CanRedo ?? _history?.CanRedo == true;

    /// <summary>Enregistre l'état courant avant une modification annulable.</summary>
    private void PushHistory()
    {
        if (History is { } own) // l'historique de l'application mémorise ce qu'il veut
        {
            own.Checkpoint();
            return;
        }

        if (!HistoryEnabled || !AutoApplyChanges) return; // en mode contrôlé, l'historique appartient à l'application
        (_history ??= new GraphHistory(HistoryLimit)).Push(Nodes, Edges);
    }

    public async Task<bool> Undo()
    {
        if (History is { } own)
        {
            if (!own.CanUndo) return false;
            own.Undo();
            return true;
        }

        if (!AutoApplyChanges || _history == null || !_history.TryUndo(Nodes, Edges, out var snapshot)) return false;
        await Restore(snapshot);
        return true;
    }

    public async Task<bool> Redo()
    {
        if (History is { } own)
        {
            if (!own.CanRedo) return false;
            own.Redo();
            return true;
        }

        if (!AutoApplyChanges || _history == null || !_history.TryRedo(Nodes, Edges, out var snapshot)) return false;
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

    private bool _keyFromField;

    /// <summary>Le pont JS dit, juste avant chaque touche, si elle vient d'un champ de saisie du graphe.</summary>
    [JSInvokable]
    public Task OnKeyOrigin(bool inField)
    {
        _keyFromField = inField;
        return Task.CompletedTask;
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (_keyFromField) return; // la touche appartient au champ : ni suppression, ni annulation, ni déplacement
        _tabbed = e.Key == "Tab";
        if (e.Key == " " && PanActivationKey == "Space")
        {
            _space = true;
            // Maintenir Espace répète la touche : le dessin n'est pas refait (une sélection au clavier le signale par ses changes).
            _keepsNextRender = true;
        }
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
        if (e.Key == " ")
        {
            _space = false;
            _keepsNextRender = true;
        }

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
        if (AutoPanOnNodeFocus) await EnsureVisible(start.Keys.First());
    }

    internal async Task NodeFocused(Node n)
    {
        _focusedNode = n.Id;
        if (!AutoPanOnNodeFocus || !_tabbed) return; // un clic donne aussi le focus : la vue ne doit pas bouger
        _tabbed = false;
        await EnsureVisible(n.Id);
    }

    private void EdgeFocused(Edge e) => _focusedEdge = e.Id;

    // ---------- Double-clic, menu contextuel, survol, dépôt ----------

    internal async Task NodeDoubleClick(Node n, MouseEventArgs e)
    {
        if (_lastPressUntracked) return; // double-clic dans un contrôle du node : il est à lui, comme en WPF
        await OnNodeDoubleClick.InvokeAsync(NodeArgs(n, MakePointer(Rel(e.ClientX, e.ClientY), e.ShiftKey, e.CtrlKey, e.AltKey, e.MetaKey)));
        await ReleaseHeldClick();
    }

    private async Task EdgeDoubleClick(Edge edge, MouseEventArgs e)
    {
        if (_lastPressUntracked) return;
        await OnEdgeDoubleClick.InvokeAsync(new EdgeEventArgs(edge, MakePointer(Rel(e.ClientX, e.ClientY), e.ShiftKey, e.CtrlKey, e.AltKey, e.MetaKey)));
        await ReleaseHeldClick();
    }

    // En WPF, un double-clic donne : clic, double-clic, clic. Le navigateur envoie son « dblclick » après le second clic :
    // celui-ci est retenu jusque-là.
    private Func<Task>? _heldClick;

    private Task RaiseClick(Func<Task> click, bool awaitsDoubleClick)
    {
        if (!_secondPress || !awaitsDoubleClick) return click();
        _heldClick = click;
        _ = ReleaseUnlessDoubleClicked(click);
        return Task.CompletedTask;
    }

    // Un élément du modèle peut arrêter le « dblclick » : le clic retenu ne doit pas être perdu.
    private async Task ReleaseUnlessDoubleClicked(Func<Task> click)
    {
        await Task.Delay(500);
        if (ReferenceEquals(_heldClick, click)) await InvokeAsync(ReleaseHeldClick);
    }

    private Task ReleaseHeldClick()
    {
        var click = _heldClick;
        _heldClick = null;
        return click?.Invoke() ?? Task.CompletedTask;
    }

    private async Task PaneDoubleClick(MouseEventArgs e)
    {
        if (!ZoomOnDoubleClick || _lastPressUntracked) return; // ni sur un panneau, ni sur un bouton posé sur le graphe
        var p = Rel(e.ClientX, e.ClientY);
        await AnimateTo(ViewportMath.ZoomAt(Viewport, p, Viewport.Zoom * 2, MinZoom, MaxZoom), TimeSpan.FromMilliseconds(200));
    }

    internal async Task NodeContextMenu(Node n, MouseEventArgs e)
    {
        if (_rightDragged) return; // le bouton droit vient de déplacer la vue : ce n'est pas un clic droit
        var pointer = MakePointer(Rel(e.ClientX, e.ClientY), e.ShiftKey, e.CtrlKey, e.AltKey, e.MetaKey);
        // Comme en WPF : le menu de la sélection, puis celui du node visé.
        var args = NodeArgs(n, pointer);
        if (n.Selected && Nodes.Count(x => x.Selected) > 1) await OnSelectionContextMenu.InvokeAsync(new PaneEventArgs(pointer));
        await OnNodeContextMenu.InvokeAsync(args);
    }

    private async Task EdgeContextMenu(Edge edge, MouseEventArgs e)
    {
        if (_rightDragged) return;
        await OnEdgeContextMenu.InvokeAsync(new EdgeEventArgs(edge, MakePointer(Rel(e.ClientX, e.ClientY), e.ShiftKey, e.CtrlKey, e.AltKey, e.MetaKey)));
    }

    private async Task PaneContextMenu(MouseEventArgs e)
    {
        if (_rightDragged) return;
        await OnPaneContextMenu.InvokeAsync(new PaneEventArgs(MakePointer(Rel(e.ClientX, e.ClientY), e.ShiftKey, e.CtrlKey, e.AltKey, e.MetaKey)));
    }

    // Survol. Comme en WPF, le « fond » est ce qui n'est ni un node ni un edge : le navigateur, lui, ne quitte pas le
    // conteneur quand le pointeur passe sur un de ses enfants. L'élément survolé est donc suivi ici.
    private string? _hoverElement; // « n:id » ou « e:id » ; null sur le fond
    private bool _paneHovered;

    private bool PaneHoverWanted => OnPaneMouseEnter.HasDelegate || OnPaneMouseLeave.HasDelegate || OnPaneMouseMove.HasDelegate;

    private Task SetPaneHover(bool over, PointerEventArgs e)
    {
        if (_paneHovered == over) return Task.CompletedTask;
        _paneHovered = over;
        return (over ? OnPaneMouseEnter : OnPaneMouseLeave).InvokeAsync(new PaneEventArgs(MakePointer(e)));
    }

    private async Task EnterElement(string key, PointerEventArgs e, Func<Task> enter)
    {
        _hoverElement = key;
        await SetPaneHover(false, e);
        await enter();
    }

    private async Task LeaveElement(PointerEventArgs e, Func<Task> leave)
    {
        _hoverElement = null;
        await leave();
        var p = Rel(e.ClientX, e.ClientY);
        if (p.X >= 0 && p.Y >= 0 && p.X < _width && p.Y < _height) await SetPaneHover(true, e);
    }

    internal EventCallback<PointerEventArgs> NodeEnterHandler(Node n) => OnNodeMouseEnter.HasDelegate || PaneHoverWanted
        ? Cb(e => EnterElement("n:" + n.Id, e, () => OnNodeMouseEnter.InvokeAsync(new NodeEventArgs(n, MakePointer(e)))))
        : default;

    internal EventCallback<PointerEventArgs> NodeLeaveHandler(Node n) => OnNodeMouseLeave.HasDelegate || PaneHoverWanted
        ? Cb(e => LeaveElement(e, () => OnNodeMouseLeave.InvokeAsync(new NodeEventArgs(n, MakePointer(e)))))
        : default;

    private EventCallback<PointerEventArgs> EdgeEnterHandler(Edge edge) => OnEdgeMouseEnter.HasDelegate || PaneHoverWanted
        ? Cb(e => EnterElement("e:" + edge.Id, e, () => OnEdgeMouseEnter.InvokeAsync(new EdgeEventArgs(edge, MakePointer(e)))))
        : default;

    private EventCallback<PointerEventArgs> EdgeLeaveHandler(Edge edge) => OnEdgeMouseLeave.HasDelegate || PaneHoverWanted
        ? Cb(e => LeaveElement(e, () => OnEdgeMouseLeave.InvokeAsync(new EdgeEventArgs(edge, MakePointer(e)))))
        : default;

    private EventCallback<PointerEventArgs> PaneEnterHandler => PaneHoverWanted
        ? Cb(e => _hoverElement == null ? SetPaneHover(true, e) : Task.CompletedTask)
        : default;

    private EventCallback<PointerEventArgs> PaneLeaveHandler => PaneHoverWanted
        ? Cb(e =>
        {
            _hoverElement = null;
            return SetPaneHover(false, e);
        })
        : default;

    // Un survol ne change rien au dessin : ni le rendu qui suit son évènement, ni celui d'un parent qui l'écoute. Ce que le
    // survol change pour de bon (fil d'une connexion par clics, données, modèles redéclarés) est signalé comme ailleurs.
    private EventCallback<PointerEventArgs> Cb(Func<PointerEventArgs, Task> f) => EventCallback.Factory.Create<PointerEventArgs>(this,
        async e =>
        {
            await NotifyKeepingLayer(() => f(e));
            _keepsNextRender = true;
        });

    private async Task OnDropped(DragEventArgs e) =>
        await OnDrop.InvokeAsync(new FlowDropEventArgs(ClientToFlow(e.ClientX, e.ClientY), e));
}
