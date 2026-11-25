namespace Wpflow.Blazor;

/// <summary>
///     Correspondance entre les propriétés d'un ViewModel de node et un <see cref="Node" />
///     (voir <see cref="FlowView.NodesSource" />). Les propriétés sont décrites par <see cref="NodeMappingBase" />.
/// </summary>
public sealed class NodeMapping : NodeMappingBase
{
    internal static readonly NodeMapping Default = new();
}

/// <summary>
///     Correspondance entre les propriétés d'un ViewModel d'edge et un <see cref="Edge" />
///     (voir <see cref="FlowView.EdgesSource" />). Les propriétés sont décrites par <see cref="EdgeMappingBase" />.
/// </summary>
public sealed class EdgeMapping : EdgeMappingBase
{
    internal static readonly EdgeMapping Default = new();
}

/// <summary>
///     L'historique de l'application, à la place de celui de la vue (<see cref="FlowView.History" />) : la vue appelle
///     <see cref="Checkpoint" /> avant chaque modification faite par un geste et délègue annuler et rétablir.
/// </summary>
public interface IFlowHistory
{
    bool CanUndo { get; }
    bool CanRedo { get; }

    /// <summary>L'état va changer : mémorisez ce qu'il faut pour pouvoir revenir en arrière.</summary>
    void Checkpoint();

    void Undo();
    void Redo();
}

/// <summary>
///     Ce qu'un ViewModel peut faire sur la vue sans la connaître : <c>ControllerChanged="c => vm.Flow = c"</c>.
///     Comme les méthodes de <see cref="FlowView" />, ces opérations sont asynchrones.
/// </summary>
public interface IFlowController
{
    Task<bool> FitView(FitViewOptions? options = null);
    Task ZoomIn();
    Task ZoomOut();

    /// <summary>Zoom absolu, centré sur la vue.</summary>
    Task ZoomTo(double zoom);

    /// <summary>Centre la vue sur un node (par son id), sans changer le zoom. Faux si le node est inconnu ou pas encore mesuré.</summary>
    Task<bool> CenterOn(string nodeId);

    Task SetCenter(double x, double y, double? zoom = null, TimeSpan duration = default);
    Task SelectAll();
    Task ClearSelection();
    Task<bool> DeleteSelection();
    bool Copy();
    Task<bool> Cut();
    Task<bool> Paste();
    Task<bool> Undo();
    Task<bool> Redo();

    /// <summary>Convertit un point de la vue (pixels, relatif au conteneur) en coordonnées flow.</summary>
    XYPosition ScreenToFlow(XYPosition container);

    /// <summary>Convertit des coordonnées flow en point de la vue.</summary>
    XYPosition FlowToScreen(XYPosition flow);
}
