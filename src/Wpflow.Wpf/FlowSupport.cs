using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Wpflow.Core;

namespace Wpflow.Wpf;

public enum ColorMode
{
    Light,
    Dark
}

public sealed class FlowDropEventArgs(XYPosition position, IDataObject data) : EventArgs
{
    /// <summary>Position du dépôt en coordonnées flow.</summary>
    public XYPosition Position { get; } = position;

    public IDataObject Data { get; } = data;
}

/// <summary>Peer UI Automation de la surface : un conteneur nommé dont les enfants sont les nodes.</summary>
public sealed class FlowViewAutomationPeer(FlowView owner) : FrameworkElementAutomationPeer(owner)
{
    protected override string GetClassNameCore() => nameof(FlowView);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrEmpty(name) ? "Flow diagram" : name;
    }
}

/// <summary>Peer d'un node : expose son nom et son état de sélection (pattern SelectionItem).</summary>
public sealed class NodeHostAutomationPeer(NodeHost owner) : FrameworkElementAutomationPeer(owner), ISelectionItemProvider
{
    private NodeHost Host => (NodeHost)Owner;

    protected override string GetClassNameCore() => nameof(NodeHost);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        if (!string.IsNullOrEmpty(name)) return name;
        return Host.Node is { } n ? $"Node {(n.Data as string ?? n.Id)}" : "Node";
    }

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.SelectionItem ? this : base.GetPattern(patternInterface);

    public bool IsSelected => Host.IsSelected;
    public IRawElementProviderSimple? SelectionContainer => null;

    public void Select() => Host.RequestSelect(false);
    public void AddToSelection() => Host.RequestSelect(true);
    public void RemoveFromSelection() => Host.RequestDeselect();
}
