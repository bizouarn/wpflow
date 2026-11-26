using Wpflow.Core;

namespace Scenarios;

/// <summary>
///     Un geste et la suite exacte des évènements qu'il doit lever, la même en WPF et en Blazor : cette table est le tableau
///     de parité. Une suite vide dit que rien ne doit se produire.
/// </summary>
public sealed record Gesture(string Name, params string[] Expected)
{
    /// <summary>
    ///     Réglages posés avant le geste : « Réglage=valeur » sur la vue, ou « node:id:Propriété=valeur » et
    ///     « edge:id:Propriété=valeur » sur un élément du schéma.
    /// </summary>
    public string[] Options { get; init; } = [];

    /// <summary>Évènements de fond (<see cref="EventCatalog.Background" />) contrôlés aussi par ce geste.</summary>
    public string[] Also { get; init; } = [];

    /// <summary>Si renseigné, seuls ces évènements sont contrôlés.</summary>
    public string[] Only { get; init; } = [];

    /// <summary>Un écart voulu entre les deux vues : la suite attendue en Blazor, avec sa raison dans <see cref="Why" />.</summary>
    public string[]? Blazor { get; init; }

    public string? Why { get; init; }

    public IReadOnlyList<string> ExpectedFor(bool blazor) => blazor && Blazor != null ? Blazor : Expected;

    /// <summary>Le journal, réduit à ce que ce geste contrôle ; les évènements en rafale sont regroupés.</summary>
    public IReadOnlyList<string> Reduce(IReadOnlyList<string> journal)
    {
        var result = new List<string>();
        var run = new HashSet<string>(); // les évènements continus déjà notés dans la rafale en cours
        foreach (var entry in journal)
        {
            var space = entry.IndexOf(' ');
            var name = space < 0 ? entry : entry[..space];
            if (Only.Length > 0 ? !Only.Contains(name) : EventCatalog.Background.Contains(name) && !Also.Contains(name)) continue;
            if (!EventCatalog.Continuous.Contains(name))
            {
                run.Clear();
                result.Add(entry);
            }
            else if (run.Add(entry)) result.Add(entry);
        }

        return result;
    }
}

/// <summary>La matrice des évènements et des gestes (ROADMAP-TESTS, lot 3).</summary>
public static class GestureMatrix
{
    private const string Orders = "SelectionChange orders|";
    private const string Both = "SelectionChange customers,orders|";
    private const string Cleared = "SelectionChange |";
    private const string Lines = "SelectionChange |r-lines";
    private const string Link = "customers.id>lines.id";
    private const string Relinked = "orders.id>lines.id";
    private const string Removed = "orders|r-lines,r-orders";

    private static readonly string[] Drag = [Orders, "NodeDragStart orders", "NodeDrag orders", "NodeDragStop orders"];
    private static readonly string[] Pan = ["MoveStart", "Move", "MoveEnd"];
    private static readonly string[] Dropped = ["ConnectStart customers.id", "ConnectEnd customers.id -"];
    private static readonly string[] Armed = ["ConnectStart customers.id", "ClickConnectStart customers.id"];

    private static Gesture G(string name, params string[] expected) => new(name, expected);

    private static Gesture With(string name, string option, params string[] expected) => new(name, expected) { Options = [option] };

    public static readonly IReadOnlyList<Gesture> All =
    [
        // ---------- node ----------
        G("node.click", Orders, "NodeClick orders"),
        G("node.click-selected", "NodeClick orders"),
        G("node.click-column", Orders, "NodeClick orders.note"),
        G("node.double-click", Orders, "NodeClick orders", "NodeDoubleClick orders", "NodeClick orders"),
        G("node.right-click", "NodeContextMenu orders"),
        G("node.middle-click"),
        G("node.ctrl-click-adds", Both, "NodeClick orders"),
        G("node.ctrl-click-removes", Cleared, "NodeClick orders"),
        G("node.shift-click-adds", Both, "NodeClick orders"),
        With("node.drag-under-threshold", "NodeDragThreshold=10", Orders, "NodeClick orders"),
        G("node.drag", Drag),
        G("node.drag-back-to-start", Drag),
        With("node.drag-released-outside", "AutoPanOnNodeDrag=false", Drag),
        G("node.drag-escape", Orders, "NodeDragStart orders", "NodeDrag orders", Cleared, "NodeDrag orders", "NodeDragStop orders"),
        G("node.drag-interrupted", Drag),

        // ---------- sélection ----------
        G("selection.click-one", "NodeClick orders"),
        G("selection.drag", "NodeDragStart lines,orders", "SelectionDragStart lines,orders", "NodeDrag lines,orders",
            "SelectionDrag lines,orders", "NodeDragStop lines,orders", "SelectionDragStop lines,orders"),
        G("selection.right-click", "SelectionContextMenu", "NodeContextMenu lines"),
        G("selection.box", "SelectionStart", Orders, "SelectionEnd"),
        G("selection.box-interrupted", "SelectionStart", Orders, "SelectionEnd"),

        // ---------- fond ----------
        G("pane.click", Cleared, "PaneClick"),
        new("pane.double-click", "PaneClick", "MoveStart", "Move", "PaneClick", "Move", "MoveEnd")
        {
            Blazor = ["PaneClick", "PaneClick", "MoveStart", "Move", "MoveEnd"],
            Why = "WPF zoome au second appui ; le navigateur n'envoie son « dblclick » qu'après le second clic."
        },
        G("pane.right-click", "PaneContextMenu"),
        G("pane.middle-click"),
        With("pane.drag-under-threshold", "PaneDragThreshold=10", "PaneClick"),
        G("pane.click-after-drag-back", Pan),
        G("pane.drag", Pan),
        new("pane.drag-with-selection", Cleared, "MoveStart", "Move", "MoveEnd")
        {
            Blazor = Pan,
            Why = "WPF efface la sélection dès l'appui sur le fond ; Blazor la garde pendant un glisser, comme React Flow."
        },
        G("pane.middle-drag", Pan),
        With("pane.right-drag", "PanOnDragButtons=Right", Pan),
        G("pane.drag-released-outside", Pan),
        G("pane.drag-interrupted", Pan),
        G("pane.wheel", "PaneScroll", "MoveStart", "Move", "MoveEnd"),

        // ---------- edge et label ----------
        G("edge.click", Lines, "EdgeClick r-lines"),
        G("edge.click-near-end", Lines, "EdgeClick r-lines"),
        G("edge.double-click", Lines, "EdgeClick r-lines", "EdgeDoubleClick r-lines", "EdgeClick r-lines"),
        G("edge.right-click", "EdgeContextMenu r-lines"),
        G("edge.middle-click"),
        G("edge.ctrl-click-adds", "SelectionChange orders|r-lines", "EdgeClick r-lines"),
        G("edge.drag", Lines),
        G("label.click"),
        G("label.double-click"),

        // ---------- handle et connexion ----------
        G("handle.click", Armed),
        G("handle.click-connect", [.. Armed, "Connect " + Link, "ClickConnectEnd customers.id " + Link]),
        G("handle.click-connect-cancel", [.. Armed, "ClickConnectEnd customers.id -", "PaneClick"]),
        G("handle.click-connect-escape", [.. Armed, "ClickConnectEnd customers.id -"]),
        G("handle.drag-connect", "ConnectStart customers.id", "Connect " + Link, "ConnectEnd customers.id " + Link),
        G("handle.drag-void", Dropped),
        With("handle.drag-released-outside", "AutoPanOnConnect=false", Dropped),
        G("handle.drag-interrupted", Dropped),
        G("handle.right-click", "NodeContextMenu customers.id"),
        G("handle.connected", "ConnectStart box.out", "Connect box.out>grip.in", "HandleConnected box.out>grip.in",
            "ConnectEnd box.out box.out>grip.in"),

        // ---------- reconnexion ----------
        G("reconnect.to-handle", Lines, "ConnectStart orders.id", "ReconnectStart r-lines", "Reconnect r-lines " + Relinked,
            "ReconnectEnd r-lines " + Relinked, "ConnectEnd orders.id " + Relinked),
        G("reconnect.to-void", Lines, "ConnectStart orders.id", "ReconnectStart r-lines", "ReconnectEnd r-lines -",
            "ConnectEnd orders.id -"),
        G("reconnect.interrupted", Lines, "ConnectStart orders.id", "ReconnectStart r-lines", "ReconnectEnd r-lines -",
            "ConnectEnd orders.id -"),

        // ---------- clavier et dépôt ----------
        G("delete.key", "BeforeDelete " + Removed, Cleared, "Delete " + Removed),
        G("drop.palette", "Drop"),

        // ---------- compagnons ----------
        G("controls.zoom-in", "Move", "ZoomIn"),
        G("controls.zoom-out", "Move", "ZoomOut"),
        G("controls.fit", "Move", "FitView"),
        G("controls.lock", "InteractiveChange false"),
        G("minimap.node-click", "MiniMapNodeClick orders", "Move"),
        G("resizer.drag", "ResizeStart box", "Resize box", "ResizeEnd box"),
        G("resize-control.drag", "ControlResizeStart grip", "ControlResize grip", "ControlResizeEnd grip"),
        G("toolbar.click"),
        G("widget.click"),
        G("widget.double-click"),

        // ---------- survol ----------
        new("hover.node", "NodeMouseEnter orders", "NodeMouseMove orders", "NodeMouseLeave orders")
            { Only = ["NodeMouseEnter", "NodeMouseMove", "NodeMouseLeave"] },
        new("hover.edge", "EdgeMouseEnter r-lines", "EdgeMouseMove r-lines", "EdgeMouseLeave r-lines")
            { Only = ["EdgeMouseEnter", "EdgeMouseMove", "EdgeMouseLeave"] },
        new("hover.pane", "PaneMouseEnter", "PaneMouseMove", "PaneMouseLeave")
            { Only = ["PaneMouseEnter", "PaneMouseMove", "PaneMouseLeave"] },

        // ---------- cycle de vie et notifications de fond ----------
        new("lifecycle.load", "Init", "NodesReady") { Only = ["Init", "NodesReady"] },
        new("lifecycle.error", "Error") { Only = ["Error"] },
        new("background.node-drag", "NodesChange") { Only = ["NodesChange"] },
        new("background.edge-click", "EdgesChange") { Only = ["EdgesChange"] },
        new("background.state", "StateChange") { Only = ["StateChange"] },

        // ---------- réglages qui coupent une interaction ----------
        With("locked.node.click", "IsLocked=true", "NodeClick orders"),
        With("locked.node.drag", "IsLocked=true", Pan),
        With("locked.edge.click", "IsLocked=true", "EdgeClick r-lines"),
        With("locked.handle.drag", "IsLocked=true"),
        With("locked.delete.key", "IsLocked=true"),
        With("no-nodes-draggable.node.drag", "NodesDraggable=false", Orders),
        With("node-not-draggable.drag", "node:orders:Draggable=false", Orders),
        With("no-elements-selectable.node.click", "ElementsSelectable=false", "NodeClick orders"),
        With("node-not-selectable.click", "node:orders:Selectable=false", "NodeClick orders"),
        With("edge-not-selectable.click", "edge:r-lines:Selectable=false", "EdgeClick r-lines"),
        With("no-nodes-connectable.handle.drag", "NodesConnectable=false"),
        With("node-not-connectable.handle.drag", "node:customers:Connectable=false"),
        With("no-edges-reconnectable.drag", "EdgesReconnectable=false", Lines),
        With("edge-not-reconnectable.drag", "edge:r-lines:Reconnectable=false", Lines),
        With("node-not-deletable.delete.key", "node:orders:Deletable=false"),
        With("no-delete-keys.delete.key", "DeleteKeys="),
        With("no-pan-on-drag.pane.drag", "PanOnDrag=false"),
        With("no-zoom-on-scroll.wheel", "ZoomOnScroll=false", "PaneScroll"),
        With("no-zoom-on-double-click.pane", "ZoomOnDoubleClick=false", "PaneClick", "PaneClick"),
        With("no-connect-on-click.handle.click", "ConnectOnClick=false", Dropped),
        With("no-select-on-drag.node.drag", "SelectNodesOnDrag=false", "NodeDragStart orders", "NodeDrag orders", "NodeDragStop orders")
    ];

    public static Gesture Named(string name) => All.First(g => g.Name == name);

    /// <summary>
    ///     Le schéma de la matrice : la boutique, un node redimensionnable avec barre d'outils (<c>box</c>) et un node à
    ///     poignée de redimensionnement libre (<c>grip</c>).
    /// </summary>
    public static Schema Schema()
    {
        var shop = Schemas.Shop();
        return new Schema(
        [
            .. shop.Nodes.Where(n => n.Id != "audit"),
            Schemas.TableNode("audit", 380, 330, new Table("audit", [new("id", "int", true)], Locked: true)),
            new Node("box", new XYPosition(0, 220), Schemas.SizableType) { FixedWidth = 160, FixedHeight = 80 },
            new Node("grip", new XYPosition(760, 220), Schemas.GripType) { FixedWidth = 160, FixedHeight = 80 }
        ], shop.Edges);
    }

    /// <summary>Le schéma, les réglages d'éléments du geste appliqués.</summary>
    public static Schema SchemaFor(IReadOnlyList<string> options)
    {
        var schema = Schema();
        var nodes = schema.Nodes.ToList();
        var edges = schema.Edges.ToList();
        foreach (var option in options)
        {
            var parts = option.Split(':', '=');
            if (parts is not [var kind, var id, var property, var value] || kind is not ("node" or "edge")) continue;
            var on = bool.Parse(value);
            if (kind == "node")
            {
                var i = nodes.FindIndex(n => n.Id == id);
                nodes[i] = property switch
                {
                    "Draggable" => nodes[i] with { Draggable = on },
                    "Selectable" => nodes[i] with { Selectable = on },
                    "Connectable" => nodes[i] with { Connectable = on },
                    "Deletable" => nodes[i] with { Deletable = on },
                    _ => throw new ArgumentException(option)
                };
            }
            else
            {
                var i = edges.FindIndex(e => e.Id == id);
                edges[i] = property switch
                {
                    "Selectable" => edges[i] with { Selectable = on },
                    "Reconnectable" => edges[i] with { Reconnectable = on },
                    _ => throw new ArgumentException(option)
                };
            }
        }

        return new Schema(nodes, edges);
    }

    /// <summary>Les réglages de la vue d'un geste (nom, valeur), ceux des éléments exclus.</summary>
    public static IEnumerable<(string Name, string Value)> ViewOptions(IEnumerable<string> options) =>
        options.Where(o => !o.StartsWith("node:") && !o.StartsWith("edge:"))
            .Select(o => o.Split('=', 2))
            .Select(p => (p[0], p[1]));
}
