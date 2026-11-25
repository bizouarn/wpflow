using System.Reflection;
using Microsoft.AspNetCore.Components;
using WpfView = Wpflow.Wpf.FlowView;
using BlazorView = Wpflow.Blazor.FlowView;

namespace Wpflow.Wpf.Tests;

/// <summary>
///     Les deux vues offrent les mêmes évènements et les mêmes réglages. Un ajout d'un seul côté fait échouer ces tests : il
///     faut le faire des deux, ou l'inscrire ici avec la raison pour laquelle il est propre à une plateforme.
/// </summary>
public class ViewParityTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    /// <summary>Évènement WPF → rappel Blazor.</summary>
    private static readonly Dictionary<string, string> Events = new()
    {
        ["FlowInitialized"] = "OnInit", ["NodesReady"] = "OnNodesInitialized", ["ErrorOccurred"] = "OnError",
        ["StateChanged"] = "OnStateChange", ["NodeChangesRequested"] = "OnNodesChange", ["EdgeChangesRequested"] = "OnEdgesChange",
        ["ConnectRequested"] = "OnConnect", ["DropRequested"] = "OnDrop", ["BeforeDelete"] = "OnBeforeDelete",
        ["Deleted"] = "OnDelete", ["SelectionChanged"] = "OnSelectionChange",
        ["NodeClick"] = "OnNodeClick", ["EdgeClick"] = "OnEdgeClick", ["PaneClick"] = "OnPaneClick",
        ["NodeDoubleClick"] = "OnNodeDoubleClick", ["EdgeDoubleClick"] = "OnEdgeDoubleClick",
        ["NodeContextMenuRequested"] = "OnNodeContextMenu", ["EdgeContextMenuRequested"] = "OnEdgeContextMenu",
        ["PaneContextMenuRequested"] = "OnPaneContextMenu", ["SelectionContextMenuRequested"] = "OnSelectionContextMenu",
        ["NodeMouseEnter"] = "OnNodeMouseEnter", ["NodeMouseMove"] = "OnNodeMouseMove", ["NodeMouseLeave"] = "OnNodeMouseLeave",
        ["EdgeMouseEnter"] = "OnEdgeMouseEnter", ["EdgeMouseMove"] = "OnEdgeMouseMove", ["EdgeMouseLeave"] = "OnEdgeMouseLeave",
        ["PaneMouseEnter"] = "OnPaneMouseEnter", ["PaneMouseMove"] = "OnPaneMouseMove", ["PaneMouseLeave"] = "OnPaneMouseLeave",
        ["PaneScroll"] = "OnPaneScroll",
        ["NodeDragStarted"] = "OnNodeDragStart", ["NodeDragging"] = "OnNodeDrag", ["NodeDragStopped"] = "OnNodeDragStop",
        ["SelectionDragStarted"] = "OnSelectionDragStart", ["SelectionDragging"] = "OnSelectionDrag",
        ["SelectionDragStopped"] = "OnSelectionDragStop",
        ["SelectionStarted"] = "OnSelectionStart", ["SelectionEnded"] = "OnSelectionEnd",
        ["ConnectStarted"] = "OnConnectStart", ["ConnectEnded"] = "OnConnectEnd",
        ["ClickConnectStarted"] = "OnClickConnectStart", ["ClickConnectEnded"] = "OnClickConnectEnd",
        ["EdgeReconnected"] = "OnReconnect", ["ReconnectStarted"] = "OnReconnectStart", ["ReconnectEnded"] = "OnReconnectEnd",
        ["ViewportMoveStarted"] = "OnMoveStart", ["ViewportMoved"] = "OnMove", ["ViewportMoveEnded"] = "OnMoveEnd"
    };

    /// <summary>Les rappels <c>XChanged</c> sont les liaisons bidirectionnelles de propriétés que WPF lie en <c>TwoWay</c>.</summary>
    private static readonly string[] TwoWayBindings =
    [
        "NodesChanged", "EdgesChanged", "ViewportChanged", "IsLockedChanged", "SelectedNodeItemsChanged",
        "SelectedEdgeItemsChanged", "SelectedNodeChanged", "ControllerChanged"
    ];

    /// <summary>Propriété WPF sans homonyme en Blazor : la raison, ou l'équivalent.</summary>
    private static readonly Dictionary<string, string> WpfOnly = new()
    {
        ["ColorMode"] = "Dark", ["SnapToGrid"] = "SnapGrid", ["ConnectionState"] = "CurrentConnection",
        ["BeforeDeleteAsync"] = "OnBeforeDelete",
        ["EdgeStroke"] = "CSS", ["EdgeStrokeThickness"] = "CSS", ["SelectionOutlineGap"] = "CSS",
        ["SelectionOutlineThickness"] = "CSS", ["ConnectionLineStyle"] = "CSS",
        ["PortalLayer"] = "calque WPF ; en Blazor, tout contenu se place dans ChildContent",
        ["SelectedNodes"] = "SelectedNodeItems (SelectedNodes donne les records en Blazor)",
        ["SelectedEdges"] = "SelectedEdgeItems",
        ["CommandsReceiveEventArgs"] = "les EventCallback reçoivent toujours les arguments"
    };

    /// <summary>Paramètre Blazor sans homonyme en WPF : la raison, ou l'équivalent.</summary>
    private static readonly Dictionary<string, string> BlazorOnly = new()
    {
        ["Dark"] = "ColorMode", ["DefaultEdgeType"] = "DefaultEdgeOptions", ["DeleteKeyEnabled"] = "DeleteKeys (liste vide)",
        ["ChildContent"] = "Content", ["Class"] = "Style",
        ["NodeTemplate"] = "ressources wpflow-node-*", ["NodeTemplates"] = "ressources wpflow-node-*",
        ["EdgeTemplates"] = "ressources wpflow-edge-template-*", ["EdgeTypes"] = "ressources wpflow-edge-*",
        ["MarkerTemplates"] = "ressources wpflow-marker-*", ["ShowBackground"] = "élément FlowBackground",
        ["SelectedNodeItems"] = "SelectedNodes", ["SelectedEdgeItems"] = "SelectedEdges"
    };

    // Les commandes WPF : en Blazor, les EventCallback (comparés par Every_event_exists_on_both_sides) en tiennent lieu.
    private static bool IsCommand(string name) => name.EndsWith("Command");

    private static bool IsParameter(PropertyInfo p) => p.GetCustomAttribute<ParameterAttribute>() != null;

    private static bool IsCallback(Type type) =>
        type == typeof(EventCallback) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EventCallback<>));

    [Fact]
    public void Every_event_exists_on_both_sides()
    {
        var wpf = typeof(WpfView).GetEvents(Declared).Select(e => e.Name).ToHashSet();
        var blazor = typeof(BlazorView).GetProperties(Declared).Where(IsParameter).ToDictionary(p => p.Name, p => p.PropertyType);

        var problems = new List<string>();
        problems.AddRange(wpf.Where(e => !Events.ContainsKey(e)).Select(e => "WPF sans correspondance : " + e));
        foreach (var (wpfEvent, callback) in Events)
        {
            if (!wpf.Contains(wpfEvent)) problems.Add("absent de WPF : " + wpfEvent);
            if (!blazor.ContainsKey(callback)) problems.Add("absent de Blazor : " + callback);
        }

        problems.AddRange(blazor.Where(p => IsCallback(p.Value) && !TwoWayBindings.Contains(p.Key) && !Events.ContainsValue(p.Key))
            .Select(p => "Blazor sans correspondance : " + p.Key));
        Assert.True(problems.Count == 0, string.Join("\n", problems.Order()));
    }

    [Fact]
    public void Every_setting_exists_on_both_sides()
    {
        var wpf = typeof(WpfView).GetProperties(Declared).Select(p => p.Name).ToHashSet();
        var blazor = typeof(BlazorView).GetProperties(Declared).Select(p => p.Name).ToHashSet();
        var parameters = typeof(BlazorView).GetProperties(Declared).Where(p => IsParameter(p) && !IsCallback(p.PropertyType))
            .Select(p => p.Name);

        var problems = wpf.Where(n => !IsCommand(n) && !blazor.Contains(n) && !WpfOnly.ContainsKey(n))
            .Select(n => "absent de Blazor : " + n)
            .Concat(parameters.Where(n => n != "OnBeforeDelete" && !BlazorOnly.ContainsKey(n) &&
                                          typeof(WpfView).GetProperty(n) == null)
                .Select(n => "absent de WPF : " + n))
            .Order().ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
