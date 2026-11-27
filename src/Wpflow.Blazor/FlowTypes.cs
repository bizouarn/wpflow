using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Wpflow.Blazor;

/// <summary>Un motif de fond ; à superposer avec <see cref="FlowView.AdditionalBackgrounds" />.</summary>
public sealed record BackgroundLayer(BackgroundVariant Variant = BackgroundVariant.Dots, double Gap = 20, string? Color = null,
    double? Size = null, double LineWidth = 1, XYPosition? Offset = null);

/// <summary>Position du pointeur au moment d'un évènement : écran (relatif au conteneur), flow et modificateurs.</summary>
public sealed record FlowPointer(XYPosition Screen, XYPosition Flow, bool Shift, bool Ctrl, bool Alt, bool Meta);

public sealed record NodeEventArgs(Node Node, FlowPointer Pointer)
{
    /// <summary>
    ///     Les attributs <c>data-*</c> de l'élément pressé dans le node et de ses parents (le plus proche l'emporte), sans le
    ///     préfixe : <c>data-column="id"</c> donne <c>TargetData["column"]</c>. Renseigné pour le clic, le double-clic et le
    ///     menu contextuel ; vide au survol.
    /// </summary>
    public IReadOnlyDictionary<string, string> TargetData { get; init; } = NoData;

    private static readonly IReadOnlyDictionary<string, string> NoData =
        System.Collections.ObjectModel.ReadOnlyDictionary<string, string>.Empty;
}

public sealed record EdgeEventArgs(Edge Edge, FlowPointer Pointer);

public sealed record PaneEventArgs(FlowPointer Pointer);

public sealed record NodeDragEventArgs(Node Node, IReadOnlyList<Node> Nodes, FlowPointer Pointer);

public sealed record SelectionChangeArgs(IReadOnlyList<Node> Nodes, IReadOnlyList<Edge> Edges);

public sealed record ConnectStartEventArgs(string NodeId, string? HandleId, HandleType HandleType)
{
    /// <summary>Vrai quand on rattrape l'extrémité d'un edge existant (reconnexion) plutôt qu'un nouveau fil.</summary>
    public bool IsReconnection { get; init; }
}

public sealed record ConnectEndEventArgs(ConnectStartEventArgs From, Connection? Connection, bool IsValid, FlowPointer Pointer);

public sealed record ReconnectEventArgs(Edge OldEdge, Connection NewConnection);

public sealed record ReconnectEndEventArgs(Edge Edge, HandleType HandleType, Connection? Connection);

/// <summary>Éléments sur le point d'être supprimés (<c>onBeforeDelete</c>).</summary>
public sealed record DeleteRequest(IReadOnlyList<Node> Nodes, IReadOnlyList<Edge> Edges);

public sealed record FlowDropEventArgs(XYPosition Position, DragEventArgs Drag);

public sealed record FlowError(string Code, string Message);

/// <summary>Contexte passé aux modèles d'edges et de labels : l'edge, son tracé et ses extrémités (coordonnées flow).</summary>
public sealed record EdgeRenderContext(Edge Edge, EdgePath Path, EdgeGeometry Geometry, bool Selected);

/// <summary>État de la connexion en cours (<c>useConnection</c>) ; <c>Path</c> est le tracé SVG du fil (coordonnées flow), pour un <c>ConnectionLineTemplate</c>.</summary>
public sealed record ConnectionState(bool InProgress, string? FromNodeId, string? FromHandleId, HandleType? FromType,
    string? ToNodeId, string? ToHandleId, bool IsValid, XYPosition Position, string? Path = null);

/// <summary>Réglages d'un redimensionnement ; renseignés par <c>NodeResizer</c> / <c>NodeResizeControl</c>.</summary>
public sealed class ResizeOptions
{
    public double MinWidth { get; init; } = 60;
    public double MinHeight { get; init; } = 40;
    public double MaxWidth { get; init; } = double.PositiveInfinity;
    public double MaxHeight { get; init; } = double.PositiveInfinity;
    public bool KeepAspectRatio { get; init; }
    public Func<ResizeDirection, Rect, bool>? ShouldResize { get; init; }
    public Action<ResizeInfo>? Started { get; init; }
    public Action<ResizeInfo>? Resizing { get; init; }
    public Action<ResizeInfo>? Ended { get; init; }

    /// <summary>Les réglages des deux composants de redimensionnement, dont les évènements sont relayés à leurs paramètres.</summary>
    internal static ResizeOptions Of(double minWidth, double minHeight, double maxWidth, double maxHeight, bool keepAspectRatio,
        Func<ResizeDirection, Rect, bool>? shouldResize, EventCallback<ResizeInfo> started, EventCallback<ResizeInfo> resizing,
        EventCallback<ResizeInfo> ended) => new()
    {
        MinWidth = minWidth, MinHeight = minHeight, MaxWidth = maxWidth, MaxHeight = maxHeight,
        KeepAspectRatio = keepAspectRatio, ShouldResize = shouldResize,
        Started = a => _ = started.InvokeAsync(a),
        Resizing = a => _ = resizing.InvokeAsync(a),
        Ended = a => _ = ended.InvokeAsync(a)
    };
}


/// <summary>Handle mesuré par le pont JS (relatif au coin du node, en unités flow).</summary>
public sealed class MeasuredHandle
{
    public string? Id { get; set; }
    public string Type { get; set; } = "source";
    public string Position { get; set; } = "bottom";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Connectable { get; set; } = true;
    public bool ConnectableStart { get; set; } = true;
    public bool ConnectableEnd { get; set; } = true;
}

public sealed class MeasuredNode
{
    public string Id { get; set; } = "";
    public double Width { get; set; }
    public double Height { get; set; }
    public List<MeasuredHandle> Handles { get; set; } = [];

    /// <summary>Les handles ne sont qu'une partie : la suite arrive dans les messages suivants.</summary>
    public bool Partial { get; set; }
}

/// <summary>Réglages propres à un <c>FlowHandle</c> (validateur et évènement), enregistrés auprès de la vue.</summary>
public sealed record HandleExtras(Func<Connection, bool>? IsValidConnection, Microsoft.AspNetCore.Components.EventCallback<Connection> Connected,
    bool ConnectableStart = true, bool ConnectableEnd = true, bool Connectable = true);
