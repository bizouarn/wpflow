using System.Reflection;
using System.Windows.Input;
using Wpflow.Wpf;

namespace Wpflow.Wpf.Tests;

/// <summary>Chaque évènement de <see cref="FlowView" /> doit être accessible à un ViewModel par une commande.</summary>
public class EventCommandTests
{
    /// <summary>Évènements dont la commande reçoit les arguments de l'évènement.</summary>
    internal static readonly Dictionary<string, string> Notified = new()
    {
        ["FlowInitialized"] = "InitCommand", ["ErrorOccurred"] = "ErrorCommand", ["StateChanged"] = "StateChangedCommand",
        ["NodeChangesRequested"] = "NodeChangesCommand", ["EdgeChangesRequested"] = "EdgeChangesCommand",
        ["NodeMouseEnter"] = "NodeMouseEnterCommand", ["NodeMouseMove"] = "NodeMouseMoveCommand", ["NodeMouseLeave"] = "NodeMouseLeaveCommand",
        ["EdgeMouseEnter"] = "EdgeMouseEnterCommand", ["EdgeMouseMove"] = "EdgeMouseMoveCommand", ["EdgeMouseLeave"] = "EdgeMouseLeaveCommand",
        ["PaneMouseEnter"] = "PaneMouseEnterCommand", ["PaneMouseMove"] = "PaneMouseMoveCommand", ["PaneMouseLeave"] = "PaneMouseLeaveCommand",
        ["NodeDragStarted"] = "NodeDragStartCommand", ["NodeDragging"] = "NodeDragCommand",
        ["SelectionDragStarted"] = "SelectionDragStartCommand", ["SelectionDragging"] = "SelectionDragCommand",
        ["SelectionDragStopped"] = "SelectionDragStopCommand",
        ["SelectionStarted"] = "SelectionStartCommand", ["SelectionEnded"] = "SelectionEndCommand",
        ["EdgeReconnected"] = "EdgeReconnectCommand", ["ReconnectStarted"] = "ReconnectStartCommand", ["ReconnectEnded"] = "ReconnectEndCommand",
        ["ClickConnectStarted"] = "ClickConnectStartCommand", ["ClickConnectEnded"] = "ClickConnectEndCommand",
        ["ViewportMoveStarted"] = "ViewportMoveStartCommand", ["ViewportMoved"] = "ViewportMoveCommand",
        ["ViewportMoveEnded"] = "ViewportMoveEndCommand", ["PaneScroll"] = "PaneScrollCommand",
        ["Deleted"] = "DeletedCommand", ["SelectionChanged"] = "SelectionChangedCommand", ["NodesReady"] = "NodesReadyCommand"
    };

    /// <summary>Évènements dont la commande existait, avec son propre paramètre (voir doc/learn/13-mvvm.md).</summary>
    internal static readonly Dictionary<string, string> Dedicated = new()
    {
        ["NodeClick"] = "NodeClickCommand", ["EdgeClick"] = "EdgeClickCommand", ["PaneClick"] = "PaneClickCommand",
        ["NodeDoubleClick"] = "NodeDoubleClickCommand", ["EdgeDoubleClick"] = "EdgeDoubleClickCommand",
        ["NodeContextMenuRequested"] = "NodeContextMenuCommand", ["EdgeContextMenuRequested"] = "EdgeContextMenuCommand",
        ["PaneContextMenuRequested"] = "PaneContextMenuCommand", ["SelectionContextMenuRequested"] = "SelectionContextMenuCommand",
        ["ConnectRequested"] = "ConnectCommand", ["ConnectStarted"] = "ConnectStartCommand", ["ConnectEnded"] = "ConnectEndCommand",
        ["NodeDragStopped"] = "NodeDragStopCommand", ["DropRequested"] = "DropCommand",
        ["BeforeDelete"] = "BeforeDeleteAsync" // une fonction liable : elle répond, une commande ne le pourrait pas
    };

    private sealed class Recorder : ICommand
    {
        public List<object?> Parameters { get; } = [];
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => Parameters.Add(parameter);
    }

    [Fact]
    public void Every_public_event_has_a_bindable_counterpart()
    {
        var missing = typeof(FlowView).GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(e => e.Name)
            .Where(name => !(Notified.TryGetValue(name, out var property) || Dedicated.TryGetValue(name, out property))
                           || typeof(FlowView).GetProperty(property) == null)
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Each_notification_command_runs_when_its_event_is_raised() => Sta.Run(() =>
    {
        var silent = new List<string>();
        foreach (var (name, property) in Notified)
        {
            var flow = new FlowView();
            var recorder = new Recorder();
            if (typeof(FlowView).GetProperty(property) is not { } command)
            {
                silent.Add(name);
                continue;
            }

            command.SetValue(flow, recorder);
            var raise = (Delegate?)typeof(FlowView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(flow);
            raise?.DynamicInvoke(flow, null);
            if (recorder.Parameters.Count != 1) silent.Add(name);
        }

        Assert.Empty(silent);
    });
}
