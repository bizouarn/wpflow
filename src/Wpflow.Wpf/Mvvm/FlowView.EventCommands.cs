using System.Windows;
using System.Windows.Input;

namespace Wpflow.Wpf;

// Les commandes de notification : une par évènement qui n'avait pas de commande. Leur paramètre est l'argument de
// l'évènement ; elles ne remplacent aucune action. La vue ne s'abonne à un évènement que tant que la commande est définie :
// sans commande ni abonné, elle continue de ne rien calculer (survol, arguments de glisser, erreurs).
public partial class FlowView
{
    private readonly Dictionary<DependencyProperty, Delegate> _commandHooks = new();

    private static DependencyProperty Notification<T>(string name, Action<FlowView, EventHandler<T>> add,
        Action<FlowView, EventHandler<T>> remove) =>
        DependencyProperty.Register(name, typeof(ICommand), typeof(FlowView), new PropertyMetadata(null, (d, e) =>
        {
            var view = (FlowView)d;
            if (view._commandHooks.Remove(e.Property, out var old)) remove(view, (EventHandler<T>)old);
            if (e.NewValue == null) return;
            var property = e.Property;
            EventHandler<T> handler = (_, args) => Run((ICommand?)view.GetValue(property), args);
            view._commandHooks[property] = handler;
            add(view, handler);
        }));

    private static DependencyProperty Notification(string name, Action<FlowView, EventHandler> add,
        Action<FlowView, EventHandler> remove) =>
        DependencyProperty.Register(name, typeof(ICommand), typeof(FlowView), new PropertyMetadata(null, (d, e) =>
        {
            var view = (FlowView)d;
            if (view._commandHooks.Remove(e.Property, out var old)) remove(view, (EventHandler)old);
            if (e.NewValue == null) return;
            var property = e.Property;
            EventHandler handler = (_, args) => Run((ICommand?)view.GetValue(property), args);
            view._commandHooks[property] = handler;
            add(view, handler);
        }));

    public static readonly DependencyProperty InitCommandProperty = Notification(nameof(InitCommand),
        (v, h) => v.FlowInitialized += h, (v, h) => v.FlowInitialized -= h);

    public static readonly DependencyProperty StateChangedCommandProperty = Notification(nameof(StateChangedCommand),
        (v, h) => v.StateChanged += h, (v, h) => v.StateChanged -= h);

    public static readonly DependencyProperty ErrorCommandProperty = Notification<FlowErrorEventArgs>(nameof(ErrorCommand),
        (v, h) => v.ErrorOccurred += h, (v, h) => v.ErrorOccurred -= h);

    public static readonly DependencyProperty NodeChangesCommandProperty = Notification<NodeChangesEventArgs>(nameof(NodeChangesCommand),
        (v, h) => v.NodeChangesRequested += h, (v, h) => v.NodeChangesRequested -= h);

    public static readonly DependencyProperty EdgeChangesCommandProperty = Notification<EdgeChangesEventArgs>(nameof(EdgeChangesCommand),
        (v, h) => v.EdgeChangesRequested += h, (v, h) => v.EdgeChangesRequested -= h);

    public static readonly DependencyProperty NodeMouseEnterCommandProperty = Notification<ElementEventArgs>(nameof(NodeMouseEnterCommand),
        (v, h) => v.NodeMouseEnter += h, (v, h) => v.NodeMouseEnter -= h);

    public static readonly DependencyProperty NodeMouseMoveCommandProperty = Notification<ElementEventArgs>(nameof(NodeMouseMoveCommand),
        (v, h) => v.NodeMouseMove += h, (v, h) => v.NodeMouseMove -= h);

    public static readonly DependencyProperty NodeMouseLeaveCommandProperty = Notification<ElementEventArgs>(nameof(NodeMouseLeaveCommand),
        (v, h) => v.NodeMouseLeave += h, (v, h) => v.NodeMouseLeave -= h);

    public static readonly DependencyProperty EdgeMouseEnterCommandProperty = Notification<ElementEventArgs>(nameof(EdgeMouseEnterCommand),
        (v, h) => v.EdgeMouseEnter += h, (v, h) => v.EdgeMouseEnter -= h);

    public static readonly DependencyProperty EdgeMouseMoveCommandProperty = Notification<ElementEventArgs>(nameof(EdgeMouseMoveCommand),
        (v, h) => v.EdgeMouseMove += h, (v, h) => v.EdgeMouseMove -= h);

    public static readonly DependencyProperty EdgeMouseLeaveCommandProperty = Notification<ElementEventArgs>(nameof(EdgeMouseLeaveCommand),
        (v, h) => v.EdgeMouseLeave += h, (v, h) => v.EdgeMouseLeave -= h);

    public static readonly DependencyProperty PaneMouseEnterCommandProperty = Notification<ElementEventArgs>(nameof(PaneMouseEnterCommand),
        (v, h) => v.PaneMouseEnter += h, (v, h) => v.PaneMouseEnter -= h);

    public static readonly DependencyProperty PaneMouseMoveCommandProperty = Notification<ElementEventArgs>(nameof(PaneMouseMoveCommand),
        (v, h) => v.PaneMouseMove += h, (v, h) => v.PaneMouseMove -= h);

    public static readonly DependencyProperty PaneMouseLeaveCommandProperty = Notification<ElementEventArgs>(nameof(PaneMouseLeaveCommand),
        (v, h) => v.PaneMouseLeave += h, (v, h) => v.PaneMouseLeave -= h);

    public static readonly DependencyProperty NodeDragStartCommandProperty = Notification<NodeDragEventArgs>(nameof(NodeDragStartCommand),
        (v, h) => v.NodeDragStarted += h, (v, h) => v.NodeDragStarted -= h);

    public static readonly DependencyProperty NodeDragCommandProperty = Notification<NodeDragEventArgs>(nameof(NodeDragCommand),
        (v, h) => v.NodeDragging += h, (v, h) => v.NodeDragging -= h);

    public static readonly DependencyProperty SelectionDragStartCommandProperty = Notification<NodeDragEventArgs>(nameof(SelectionDragStartCommand),
        (v, h) => v.SelectionDragStarted += h, (v, h) => v.SelectionDragStarted -= h);

    public static readonly DependencyProperty SelectionDragCommandProperty = Notification<NodeDragEventArgs>(nameof(SelectionDragCommand),
        (v, h) => v.SelectionDragging += h, (v, h) => v.SelectionDragging -= h);

    public static readonly DependencyProperty SelectionDragStopCommandProperty = Notification<NodeDragEventArgs>(nameof(SelectionDragStopCommand),
        (v, h) => v.SelectionDragStopped += h, (v, h) => v.SelectionDragStopped -= h);

    public static readonly DependencyProperty SelectionStartCommandProperty = Notification<SelectionBoxEventArgs>(nameof(SelectionStartCommand),
        (v, h) => v.SelectionStarted += h, (v, h) => v.SelectionStarted -= h);

    public static readonly DependencyProperty SelectionEndCommandProperty = Notification<SelectionBoxEventArgs>(nameof(SelectionEndCommand),
        (v, h) => v.SelectionEnded += h, (v, h) => v.SelectionEnded -= h);

    public static readonly DependencyProperty EdgeReconnectCommandProperty = Notification<ReconnectEventArgs>(nameof(EdgeReconnectCommand),
        (v, h) => v.EdgeReconnected += h, (v, h) => v.EdgeReconnected -= h);

    public static readonly DependencyProperty ReconnectStartCommandProperty = Notification<ElementEventArgs>(nameof(ReconnectStartCommand),
        (v, h) => v.ReconnectStarted += h, (v, h) => v.ReconnectStarted -= h);

    public static readonly DependencyProperty ReconnectEndCommandProperty = Notification<ReconnectEndEventArgs>(nameof(ReconnectEndCommand),
        (v, h) => v.ReconnectEnded += h, (v, h) => v.ReconnectEnded -= h);

    public static readonly DependencyProperty ClickConnectStartCommandProperty = Notification<ConnectStartEventArgs>(nameof(ClickConnectStartCommand),
        (v, h) => v.ClickConnectStarted += h, (v, h) => v.ClickConnectStarted -= h);

    public static readonly DependencyProperty ClickConnectEndCommandProperty = Notification<ConnectEndEventArgs>(nameof(ClickConnectEndCommand),
        (v, h) => v.ClickConnectEnded += h, (v, h) => v.ClickConnectEnded -= h);

    public static readonly DependencyProperty ViewportMoveStartCommandProperty = Notification<ViewportMoveEventArgs>(nameof(ViewportMoveStartCommand),
        (v, h) => v.ViewportMoveStarted += h, (v, h) => v.ViewportMoveStarted -= h);

    public static readonly DependencyProperty ViewportMoveCommandProperty = Notification<ViewportMoveEventArgs>(nameof(ViewportMoveCommand),
        (v, h) => v.ViewportMoved += h, (v, h) => v.ViewportMoved -= h);

    public static readonly DependencyProperty ViewportMoveEndCommandProperty = Notification<ViewportMoveEventArgs>(nameof(ViewportMoveEndCommand),
        (v, h) => v.ViewportMoveEnded += h, (v, h) => v.ViewportMoveEnded -= h);

    public static readonly DependencyProperty PaneScrollCommandProperty = Notification<MouseWheelEventArgs>(nameof(PaneScrollCommand),
        (v, h) => v.PaneScroll += h, (v, h) => v.PaneScroll -= h);

    public ICommand? InitCommand { get => (ICommand?)GetValue(InitCommandProperty); set => SetValue(InitCommandProperty, value); }
    public ICommand? StateChangedCommand { get => (ICommand?)GetValue(StateChangedCommandProperty); set => SetValue(StateChangedCommandProperty, value); }
    public ICommand? ErrorCommand { get => (ICommand?)GetValue(ErrorCommandProperty); set => SetValue(ErrorCommandProperty, value); }
    public ICommand? NodeChangesCommand { get => (ICommand?)GetValue(NodeChangesCommandProperty); set => SetValue(NodeChangesCommandProperty, value); }
    public ICommand? EdgeChangesCommand { get => (ICommand?)GetValue(EdgeChangesCommandProperty); set => SetValue(EdgeChangesCommandProperty, value); }
    public ICommand? NodeMouseEnterCommand { get => (ICommand?)GetValue(NodeMouseEnterCommandProperty); set => SetValue(NodeMouseEnterCommandProperty, value); }
    public ICommand? NodeMouseMoveCommand { get => (ICommand?)GetValue(NodeMouseMoveCommandProperty); set => SetValue(NodeMouseMoveCommandProperty, value); }
    public ICommand? NodeMouseLeaveCommand { get => (ICommand?)GetValue(NodeMouseLeaveCommandProperty); set => SetValue(NodeMouseLeaveCommandProperty, value); }
    public ICommand? EdgeMouseEnterCommand { get => (ICommand?)GetValue(EdgeMouseEnterCommandProperty); set => SetValue(EdgeMouseEnterCommandProperty, value); }
    public ICommand? EdgeMouseMoveCommand { get => (ICommand?)GetValue(EdgeMouseMoveCommandProperty); set => SetValue(EdgeMouseMoveCommandProperty, value); }
    public ICommand? EdgeMouseLeaveCommand { get => (ICommand?)GetValue(EdgeMouseLeaveCommandProperty); set => SetValue(EdgeMouseLeaveCommandProperty, value); }
    public ICommand? PaneMouseEnterCommand { get => (ICommand?)GetValue(PaneMouseEnterCommandProperty); set => SetValue(PaneMouseEnterCommandProperty, value); }
    public ICommand? PaneMouseMoveCommand { get => (ICommand?)GetValue(PaneMouseMoveCommandProperty); set => SetValue(PaneMouseMoveCommandProperty, value); }
    public ICommand? PaneMouseLeaveCommand { get => (ICommand?)GetValue(PaneMouseLeaveCommandProperty); set => SetValue(PaneMouseLeaveCommandProperty, value); }
    public ICommand? NodeDragStartCommand { get => (ICommand?)GetValue(NodeDragStartCommandProperty); set => SetValue(NodeDragStartCommandProperty, value); }
    public ICommand? NodeDragCommand { get => (ICommand?)GetValue(NodeDragCommandProperty); set => SetValue(NodeDragCommandProperty, value); }
    public ICommand? SelectionDragStartCommand { get => (ICommand?)GetValue(SelectionDragStartCommandProperty); set => SetValue(SelectionDragStartCommandProperty, value); }
    public ICommand? SelectionDragCommand { get => (ICommand?)GetValue(SelectionDragCommandProperty); set => SetValue(SelectionDragCommandProperty, value); }
    public ICommand? SelectionDragStopCommand { get => (ICommand?)GetValue(SelectionDragStopCommandProperty); set => SetValue(SelectionDragStopCommandProperty, value); }
    public ICommand? SelectionStartCommand { get => (ICommand?)GetValue(SelectionStartCommandProperty); set => SetValue(SelectionStartCommandProperty, value); }
    public ICommand? SelectionEndCommand { get => (ICommand?)GetValue(SelectionEndCommandProperty); set => SetValue(SelectionEndCommandProperty, value); }
    public ICommand? EdgeReconnectCommand { get => (ICommand?)GetValue(EdgeReconnectCommandProperty); set => SetValue(EdgeReconnectCommandProperty, value); }
    public ICommand? ReconnectStartCommand { get => (ICommand?)GetValue(ReconnectStartCommandProperty); set => SetValue(ReconnectStartCommandProperty, value); }
    public ICommand? ReconnectEndCommand { get => (ICommand?)GetValue(ReconnectEndCommandProperty); set => SetValue(ReconnectEndCommandProperty, value); }
    public ICommand? ClickConnectStartCommand { get => (ICommand?)GetValue(ClickConnectStartCommandProperty); set => SetValue(ClickConnectStartCommandProperty, value); }
    public ICommand? ClickConnectEndCommand { get => (ICommand?)GetValue(ClickConnectEndCommandProperty); set => SetValue(ClickConnectEndCommandProperty, value); }
    public ICommand? ViewportMoveStartCommand { get => (ICommand?)GetValue(ViewportMoveStartCommandProperty); set => SetValue(ViewportMoveStartCommandProperty, value); }
    public ICommand? ViewportMoveCommand { get => (ICommand?)GetValue(ViewportMoveCommandProperty); set => SetValue(ViewportMoveCommandProperty, value); }
    public ICommand? ViewportMoveEndCommand { get => (ICommand?)GetValue(ViewportMoveEndCommandProperty); set => SetValue(ViewportMoveEndCommandProperty, value); }
    public ICommand? PaneScrollCommand { get => (ICommand?)GetValue(PaneScrollCommandProperty); set => SetValue(PaneScrollCommandProperty, value); }
}
