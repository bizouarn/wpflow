using System.Windows.Input;

namespace Wpflow.Wpf;

/// <summary>
///     Commandes routées de <see cref="FlowView" />, utilisables en XAML sans code-behind :
///     <c>&lt;Button Command="{x:Static flow:FlowCommands.FitView}" CommandTarget="{Binding ElementName=Flow}" /&gt;</c>.
///     Un bouton placé <b>dans</b> la <see cref="FlowView" /> (overlay) n'a pas besoin de <c>CommandTarget</c>.
///     <c>CanExecute</c> suit l'état (Annuler grisé sans historique, Supprimer sans sélection…).
/// </summary>
public static class FlowCommands
{
    private static RoutedUICommand Create(string text, string name, Key? key = null, ModifierKeys modifiers = ModifierKeys.None) =>
        new(text, name, typeof(FlowCommands), key is { } k ? [new KeyGesture(k, modifiers)] : null);

    public static RoutedUICommand FitView { get; } = Create("Cadrer le graphe", nameof(FitView));
    public static RoutedUICommand ZoomIn { get; } = Create("Zoom avant", nameof(ZoomIn));
    public static RoutedUICommand ZoomOut { get; } = Create("Zoom arrière", nameof(ZoomOut));
    public static RoutedUICommand Undo { get; } = Create("Annuler", nameof(Undo));
    public static RoutedUICommand Redo { get; } = Create("Rétablir", nameof(Redo));
    public static RoutedUICommand SelectAll { get; } = Create("Tout sélectionner", nameof(SelectAll));
    public static RoutedUICommand ClearSelection { get; } = Create("Désélectionner", nameof(ClearSelection));
    public static RoutedUICommand DeleteSelection { get; } = Create("Supprimer la sélection", nameof(DeleteSelection));
    public static RoutedUICommand Copy { get; } = Create("Copier", nameof(Copy));
    public static RoutedUICommand Cut { get; } = Create("Couper", nameof(Cut));
    public static RoutedUICommand Paste { get; } = Create("Coller", nameof(Paste));

    /// <summary>Bascule <see cref="FlowView.IsLocked" />.</summary>
    public static RoutedUICommand ToggleLock { get; } = Create("Verrouiller", nameof(ToggleLock));
}

/// <summary>
///     Ce qu'un ViewModel peut demander à la vue sans en connaître le type (liaison <c>OneWayToSource</c> sur
///     <see cref="FlowView.Controller" />).
/// </summary>
public interface IFlowController
{
    void FitView();
    void ZoomIn();
    void ZoomOut();

    /// <summary>Zoom absolu, centré sur la vue.</summary>
    void ZoomTo(double zoom);

    /// <summary>Centre la vue sur un node (par son id), sans changer le zoom. Renvoie <c>false</c> si le node est inconnu ou pas encore mesuré.</summary>
    bool CenterOn(string nodeId);

    void SelectAll();
    void ClearSelection();
    void DeleteSelection();
    bool Copy();
    bool Cut();
    bool Paste();

    /// <summary>Cadre les nodes avec options (marge, bornes de zoom, sous-ensemble, durée de transition).</summary>
    bool FitView(FitViewOptions? options);

    /// <summary>Centre la vue sur un point flow (<c>setCenter</c>).</summary>
    void SetCenter(double x, double y, double? zoom = null, System.TimeSpan duration = default);
    void Undo();
    void Redo();

    /// <summary>Convertit un point de la vue (pixels) en coordonnées flow.</summary>
    Wpflow.Core.XYPosition ScreenToFlow(System.Windows.Point point);

    /// <summary>Convertit des coordonnées flow en point de la vue (pixels).</summary>
    System.Windows.Point FlowToScreen(Wpflow.Core.XYPosition position);
}

/// <summary>
///     Historique fourni par le ViewModel (undo/redo applicatif). Quand <see cref="FlowView.History" /> est défini, la vue
///     n'a plus d'historique propre : elle demande <see cref="Checkpoint" /> avant chaque modification faite par un geste
///     (déplacement, suppression, connexion) et délègue Ctrl+Z / Ctrl+Y à <see cref="Undo" /> / <see cref="Redo" />.
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

/// <summary>Contrôleur remis au ViewModel : il ne laisse voir que <see cref="IFlowController" />, pas le contrôle.</summary>
internal sealed class FlowController(FlowView flow) : IFlowController
{
    public void FitView() => flow.FitView();
    public void ZoomIn() => flow.ZoomIn();
    public void ZoomOut() => flow.ZoomOut();
    public void ZoomTo(double zoom) => flow.Zoom = zoom;
    public bool CenterOn(string nodeId) => flow.CenterOn(nodeId);
    public void SelectAll() => flow.SelectAllElements();
    public void ClearSelection() => flow.ClearSelection();
    public void DeleteSelection() => flow.DeleteSelection();
    public bool Copy() => flow.Copy();
    public bool Cut() => flow.Cut();
    public bool Paste() => flow.Paste();
    public bool FitView(FitViewOptions? options) => flow.FitView(options);
    public void SetCenter(double x, double y, double? zoom = null, System.TimeSpan duration = default) => flow.SetCenter(x, y, zoom, duration);
    public void Undo() => flow.Undo();
    public void Redo() => flow.Redo();
    public Wpflow.Core.XYPosition ScreenToFlow(System.Windows.Point point) => flow.ScreenToFlow(point);

    public System.Windows.Point FlowToScreen(Wpflow.Core.XYPosition position)
    {
        var v = flow.Viewport;
        return new System.Windows.Point(position.X * v.Zoom + v.X, position.Y * v.Zoom + v.Y);
    }
}
