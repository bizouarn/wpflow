// Compilé dans Wpflow.Core et, par lien, dans les deux vues (voir ViewLogic.cs).

namespace Wpflow.Core;

/// <summary>
///     Virtualisation : « faut-il afficher cet élément ? ». Les zones viennent de <see cref="Virtualization.Zones" /> : un
///     élément apparaît en entrant dans la zone d'affichage et n'est retiré qu'en sortant de la zone de maintien, plus large,
///     pour ne pas être recréé à chaque va-et-vient. Chaque vue décide ce qu'elle fait d'un élément seulement maintenu (WPF
///     le masque ; Blazor ne le retire qu'à la reconstruction suivante).
/// </summary>
internal static class VirtualizationRule
{
    internal enum Presence
    {
        Absent, // rien à créer, et ce qui existe peut être retiré
        Kept,   // à ne pas créer ; ce qui existe est gardé
        Shown
    }

    public static Presence Of(Rect element, Rect show, Rect keep) =>
        element.Overlaps(show) ? Presence.Shown : element.Overlaps(keep) ? Presence.Kept : Presence.Absent;

    /// <summary>Un node sélectionné est toujours affiché.</summary>
    public static Presence OfNode(bool selected, Rect node, Rect show, Rect keep) =>
        selected ? Presence.Shown : Of(node, show, keep);

    /// <summary>Un edge suit le rectangle qui englobe ses deux nodes.</summary>
    public static Presence OfEdge(Rect source, Rect target, Rect show, Rect keep) =>
        GraphUtils.IsEdgeVisible(source, target, show) ? Presence.Shown
        : GraphUtils.IsEdgeVisible(source, target, keep) ? Presence.Kept
        : Presence.Absent;

    /// <summary>
    ///     Ce qui est dessiné ne convient-il plus ? Un élément à afficher manque, ou un élément dessiné est sorti de la zone
    ///     de maintien.
    /// </summary>
    public static bool IsOutdated(bool drawn, Presence presence) => drawn ? presence == Presence.Absent : presence == Presence.Shown;
}
