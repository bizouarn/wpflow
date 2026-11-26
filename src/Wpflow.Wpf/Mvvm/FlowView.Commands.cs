using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Wpflow.Core;

namespace Wpflow.Wpf;

// Partie MVVM de FlowView : commandes (ICommand), état bindable (zoom, connexion, historique), contrôleur et historique du ViewModel.
public partial class FlowView
{
    #region Commandes

    private static DependencyProperty CommandProperty(string name) =>
        DependencyProperty.Register(name, typeof(ICommand), typeof(FlowView), new PropertyMetadata(null));

    /// <summary>
    ///     Quand une connexion est réalisée entre deux handles. Paramètre : la <see cref="Connection" />. Si elle est
    ///     définie, <b>elle remplace la création de l'edge</b> (le ViewModel ajoute son lien) ; <c>CanExecute</c> faux refuse la
    ///     connexion — c'est le validateur côté ViewModel. Une reconnexion d'edge n'est que soumise à <c>CanExecute</c>.
    /// </summary>
    public static readonly DependencyProperty ConnectCommandProperty = CommandProperty(nameof(ConnectCommand));

    /// <summary>Fin d'un déplacement de nodes. Paramètre : la liste des éléments déplacés (ViewModels ou <see cref="Node" />). Notification : la position est déjà écrite.</summary>
    public static readonly DependencyProperty NodeDragStopCommandProperty = CommandProperty(nameof(NodeDragStopCommand));

    /// <summary>
    ///     Suppression (Suppr) de nodes. Paramètre : la liste des éléments supprimés (descendants compris). Si elle est
    ///     définie, <b>elle remplace la suppression automatique</b> des nodes et de leurs edges ; <c>CanExecute</c> faux annule
    ///     toute la suppression.
    /// </summary>
    public static readonly DependencyProperty NodesDeleteCommandProperty = CommandProperty(nameof(NodesDeleteCommand));

    /// <summary>Suppression d'edges sélectionnés. Paramètre : la liste des edges ; remplace la suppression automatique de ces edges.</summary>
    public static readonly DependencyProperty EdgesDeleteCommandProperty = CommandProperty(nameof(EdgesDeleteCommand));

    /// <summary>Dépôt sur la surface. Paramètre : un <see cref="FlowDropEventArgs" />.</summary>
    public static readonly DependencyProperty DropCommandProperty = CommandProperty(nameof(DropCommand));

    /// <summary>Clic (sans déplacement) sur un node. Paramètre : l'élément (ViewModel ou <see cref="Node" />).</summary>
    public static readonly DependencyProperty NodeClickCommandProperty = CommandProperty(nameof(NodeClickCommand));

    /// <summary>Clic sur un edge. Paramètre : l'élément (ViewModel ou <see cref="Edge" />).</summary>
    public static readonly DependencyProperty EdgeClickCommandProperty = CommandProperty(nameof(EdgeClickCommand));

    /// <summary>Clic sur le fond. Paramètre : la position en coordonnées flow (<see cref="XYPosition" />).</summary>
    public static readonly DependencyProperty PaneClickCommandProperty = CommandProperty(nameof(PaneClickCommand));

    public ICommand? ConnectCommand
    {
        get => (ICommand?)GetValue(ConnectCommandProperty);
        set => SetValue(ConnectCommandProperty, value);
    }

    public ICommand? NodeDragStopCommand
    {
        get => (ICommand?)GetValue(NodeDragStopCommandProperty);
        set => SetValue(NodeDragStopCommandProperty, value);
    }

    public ICommand? NodesDeleteCommand
    {
        get => (ICommand?)GetValue(NodesDeleteCommandProperty);
        set => SetValue(NodesDeleteCommandProperty, value);
    }

    public ICommand? EdgesDeleteCommand
    {
        get => (ICommand?)GetValue(EdgesDeleteCommandProperty);
        set => SetValue(EdgesDeleteCommandProperty, value);
    }

    public ICommand? DropCommand
    {
        get => (ICommand?)GetValue(DropCommandProperty);
        set => SetValue(DropCommandProperty, value);
    }

    public ICommand? NodeClickCommand
    {
        get => (ICommand?)GetValue(NodeClickCommandProperty);
        set => SetValue(NodeClickCommandProperty, value);
    }

    public ICommand? EdgeClickCommand
    {
        get => (ICommand?)GetValue(EdgeClickCommandProperty);
        set => SetValue(EdgeClickCommandProperty, value);
    }

    public ICommand? PaneClickCommand
    {
        get => (ICommand?)GetValue(PaneClickCommandProperty);
        set => SetValue(PaneClickCommandProperty, value);
    }

    /// <summary>Exécute la commande si elle existe et l'accepte ; renvoie <c>true</c> si elle a été exécutée.</summary>
    private static bool Run(ICommand? command, object? parameter)
    {
        if (command == null || !command.CanExecute(parameter)) return false;
        command.Execute(parameter);
        return true;
    }

    private IReadOnlyList<object> ElementsOfNodes(IEnumerable<string> ids) =>
        ids.Select(NodeElement).Where(e => e != null).Cast<object>().ToList();

    private IReadOnlyList<object> ElementsOfEdges(IEnumerable<string> ids) =>
        ids.Select(EdgeElement).Where(e => e != null).Cast<object>().ToList();

    #endregion

    #region Validation

    /// <summary>Validation d'une connexion (<c>isValidConnection</c>) ; renvoyer <c>false</c> la refuse. Liable à une propriété du ViewModel.</summary>
    public static readonly DependencyProperty IsValidConnectionProperty = DependencyProperty.Register(
        nameof(IsValidConnection), typeof(Func<Connection, bool>), typeof(FlowView), new PropertyMetadata(null));

    public Func<Connection, bool>? IsValidConnection
    {
        get => (Func<Connection, bool>?)GetValue(IsValidConnectionProperty);
        set => SetValue(IsValidConnectionProperty, value);
    }

    #endregion

    #region État bindable

    private static readonly DependencyPropertyKey IsConnectingKey = DependencyProperty.RegisterReadOnly(
        nameof(IsConnecting), typeof(bool), typeof(FlowView), new PropertyMetadata(false));

    private static readonly DependencyPropertyKey CanUndoKey = DependencyProperty.RegisterReadOnly(
        nameof(CanUndo), typeof(bool), typeof(FlowView), new PropertyMetadata(false));

    private static readonly DependencyPropertyKey CanRedoKey = DependencyProperty.RegisterReadOnly(
        nameof(CanRedo), typeof(bool), typeof(FlowView), new PropertyMetadata(false));

    /// <summary>Une connexion est en cours de tracé.</summary>
    public static readonly DependencyProperty IsConnectingProperty = IsConnectingKey.DependencyProperty;

    public static readonly DependencyProperty CanUndoProperty = CanUndoKey.DependencyProperty;
    public static readonly DependencyProperty CanRedoProperty = CanRedoKey.DependencyProperty;

    /// <summary>Niveau de zoom (lecture et écriture, <c>TwoWay</c> par défaut) ; écrit, il zoome en gardant le centre de la vue.</summary>
    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom), typeof(double), typeof(FlowView),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, e) => ((FlowView)d).OnZoomAssigned((double)e.NewValue)));

    public bool IsConnecting => (bool)GetValue(IsConnectingProperty);
    public bool CanUndo => (bool)GetValue(CanUndoProperty);
    public bool CanRedo => (bool)GetValue(CanRedoProperty);

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    private bool _syncingZoom;

    private void OnZoomAssigned(double zoom)
    {
        if (_syncingZoom || zoom <= 0 || double.IsNaN(zoom)) return;
        var center = new XYPosition(ActualWidth / 2, ActualHeight / 2);
        Viewport = ViewportMath.ZoomAt(Viewport, center, zoom, MinZoom, MaxZoom);
        SyncZoom(); // la valeur demandée a pu être ramenée dans [MinZoom, MaxZoom]
    }

    /// <summary>Aligne la propriété <see cref="Zoom" /> sur le viewport réel.</summary>
    private void SyncZoom()
    {
        if (Math.Abs(Zoom - Viewport.Zoom) < 1e-9) return;
        _syncingZoom = true;
        try
        {
            SetCurrentValue(ZoomProperty, Viewport.Zoom);
        }
        finally
        {
            _syncingZoom = false;
        }
    }

    private void SetConnecting(bool value)
    {
        if (IsConnecting != value) SetValue(IsConnectingKey, value);
    }

    /// <summary>Recalcule <see cref="CanUndo" /> / <see cref="CanRedo" /> (historique propre ou celui du ViewModel).</summary>
    private void UpdateHistoryState()
    {
        var (undo, redo) = History is { } h ? (h.CanUndo, h.CanRedo) : (_history.CanUndo, _history.CanRedo);
        if (CanUndo != undo) SetValue(CanUndoKey, undo);
        if (CanRedo != redo) SetValue(CanRedoKey, redo);
        CommandManager.InvalidateRequerySuggested();
    }

    #endregion

    #region Contrôleur et historique du ViewModel

    /// <summary>
    ///     Ce que le ViewModel peut faire sur la vue (zoom, cadrage, sélection, annuler…) sans la connaître :
    ///     <c>Controller="{Binding Flow, Mode=OneWayToSource}"</c> avec une propriété <c>IFlowController Flow</c> dans le ViewModel.
    /// </summary>
    public static readonly DependencyProperty ControllerProperty = DependencyProperty.Register(
        nameof(Controller), typeof(IFlowController), typeof(FlowView),
        new PropertyMetadata(null, (d, e) =>
        {
            // Une liaison OneWayToSource remet la valeur à null en s'installant : on republie le contrôleur, qu'elle pousse alors au ViewModel.
            if (e.NewValue == null) ((FlowView)d).SetCurrentValue(ControllerProperty, new FlowController((FlowView)d));
        }));

    public IFlowController? Controller
    {
        get => (IFlowController?)GetValue(ControllerProperty);
        set => SetValue(ControllerProperty, value);
    }

    /// <summary>Historique du ViewModel : remplace celui de la vue (voir <see cref="IFlowHistory" />).</summary>
    public static readonly DependencyProperty HistoryProperty = DependencyProperty.Register(
        nameof(History), typeof(IFlowHistory), typeof(FlowView),
        new PropertyMetadata(null, (d, e) => ((FlowView)d).OnHistoryChanged((IFlowHistory?)e.OldValue, (IFlowHistory?)e.NewValue)));

    public IFlowHistory? History
    {
        get => (IFlowHistory?)GetValue(HistoryProperty);
        set => SetValue(HistoryProperty, value);
    }

    private void OnHistoryChanged(IFlowHistory? old, IFlowHistory? current)
    {
        // Abonnement faible : l'historique du ViewModel vit souvent plus longtemps que la vue.
        if (old is INotifyPropertyChanged o) PropertyChangedEventManager.RemoveHandler(o, OnHistoryPropertyChanged, string.Empty);
        if (current is INotifyPropertyChanged c) PropertyChangedEventManager.AddHandler(c, OnHistoryPropertyChanged, string.Empty);
        UpdateHistoryState();
    }

    private void OnHistoryPropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateHistoryState();

    #endregion

    #region Commandes routées et actions du contrôleur

    private void InitCommands()
    {
        SetCurrentValue(ControllerProperty, new FlowController(this));

        void Bind(RoutedUICommand command, Action execute, Func<bool>? canExecute = null) =>
            CommandBindings.Add(new CommandBinding(command, (_, e) =>
            {
                execute();
                e.Handled = true;
            }, (_, e) =>
            {
                e.CanExecute = canExecute?.Invoke() ?? true;
                e.Handled = true;
            }));

        Bind(FlowCommands.FitView, FitView);
        Bind(FlowCommands.ZoomIn, ZoomIn, () => Zoom < MaxZoom);
        Bind(FlowCommands.ZoomOut, ZoomOut, () => Zoom > MinZoom);
        Bind(FlowCommands.Undo, Undo, () => CanUndo);
        Bind(FlowCommands.Redo, Redo, () => CanRedo);
        Bind(FlowCommands.SelectAll, SelectAll, () => Nodes.Count + Edges.Count > 0);
        Bind(FlowCommands.ClearSelection, ClearSelection, () => Nodes.Any(n => n.Selected) || Edges.Any(e => e.Selected));
        Bind(FlowCommands.DeleteSelection, DeleteSelection, () => !IsLocked && (Nodes.Any(n => n.Selected) || Edges.Any(e => e.Selected)));
        Bind(FlowCommands.ToggleLock, () => IsLocked = !IsLocked);
        Bind(FlowCommands.Copy, () => Copy(), () => Nodes.Any(n => n.Selected));
        Bind(FlowCommands.Cut, () => Cut(), () => !IsLocked && Nodes.Any(n => n.Selected));
        Bind(FlowCommands.Paste, () => Paste(), () => CanPaste);
    }

    /// <summary>Sélectionne tous les nodes et tous les edges.</summary>
    public void SelectAll() => Select(Nodes.Select(n => n.Id), Edges.Select(x => x.Id), false);

    /// <summary>Désélectionne tout.</summary>
    public void ClearSelection() => Select([], [], false);

    internal bool CenterOn(string nodeId)
    {
        if (!_nodeIndex.TryGetValue(nodeId, out var node) || node.Rect is not { } rect || ActualWidth <= 0) return false;
        var zoom = Viewport.Zoom;
        Viewport = new Viewport(ActualWidth / 2 - (rect.X + rect.Width / 2) * zoom,
            ActualHeight / 2 - (rect.Y + rect.Height / 2) * zoom, zoom);
        return true;
    }

    #endregion
}
