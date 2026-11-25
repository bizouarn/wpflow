using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Wpflow.Core;

namespace Wpflow.Wpf;

// Souris et clavier : tous les gestes passent ici (modes Pan / DragNodes / Connect / Select).
public partial class FlowView
{
    private bool _panMoved, _connectMoved, _selectionStarted;
    private DependencyObject? _pressedSource;
    private string? _pendingSelect; // node pressé sans être sélectionné (SelectNodesOnDrag faux) : sélectionné au clic
    private HandleRef? _clickConnect; // première extrémité d'une connexion « par deux clics »
    private (FlowElementKind Kind, string? Id)? _hover;
    private DispatcherTimer? _wheelTimer;

    private static bool Has(ModifierKeys key) => key != ModifierKeys.None && Keyboard.Modifiers.HasFlag(key);

    /// <summary>Vrai si le point cliqué est sur la zone de saisie déclarée (<see cref="NodeHost.DragHandleProperty" />) du node.</summary>
    private static bool IsOnDragHandle(DependencyObject? source, NodeHost host)
    {
        var key = host.Node?.DragHandle;
        if (key == null) return true;
        for (var d = source; d != null && !ReferenceEquals(d, host); d = ParentOf(d))
            if (NodeHost.GetDragHandle(d) == key)
                return true;
        return false;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        StopViewportAnimation(false);
        var (host, handle, edgeId) = HitTarget(e.OriginalSource as DependencyObject);
        var pos = e.GetPosition(this);
        var additive = Has(MultiSelectionKey) || Has(SelectionKey);

        _mouseStart = pos;
        _mouseStartFlow = ScreenToFlow(pos);
        _viewportStart = Viewport;
        _pressedNode = host?.Node?.Id;
        _pressedEdge = host == null && handle == null ? edgeId : null;
        _pressedPane = host == null && handle == null && edgeId == null;
        _pressedSource = e.OriginalSource as DependencyObject; // au relâchement, la capture a remplacé la source par la vue
        _pendingSelect = null;

        if (e.ClickCount == 2)
        {
            if (host?.Node != null) RaiseElementEvent(FlowElementKind.Node, host.Node.Id, _mouseStartFlow, true, _pressedSource);
            else if (edgeId != null) RaiseElementEvent(FlowElementKind.Edge, edgeId, _mouseStartFlow, true, _pressedSource);
            else if (ZoomOnDoubleClick && handle == null)
            {
                SetViewport(ViewportMath.ZoomAt(Viewport, new XYPosition(pos.X, pos.Y), Viewport.Zoom * 2, MinZoom, MaxZoom),
                    TimeSpan.FromMilliseconds(200));
                e.Handled = true;
                return;
            }
        }

        // Connexion par deux clics : le second clic, sur un handle, la termine ; ailleurs, il l'annule.
        if (_clickConnect is { } armed)
        {
            _clickConnect = null;
            HandleRef? target = null;
            Connection? connection = null;
            if (handle != null) connection = TryConnect(armed, _mouseStartFlow, out target);
            ClearConnectionLine();
            SetConnecting(false);
            ClickConnectEnded?.Invoke(this, new ConnectEndEventArgs(_mouseStartFlow, armed.NodeId, armed.Info.Id,
                armed.Info.Type, target?.NodeId, target?.Info.Id, connection));
            if (handle != null)
            {
                e.Handled = true;
                return;
            }
        }

        // Touche d'activation maintenue : le glisser déplace la vue, même au-dessus d'un node.
        if (Keyboard.IsKeyDown(PanActivationKey))
        {
            _mode = Mode.Pan;
            CaptureMouse();
            e.Handled = true;
            return;
        }

        var handleInfo = handle != null && host?.Node != null && !IsLocked && NodesConnectable && host.Node.Connectable
            ? host.Handles.FirstOrDefault(h => h.Id == handle.HandleId && h.Type == handle.Type && h.Position == handle.Position)
            : null;
        if (handleInfo is { Connectable: true, ConnectableStart: true })
        {
            _connectFrom = new HandleRef(host!.Node!.Id, handleInfo);
            _mode = Mode.Connect;
            RaiseConnectStarted(_connectFrom, false);
        }
        else if (handle != null && handleInfo != null)
        {
            // Handle qui n'accepte pas de départ de connexion : rien ne se passe.
        }
        else if (host?.Node != null)
        {
            var node = host.Node;
            if (IsLocked || !node.Selectable || !IsOnDragHandle(e.OriginalSource as DependencyObject, host))
            {
                // Hors de la zone de saisie, le node reste en place mais se sélectionne ; le glisser déplace la vue.
                if (!IsLocked && node.Selectable && !node.Selected) Select([node.Id], [], additive);
                _mode = CanPanWith(FlowMouseButtons.Left) ? Mode.Pan : Mode.None;
            }
            else if (node.Selected && additive)
            {
                // Ctrl/Maj + clic sur un node déjà sélectionné : on le désélectionne, sans drag.
                ApplyNodeChanges([new NodeSelectChange(node.Id, false)]);
            }
            else
            {
                if (!node.Selected)
                {
                    if (SelectNodesOnDrag) Select([node.Id], [], additive);
                    else _pendingSelect = node.Id;
                }

                _dragStart = CollectDragStart(node.Id, additive);
                if (_dragStart.Count > 0) _mode = Mode.DragNodes;
            }
        }
        else if (edgeId != null && !IsLocked)
        {
            var edge = Edges.FirstOrDefault(x => x.Id == edgeId);
            if (edge is { Selectable: true }) Select([], [edgeId], additive);
            if (edge is { Reconnectable: true } && EdgesReconnectable) TryBeginReconnect(edgeId, ScreenToFlow(pos));
        }
        else if ((Has(SelectionKey) || SelectionOnDrag) && !IsLocked && ElementsSelectable)
        {
            _mode = Mode.Select;
        }
        else
        {
            if (!additive) Select([], [], false);
            _mode = CanPanWith(FlowMouseButtons.Left) ? Mode.Pan : Mode.None;
        }

        var src = e.OriginalSource as DependencyObject;
        if (_mode == Mode.DragNodes && src != null && GetNoDrag(src)) _mode = Mode.None;
        if (_mode == Mode.Pan && src != null && GetNoPan(src)) _mode = Mode.None;
        if (_mode == Mode.Connect) SetConnecting(true);
        if (_mode != Mode.None) CaptureMouse();
        StartAutoPanIfEnabled();
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton != MouseButton.Middle || !CanPanWith(FlowMouseButtons.Middle)) return;
        Focus();
        StopViewportAnimation(false);
        _mouseStart = e.GetPosition(this);
        _viewportStart = Viewport;
        _mode = Mode.Pan;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.ChangedButton == MouseButton.Middle && _mode == Mode.Pan) FinishPan();
    }

    private void FinishPan()
    {
        var moved = _panMoved;
        EndMode();
        if (moved) ViewportMoveEnded?.Invoke(this, new ViewportMoveEventArgs(Viewport));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pos = e.GetPosition(this);
        if (_mode == Mode.None)
        {
            UpdateHover(e, pos);
            if (_clickConnect is { } armed) UpdateConnectionLine(armed, ScreenToFlow(pos));
            return;
        }

        var distance = Math.Abs(pos.X - _mouseStart.X) + Math.Abs(pos.Y - _mouseStart.Y);
        switch (_mode)
        {
            case Mode.Pan:
                if (!_panMoved)
                {
                    if (distance < PaneDragThreshold) break;
                    _panMoved = true;
                    ViewportMoveStarted?.Invoke(this, new ViewportMoveEventArgs(Viewport));
                }

                Viewport = ViewportMath.PanBy(_viewportStart, pos.X - _mouseStart.X, pos.Y - _mouseStart.Y);
                break;

            case Mode.DragNodes:
                UpdateDrag(pos);
                break;

            case Mode.Connect:
                if (!_connectMoved && distance < ConnectionDragThreshold) break;
                _connectMoved = true;
                UpdateConnectionLine(ScreenToFlow(pos));
                break;

            case Mode.Select:
                UpdateSelectionBox(pos);
                break;
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover is { } old) RaiseHover(old.Kind, old.Id, ScreenToFlow(e.GetPosition(this)), HoverPhase.Leave);
        _hover = null;
    }

    private enum HoverPhase
    {
        Enter,
        Move,
        Leave
    }

    private bool HasHoverHandlers => NodeMouseEnter != null || NodeMouseMove != null || NodeMouseLeave != null ||
                                     EdgeMouseEnter != null || EdgeMouseMove != null || EdgeMouseLeave != null ||
                                     PaneMouseEnter != null || PaneMouseMove != null || PaneMouseLeave != null;

    private void UpdateHover(MouseEventArgs e, Point pos)
    {
        if (!HasHoverHandlers) return;
        var (host, _, edgeId) = HitTarget(e.OriginalSource as DependencyObject);
        var current = host?.Node != null
            ? (FlowElementKind.Node, host.Node.Id)
            : edgeId != null
                ? (FlowElementKind.Edge, edgeId)
                : (FlowElementKind.Pane, (string?)null);
        var flow = ScreenToFlow(pos);
        if (_hover != current)
        {
            if (_hover is { } old) RaiseHover(old.Kind, old.Id, flow, HoverPhase.Leave);
            _hover = current;
            RaiseHover(current.Item1, current.Item2, flow, HoverPhase.Enter);
        }

        RaiseHover(current.Item1, current.Item2, flow, HoverPhase.Move);
    }

    private void RaiseHover(FlowElementKind kind, string? id, XYPosition position, HoverPhase phase)
    {
        var handler = (kind, phase) switch
        {
            (FlowElementKind.Node, HoverPhase.Enter) => NodeMouseEnter,
            (FlowElementKind.Node, HoverPhase.Move) => NodeMouseMove,
            (FlowElementKind.Node, _) => NodeMouseLeave,
            (FlowElementKind.Edge, HoverPhase.Enter) => EdgeMouseEnter,
            (FlowElementKind.Edge, HoverPhase.Move) => EdgeMouseMove,
            (FlowElementKind.Edge, _) => EdgeMouseLeave,
            (_, HoverPhase.Enter) => PaneMouseEnter,
            (_, HoverPhase.Move) => PaneMouseMove,
            _ => PaneMouseLeave
        };
        if (handler == null) return;
        var element = kind switch
        {
            FlowElementKind.Node when id != null => NodeElement(id),
            FlowElementKind.Edge when id != null => EdgeElement(id),
            _ => null
        };
        handler(this, new ElementEventArgs(kind, id, element, position));
    }

    private NodeDragEventArgs? DragArgs(Point pos)
    {
        var ids = _dragStart.Keys.ToList();
        return new NodeDragEventArgs(ids, ElementsOfNodes(ids), ScreenToFlow(pos));
    }

    /// <summary>Déplace les nodes pour la position de souris donnée ; le delta se mesure en coordonnées flow, donc reste juste quand la vue défile.</summary>
    private void UpdateDrag(Point pos)
    {
        var zoom = Viewport.Zoom;
        var m = ScreenToFlow(pos);
        var dx = m.X - _mouseStartFlow.X;
        var dy = m.Y - _mouseStartFlow.Y;
        if (!_dragMoved && (Math.Abs(dx) + Math.Abs(dy)) * zoom < NodeDragThreshold) return;
        if (!_dragMoved)
        {
            _dragMoved = true;
            PushHistory();
            if (_pendingSelect != null)
            {
                Select([_pendingSelect], [], false);
                _pendingSelect = null;
            }

            ApplyNodeChanges(_dragStart.Keys.Select(id => (NodeChange)new NodeInteractionChange(id, Dragging: true)).ToList());
            if (NodeDragStarted != null) NodeDragStarted(this, DragArgs(pos)!);
            if (_dragStart.Count > 1 && SelectionDragStarted != null) SelectionDragStarted(this, DragArgs(pos)!);
        }

        ApplyNodeChanges(_dragStart.Select(kv =>
        {
            var p = new XYPosition(kv.Value.X + dx, kv.Value.Y + dy);
            if (SnapToGrid) p = ViewportMath.SnapPosition(p, SnapGrid, SnapGrid);
            return (NodeChange)new NodePositionChange(kv.Key, ConstrainPosition(kv.Key, p));
        }).ToList());
        ExpandParents();
        if (NodeDragging != null) NodeDragging(this, DragArgs(pos)!);
        if (_dragStart.Count > 1 && SelectionDragging != null) SelectionDragging(this, DragArgs(pos)!);
    }

    /// <summary>
    ///     Un enfant <see cref="Node.ExpandParent" /> qui dépasse de son parent l'agrandit (<c>expandParent</c>). À droite et en bas,
    ///     seule la taille change ; à gauche et en haut, le parent recule et ses enfants sont recalés pour ne pas bouger à l'écran.
    /// </summary>
    internal void ExpandParents() => ExpandParents(_dragStart.Keys.ToList());

    internal void ExpandParents(IReadOnlyCollection<string> moved)
    {
        var parents = moved.Select(RawNode).Where(n => n is { ExpandParent: true, ParentId: not null })
            .Select(n => n!.ParentId!).Distinct().ToList();
        foreach (var parentId in parents)
        {
            if (RawNode(parentId) is not { } parent) continue;
            var pw = parent.Width ?? parent.FixedWidth ?? 0;
            var ph = parent.Height ?? parent.FixedHeight ?? 0;
            double minX = 0, minY = 0, maxX = pw, maxY = ph;
            foreach (var id in moved)
            {
                if (RawNode(id) is not { ExpandParent: true } child || child.ParentId != parentId) continue;
                var shift = NodeConstraints.OriginOffset(child, NodeOrigin);
                var (x, y) = (child.Position.X - shift.X, child.Position.Y - shift.Y);
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x + (child.Width ?? 0));
                maxY = Math.Max(maxY, y + (child.Height ?? 0));
            }

            if (minX == 0 && minY == 0 && maxX <= pw && maxY <= ph) continue;

            var changes = new List<NodeChange>();
            var newPosition = parent.Position + new XYPosition(minX, minY);
            changes.Add(new NodeResizeChange(parentId, maxX - minX, maxY - minY, newPosition));
            if (minX != 0 || minY != 0)
            {
                // Les enfants gardent leur place à l'écran : on compense le recul du parent.
                foreach (var sibling in Nodes.Where(n => n.ParentId == parentId))
                    changes.Add(new NodePositionChange(sibling.Id, sibling.Position - new XYPosition(minX, minY)));
                foreach (var id in _dragStart.Keys.ToList()) // sans geste en cours, _dragStart est vide
                    if (RawNode(id)?.ParentId == parentId)
                        _dragStart[id] -= new XYPosition(minX, minY); // le déplacement en cours se mesure dans le nouveau repère
            }

            ApplyNodeChanges(changes);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        var pos = e.GetPosition(this);
        var moved = Math.Abs(pos.X - _mouseStart.X) + Math.Abs(pos.Y - _mouseStart.Y) >= 4;
        var dragged = _mode == Mode.DragNodes && _dragMoved ? DragArgs(pos) : null;
        var (nodeId, edgeId, pane) = (_pressedNode, _pressedEdge, _pressedPane);
        var pendingSelect = _pendingSelect;
        var source = _pressedSource;
        var wasConnect = _mode == Mode.Connect;
        var wasSelect = _mode == Mode.Select;
        var wasPan = _mode == Mode.Pan && _panMoved;
        var flow = ScreenToFlow(pos);

        HandleRef? armed = null;
        if (wasConnect)
        {
            if (ConnectOnClick && !moved && _reconnecting == null && _connectFrom != null) armed = _connectFrom;
            else FinishConnection(flow);
        }

        var box = wasSelect ? SelectionArea(pos) : (Core.Rect?)null;
        EndMode();
        StopAutoPan();
        if (armed != null)
        {
            _clickConnect = armed;
            SetConnecting(true);
            UpdateConnectionLine(armed, flow);
            ClickConnectStarted?.Invoke(this, new ConnectStartEventArgs(armed.NodeId, armed.Info.Id, armed.Info.Type, false));
        }

        if (wasPan) ViewportMoveEnded?.Invoke(this, new ViewportMoveEventArgs(Viewport));
        if (box is { } area) SelectionEnded?.Invoke(this, new SelectionBoxEventArgs(area));

        // Commandes du ViewModel, une fois l'état de la vue revenu au repos.
        if (dragged != null)
        {
            ApplyNodeChanges(dragged.NodeIds.Select(id => (NodeChange)new NodeInteractionChange(id, Dragging: false)).ToList());
            NodeDragStopped?.Invoke(this, dragged);
            if (dragged.NodeIds.Count > 1) SelectionDragStopped?.Invoke(this, dragged);
            Run(NodeDragStopCommand, dragged.Elements);
        }

        if (wasConnect || moved) return;
        if (pendingSelect != null) Select([pendingSelect], [], false);
        if (nodeId != null)
        {
            var args = PressArgs(FlowElementKind.Node, nodeId, flow, source);
            NodeClick?.Invoke(this, args);
            RunElementCommand(NodeClickCommand, args, NodeElement(nodeId));
        }
        else if (edgeId != null)
        {
            var args = PressArgs(FlowElementKind.Edge, edgeId, flow, source);
            EdgeClick?.Invoke(this, args);
            RunElementCommand(EdgeClickCommand, args, EdgeElement(edgeId));
        }
        else if (pane)
        {
            var args = PressArgs(FlowElementKind.Pane, null, flow, source);
            PaneClick?.Invoke(this, args);
            RunElementCommand(PaneClickCommand, args, flow);
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        EndMode();
    }

    private void EndMode()
    {
        _mode = Mode.None;
        _connectFrom = null;
        _reconnecting = null;
        _dragMoved = false;
        _panMoved = _connectMoved = _selectionStarted = false;
        _pendingSelect = null;
        _pressedNode = _pressedEdge = null;
        _pressedPane = false;
        _pressedSource = null;
        SetConnecting(false);
        _dragStart = new Dictionary<string, XYPosition>();
        StopAutoPan();
        ClearConnectionLine();
        if (_selectionBox != null) _selectionBox.Visibility = Visibility.Collapsed;
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (e.OriginalSource is DependencyObject wheelSource && GetNoWheel(wheelSource)) return;
        StopViewportAnimation(false);
        var (host, _, edgeId) = HitTarget(e.OriginalSource as DependencyObject);
        if (host == null && edgeId == null) PaneScroll?.Invoke(this, e);

        var p = e.GetPosition(this);
        var wheel = WheelInput.Resolve(0, -e.Delta, Has(ZoomActivationKey), Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), ZoomOnScroll,
            PanOnScroll, PanOnScrollSpeed, PanOnScrollMode, PreventScrolling);
        switch (wheel.Kind)
        {
            case WheelKind.Pan:
                MarkWheelMove();
                Viewport = ViewportMath.PanBy(Viewport, wheel.PanX, wheel.PanY);
                break;
            case WheelKind.Zoom:
                MarkWheelMove();
                Viewport = ViewportMath.ZoomAt(Viewport, new XYPosition(p.X, p.Y), Viewport.Zoom * wheel.ZoomFactor, MinZoom, MaxZoom);
                break;
        }

        e.Handled = wheel.Handled;
    }

    // Une rafale de crans de molette forme un seul mouvement : début au premier, fin 150 ms après le dernier.
    private void MarkWheelMove()
    {
        if (_wheelTimer == null)
        {
            ViewportMoveStarted?.Invoke(this, new ViewportMoveEventArgs(Viewport));
            _wheelTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Input, (_, _) =>
            {
                _wheelTimer!.Stop();
                _wheelTimer = null;
                ViewportMoveEnded?.Invoke(this, new ViewportMoveEventArgs(Viewport));
            }, Dispatcher);
        }
        else _wheelTimer.Stop();

        _wheelTimer.Start();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (DeleteKeys.Contains(e.Key))
        {
            DeleteSelection();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.C)
        {
            e.Handled = Copy();
        }
        else if (ctrl && e.Key == Key.X)
        {
            e.Handled = Cut();
        }
        else if (ctrl && e.Key == Key.V)
        {
            e.Handled = Paste();
        }
        else if (ctrl && e.Key == Key.Z && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            Undo();
            e.Handled = true;
        }
        else if (ctrl && (e.Key == Key.Y || (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))))
        {
            Redo();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.A)
        {
            Select(Nodes.Where(n => !n.Hidden).Select(n => n.Id), Edges.Where(x => !x.Hidden).Select(x => x.Id), false);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (_clickConnect is { } armed)
            {
                _clickConnect = null;
                ClearConnectionLine();
                SetConnecting(false);
                ClickConnectEnded?.Invoke(this, new ConnectEndEventArgs(_mouseStartFlow, armed.NodeId, armed.Info.Id,
                    armed.Info.Type, null, null, null));
            }

            Select([], [], false);
            e.Handled = true;
        }
        else if (!DisableKeyboardA11y && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            MoveSelectionByKeyboard(e.Key, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
    }

    // Tab / Maj+Tab : sélection de l'élément suivant / précédent (accessibilité clavier).
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key != Key.Tab || IsLocked || DisableKeyboardA11y) return;
        // Si le focus est dans un node (TextBox…), on laisse la navigation standard.
        if (e.OriginalSource is DependencyObject src && FindAncestor<NodeHost>(src) != null) return;

        var items = new List<(bool IsNode, string Id, bool Selected)>();
        if (NodesFocusable) items.AddRange(Nodes.Where(n => !n.Hidden).Select(n => (true, n.Id, n.Selected)));
        if (EdgesFocusable) items.AddRange(Edges.Where(x => !x.Hidden).Select(x => (false, x.Id, x.Selected)));
        if (items.Count == 0) return;

        var back = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var current = items.FindIndex(i => i.Selected);
        var next = current < 0 ? (back ? items.Count - 1 : 0) : (current + (back ? -1 : 1) + items.Count) % items.Count;
        var target = items[next];
        if (target.IsNode) Select([target.Id], [], false);
        else Select([], [target.Id], false);
        if (AutoPanOnNodeFocus && target.IsNode && _nodeIndex.TryGetValue(target.Id, out var node) && node.Rect is { } rect)
            EnsureVisible(rect);
        e.Handled = true;
    }

    /// <summary>Flèches : déplace les nodes sélectionnés de 5 px (20 avec Maj), comme React Flow.</summary>
    private void MoveSelectionByKeyboard(Key key, bool shift)
    {
        if (IsLocked) return;
        var (dx, dy) = KeyboardMove.Delta(key switch
        {
            Key.Left => KeyboardMove.Arrow.Left,
            Key.Right => KeyboardMove.Arrow.Right,
            Key.Up => KeyboardMove.Arrow.Up,
            _ => KeyboardMove.Arrow.Down
        }, shift);

        var start = CollectDragStart();
        if (start.Count == 0) return;
        PushHistory();
        ApplyNodeChanges(start.Select(kv => (NodeChange)new NodePositionChange(kv.Key,
            ConstrainPosition(kv.Key, new XYPosition(kv.Value.X + dx, kv.Value.Y + dy)))).ToList());
        if (AutoPanOnNodeFocus && start.Keys.FirstOrDefault() is { } first && _nodeIndex.TryGetValue(first, out var moved) && moved.Rect is { } r)
            EnsureVisible(r);
    }

    // Clic droit : menu contextuel du node, de l'edge, de la sélection ou du fond (voir FlowView.Interaction.cs).
    private bool IsSelectionMenu(string nodeId) => Nodes.Count(n => n.Selected) > 1 && Nodes.Any(n => n.Id == nodeId && n.Selected);

    // Appelé par OnMouseRightButtonUp.
    private void RaiseSelectionContextMenu(XYPosition position, DependencyObject? source)
    {
        var args = PressArgs(FlowElementKind.Node, null, position, source);
        SelectionContextMenuRequested?.Invoke(this, args);
        RunElementCommand(SelectionContextMenuCommand, args, ElementsOfNodes(SelectedNodeIds()));
    }
}
