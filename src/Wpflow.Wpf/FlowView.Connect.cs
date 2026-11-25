using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpflow.Core;
using Path = System.Windows.Shapes.Path;

namespace Wpflow.Wpf;

// Connexions (aperçu, accroche, validation, reconnexion) et rectangle de sélection.
public partial class FlowView
{
    private ContentControl? _connectionHost; // rendu libre de l'aperçu (ConnectionLineTemplate)
    private readonly List<Handle> _statusHandles = new();
    private HandleRef? _statusTarget;
    private bool? _statusValid;

    private static Position Opposite(Position p) => p switch
    {
        Position.Left => Position.Right,
        Position.Right => Position.Left,
        Position.Top => Position.Bottom,
        _ => Position.Top
    };

    private void UpdateConnectionLine(XYPosition mouseFlow)
    {
        if (_connectFrom is { } from) UpdateConnectionLine(from, mouseFlow);
    }

    /// <summary>Redessine l'aperçu d'une connexion partie de <paramref name="from" />, accrochée au handle le plus proche s'il y en a un.</summary>
    private void UpdateConnectionLine(HandleRef from, XYPosition mouseFlow)
    {
        if (_connectionPath == null || !_nodeIndex.TryGetValue(from.NodeId, out var node)) return;
        var start = GraphUtils.GetHandlePosition(node, from.Info.Position, from.Info);
        var target = FindCandidate(from, mouseFlow);
        bool? valid = target == null ? null : Validate(from, target, out _);

        var end = mouseFlow;
        var endSide = Opposite(from.Info.Position);
        if (target != null && _nodeIndex.TryGetValue(target.NodeId, out var targetNode))
        {
            end = GraphUtils.GetHandleCenter(targetNode, target.Info); // le fil s'accroche au handle
            endSide = target.Info.Position;
        }

        var path = ConnectionLineType switch
        {
            ConnectionLineType.Straight => EdgePaths.Straight(start, end),
            ConnectionLineType.Step => EdgePaths.Step(start, from.Info.Position, end, endSide),
            ConnectionLineType.SmoothStep => EdgePaths.SmoothStep(start, from.Info.Position, end, endSide),
            ConnectionLineType.SimpleBezier => EdgePaths.SimpleBezier(start, from.Info.Position, end, endSide),
            _ => EdgePaths.Bezier(start, from.Info.Position, end, endSide)
        };

        var state = new FlowConnectionState(from.NodeId, from.Info.Id, from.Info.Type, from.Info.Position, start, end,
            target?.NodeId, target?.Info.Id, valid, _reconnecting != null);
        SetConnectionState(state);
        ApplyHandleStatus(from, target, valid);

        var geometry = Geometry.Parse(path.Path);
        if (ConnectionLineTemplate is { } template)
        {
            if (_connectionHost == null)
            {
                _connectionHost = new ContentControl { IsHitTestVisible = false, Focusable = false, IsTabStop = false };
                _viewportGrid?.Children.Add(_connectionHost);
            }

            geometry.Freeze();
            _connectionPath.Data = null;
            _connectionHost.Visibility = Visibility.Visible;
            if (!ReferenceEquals(_connectionHost.ContentTemplate, template)) _connectionHost.ContentTemplate = template;
            _connectionHost.Content = new ConnectionLineContext(geometry, path.Path, new Point(start.X, start.Y),
                new Point(end.X, end.Y), from.Info.Position, endSide, valid, state);
        }
        else
        {
            if (_connectionHost != null) _connectionHost.Visibility = Visibility.Collapsed;
            _connectionPath.Data = geometry;
        }
    }

    /// <summary>Efface l'aperçu, l'état de connexion et les états visuels des handles.</summary>
    private void ClearConnectionLine()
    {
        if (_clickConnect != null) return; // une connexion par clics reste affichée jusqu'au second clic
        if (_connectionPath != null) _connectionPath.Data = null;
        if (_connectionHost != null)
        {
            _connectionHost.Content = null;
            _connectionHost.Visibility = Visibility.Collapsed;
        }

        SetConnectionState(null);
        ApplyHandleStatus(null, null, null);
    }

    /// <summary>Marque les handles concernés (origine, cible valide ou refusée) ; ne fait rien tant que la cible ne change pas.</summary>
    private void ApplyHandleStatus(HandleRef? origin, HandleRef? target, bool? valid)
    {
        if (origin != null && _statusHandles.Count > 0 && _statusTarget == target && _statusValid == valid) return;
        foreach (var h in _statusHandles) h.ConnectionStatus = HandleConnectionStatus.None;
        _statusHandles.Clear();
        _statusTarget = target;
        _statusValid = valid;
        if (origin == null) return;
        Mark(origin, HandleConnectionStatus.Origin);
        if (target != null) Mark(target, valid == true ? HandleConnectionStatus.Valid : HandleConnectionStatus.Invalid);
    }

    private void Mark(HandleRef reference, HandleConnectionStatus status)
    {
        if (!_hosts.TryGetValue(reference.NodeId, out var host)) return;
        var handle = FindDescendants<Handle>(host).FirstOrDefault(h => h.HandleId == reference.Info.Id
                                                                       && h.Type == reference.Info.Type
                                                                       && h.Position == reference.Info.Position);
        if (handle == null) return;
        handle.ConnectionStatus = status;
        _statusHandles.Add(handle);
    }

    /// <summary>Termine le geste : réalise la connexion si possible, puis notifie <see cref="ConnectEnded" /> (réussie ou non).</summary>
    private void FinishConnection(XYPosition mouseFlow)
    {
        if (_connectFrom is not { } from) return;
        var reconnecting = _reconnecting;
        var connection = TryConnect(from, mouseFlow, out var target);
        if (reconnecting != null) ReconnectEnded?.Invoke(this, new ReconnectEndEventArgs(reconnecting, connection, mouseFlow));
        RaiseConnectEnded(from, target, connection, mouseFlow);
    }

    /// <summary>Handle accepté le plus proche de la souris (dans <see cref="ConnectionRadius" />), ou <c>null</c>.</summary>
    private HandleRef? FindCandidate(HandleRef from, XYPosition mouseFlow) =>
        ConnectionSearch.FindCandidate(from, mouseFlow, ConnectionRadius, ConnectionMode,
            _handleMap, id => _nodeIndex.GetValueOrDefault(id));

    /// <summary>La connexion est-elle permise (doublon, validateurs de la vue et des handles) ?</summary>
    private bool Validate(HandleRef from, HandleRef target, out Connection connection) =>
        ConnectionSearch.Validate(from, target, Edges, _reconnecting?.Id, IsValidConnection, out connection);

    private Connection? TryConnect(HandleRef from, XYPosition mouseFlow, out HandleRef? target)
    {
        var connection = TryConnectCore(from, mouseFlow, out target);
        if (connection != null)
        {
            FindHandleElement(from)?.RaiseConnected(connection);
            if (target != null) FindHandleElement(target)?.RaiseConnected(connection);
        }

        return connection;
    }

    private Handle? FindHandleElement(HandleRef reference) =>
        _hosts.TryGetValue(reference.NodeId, out var host)
            ? FindDescendants<Handle>(host).FirstOrDefault(h => h.HandleId == reference.Info.Id && h.Type == reference.Info.Type
                                                                 && h.Position == reference.Info.Position)
            : null;

    private Connection? TryConnectCore(HandleRef from, XYPosition mouseFlow, out HandleRef? target)
    {
        target = FindCandidate(from, mouseFlow);
        if (target is null || !Validate(from, target, out var connection)) return null;

        if (ConnectCommand is { } command)
        {
            if (!command.CanExecute(connection)) return null; // le ViewModel refuse (validateur)
            if (_reconnecting == null)
            {
                PushHistory();
                command.Execute(connection); // c'est lui qui ajoute le lien
                return connection;
            }
        }

        if (_reconnecting is { } old)
        {
            // Reconnexion : l'edge garde son identité, seules ses extrémités changent.
            PushHistory();
            ApplyEdgeChanges([new EdgeReplaceChange(old.Id, old with
            {
                Source = connection.Source, Target = connection.Target,
                SourceHandle = connection.SourceHandle, TargetHandle = connection.TargetHandle
            })]);
            EdgeReconnected?.Invoke(this, new ReconnectEventArgs(old, connection));
            return connection;
        }

        ConnectRequested?.Invoke(this, new ConnectEventArgs(connection));
        if (AutoApplyChanges && AutoConnect)
        {
            PushHistory();
            var id = GraphQueries.ConnectionId(connection.Source, connection.SourceHandle, connection.Target, connection.TargetHandle);
            var edge = DefaultEdgeOptions is { } defaults
                ? defaults with
                {
                    Id = id, Source = connection.Source, Target = connection.Target,
                    SourceHandle = connection.SourceHandle, TargetHandle = connection.TargetHandle, Selected = false
                }
                : new Edge(id, connection.Source, connection.Target, connection.SourceHandle, connection.TargetHandle);
            ApplyEdgeChanges([new EdgeAddChange(edge)]);
        }

        return connection;
    }

    private HandleRef? FindHandle(string nodeId, string? handleId, HandleType type) =>
        _handleMap.TryGetValue(nodeId, out var list)
            ? list.Where(h => h.Type == type && (handleId == null || h.Id == handleId))
                .Select(h => new HandleRef(nodeId, h)).FirstOrDefault()
            : null;

    /// <summary>Si le clic tombe près d'une extrémité de l'edge, démarre une reconnexion de cette extrémité.</summary>
    private void TryBeginReconnect(string edgeId, XYPosition mouseFlow)
    {
        var edge = Edges.FirstOrDefault(e => e.Id == edgeId);
        if (edge == null || GetEdgeEnds(edge) is not { } ends) return;
        var reach = ReconnectRadius / Viewport.Zoom;
        double Dist(XYPosition p) => Math.Sqrt(Math.Pow(p.X - mouseFlow.X, 2) + Math.Pow(p.Y - mouseFlow.Y, 2));

        HandleRef? fixedEnd;
        if (Dist(ends.Target) <= reach && Dist(ends.Target) <= Dist(ends.Source))
            fixedEnd = FindHandle(edge.Source, edge.SourceHandle, HandleType.Source); // on déplace la cible
        else if (Dist(ends.Source) <= reach)
            fixedEnd = FindHandle(edge.Target, edge.TargetHandle, HandleType.Target); // on déplace la source
        else
            return;

        if (fixedEnd == null) return;
        _reconnecting = edge;
        _connectFrom = fixedEnd;
        _mode = Mode.Connect;
        RaiseConnectStarted(fixedEnd, true);
        ReconnectStarted?.Invoke(this, new ElementEventArgs(FlowElementKind.Edge, edge.Id, EdgeElement(edge.Id), mouseFlow));
    }

    /// <summary>Zone du rectangle de sélection en cours, en coordonnées flow.</summary>
    private Core.Rect SelectionArea(Point pos) => SelectionOps.Area(_mouseStartFlow, ScreenToFlow(pos));

    private void UpdateSelectionBox(Point pos)
    {
        if (_selectionBox == null) return;
        var origin = FlowToScreen(_mouseStartFlow);// le point de départ suit le fond quand la vue défile
        var x = Math.Min(origin.X, pos.X);
        var y = Math.Min(origin.Y, pos.Y);
        var w = Math.Abs(pos.X - origin.X);
        var h = Math.Abs(pos.Y - origin.Y);
        _selectionBox.Margin = new Thickness(x, y, 0, 0);
        _selectionBox.Width = w;
        _selectionBox.Height = h;
        _selectionBox.Visibility = Visibility.Visible;

        var rect = SelectionArea(pos);
        if (!_selectionStarted)
        {
            _selectionStarted = true;
            SelectionStarted?.Invoke(this, new SelectionBoxEventArgs(rect));
        }

        var (picked, pickedEdges) = SelectionOps.Box(rect, _nodeIndex.Values, Edges, SelectionMode);
        Select(picked, pickedEdges, false);
    }
}
