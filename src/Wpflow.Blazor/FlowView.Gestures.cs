using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Wpflow.Blazor;

/// <summary>Gestes de pointeur : pan, glisser, connexion, reconnexion, redimensionnement, rotation, rectangle de sélection.</summary>
public partial class FlowView
{
    private enum Mode
    {
        None,
        Pan,
        DragNodes,
        Connect,
        Box,
        Resize,
        Rotate,
        Reconnect
    }

    private Mode _mode;
    private XYPosition _startPointer; // pixels, relatif au conteneur
    private XYPosition _pointer;
    private XYPosition _startFlow;
    private Viewport _startViewport;
    private double _threshold = 1;
    private bool _moved;
    private double _pressDistance; // le plus loin que le pointeur soit allé du point pressé : au-delà du seuil, plus de clic
    private bool _noClick; // l'appui a déjà servi (second clic d'une connexion)
    private string? _pendingSelect; // node pressé sans être sélectionné (SelectNodesOnDrag faux) : sélectionné au clic
    private bool _rightDragged; // le bouton droit a déplacé la vue : le menu contextuel qui suit est ignoré
    private string? _pressedNode;
    private string? _pressedEdge;
    private bool _pressedMulti;
    private long _pressedButton;
    private bool _secondPress; // second appui d'un double-clic, signalé par le pont JS
    private Dictionary<string, XYPosition> _dragStart = new();
    private (string NodeId, HandleInfo Handle)? _from;
    private (string NodeId, HandleInfo Handle)? _candidate;
    private bool _candidateValid;
    private bool _clickMode;
    private XYPosition _cursorFlow;
    private XYPosition _boxEnd;
    private ConnectStartEventArgs? _connectArgs;

    private (string NodeId, ResizeDirection Direction, Rect Start, ResizeOptions Options)? _resize;
    private (string NodeId, XYPosition Center, double Snap)? _rotate;
    private (Edge Edge, HandleType Moving)? _reconnect;

    // Drapeaux de l'appui en cours, posés par le pont JS (élément `nodrag`, `nopan`, contrôle HTML).
    private bool _pressNoDrag, _pressNoPan, _pressUntracked, _pressOutsideHandle;

    // L'appui courant est sur un contrôle, un panneau ou une barre d'outils : son double-clic ne concerne pas le graphe.
    private bool _lastPressUntracked;

    private bool CanDrag => NodesDraggable && !IsLocked;
    private bool CanConnect => NodesConnectable && !IsLocked;
    private bool CanSelect => ElementsSelectable && !IsLocked;

    private bool Held(string key, bool shift, bool ctrl, bool alt, bool meta) => key switch
    {
        "Shift" => shift,
        "Control" or "Ctrl" => ctrl || meta,
        "Meta" => meta,
        "Alt" => alt,
        "Space" => _space,
        _ => false
    };

    [JSInvokable]
    public Task OnPressInfo(int flags)
    {
        _pressNoDrag = (flags & 1) != 0;
        _pressNoPan = (flags & 2) != 0;
        _pressUntracked = _lastPressUntracked = (flags & 4) != 0;
        _pressOutsideHandle = (flags & 8) != 0;
        _rightDragged = false;
        return Task.CompletedTask;
    }

    // Attributs data-* de l'élément pressé : « pending » vient du pont JS, juste avant l'évènement Blazor de l'appui.
    private (string NodeId, Dictionary<string, string> Data)? _pendingData, _pressData;

    [JSInvokable]
    public Task OnPressData(string nodeId, Dictionary<string, string> data)
    {
        _pendingData = (nodeId, data);
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnSecondPress()
    {
        _secondPress = true;
        return Task.CompletedTask;
    }

    private NodeEventArgs NodeArgs(Node n, FlowPointer pointer) =>
        _pressData is { } p && p.NodeId == n.Id ? new NodeEventArgs(n, pointer) { TargetData = p.Data } : new NodeEventArgs(n, pointer);

    private (bool NoDrag, bool NoPan, bool Untracked, bool OutsideHandle) BeginGesture(PointerEventArgs e)
    {
        var flags = (_pressNoDrag, _pressNoPan, _pressUntracked, _pressOutsideHandle);
        _pressNoDrag = _pressNoPan = _pressUntracked = _pressOutsideHandle = false;
        _pressDistance = 0;
        _noClick = false;
        _pendingSelect = null;
        _rightDragged = false;
        _pressData = _pendingData;
        _pendingData = null;
        _startPointer = _pointer = Rel(e.ClientX, e.ClientY);
        _startFlow = ScreenToFlow(_startPointer);
        _startViewport = Viewport;
        _moved = false;
        _mode = Mode.None;
        _pressedNode = _pressedEdge = null;
        _pressedMulti = e.ShiftKey || Held(MultiSelectionKey, e.ShiftKey, e.CtrlKey, e.AltKey, e.MetaKey);
        _secondPress = false;
        _tabbed = false;
        _pressedButton = e.Button;
        _dragStart = new();
        _resize = null;
        _rotate = null;
        _reconnect = null;
        _threshold = 1;
        if (!_clickMode)
        {
            _from = null;
            _candidate = null;
        }

        return flags;
    }

    // ---------- Appuis (évènements Blazor) ----------

    private async Task OnPanePointerDown(PointerEventArgs e)
    {
        var flags = BeginGesture(e);
        if (_clickMode) await CancelClickConnect();
        if (flags.Untracked) return;
        var button = (int)e.Button;
        var panKey = _space && PanActivationKey == "Space" || Held(PanActivationKey, e.ShiftKey, e.CtrlKey, e.AltKey, e.MetaKey);
        var selKey = Held(SelectionKey, e.ShiftKey, e.CtrlKey, e.AltKey, e.MetaKey);
        _threshold = PaneDragThreshold;
        if (button == 0 && CanSelect && !panKey && !flags.NoPan && (SelectionOnDrag || selKey))
        {
            _mode = Mode.Box;
            _boxEnd = _startPointer;
            _boxBase = _pressedMulti ? Nodes.Where(x => x.Selected).Select(x => x.Id).ToHashSet() : new HashSet<string>();
            if (AutoPanOnSelection) StartAutoPan();
        }
        else if (!flags.NoPan && (PanOnDrag && PanOnDragButtons.Contains(button) || panKey && button == 0))
        {
            _mode = Mode.Pan;
            _keepsNextRender = true; // saisir le fond ne change rien au dessin
        }
    }

    internal async Task OnNodePointerDown(Node n, PointerEventArgs e)
    {
        var flags = BeginGesture(e);
        if (_clickMode) await CancelClickConnect();
        var button = (int)e.Button;
        var panKey = _space && PanActivationKey == "Space" || Held(PanActivationKey, e.ShiftKey, e.CtrlKey, e.AltKey, e.MetaKey);
        if (!flags.Untracked && !flags.NoPan && (button == 1 || panKey && button == 0))
        {
            _threshold = PaneDragThreshold;
            _mode = Mode.Pan;
            return;
        }

        // Un contrôle HTML du node garde l'appui, comme en WPF : ni sélection ni clic.
        if (button != 0 || flags.Untracked) return;
        _pressedNode = n.Id;

        // Verrouillé, non sélectionnable ou hors de la zone de saisie : le node reste en place, le glisser déplace la vue.
        if (IsLocked || !n.Selectable || flags.OutsideHandle)
        {
            if (!IsLocked && n.Selectable && !n.Selected) await SelectElements([n.Id], [], _pressedMulti);
            if (!flags.NoPan && PanOnDrag && PanOnDragButtons.Contains(0))
            {
                _threshold = PaneDragThreshold;
                _mode = Mode.Pan;
            }

            return;
        }

        if (n.Selected && _pressedMulti) // désélectionné : pas de glissement
        {
            await ApplyNodeChanges([new NodeSelectChange(n.Id, false)]);
            return;
        }

        if (!n.Selected)
        {
            if (SelectNodesOnDrag) await SelectElements([n.Id], [], _pressedMulti);
            else _pendingSelect = n.Id;
        }

        if (flags.NoDrag || !CanDrag || !n.Draggable) return;
        _dragStart = DragSelection.CollectStart(Nodes, CanDrag, CanSelect, SelectNodesOnDrag, n.Id, _pressedMulti);
        // Mode contrôlé : la sélection demandée n'est pas encore appliquée, seul le node saisi suit le pointeur.
        if (_dragStart.Count == 0) _dragStart[n.Id] = n.Position;
        _mode = Mode.DragNodes;
        _threshold = NodeDragThreshold;
        if (AutoPanOnNodeDrag) StartAutoPan();
    }

    private async Task OnEdgePointerDown(Edge edge, PointerEventArgs e)
    {
        var flags = BeginGesture(e);
        if (_clickMode) await CancelClickConnect();
        if ((int)e.Button != 0 || flags.Untracked) return;
        _pressedEdge = edge.Id;

        if (CanSelect && edge.Selectable) await SelectElements([], [edge.Id], _pressedMulti);

        // Un appui près d'une extrémité, suivi d'un glisser, la déplace (reconnexion) ; sans glisser, c'est un clic.
        if (EdgesReconnectable && !IsLocked && edge.Reconnectable && _edgeContexts.TryGetValue(edge.Id, out var ctx))
        {
            var g = ctx.Geometry;
            if (ViewLogic.ReconnectedEnd(_startFlow, g.Source, g.Target, ReconnectRadius / Viewport.Zoom) is { } m)
            {
                var fixedHandle = m == HandleType.Source
                    ? FindHandle(edge.Target, edge.TargetHandle, HandleType.Target)
                    : FindHandle(edge.Source, edge.SourceHandle, HandleType.Source);
                if (fixedHandle != null)
                {
                    _reconnect = (edge, m);
                    _from = (m == HandleType.Source ? edge.Target : edge.Source, fixedHandle);
                    _cursorFlow = _startFlow;
                    _mode = Mode.Reconnect;
                    _threshold = Math.Max(ViewLogic.ClickTolerance, ConnectionDragThreshold);
                }
            }
        }
    }

    /// <summary>Sélectionne ces éléments ; <paramref name="additive" /> les ajoute à la sélection.</summary>
    private async Task SelectElements(IEnumerable<string> nodeIds, IEnumerable<string> edgeIds, bool additive)
    {
        var (nodeChanges, edgeChanges) = SelectionOps.Select(Nodes, Edges, nodeIds, edgeIds, additive, ElementsSelectable);
        await ApplyNodeChanges(nodeChanges);
        await ApplyEdgeChanges(edgeChanges);
    }

    internal async Task StartConnect(string nodeId, HandleInfo handle, PointerEventArgs e)
    {
        var wasClick = _clickMode;
        var previous = _from;
        BeginGesture(e);
        if ((int)e.Button != 0) return;

        // Deuxième clic de la connexion par clics : le handle cliqué est la cible, et ce n'est pas un autre clic.
        if (wasClick && previous is { } from && CanConnect && handle.Connectable)
        {
            _noClick = true;
            _from = from;
            _cursorFlow = _startFlow;
            _candidate = FindCandidate(_startFlow);
            await FinishConnect();
            return;
        }

        // Un handle d'où aucune connexion ne peut partir : le tirer ne déplace rien ; un clic reste un clic sur le node.
        if (!CanConnect || !handle.Connectable || !handle.ConnectableStart ||
            Extras(nodeId, handle.Id, handle.Type) is { ConnectableStart: false } or { Connectable: false } ||
            _nodeIndex.TryGetValue(nodeId, out var n) && !n.Connectable)
        {
            _pressedNode = nodeId;
            return;
        }
        _from = (nodeId, handle);
        _cursorFlow = _startFlow;
        _mode = Mode.Connect;
        _threshold = ConnectionDragThreshold;
        _connectArgs = new ConnectStartEventArgs(nodeId, handle.Id, handle.Type);
        if (AutoPanOnConnect) StartAutoPan();
        await OnConnectStart.InvokeAsync(_connectArgs);
    }

    internal void StartResize(string nodeId, ResizeDirection direction, ResizeOptions options, PointerEventArgs e)
    {
        BeginGesture(e);
        if ((int)e.Button != 0 || IsLocked || !_nodeIndex.TryGetValue(nodeId, out var n) || n.Width is not { } w || n.Height is not { } h) return;
        _resize = (nodeId, direction, new Rect(n.Position.X, n.Position.Y, n.FixedWidth ?? w, n.FixedHeight ?? h), options);
        _mode = Mode.Resize;
        _threshold = 0;
        PushHistory();
        _ = ApplyNodeChanges([new NodeInteractionChange(nodeId, Resizing: true)]);
        options.Started?.Invoke(new ResizeInfo(nodeId, direction, _resize.Value.Start, _resize.Value.Start));
    }

    internal void StartRotate(string nodeId, double snap, PointerEventArgs e)
    {
        BeginGesture(e);
        if ((int)e.Button != 0 || IsLocked || !_nodeIndex.TryGetValue(nodeId, out var n) || n.Width is not { } w || n.Height is not { } h) return;
        var p = _abs[nodeId];
        var center = new XYPosition(p.X + w / 2, p.Y + h / 2);
        _rotate = (nodeId, center, snap);
        _mode = Mode.Rotate;
        _threshold = 0;
        PushHistory();
    }

    // ---------- Suivi du pointeur (appelé par le pont JS) ----------

    [JSInvokable]
    public Task OnPointerMove(double clientX, double clientY, bool shift, bool ctrl) => InvokeAsync(async () =>
    {
        _pointer = Rel(clientX, clientY);
        var dx = _pointer.X - _startPointer.X;
        var dy = _pointer.Y - _startPointer.Y;
        _pressDistance = Math.Max(_pressDistance, Math.Abs(dx) + Math.Abs(dy));
        if (_mode == Mode.None) return;
        var first = false;
        if (!_moved)
        {
            if (Math.Sqrt(dx * dx + dy * dy) <= _threshold) return;
            _moved = true;
            first = true;
        }

        await ApplyGesture(first, shift);
    });

    private async Task ApplyGesture(bool first, bool shift = false)
    {
        var flow = ScreenToFlow(_pointer);
        switch (_mode)
        {
            case Mode.Pan:
                if (first) await NotifyKeepingLayer(() => OnMoveStart.InvokeAsync(Viewport));
                await SetViewport(new Viewport(_startViewport.X + _pointer.X - _startPointer.X,
                    _startViewport.Y + _pointer.Y - _startPointer.Y, _startViewport.Zoom));
                break;
            case Mode.DragNodes:
                await DragTo(flow.X - _startFlow.X, flow.Y - _startFlow.Y, first);
                break;
            case Mode.Connect:
            case Mode.Reconnect:
                if (first && _mode == Mode.Reconnect && _reconnect is { } started && _from is { } anchor)
                {
                    // La reconnexion commence au premier mouvement, avec les mêmes évènements qu'en WPF.
                    _connectArgs = new ConnectStartEventArgs(anchor.NodeId, anchor.Handle.Id, anchor.Handle.Type) { IsReconnection = true };
                    if (AutoPanOnConnect) StartAutoPan();
                    await OnConnectStart.InvokeAsync(_connectArgs);
                    await OnReconnectStart.InvokeAsync(started.Edge);
                }

                _cursorFlow = flow;
                _candidate = FindCandidate(flow);
                _keepsNextRender = true; // le fil est hors de la couche
                StateHasChanged();
                break;
            case Mode.Box:
                if (first) await OnSelectionStart.InvokeAsync();
                _boxEnd = _pointer;
                UpdateBoxSelection();
                _keepsNextRender = true; // le rectangle est hors de la couche ; une sélection changée se signale par ses changes
                StateHasChanged();
                break;
            case Mode.Resize:
                await ResizeTo(flow.X - _startFlow.X, flow.Y - _startFlow.Y);
                break;
            case Mode.Rotate:
                await RotateTo(flow, shift);
                break;
        }
    }

    /// <summary>
    ///     Le pont JS n'a pas eu de réponse à un relâchement (connexion coupée) : le geste resté en cours est abandonné.
    ///     Sans effet si le relâchement est bien arrivé.
    /// </summary>
    [JSInvokable]
    public Task OnGestureLost() => _mode == Mode.None
        ? Task.CompletedTask
        : OnPointerUp(_left + _pointer.X * _scaleX, _top + _pointer.Y * _scaleY, false, true);

    [JSInvokable]
    public Task OnPointerUp(double clientX, double clientY, bool shift, bool cancelled) => InvokeAsync(async () =>
    {
        _pointer = Rel(clientX, clientY);
        var mode = _mode;
        _mode = Mode.None;
        StopAutoPan();
        var pointer = MakePointer(_pointer, shift);
        // La plus grande distance parcourue compte : un glisser revenu à son départ n'est pas un clic ; un seuil de
        // glisser plus grand que la tolérance laisse le clic jusqu'à lui.
        var distance = Math.Max(_pressDistance, Math.Abs(_pointer.X - _startPointer.X) + Math.Abs(_pointer.Y - _startPointer.Y));
        var clicked = ViewLogic.IsClick(distance, mode switch
        {
            Mode.DragNodes => NodeDragThreshold,
            Mode.Pan => PaneDragThreshold,
            _ => 0
        });
        _rightDragged = _pressedButton == 2 && mode == Mode.Pan && _moved;
        switch (mode)
        {
            case Mode.Pan when _moved:
                await NotifyKeepingLayer(() => OnMoveEnd.InvokeAsync(Viewport));
                break;
            case Mode.DragNodes when _moved:
                await FinishDrag(pointer);
                break;
            case Mode.Connect:
                if (cancelled)
                {
                    _candidate = null; // abandon : le geste se termine sans connexion
                    await FinishConnect();
                }
                else if (!clicked || !ConnectOnClick) await FinishConnect();
                else await BeginClickConnect();

                break;
            case Mode.Reconnect when _moved:
                if (cancelled) _candidate = null;
                await FinishReconnect();
                break;
            case Mode.Box when _moved:
                await OnSelectionEnd.InvokeAsync();
                break;
            case Mode.Resize:
                await FinishResize();
                break;
            case Mode.Rotate:
                await ApplyNodeChanges(_rotate is { } r ? [new NodeInteractionChange(r.NodeId, Resizing: false)] : []);
                break;
        }

        // Seul le bouton gauche clique : un clic droit ou milieu ne désélectionne pas et ne lève pas OnPaneClick.
        if (clicked && !cancelled && !_noClick && _pressedButton == 0 && mode is not (Mode.Resize or Mode.Rotate or Mode.Connect))
            await HandleClick(mode, pointer, shift);

        if (!_clickMode)
        {
            _from = null;
            _candidate = null;
        }

        _dragStart = new();
        _pressedNode = _pressedEdge = null;
        _resize = null;
        _rotate = null;
        _reconnect = null;
        _moved = false;
        // La fin d'un pan ne change rien au dessin ; ce qu'un clic modifie (sélection) est signalé par les changes.
        if (mode == Mode.Pan && !_clickMode) _keepsNextRender = true;
        else _forceLayer = true;
        StateHasChanged();
    });

    private async Task HandleClick(Mode mode, FlowPointer pointer, bool shift)
    {
        if (_pressedNode != null && _nodeIndex.TryGetValue(_pressedNode, out var n))
        {
            // La sélection est faite à l'appui ; un clic dans une sélection multiple la garde, comme en WPF.
            if (_pendingSelect == n.Id && CanSelect) await SelectOnly(n.Id, null);

            var args = NodeArgs(_nodeIndex[n.Id], pointer);
            await RaiseClick(() => OnNodeClick.InvokeAsync(args), OnNodeDoubleClick.HasDelegate);
        }
        else if (_pressedEdge != null)
        {
            var edge = Edges.FirstOrDefault(x => x.Id == _pressedEdge);
            if (edge != null) await RaiseClick(() => OnEdgeClick.InvokeAsync(new EdgeEventArgs(edge, pointer)), OnEdgeDoubleClick.HasDelegate);
        }
        else if (mode is Mode.Pan or Mode.None or Mode.Box)
        {
            if (!_pressedMulti && !(_clickMode && mode == Mode.None)) await ClearSelection();
            await OnPaneClick.InvokeAsync(new PaneEventArgs(pointer));
        }
    }

    // ---------- Glisser ----------

    private IReadOnlyList<Node> DraggedNodes() => _dragStart.Keys.Select(id => _nodeIndex[id]).ToList();

    private async Task DragTo(double dx, double dy, bool first)
    {
        if (first)
        {
            _pendingSelect = null; // SelectNodesOnDrag faux : un node glissé reste non sélectionné
            PushHistory();
            var start = DraggedNodes();
            var args = new NodeDragEventArgs(_nodeIndex[_pressedNode ?? start[0].Id], start, MakePointer(_pointer));
            await OnNodeDragStart.InvokeAsync(args);
            if (start.Count > 1) await OnSelectionDragStart.InvokeAsync(args);
            // Un node glissé sans sélection préalable (SelectNodesOnDrag faux) reste non sélectionné.
        }

        var changes = new List<NodeChange>();
        foreach (var (id, start) in _dragStart)
        {
            if (first) changes.Add(new NodeInteractionChange(id, true));
            var pos = new XYPosition(start.X + dx, start.Y + dy);
            if (SnapGrid is { } g and > 0) pos = ViewportMath.SnapPosition(pos, g, g);
            pos = ConstrainPosition(_nodeIndex[id], pos);
            changes.Add(new NodePositionChange(id, pos));
        }

        await ApplyNodeChanges(changes);
        await ExpandParents();
        var moved = DraggedNodes();
        var drag = new NodeDragEventArgs(_nodeIndex[_pressedNode ?? moved[0].Id], moved, MakePointer(_pointer));
        await OnNodeDrag.InvokeAsync(drag);
        if (moved.Count > 1) await OnSelectionDrag.InvokeAsync(drag);
    }

    /// <summary>Agrandit les parents que les enfants déplacés dépassent (<see cref="ParentExpansion" />).</summary>
    private async Task ExpandParents()
    {
        _rawLookup ??= id => _nodeIndex.GetValueOrDefault(id);
        var moved = _dragStart.Keys.ToList();
        foreach (var parentId in ParentExpansion.Parents(moved, _rawLookup))
        {
            if (ParentExpansion.Compute(parentId, moved, _rawLookup, Nodes, NodeOrigin) is not { } expansion) continue;
            if (expansion.Shift != XYPosition.Zero)
                foreach (var id in _dragStart.Keys.ToList()) // un geste fini entre-temps a vidé _dragStart
                    if (_nodeIndex.GetValueOrDefault(id)?.ParentId == parentId)
                        _dragStart[id] -= expansion.Shift; // le déplacement en cours se mesure dans le nouveau repère

            await ApplyNodeChanges(expansion.Changes);
        }
    }

    /// <summary>Applique <c>Node.Extent</c>, <c>ExtentParent</c> et <see cref="NodeExtent" /> à une position stockée.</summary>
    private XYPosition ConstrainPosition(Node n, XYPosition pos) =>
        NodeConstraints.Constrain(n, pos, n.ParentId != null ? _absNodes.GetValueOrDefault(n.ParentId) : null, NodeExtent, NodeOrigin);

    private async Task FinishDrag(FlowPointer pointer)
    {
        var nodes = DraggedNodes();
        await ApplyNodeChanges(_dragStart.Keys.Select(id => (NodeChange)new NodeInteractionChange(id, false)).ToList());
        var final = _dragStart.Keys.Select(id => _nodeIndex[id]).ToList();
        var args = new NodeDragEventArgs(_nodeIndex[_pressedNode ?? nodes[0].Id], final, pointer);
        await OnNodeDragStop.InvokeAsync(args);
        if (final.Count > 1) await OnSelectionDragStop.InvokeAsync(args);
        // Sans sélection au départ (SelectNodesOnDrag faux), le node glissé n'est pas sélectionné : rien à faire.
    }

    // ---------- Redimensionner, pivoter ----------

    private async Task ResizeTo(double dx, double dy)
    {
        if (_resize is not { } r) return;
        var o = r.Options;
        var rect = NodeResize.Resize(r.Direction, r.Start, dx, dy, o.MinWidth, o.MinHeight, o.MaxWidth, o.MaxHeight, o.KeepAspectRatio);
        if (o.ShouldResize?.Invoke(r.Direction, rect) == false) return;
        var movesOrigin = Math.Abs(rect.X - r.Start.X) > 0.001 || Math.Abs(rect.Y - r.Start.Y) > 0.001;
        await ApplyNodeChanges([new NodeResizeChange(r.NodeId, rect.Width, rect.Height, movesOrigin ? new XYPosition(rect.X, rect.Y) : null)]);
        o.Resizing?.Invoke(new ResizeInfo(r.NodeId, r.Direction, r.Start, rect));
    }

    private async Task FinishResize()
    {
        if (_resize is not { } r) return;
        await ApplyNodeChanges([new NodeInteractionChange(r.NodeId, Resizing: false)]);
        var n = _nodeIndex[r.NodeId];
        var current = new Rect(n.Position.X, n.Position.Y, n.FixedWidth ?? n.Width ?? r.Start.Width, n.FixedHeight ?? n.Height ?? r.Start.Height);
        r.Options.Ended?.Invoke(new ResizeInfo(r.NodeId, r.Direction, r.Start, current));
    }

    private async Task RotateTo(XYPosition flow, bool shift)
    {
        if (_rotate is not { } r || !_nodeIndex.TryGetValue(r.NodeId, out var n)) return;
        var angle = NodeRotation.Resolve(NodeRotation.AngleTo(r.Center, flow), shift, r.Snap);
        await ApplyNodeChanges([new NodeReplaceChange(n.Id, n with { Rotation = Math.Round(angle, 2) })]);
    }

    // ---------- Connexion ----------

    private (string NodeId, HandleInfo Handle)? FindCandidate(XYPosition flow)
    {
        _candidateValid = false;
        if (_from is not { } from) return null;
        var hit = ConnectionSearch.FindCandidate(new HandleRef(from.NodeId, from.Handle), flow, ConnectionRadius / Viewport.Zoom, ConnectionMode,
            _handles, id => _absNodes.GetValueOrDefault(id));
        if (hit == null) return null;
        var target = (hit.NodeId, hit.Info);
        _candidateValid = TryBuild(from, target) != null;
        return target;
    }

    /// <summary>La connexion de <paramref name="from" /> vers <paramref name="to" />, ou <c>null</c> si elle est refusée (doublon, validateurs).</summary>
    private Connection? TryBuild((string NodeId, HandleInfo Handle) from, (string NodeId, HandleInfo Handle) to) =>
        ConnectionSearch.Validate(new HandleRef(from.NodeId, from.Handle), new HandleRef(to.NodeId, to.Handle), Edges, _reconnect?.Edge.Id,
            IsValidConnection, out var c)
            ? c
            : null;

    private Edge NewEdge(Connection c)
    {
        var id = GraphQueries.ConnectionId(c.Source, c.SourceHandle, c.Target, c.TargetHandle);
        var edge = new Edge(id, c.Source, c.Target, c.SourceHandle, c.TargetHandle, DefaultEdgeType);
        return DefaultEdgeOptions?.Invoke(edge) ?? edge;
    }

    private async Task FinishConnect()
    {
        var from = _from;
        var candidate = _candidate;
        var args = _connectArgs ?? (from is { } f ? new ConnectStartEventArgs(f.NodeId, f.Handle.Id, f.Handle.Type) : null);
        Connection? connection = null;
        if (from is { } fr && candidate is { } to) connection = TryBuild(fr, to);

        var wasClick = _clickMode;
        _clickMode = false;
        _from = null;
        _candidate = null;
        _forceLayer = true; // le fil disparaît
        if (connection != null)
        {
            await OnConnect.InvokeAsync(connection);
            if (from is { } cf) await (Extras(cf.NodeId, cf.Handle.Id, cf.Handle.Type)?.Connected.InvokeAsync(connection) ?? Task.CompletedTask);
            if (candidate is { } ct) await (Extras(ct.NodeId, ct.Handle.Id, ct.Handle.Type)?.Connected.InvokeAsync(connection) ?? Task.CompletedTask);
            // En mode contrôlé, OnConnect suffit : demander en plus l'ajout créerait un second edge (comme en WPF).
            if (AutoApplyChanges && AutoConnect)
            {
                PushHistory();
                await ApplyEdgeChanges([new EdgeAddChange(NewEdge(connection))]);
            }
        }

        if (args != null)
        {
            var end = new ConnectEndEventArgs(args, connection, connection != null, MakePointer(_pointer));
            await (wasClick ? OnClickConnectEnd : OnConnectEnd).InvokeAsync(end);
        }

        _connectArgs = null;
    }

    private async Task BeginClickConnect()
    {
        if (_from is not { } f) return;
        _clickMode = true;
        _forceLayer = true;
        await OnClickConnectStart.InvokeAsync(new ConnectStartEventArgs(f.NodeId, f.Handle.Id, f.Handle.Type));
    }

    private async Task CancelClickConnect()
    {
        if (!_clickMode) return;
        var args = _from is { } f ? new ConnectStartEventArgs(f.NodeId, f.Handle.Id, f.Handle.Type) : null;
        _clickMode = false;
        _from = null;
        _candidate = null;
        _connectArgs = null;
        if (args != null) await OnClickConnectEnd.InvokeAsync(new ConnectEndEventArgs(args, null, false, MakePointer(_pointer)));
        Refresh();
    }

    /// <summary>Mouvement sans bouton enfoncé : fait suivre le fil de la connexion par clics.</summary>
    private Task OnHoverMove(PointerEventArgs e)
    {
        _pointer = Rel(e.ClientX, e.ClientY);
        if (_clickMode)
        {
            _cursorFlow = ScreenToFlow(_pointer);
            _candidate = FindCandidate(_cursorFlow);
        }

        // Un élément survolé a pu disparaître sans « pointerleave ».
        if (_hoverElement is ['n', ':', .. var gone] && !_nodeIndex.ContainsKey(gone)) _hoverElement = null;
        return _hoverElement == null ? OnPaneMouseMove.InvokeAsync(new PaneEventArgs(MakePointer(e))) : Task.CompletedTask;
    }

    private EventCallback<PointerEventArgs> HoverMoveHandler => _clickMode || OnPaneMouseMove.HasDelegate ? Cb(OnHoverMove) : default;

    private async Task FinishReconnect()
    {
        if (_reconnect is not { } r || _from is not { } from)
        {
            return;
        }

        Connection? connection = null;
        if (_candidate is { } to && TryBuild(from, to) is { } built)
        {
            connection = built;
            // L'edge tel qu'il est maintenant : l'appui l'a sélectionné depuis que le geste l'a retenu.
            var current = Edges.FirstOrDefault(x => x.Id == r.Edge.Id) ?? r.Edge;
            var list = GraphQueries.ReconnectEdge(current, connection, Edges, shouldReplaceId: false);
            if (!ReferenceEquals(list, Edges))
            {
                var updated = list.First(e => e.Id == r.Edge.Id);
                PushHistory();
                await ApplyEdgeChanges([new EdgeReplaceChange(r.Edge.Id, updated)]);
                await OnReconnect.InvokeAsync(new ReconnectEventArgs(r.Edge, connection));
            }
            else connection = null;
        }

        _from = null;
        _candidate = null;
        await OnReconnectEnd.InvokeAsync(new ReconnectEndEventArgs(r.Edge, r.Moving, connection));
        if (_connectArgs is { } args)
            await OnConnectEnd.InvokeAsync(new ConnectEndEventArgs(args, connection, connection != null, MakePointer(_pointer)));
        _connectArgs = null;
    }

    /// <summary>Chemin du fil de connexion en cours (coordonnées flow), ou <c>null</c> s'il n'y en a pas.</summary>
    private string? ConnectionPath()
    {
        if (!ShowsConnection || _from is not { } from) return null;
        if (!_absNodes.TryGetValue(from.NodeId, out var n)) return null;
        var start = GraphUtils.GetHandlePosition(n, from.Handle.Position, from.Handle);
        var startSide = GraphUtils.GetHandleSide(n, from.Handle.Position);
        var endSide = ViewLogic.Opposite(startSide);
        var end = _cursorFlow;
        if (_candidate is { } cand && _absNodes.TryGetValue(cand.NodeId, out var cn))
        {
            end = GraphUtils.GetHandlePosition(cn, cand.Handle.Position, cand.Handle);
            endSide = GraphUtils.GetHandleSide(cn, cand.Handle.Position);
        }

        return ViewLogic.ConnectionLine(ConnectionLineType, start, startSide, end, endSide).Path;
    }

    private ConnectionState CurrentConnectionState()
    {
        var inProgress = ShowsConnection;
        return new ConnectionState(inProgress, _from?.NodeId, _from?.Handle.Id, _from?.Handle.Type,
            _candidate?.NodeId, _candidate?.Handle.Id, _candidate != null && _candidateValid, _cursorFlow, inProgress ? ConnectionPath() : null);
    }

    // ---------- Rectangle de sélection ----------

    private string BoxStyle()
    {
        var origin = FlowToScreen(_startFlow); // le point de départ suit le fond quand la vue défile
        var x = Math.Min(origin.X, _boxEnd.X);
        var y = Math.Min(origin.Y, _boxEnd.Y);
        return $"left:{F(x)}px;top:{F(y)}px;width:{F(Math.Abs(_boxEnd.X - origin.X))}px;height:{F(Math.Abs(_boxEnd.Y - origin.Y))}px;";
    }

    private void UpdateBoxSelection()
    {
        var area = SelectionOps.Area(_startFlow, ScreenToFlow(_boxEnd));
        var (picked, pickedEdges) = SelectionOps.Box(area, _absNodes.Values, Edges, SelectionMode);
        if (_pressedMulti) picked.UnionWith(_boxBase);
        var (nodeChanges, edgeChanges) = SelectionOps.Select(Nodes, Edges, picked, pickedEdges, false, CanSelect);
        if (nodeChanges.Count > 0) _ = ApplyNodeChanges(nodeChanges);
        if (edgeChanges.Count > 0) _ = ApplyEdgeChanges(edgeChanges);
    }

    private HashSet<string> _boxBase = new();

    // ---------- Défilement automatique près des bords ----------

    private Timer? _autoPanTimer;

    private void StartAutoPan()
    {
        if (_autoPanTimer != null) return;
        _autoPanTimer = new Timer(_ => _ = InvokeAsync(AutoPanTick), null, 30, 16);
    }

    private void StopAutoPan()
    {
        _autoPanTimer?.Dispose();
        _autoPanTimer = null;
    }

    private async Task AutoPanTick()
    {
        if (_mode == Mode.None || !_moved || _width <= 0) return;
        var (dx, dy) = AutoPan.Delta(_pointer.X, _pointer.Y, _width, _height, AutoPanSpeed);
        if (dx == 0 && dy == 0) return;
        await SetViewport(ViewportMath.PanBy(Viewport, dx, dy));
        if (_mode == Mode.DragNodes || _mode == Mode.Connect || _mode == Mode.Reconnect || _mode == Mode.Box) await ApplyGesture(false);
    }
}
