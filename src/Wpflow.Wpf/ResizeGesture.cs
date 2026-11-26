using System.Windows;
using System.Windows.Input;
using Wpflow.Core;

namespace Wpflow.Wpf;

/// <summary>
///     Un redimensionnement en cours, commun à <see cref="NodeResizer" /> et <see cref="NodeResizeControl" /> : historique,
///     changes du node et arguments de leurs évènements. Chaque méthode renvoie <c>null</c> quand il n'y a rien à notifier.
/// </summary>
internal sealed class ResizeGesture(FrameworkElement owner)
{
    private Core.Rect _startRect;
    private XYPosition _startMouse;

    private (FlowView Flow, NodeHost Host)? Context() =>
        FlowView.FindAncestor<NodeHost>(owner) is { } host && FlowView.FindAncestor<FlowView>(host) is { } flow
            ? (flow, host)
            : null;

    private static XYPosition MouseInFlow(FlowView flow) => flow.ScreenToFlow(Mouse.GetPosition(flow));

    public NodeResizeEventArgs? Start(ResizeDirection direction)
    {
        if (Context() is not { } c || c.Host.Node?.Rect is not { } rect) return null;
        _startRect = rect;
        _startMouse = MouseInFlow(c.Flow);
        c.Flow.PushHistory();
        c.Flow.ApplyNodeChanges([new NodeInteractionChange(c.Host.Node.Id, Resizing: true)]);
        return new NodeResizeEventArgs(c.Host.Node.Id, direction, rect, rect);
    }

    public NodeResizeEventArgs? Move(ResizeDirection direction, double minWidth, double minHeight, double maxWidth,
        double maxHeight, bool keepAspectRatio, Func<ResizeDirection, Core.Rect, bool>? shouldResize)
    {
        if (Context() is not { } c || c.Host.Node is not { } node) return null;
        var m = MouseInFlow(c.Flow);
        var r = NodeResize.Resize(direction, _startRect, m.X - _startMouse.X, m.Y - _startMouse.Y,
            minWidth, minHeight, maxWidth, maxHeight, keepAspectRatio);
        if (shouldResize is { } allowed && !allowed(direction, r)) return null;
        c.Flow.ApplyNodeChanges([new NodeResizeChange(node.Id, r.Width, r.Height, new XYPosition(r.X, r.Y))]);
        return new NodeResizeEventArgs(node.Id, direction, _startRect, r);
    }

    public NodeResizeEventArgs? End(ResizeDirection direction)
    {
        if (Context() is not { } c || c.Host.Node is not { } node) return null;
        var end = node.Rect ?? _startRect;
        c.Flow.ApplyNodeChanges([new NodeInteractionChange(node.Id, Resizing: false)]);
        return new NodeResizeEventArgs(node.Id, direction, _startRect, end);
    }
}
