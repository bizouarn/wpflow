// Compilé dans Wpflow.Core et, par lien, dans les deux vues (voir ViewLogic.cs).

namespace Wpflow.Core;

/// <summary>
///     <c>expandParent</c> : un enfant <see cref="Node.ExpandParent" /> qui dépasse de son parent l'agrandit. À droite et en bas,
///     seule la taille change ; à gauche et en haut, le parent recule et ses enfants sont recalés pour ne pas bouger à l'écran.
/// </summary>
internal static class ParentExpansion
{
    /// <summary>
    ///     L'agrandissement d'un parent : ses changes, et le recul de son coin (nul à droite et en bas). Les positions relatives
    ///     de ses enfants sont diminuées de <paramref name="Shift" />, celles retenues au début d'un glisser aussi.
    /// </summary>
    internal sealed record Expansion(IReadOnlyList<NodeChange> Changes, XYPosition Shift);

    /// <summary>Les parents que les nodes déplacés peuvent agrandir, à traiter un à un.</summary>
    public static IReadOnlyList<string> Parents(IEnumerable<string> moved, Func<string, Node?> getNode) =>
        moved.Select(getNode).Where(n => n is { ExpandParent: true, ParentId: not null })
            .Select(n => n!.ParentId!).Distinct().ToList();

    /// <summary>
    ///     L'agrandissement de <paramref name="parentId" />, ou <c>null</c> si les enfants déplacés y tiennent.
    ///     <paramref name="getNode" /> et <paramref name="nodes" /> donnent les nodes tels que stockés (positions relatives).
    /// </summary>
    public static Expansion? Compute(string parentId, IEnumerable<string> moved, Func<string, Node?> getNode,
        IReadOnlyList<Node> nodes, XYPosition nodeOrigin)
    {
        if (getNode(parentId) is not { } parent) return null;
        var pw = parent.Width ?? parent.FixedWidth ?? 0;
        var ph = parent.Height ?? parent.FixedHeight ?? 0;
        double minX = 0, minY = 0, maxX = pw, maxY = ph;
        foreach (var id in moved)
        {
            if (getNode(id) is not { ExpandParent: true } child || child.ParentId != parentId) continue;
            var offset = NodeConstraints.OriginOffset(child, nodeOrigin);
            var (x, y) = (child.Position.X - offset.X, child.Position.Y - offset.Y);
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x + (child.Width ?? 0));
            maxY = Math.Max(maxY, y + (child.Height ?? 0));
        }

        if (minX == 0 && minY == 0 && maxX <= pw && maxY <= ph) return null;

        var shift = new XYPosition(minX, minY);
        var changes = new List<NodeChange> { new NodeResizeChange(parentId, maxX - minX, maxY - minY, parent.Position + shift) };
        if (shift != XYPosition.Zero)
            // Les enfants gardent leur place à l'écran : on compense le recul du parent.
            foreach (var sibling in nodes.Where(n => n.ParentId == parentId))
                changes.Add(new NodePositionChange(sibling.Id, sibling.Position - shift));
        return new Expansion(changes, shift);
    }
}
