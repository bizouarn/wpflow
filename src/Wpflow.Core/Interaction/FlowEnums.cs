namespace Wpflow.Core;

// Types d'options partagés par les vues (WPF, Blazor) : un seul endroit, pas de copies.

public enum BackgroundVariant
{
    None,
    Dots,
    Lines,
    Cross
}

public enum FlowSelectionMode
{
    /// <summary>Un node est sélectionné dès que le rectangle le touche (<c>SelectionMode.Partial</c>).</summary>
    Partial,

    /// <summary>Le rectangle doit contenir le node entier (<c>SelectionMode.Full</c>).</summary>
    Full
}

public enum PanOnScrollMode
{
    Free,
    Vertical,
    Horizontal
}

/// <summary>Forme de l'aperçu d'une connexion en cours (<c>connectionLineType</c>).</summary>
public enum ConnectionLineType
{
    Bezier,
    Straight,
    Step,
    SmoothStep,
    SimpleBezier
}

/// <summary>Empilement des nodes (<c>zIndexMode</c>).</summary>
public enum FlowZIndexMode
{
    /// <summary>Profondeur, plus le premier plan pour les nodes sélectionnés (selon <c>ElevateNodesOnSelect</c>).</summary>
    Basic,

    /// <summary>Comme <c>Basic</c>, et un parent dont un descendant est sélectionné passe aussi devant ses voisins.</summary>
    Auto,

    /// <summary>Seul <c>Node.ZIndex</c> compte : ni profondeur ni élévation à la sélection.</summary>
    Manual
}

/// <summary>Apparence du redimensionneur de node.</summary>
public enum ResizerVariant
{
    Handle,
    Line
}

/// <summary>Alignement d'une barre d'outils le long du côté du node (<c>align</c>).</summary>
public enum ToolbarAlign
{
    Start,
    Center,
    End
}

/// <summary>Position d'un panneau dans le calque de la vue (<c>&lt;Panel position="…"&gt;</c>).</summary>
public enum PanelPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    CenterLeft,
    CenterRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

/// <summary>
///     Options de cadrage (<c>fitViewOptions</c>) : marge, bornes de zoom, sous-ensemble de nodes et durée de la transition
///     (zéro = immédiat).
/// </summary>
public sealed record FitViewOptions(double? Padding = null, double? MinZoom = null, double? MaxZoom = null,
    TimeSpan Duration = default, IReadOnlyList<string>? NodeIds = null, bool IncludeHiddenNodes = false);

/// <summary>Un redimensionnement : début, en cours ou terminé (<c>onResizeStart</c>, <c>onResize</c>, <c>onResizeEnd</c>).</summary>
public sealed record ResizeInfo(string NodeId, ResizeDirection Direction, Rect StartRect, Rect Rect);
