namespace Wpflow.Core;

/// <summary>Contraintes de déplacement et positions absolues (origine, parent, extents).</summary>
public static class NodeConstraints
{
    /// <summary>Décalage entre la position stockée d'un node et son coin haut-gauche : origine × taille.</summary>
    public static XYPosition OriginOffset(Node node, XYPosition nodeOrigin)
    {
        var o = node.Origin ?? nodeOrigin;
        if (o == XYPosition.Zero) return XYPosition.Zero;
        return new XYPosition(o.X * (node.Width ?? node.FixedWidth ?? 0), o.Y * (node.Height ?? node.FixedHeight ?? 0));
    }

    /// <summary>
    ///     Coins haut-gauche absolus quand des origines sont en jeu : parent (déjà décalé) + position relative − décalage du node.
    /// </summary>
    public static Dictionary<string, XYPosition> AbsoluteWithOrigin(IReadOnlyList<Node> nodes, XYPosition nodeOrigin)
    {
        var byId = nodes.ToDictionary(n => n.Id);
        var result = new Dictionary<string, XYPosition>(nodes.Count);
        XYPosition Of(Node n, int depth)
        {
            if (result.TryGetValue(n.Id, out var known)) return known;
            var parent = depth < 64 && n.ParentId != null && byId.TryGetValue(n.ParentId, out var p) ? Of(p, depth + 1) : XYPosition.Zero;
            return result[n.Id] = parent + n.Position - OriginOffset(n, nodeOrigin);
        }

        foreach (var n in nodes) Of(n, 0);
        return result;
    }

    /// <summary>Positions absolues (coin haut-gauche) de tous les nodes, avec ou sans origine.</summary>
    public static Dictionary<string, XYPosition> Absolute(IReadOnlyList<Node> nodes, XYPosition nodeOrigin)
    {
        var hasTree = nodes.Any(n => n.ParentId != null);
        var shifted = nodeOrigin != XYPosition.Zero || nodes.Any(n => n.Origin != null);
        if (shifted) return AbsoluteWithOrigin(nodes, nodeOrigin);
        return hasTree ? NodeTree.AbsolutePositions(nodes) : nodes.ToDictionary(n => n.Id, n => n.Position);
    }

    /// <summary>
    ///     Applique les contraintes de déplacement à une position <b>relative au parent</b> : <c>extent: parent</c>
    ///     puis l'extent du node ou <paramref name="nodeExtent" /> (évalué en coordonnées absolues).
    ///     <paramref name="parentAbsolute" /> est le parent avec sa position absolue (coin haut-gauche) et sa taille.
    /// </summary>
    public static XYPosition Constrain(Node node, XYPosition relative, Node? parentAbsolute, Rect? nodeExtent, XYPosition nodeOrigin)
    {
        // Les contraintes portent sur le coin haut-gauche ; l'origine du node décale la position qu'on stocke.
        var shift = OriginOffset(node, nodeOrigin);
        relative -= shift;
        var parentAbs = XYPosition.Zero;
        if (parentAbsolute != null)
        {
            relative = NodeTree.ClampToParent(node, relative, parentAbsolute);
            parentAbs = parentAbsolute.Position;
        }

        // L'extent propre au node l'emporte sur l'extent global ; relatif au parent s'il en a un.
        var extent = node.Extent is { } own ? own with { X = own.X + parentAbs.X, Y = own.Y + parentAbs.Y } : nodeExtent;
        if (extent is { } limit)
        {
            var abs = GraphUtils.ClampToExtent(relative + parentAbs, node.Width ?? 0, node.Height ?? 0, limit);
            relative = abs - parentAbs;
        }

        return relative + shift;
    }
}

/// <summary>Quels nodes suivent le pointeur quand on en saisit un.</summary>
public static class DragSelection
{
    /// <summary>
    ///     Nodes déplaçables sélectionnés (position de départ) ; un enfant dont un ancêtre est aussi sélectionné suit déjà son
    ///     parent. Sans sélection possible (ou sans sélection au glisser sur un node non sélectionné), seul le node saisi bouge.
    ///     Un node saisi qui n'apparaît pas sélectionné dans <paramref name="nodes" /> (mode contrôlé : la sélection demandée
    ///     n'est pas encore appliquée) est déplacé seul.
    /// </summary>
    public static Dictionary<string, XYPosition> CollectStart(IReadOnlyList<Node> nodes, bool nodesDraggable, bool elementsSelectable,
        bool selectNodesOnDrag, string? pressedId = null) =>
        CollectStart(nodes, nodesDraggable, elementsSelectable, selectNodesOnDrag, pressedId, false);

    /// <summary>
    ///     Comme l'autre surcharge ; avec <paramref name="additive" /> (Ctrl/Maj enfoncé), un node saisi dont la sélection n'est
    ///     pas encore appliquée est déplacé avec la sélection actuelle.
    /// </summary>
    // Surcharge et non paramètre facultatif : la signature publiée en 1.0 doit rester (compatibilité binaire).
    public static Dictionary<string, XYPosition> CollectStart(IReadOnlyList<Node> nodes, bool nodesDraggable, bool elementsSelectable,
        bool selectNodesOnDrag, string? pressedId, bool additive)
    {
        if (!nodesDraggable) return new Dictionary<string, XYPosition>();
        var byId = nodes.ToDictionary(n => n.Id);
        var pending = pressedId != null && byId.GetValueOrDefault(pressedId) is { Selected: false };
        if (pressedId != null && (!elementsSelectable || (pending && (!selectNodesOnDrag || !additive))))
            return byId.GetValueOrDefault(pressedId) is { Draggable: true } one
                ? new Dictionary<string, XYPosition> { [one.Id] = one.Position }
                : new Dictionary<string, XYPosition>();
        var selected = nodes.Where(n => n.Selected).Select(n => n.Id).ToHashSet();
        if (pending) selected.Add(pressedId!);
        var result = new Dictionary<string, XYPosition>();
        foreach (var n in nodes)
        {
            if (!selected.Contains(n.Id) || !n.Draggable) continue;
            var covered = false;
            for (var p = n.ParentId; p != null && byId.TryGetValue(p, out var parent); p = parent.ParentId)
                if (selected.Contains(p)) { covered = true; break; }
            if (!covered) result[n.Id] = n.Position;
        }

        return result;
    }
}

/// <summary>Empilement des nodes (<c>zIndexMode</c>).</summary>
public static class NodeZOrder
{
    /// <summary>Premier plan donné à un node sélectionné ou élevé.</summary>
    public const int Elevation = 1000;

    public static int Compute(Node node, int depth, FlowZIndexMode mode, bool elevateOnSelect, bool elevated) =>
        node.ZIndex ?? mode switch
        {
            FlowZIndexMode.Manual => 0, // seul Node.ZIndex compte
            _ => depth + ((node.Selected && elevateOnSelect && node.Type != "group") || elevated ? Elevation : 0)
        };

    /// <summary>
    ///     En mode <see cref="FlowZIndexMode.Auto" />, les ancêtres d'un node sélectionné et leurs descendants (sinon le parent
    ///     passerait devant ses autres enfants) ; <c>null</c> sinon.
    /// </summary>
    public static HashSet<string>? ElevatedSet(IReadOnlyList<Node> nodes, FlowZIndexMode mode)
    {
        if (mode != FlowZIndexMode.Auto) return null;
        var hasTree = false;
        var hasSelectedChild = false;
        for (var i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n.ParentId == null) continue;
            hasTree = true;
            if (n.Selected)
            {
                hasSelectedChild = true;
                break;
            }
        }

        if (!hasTree) return null;
        if (!hasSelectedChild) return new HashSet<string>();

        var byId = new Dictionary<string, Node>(nodes.Count);
        for (var i = 0; i < nodes.Count; i++) byId[nodes[i].Id] = nodes[i];

        var elevated = new HashSet<string>();
        for (var i = 0; i < nodes.Count; i++)
        {
            var selected = nodes[i];
            if (!selected.Selected) continue;
            for (var p = selected.ParentId; p != null && byId.TryGetValue(p, out var parent) && elevated.Add(p); p = parent.ParentId) { }
        }

        var followers = new List<string>();
        for (var i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            for (var p = n.ParentId; p != null && byId.TryGetValue(p, out var parent); p = parent.ParentId)
                if (elevated.Contains(p))
                {
                    followers.Add(n.Id);
                    break;
                }
        }

        elevated.UnionWith(followers);
        return elevated;
    }
}

/// <summary>Rotation d'un node avec la poignée de rotation.</summary>
public static class NodeRotation
{
    /// <summary>Angle (degrés, 0 = poignée en haut) du point flow <paramref name="p" /> vu du centre du node.</summary>
    public static double AngleTo(XYPosition center, XYPosition p) => Math.Atan2(p.Y - center.Y, p.X - center.X) * 180 / Math.PI + 90;

    /// <summary>Angle final : arrondi à <paramref name="snapAngle" /> avec Maj, ramené dans [0, 360[.</summary>
    public static double Resolve(double angle, bool snap, double snapAngle)
    {
        if (snap && snapAngle > 0) angle = Math.Round(angle / snapAngle) * snapAngle;
        return ((angle % 360) + 360) % 360;
    }
}

/// <summary>Déplacement des nodes sélectionnés au clavier (flèches).</summary>
public static class KeyboardMove
{
    /// <summary>5 px (20 avec Maj), comme React Flow.</summary>
    public static double Step(bool shift) => shift ? 20.0 : 5.0;

    public enum Arrow
    {
        Left,
        Right,
        Up,
        Down
    }

    public static (double X, double Y) Delta(Arrow arrow, bool shift)
    {
        var step = Step(shift);
        return arrow switch
        {
            Arrow.Left => (-step, 0.0),
            Arrow.Right => (step, 0.0),
            Arrow.Up => (0.0, -step),
            _ => (0.0, step)
        };
    }
}

/// <summary>Empilement des edges : <see cref="Edge.ZIndex" />, sinon au premier plan s'ils sont sélectionnés (<c>elevateEdgesOnSelect</c>).</summary>
public static class EdgeZOrder
{
    public static int Compute(Edge edge, bool elevateOnSelect) => edge.ZIndex ?? (edge.Selected && elevateOnSelect ? NodeZOrder.Elevation : 0);
}
