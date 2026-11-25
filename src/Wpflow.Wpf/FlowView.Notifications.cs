using System.Windows;
using System.Windows.Input;
using Wpflow.Core;

namespace Wpflow.Wpf;

/// <summary>Les nodes et les edges qu'une suppression a retirés (<c>onDelete</c>).</summary>
public sealed class DeletedEventArgs(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges) : EventArgs
{
    public IReadOnlyList<Node> Nodes { get; } = nodes;
    public IReadOnlyList<Edge> Edges { get; } = edges;
}

/// <summary>La sélection après son changement (<c>onSelectionChange</c>).</summary>
public sealed class FlowSelectionEventArgs(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges) : EventArgs
{
    public IReadOnlyList<Node> Nodes { get; } = nodes;
    public IReadOnlyList<Edge> Edges { get; } = edges;
}

// Évènements et réglages que la vue Blazor avait seule.
public partial class FlowView
{
    /// <summary>Une suppression demandée à la vue (Suppr, <see cref="DeleteElements" />) vient d'avoir lieu.</summary>
    public event EventHandler<DeletedEventArgs>? Deleted;

    /// <summary>La sélection (nodes ou edges) a changé.</summary>
    public event EventHandler<FlowSelectionEventArgs>? SelectionChanged;

    /// <summary>Levé une fois, quand tous les nodes ont été mesurés (voir <see cref="NodesInitialized" />).</summary>
    public event EventHandler? NodesReady;

    public static readonly DependencyProperty DeletedCommandProperty = Notification<DeletedEventArgs>(nameof(DeletedCommand),
        (v, h) => v.Deleted += h, (v, h) => v.Deleted -= h);

    public static readonly DependencyProperty SelectionChangedCommandProperty = Notification<FlowSelectionEventArgs>(
        nameof(SelectionChangedCommand), (v, h) => v.SelectionChanged += h, (v, h) => v.SelectionChanged -= h);

    public static readonly DependencyProperty NodesReadyCommandProperty = Notification(nameof(NodesReadyCommand),
        (v, h) => v.NodesReady += h, (v, h) => v.NodesReady -= h);

    public ICommand? DeletedCommand
    {
        get => (ICommand?)GetValue(DeletedCommandProperty);
        set => SetValue(DeletedCommandProperty, value);
    }

    public ICommand? SelectionChangedCommand
    {
        get => (ICommand?)GetValue(SelectionChangedCommandProperty);
        set => SetValue(SelectionChangedCommandProperty, value);
    }

    public ICommand? NodesReadyCommand
    {
        get => (ICommand?)GetValue(NodesReadyCommandProperty);
        set => SetValue(NodesReadyCommandProperty, value);
    }

    public static readonly DependencyProperty AutoConnectProperty = DependencyProperty.Register(
        nameof(AutoConnect), typeof(bool), typeof(FlowView), new PropertyMetadata(true));

    /// <summary>
    ///     Faux : une connexion à la souris lève <see cref="ConnectRequested" /> sans créer d'edge ; c'est à l'application de
    ///     le faire. Le reste reste appliqué par la vue, contrairement à <see cref="AutoApplyChanges" />.
    /// </summary>
    public bool AutoConnect
    {
        get => (bool)GetValue(AutoConnectProperty);
        set => SetValue(AutoConnectProperty, value);
    }

    public static readonly DependencyProperty HistoryEnabledProperty = DependencyProperty.Register(
        nameof(HistoryEnabled), typeof(bool), typeof(FlowView), new PropertyMetadata(true, (d, e) =>
        {
            if (!(bool)e.NewValue) ((FlowView)d).ClearHistory();
        }));

    /// <summary>Conserve un historique annuler/rétablir. Sans effet sur <see cref="History" />, celui du ViewModel.</summary>
    public bool HistoryEnabled
    {
        get => (bool)GetValue(HistoryEnabledProperty);
        set => SetValue(HistoryEnabledProperty, value);
    }

    public static readonly DependencyProperty HistoryLimitProperty = DependencyProperty.Register(
        nameof(HistoryLimit), typeof(int), typeof(FlowView),
        new PropertyMetadata(100, (d, e) => ((FlowView)d)._history.Limit = (int)e.NewValue));

    /// <summary>Nombre d'états gardés par l'historique de la vue.</summary>
    public int HistoryLimit
    {
        get => (int)GetValue(HistoryLimitProperty);
        set => SetValue(HistoryLimitProperty, value);
    }

    private string _selectionSignature = "|";
    private bool _nodesReadyRaised;

    private void RaiseSelectionChangedIfNeeded()
    {
        var signature = string.Join(",", Nodes.Where(n => n.Selected).Select(n => n.Id)) + "|" +
                        string.Join(",", Edges.Where(e => e.Selected).Select(e => e.Id));
        if (signature == _selectionSignature) return;
        _selectionSignature = signature;
        SelectionChanged?.Invoke(this,
            new FlowSelectionEventArgs(Nodes.Where(n => n.Selected).ToList(), Edges.Where(e => e.Selected).ToList()));
    }

    private void RaiseNodesReadyOnce()
    {
        if (_nodesReadyRaised) return;
        _nodesReadyRaised = true;
        NodesReady?.Invoke(this, EventArgs.Empty);
    }
}
