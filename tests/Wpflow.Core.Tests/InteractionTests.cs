using Wpflow.Core;

namespace Wpflow.Core.Tests;

/// <summary>Logique d'interaction partagée par les vues WPF et Blazor.</summary>
public sealed class InteractionTests
{
    private static Node N(string id, double x, double y, double w = 100, double h = 40, string? parent = null) =>
        new(id, new XYPosition(x, y), Width: w, Height: h, ParentId: parent);

    // ---------- Défilement automatique ----------

    [Fact]
    public void AutoPan_scrolls_toward_the_content_near_an_edge()
    {
        Assert.Equal((0.0, 0.0), AutoPan.Delta(400, 300, 800, 600, 15));
        Assert.Equal((15.0, 0.0), AutoPan.Delta(0, 300, 800, 600, 15)); // bord gauche : le contenu va à droite
        Assert.Equal((-15.0, 0.0), AutoPan.Delta(800, 300, 800, 600, 15));
        Assert.Equal((0.0, 7.5), AutoPan.Delta(400, 20, 800, 600, 15)); // à mi-chemin de la zone de 40 px
        Assert.Equal((15.0, 15.0), AutoPan.Delta(-50, -50, 800, 600, 15)); // hors de la zone : borné
    }

    // ---------- Molette ----------

    [Fact]
    public void Wheel_zooms_by_default_and_pans_with_PanOnScroll()
    {
        var zoom = WheelInput.Resolve(0, -100, false, false, true, false, 0.5, PanOnScrollMode.Free, true);
        Assert.Equal(WheelKind.Zoom, zoom.Kind);
        Assert.True(zoom.ZoomFactor > 1); // molette vers le haut : zoom avant
        Assert.True(zoom.Handled);

        var pan = WheelInput.Resolve(0, 100, false, false, true, true, 0.5, PanOnScrollMode.Free, true);
        Assert.Equal((WheelKind.Pan, 0.0, -50.0), (pan.Kind, pan.PanX, pan.PanY));
    }

    [Fact]
    public void Wheel_with_the_zoom_key_zooms_even_when_PanOnScroll_is_on()
    {
        var r = WheelInput.Resolve(0, 100, true, false, false, true, 0.5, PanOnScrollMode.Free, true);
        Assert.Equal(WheelKind.Zoom, r.Kind);
    }

    [Fact]
    public void Wheel_pan_modes_and_shift()
    {
        var shift = WheelInput.Resolve(0, 100, false, true, false, true, 1, PanOnScrollMode.Free, true);
        Assert.Equal((-100.0, 0.0), (shift.PanX, shift.PanY)); // Maj : horizontal
        var vertical = WheelInput.Resolve(40, 100, false, false, false, true, 1, PanOnScrollMode.Vertical, true);
        Assert.Equal((0.0, -100.0), (vertical.PanX, vertical.PanY));
        var horizontal = WheelInput.Resolve(0, 100, false, false, false, true, 1, PanOnScrollMode.Horizontal, true);
        Assert.Equal((-100.0, 0.0), (horizontal.PanX, horizontal.PanY));
    }

    [Fact]
    public void Wheel_does_nothing_when_neither_zoom_nor_pan_applies()
    {
        var r = WheelInput.Resolve(0, 100, false, false, false, false, 1, PanOnScrollMode.Free, true);
        Assert.Equal((WheelKind.None, false), (r.Kind, r.Handled));
    }

    [Fact]
    public void Pinch_zooms_ten_times_faster()
    {
        var normal = WheelInput.Resolve(0, -10, false, false, true, false, 1, PanOnScrollMode.Free, true);
        var pinch = WheelInput.Resolve(0, -10, false, false, true, false, 1, PanOnScrollMode.Free, true, pinch: true);
        Assert.True(pinch.ZoomFactor > normal.ZoomFactor);
    }

    // ---------- Cadrage, virtualisation, minimap ----------

    [Fact]
    public void FitView_frames_measured_visible_nodes_or_the_requested_subset()
    {
        var nodes = new[] { N("a", 0, 0), N("b", 1000, 0), N("hidden", 5000, 0) with { Hidden = true }, new Node("unmeasured", XYPosition.Zero) };
        var all = ViewFitting.Compute(nodes, 800, 600, 0.1, 2, 0.1)!.Value;
        var one = ViewFitting.Compute(nodes, 800, 600, 0.1, 2, 0.1, new FitViewOptions(NodeIds: ["a"]))!.Value;

        Assert.True(all.Zoom < 1);
        Assert.Equal(2, one.Zoom); // un seul petit node : zoom maximal
        Assert.Null(ViewFitting.Compute(nodes, 0, 600, 0.1, 2, 0.1));
        Assert.Null(ViewFitting.Compute([new Node("x", XYPosition.Zero)], 800, 600, 0.1, 2, 0.1));
        Assert.NotNull(ViewFitting.Compute(nodes, 800, 600, 0.1, 2, 0.1, new FitViewOptions(IncludeHiddenNodes: true)));
    }

    [Fact]
    public void Virtualization_zones_scale_with_the_zoom_and_unmeasured_nodes_get_an_assumed_size()
    {
        var (show, keep) = Virtualization.Zones(new Rect(0, 0, 100, 100), 2);
        Assert.Equal(new Rect(-150, -150, 400, 400), show);
        Assert.Equal(new Rect(-450, -450, 1000, 1000), keep);

        Assert.Equal(new Rect(5, 6, 200, 100), Virtualization.AssumedRect(new Node("n", new XYPosition(5, 6))));
        Assert.Equal(new Rect(5, 6, 30, 20), Virtualization.AssumedRect(new Node("n", new XYPosition(5, 6), InitialWidth: 30, InitialHeight: 20)));
        Assert.Equal(30, Virtualization.WithInitialSize(new Node("n", XYPosition.Zero, InitialWidth: 30, InitialHeight: 20)).Width);
    }

    [Fact]
    public void MiniMap_layout_keeps_the_aspect_ratio_and_a_margin()
    {
        var l = MiniMapMath.Compute(new Rect(0, 0, 400, 100), new Rect(0, 0, 200, 100), 200, 150);
        Assert.True(l.Height > 150 && l.Width > 400 && l.X < 0); // le contenu 400 x 100 est cadré dans le ratio 4:3, avec une marge
        Assert.Equal(200 / l.Width, l.Scale, 6);
        var onlyView = MiniMapMath.Compute(null, new Rect(0, 0, 200, 150), 200, 150);
        Assert.Equal((-5.0, -5.0, 210.0), (onlyView.X, onlyView.Y, onlyView.Width));
    }

    // ---------- Contraintes de déplacement ----------

    [Fact]
    public void OriginOffset_and_absolute_positions_account_for_the_origin_and_the_parent()
    {
        var parent = N("p", 100, 100, 200, 100);
        var child = N("c", 10, 20, 40, 20, "p");
        var centered = new XYPosition(0.5, 0.5);

        Assert.Equal(new XYPosition(20, 10), NodeConstraints.OriginOffset(child, centered));
        Assert.Equal(XYPosition.Zero, NodeConstraints.OriginOffset(child, XYPosition.Zero));
        Assert.Equal(new XYPosition(20, 5), NodeConstraints.OriginOffset(child with { Origin = new XYPosition(0.5, 0.25) }, XYPosition.Zero));

        var abs = NodeConstraints.Absolute([parent, child], centered);
        Assert.Equal(new XYPosition(0, 50), abs["p"]);
        Assert.Equal(new XYPosition(0 + 10 - 20, 50 + 20 - 10), abs["c"]);
        Assert.Equal(new XYPosition(110, 120), NodeConstraints.Absolute([parent, child], XYPosition.Zero)["c"]);
    }

    [Fact]
    public void Constrain_applies_the_parent_extent_then_the_node_extent()
    {
        var parent = N("p", 100, 100, 200, 100); // position absolue
        var child = N("c", 10, 10, 40, 20, "p") with { ExtentParent = true };

        // extent parent : 200 - 40 = 160 de large au plus, 100 - 20 = 80 de haut
        Assert.Equal(new XYPosition(160, 80), NodeConstraints.Constrain(child, new XYPosition(999, 999), parent, null, XYPosition.Zero));
        Assert.Equal(new XYPosition(0, 0), NodeConstraints.Constrain(child, new XYPosition(-5, -5), parent, null, XYPosition.Zero));

        var free = N("f", 0, 0, 100, 40);
        var limit = new Rect(0, 0, 500, 300);
        Assert.Equal(new XYPosition(400, 260), NodeConstraints.Constrain(free, new XYPosition(900, 900), null, limit, XYPosition.Zero));
        // l'extent propre au node l'emporte sur l'extent global
        var own = free with { Extent = new Rect(0, 0, 200, 100) };
        Assert.Equal(new XYPosition(100, 60), NodeConstraints.Constrain(own, new XYPosition(900, 900), null, limit, XYPosition.Zero));
    }

    [Fact]
    public void Constrain_works_on_the_top_left_corner_when_the_origin_is_centered()
    {
        var n = N("n", 0, 0, 100, 40);
        var limit = new Rect(0, 0, 500, 300);
        // stocké = centre : le coin haut-gauche ne peut pas sortir de [0, 400] x [0, 260]
        var p = NodeConstraints.Constrain(n, new XYPosition(900, 900), null, limit, new XYPosition(0.5, 0.5));
        Assert.Equal(new XYPosition(450, 280), p);
    }

    [Fact]
    public void DragSelection_collects_selected_draggable_nodes_without_their_covered_children()
    {
        var parent = N("p", 0, 0) with { Selected = true };
        var child = N("c", 5, 5, parent: "p") with { Selected = true };
        var other = N("o", 50, 50) with { Selected = true };
        var locked = N("l", 80, 80) with { Selected = true, Draggable = false };
        var idle = N("i", 90, 90);
        IReadOnlyList<Node> nodes = [parent, child, other, locked, idle];

        var start = DragSelection.CollectStart(nodes, true, true, true);
        Assert.Equal(["p", "o"], start.Keys.OrderByDescending(k => k));
        Assert.Empty(DragSelection.CollectStart(nodes, false, true, true));
    }

    [Fact]
    public void DragSelection_moves_only_the_pressed_node_when_it_is_not_selected_and_selection_on_drag_is_off()
    {
        IReadOnlyList<Node> nodes = [N("a", 0, 0) with { Selected = true }, N("b", 9, 9)];

        Assert.Equal(["b"], DragSelection.CollectStart(nodes, true, true, false, "b").Keys);
        // Mode contrôlé : la sélection de « b » est demandée mais pas encore dans la liste. L'ancienne sélection ne doit pas partir
        // à sa place (défaut vu dans l'éditeur de schéma témoin) ; avec Ctrl/Maj, elle accompagne le node saisi.
        Assert.Equal(["b"], DragSelection.CollectStart(nodes, true, true, true, "b").Keys);
        Assert.Equal(["a", "b"], DragSelection.CollectStart(nodes, true, true, true, "b", additive: true).Keys.OrderBy(x => x));
        // Une fois la sélection appliquée, toute la sélection suit.
        Assert.Equal(["a", "b"], DragSelection.CollectStart([nodes[0], nodes[1] with { Selected = true }], true, true, true, "b").Keys.OrderBy(x => x));
        Assert.Equal(["b"], DragSelection.CollectStart(nodes, true, false, true, "b").Keys); // pas de sélection possible
        Assert.Empty(DragSelection.CollectStart([N("b", 0, 0) with { Draggable = false }], true, false, true, "b"));
    }

    [Fact]
    public void NodeZOrder_follows_depth_selection_and_mode()
    {
        var n = N("n", 0, 0);
        Assert.Equal(2, NodeZOrder.Compute(n, 2, FlowZIndexMode.Basic, true, false));
        Assert.Equal(1002, NodeZOrder.Compute(n with { Selected = true }, 2, FlowZIndexMode.Basic, true, false));
        Assert.Equal(2, NodeZOrder.Compute(n with { Selected = true }, 2, FlowZIndexMode.Basic, false, false));
        Assert.Equal(1002, NodeZOrder.Compute(n, 2, FlowZIndexMode.Auto, true, true)); // élevé par un descendant
        Assert.Equal(2, NodeZOrder.Compute(n with { Type = "group", Selected = true }, 2, FlowZIndexMode.Basic, true, false));
        Assert.Equal(0, NodeZOrder.Compute(n with { Selected = true }, 2, FlowZIndexMode.Manual, true, false));
        Assert.Equal(7, NodeZOrder.Compute(n with { ZIndex = 7 }, 2, FlowZIndexMode.Manual, true, false));
    }

    [Fact]
    public void NodeZOrder_elevates_ancestors_of_a_selected_node_and_their_descendants_in_auto_mode()
    {
        IReadOnlyList<Node> nodes =
        [
            N("root", 0, 0), N("mid", 0, 0, parent: "root"), N("leaf", 0, 0, parent: "mid") with { Selected = true },
            N("sibling", 0, 0, parent: "root"), N("other", 0, 0)
        ];

        var set = NodeZOrder.ElevatedSet(nodes, FlowZIndexMode.Auto)!;
        Assert.Equal(["leaf", "mid", "root", "sibling"], set.OrderBy(x => x));
        Assert.Null(NodeZOrder.ElevatedSet(nodes, FlowZIndexMode.Basic));
        Assert.Null(NodeZOrder.ElevatedSet([N("a", 0, 0)], FlowZIndexMode.Auto));
    }

    [Fact]
    public void Rotation_angle_is_zero_above_the_centre_and_snaps_and_wraps()
    {
        var c = new XYPosition(50, 50);
        Assert.Equal(0, NodeRotation.AngleTo(c, new XYPosition(50, 0)), 6);
        Assert.Equal(90, NodeRotation.AngleTo(c, new XYPosition(100, 50)), 6);
        Assert.Equal(180, NodeRotation.AngleTo(c, new XYPosition(50, 100)), 6);
        Assert.Equal(270, NodeRotation.AngleTo(c, new XYPosition(0, 50)), 6);

        Assert.Equal(270, NodeRotation.Resolve(-90, false, 15), 6);
        Assert.Equal(45, NodeRotation.Resolve(47, true, 15), 6);
        Assert.Equal(47, NodeRotation.Resolve(47, false, 15), 6);
        Assert.Equal(0, NodeRotation.Resolve(359.9, true, 15) % 360, 6);
    }

    [Fact]
    public void EdgeZOrder_uses_the_explicit_index_or_elevates_selected_edges()
    {
        var e = new Edge("e", "a", "b");
        Assert.Equal(0, EdgeZOrder.Compute(e, true));
        Assert.Equal(1000, EdgeZOrder.Compute(e with { Selected = true }, true));
        Assert.Equal(0, EdgeZOrder.Compute(e with { Selected = true }, false));
        Assert.Equal(3, EdgeZOrder.Compute(e with { Selected = true, ZIndex = 3 }, true));
    }

    [Fact]
    public void Marker_scale_stroke_and_side_angles()
    {
        Assert.Equal((1.0, 1.0), EdgeMarkerMath.Scale(null));
        Assert.Equal((2.0, 0.5), EdgeMarkerMath.Scale(new EdgeMarkerOptions(Width: 25, Height: 6.25)));
        Assert.Equal(3, EdgeMarkerMath.StrokeWidth(new EdgeMarkerOptions(StrokeWidth: 3), 1));
        Assert.Equal(1.5, EdgeMarkerMath.StrokeWidth(null, 1.5));
        Assert.Equal([0.0, 90.0, 180.0, 270.0],
            new[] { Position.Left, Position.Top, Position.Right, Position.Bottom }.Select(EdgeMarkerMath.SideAngle));
        Assert.Equal(3, EdgeMarkerMath.ArrowPoints.Count);
    }

    [Fact]
    public void Paste_offset_grows_by_twenty_pixels_per_paste_and_zoom_step_is_symmetric()
    {
        Assert.Equal(new XYPosition(20, 20), GraphClipboard.PasteOffset(1));
        Assert.Equal(new XYPosition(60, 60), GraphClipboard.PasteOffset(3));
        Assert.Equal(1, ViewportMath.ZoomStep * (1 / ViewportMath.ZoomStep), 9);
    }

    [Fact]
    public void Keyboard_steps_are_five_pixels_or_twenty_with_shift()
    {
        Assert.Equal((-5.0, 0.0), KeyboardMove.Delta(KeyboardMove.Arrow.Left, false));
        Assert.Equal((0.0, 20.0), KeyboardMove.Delta(KeyboardMove.Arrow.Down, true));
        Assert.Equal((0.0, -5.0), KeyboardMove.Delta(KeyboardMove.Arrow.Up, false));
        Assert.Equal((20.0, 0.0), KeyboardMove.Delta(KeyboardMove.Arrow.Right, true));
    }

    // ---------- Connexions ----------

    private static HandleInfo H(string? id, HandleType type, Position pos, double x, double y) => new(id, type, pos, x, y, 8, 8);

    private static Dictionary<string, IReadOnlyList<HandleInfo>> Handles() => new()
    {
        ["a"] = [H(null, HandleType.Target, Position.Top, 46, -4), H(null, HandleType.Source, Position.Bottom, 46, 36)],
        ["b"] = [H(null, HandleType.Target, Position.Top, 46, -4), H("x", HandleType.Source, Position.Bottom, 46, 36)]
    };

    [Fact]
    public void FindCandidate_picks_the_closest_compatible_handle_within_the_radius()
    {
        var nodes = new Dictionary<string, Node> { ["a"] = N("a", 0, 0), ["b"] = N("b", 300, 100) };
        var src = new HandleRef("a", Handles()["a"][1]); // source de a

        var hit = ConnectionSearch.FindCandidate(src, new XYPosition(352, 98), 20, ConnectionMode.Strict, Handles(), id => nodes.GetValueOrDefault(id));
        Assert.Equal(("b", HandleType.Target), (hit!.NodeId, hit.Info.Type));

        Assert.Null(ConnectionSearch.FindCandidate(src, new XYPosition(600, 500), 20, ConnectionMode.Strict, Handles(), id => nodes.GetValueOrDefault(id)));
        // le handle source de b est compatible en mode lâche
        var loose = ConnectionSearch.FindCandidate(src, new XYPosition(350, 140), 20, ConnectionMode.Loose, Handles(), id => nodes.GetValueOrDefault(id));
        Assert.Equal("x", loose!.Info.Id);
        Assert.Null(ConnectionSearch.FindCandidate(src, new XYPosition(350, 140), 20, ConnectionMode.Strict, Handles(), id => nodes.GetValueOrDefault(id)));
    }

    [Fact]
    public void FindCandidate_skips_the_origin_node_hidden_and_non_connectable_nodes()
    {
        var nodes = new Dictionary<string, Node> { ["a"] = N("a", 0, 0), ["b"] = N("b", 300, 100) with { Connectable = false } };
        var src = new HandleRef("a", Handles()["a"][1]);

        Assert.Null(ConnectionSearch.FindCandidate(src, new XYPosition(352, 98), 20, ConnectionMode.Strict, Handles(), id => nodes.GetValueOrDefault(id)));
        nodes["b"] = N("b", 300, 100) with { Hidden = true };
        Assert.Null(ConnectionSearch.FindCandidate(src, new XYPosition(352, 98), 20, ConnectionMode.Strict, Handles(), id => nodes.GetValueOrDefault(id)));
        // le node d'origine n'est jamais sa propre cible
        Assert.Null(ConnectionSearch.FindCandidate(new HandleRef("a", Handles()["a"][0]), new XYPosition(50, 44), 20, ConnectionMode.Loose,
            new Dictionary<string, IReadOnlyList<HandleInfo>> { ["a"] = Handles()["a"] }, id => nodes.GetValueOrDefault(id) ?? N("a", 0, 0)));
    }

    [Fact]
    public void Validate_rejects_duplicates_and_honours_the_three_validators()
    {
        var src = new HandleRef("a", Handles()["a"][1]);
        var target = new HandleRef("b", Handles()["b"][0]);
        var edges = new[] { new Edge("e", "a", "b") };

        Assert.False(ConnectionSearch.Validate(src, target, edges, null, null, out _)); // doublon
        Assert.True(ConnectionSearch.Validate(src, target, edges, "e", null, out var c)); // l'edge déplacé n'est pas un doublon
        Assert.Equal(new Connection("a", "b", null, null), c);
        Assert.True(ConnectionSearch.Validate(src, target, [], null, null, out _));
        Assert.False(ConnectionSearch.Validate(src, target, [], null, _ => false, out _));
        Assert.False(ConnectionSearch.Validate(src with { Info = src.Info with { IsValidConnection = _ => false } }, target, [], null, null, out _));
        Assert.False(ConnectionSearch.Validate(src, target with { Info = target.Info with { IsValidConnection = _ => false } }, [], null, null, out _));
    }

    [Fact]
    public void Validate_orients_a_connection_started_from_a_target_handle()
    {
        var src = new HandleRef("b", Handles()["b"][0]); // cible de b
        var to = new HandleRef("a", Handles()["a"][1]); // source de a
        Assert.True(ConnectionSearch.Validate(src, to, [], null, null, out var c));
        Assert.Equal(("a", "b"), (c.Source, c.Target));
    }

    // ---------- Extrémités d'edges ----------

    [Fact]
    public void EdgeEndpoints_attach_to_the_handle_border_or_the_fallback_side()
    {
        var nodes = new Dictionary<string, Node> { ["a"] = N("a", 0, 0), ["b"] = N("b", 300, 100) };
        var handles = Handles();
        var ends = EdgeEndpoints.Resolve(new Edge("e", "a", "b"), id => nodes.GetValueOrDefault(id), id => handles.GetValueOrDefault(id),
            Position.Right, Position.Left)!.Value;
        Assert.Equal((new XYPosition(50, 44), Position.Bottom), (ends.Source, ends.SourcePosition));
        Assert.Equal((new XYPosition(350, 96), Position.Top), (ends.Target, ends.TargetPosition));

        // sans handle mesuré : milieu du côté de repli
        var bare = EdgeEndpoints.Resolve(new Edge("e", "a", "b"), id => nodes.GetValueOrDefault(id), _ => null, Position.Right, Position.Left)!.Value;
        Assert.Equal(new XYPosition(100, 20), bare.Source);
        Assert.Equal(new XYPosition(300, 120), bare.Target);
    }

    [Fact]
    public void EdgeEndpoints_select_the_handle_by_id_and_use_declared_handles_and_initial_sizes()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["a"] = N("a", 0, 0),
            ["b"] = new Node("b", new XYPosition(300, 100), InitialWidth: 100, InitialHeight: 40,
                Handles: [H("y", HandleType.Target, Position.Left, -4, 16)])
        };
        var handles = Handles();

        var ends = EdgeEndpoints.Resolve(new Edge("e", "a", "b", null, "y"), id => nodes.GetValueOrDefault(id), id => handles.GetValueOrDefault(id),
            Position.Right, Position.Left);
        // la liste mesurée de b ne contient pas "y" : l'extrémité se replie sur le milieu du côté
        Assert.Equal((new XYPosition(300, 120), Position.Left), (ends!.Value.Target, ends.Value.TargetPosition));

        var declared = EdgeEndpoints.Resolve(new Edge("e", "a", "b", null, "y"), id => nodes.GetValueOrDefault(id),
            id => id == "a" ? handles["a"] : null, Position.Right, Position.Left)!.Value;
        Assert.Equal((new XYPosition(296, 120), Position.Left), (declared.Target, declared.TargetPosition));
    }

    [Fact]
    public void EdgeEndpoints_ignore_hidden_edges_and_nodes_and_name_the_missing_node()
    {
        var nodes = new Dictionary<string, Node> { ["a"] = N("a", 0, 0), ["b"] = N("b", 300, 100) with { Hidden = true } };
        Func<string, Node?> get = id => nodes.GetValueOrDefault(id);

        Assert.Null(EdgeEndpoints.Resolve(new Edge("e", "a", "b"), get, _ => null, Position.Right, Position.Left));
        Assert.Null(EdgeEndpoints.Resolve(new Edge("e", "a", "a") with { Hidden = true }, get, _ => null, Position.Right, Position.Left));
        Assert.Equal("ghost", EdgeEndpoints.MissingNode(new Edge("e", "a", "ghost"), get));
        Assert.Equal("ghost", EdgeEndpoints.MissingNode(new Edge("e", "ghost", "a"), get));
        Assert.Null(EdgeEndpoints.MissingNode(new Edge("e", "a", "b"), get));
    }

    // ---------- Historique ----------

    [Fact]
    public void History_undoes_redoes_and_forgets_redo_after_a_new_push()
    {
        var history = new GraphHistory();
        IReadOnlyList<Node> s0 = [N("a", 0, 0)];
        IReadOnlyList<Node> s1 = [N("a", 10, 0)];
        IReadOnlyList<Node> s2 = [N("a", 20, 0)];
        IReadOnlyList<Edge> none = [];

        Assert.False(history.CanUndo);
        history.Push(s0, none);
        history.Push(s1, none);

        Assert.True(history.TryUndo(s2, none, out var back));
        Assert.Same(s1, back.Nodes);
        Assert.True(history.CanRedo);
        Assert.True(history.TryRedo(s1, none, out var forward));
        Assert.Same(s2, forward.Nodes);

        Assert.True(history.TryUndo(s2, none, out _));
        history.Push(s2, none);
        Assert.False(history.CanRedo);
        history.Clear();
        Assert.False(history.CanUndo);
        Assert.False(history.TryUndo(s0, none, out _));
        Assert.False(history.TryRedo(s0, none, out _));
    }

    [Fact]
    public void History_drops_the_oldest_states_beyond_its_limit()
    {
        var history = new GraphHistory(2);
        IReadOnlyList<Edge> none = [];
        for (var i = 0; i < 5; i++) history.Push([N("n" + i, 0, 0)], none);

        Assert.True(history.TryUndo([], none, out var s));
        Assert.Equal("n4", s.Nodes[0].Id);
        Assert.True(history.TryUndo([], none, out s));
        Assert.Equal("n3", s.Nodes[0].Id);
        Assert.False(history.TryUndo([], none, out _));
    }

    // ---------- Suppression ----------

    [Fact]
    public void Deletion_plan_takes_descendants_connected_edges_and_skips_undeletable_elements()
    {
        IReadOnlyList<Node> nodes = [N("p", 0, 0), N("c", 0, 0, parent: "p"), N("x", 0, 0), N("keep", 0, 0) with { Deletable = false }];
        IReadOnlyList<Edge> edges = [new Edge("e1", "c", "x"), new Edge("e2", "x", "keep"), new Edge("e3", "x", "keep") with { Deletable = false }];

        var plan = GraphDeletion.Plan(nodes, edges, ["p", "keep"], ["e3"])!;

        Assert.Equal(["c", "p"], plan.Nodes.OrderBy(i => i));
        Assert.Empty(plan.Edges); // e3 n'est pas supprimable
        Assert.Equal(["e1"], plan.EdgesOfNodes);
        Assert.Null(GraphDeletion.Plan(nodes, edges, ["keep"], null));
        Assert.Null(GraphDeletion.Plan(nodes, edges, null, null));
    }

    // ---------- Sélection ----------

    [Fact]
    public void Select_changes_make_the_selection_equal_to_the_ids()
    {
        IReadOnlyList<Node> nodes = [N("a", 0, 0) with { Selected = true }, N("b", 0, 0), N("c", 0, 0) with { Selectable = false }];
        IReadOnlyList<Edge> edges = [new Edge("e", "a", "b") with { Selected = true }, new Edge("h", "a", "b") with { Hidden = true }];

        var (nc, ec) = SelectionOps.Select(nodes, edges, ["b", "c"], [], false);
        Assert.Equal([new NodeSelectChange("a", false), new NodeSelectChange("b", true)], nc);
        Assert.Equal([new EdgeSelectChange("e", false)], ec);

        var (add, _) = SelectionOps.Select(nodes, edges, ["b"], [], true);
        Assert.Equal([new NodeSelectChange("b", true)], add); // a reste sélectionné

        var (off, eoff) = SelectionOps.Select(nodes, edges, ["b"], ["e"], false, elementsSelectable: false);
        Assert.Equal([new NodeSelectChange("a", false)], off);
        Assert.Equal([new EdgeSelectChange("e", false)], eoff);
    }

    [Fact]
    public void Box_selection_picks_partially_or_fully_covered_nodes_and_their_inner_edges()
    {
        IReadOnlyList<Node> nodes = [N("a", 0, 0), N("b", 150, 0), N("c", 500, 500), N("h", 10, 10) with { Hidden = true }];
        IReadOnlyList<Edge> edges = [new Edge("ab", "a", "b"), new Edge("bc", "b", "c")];
        var box = new Rect(-10, -10, 200, 60); // recouvre a entièrement, b partiellement

        var partial = SelectionOps.Box(box, nodes, edges, FlowSelectionMode.Partial);
        Assert.Equal(["a", "b"], partial.Nodes.OrderBy(i => i));
        Assert.Equal(["ab"], partial.Edges);

        var full = SelectionOps.Box(new Rect(-10, -10, 150, 60), nodes, edges, FlowSelectionMode.Full);
        Assert.Equal(["a"], full.Nodes);
        Assert.Empty(full.Edges);
    }

    [Fact]
    public void Area_orders_the_corners_and_announcements_use_labels()
    {
        Assert.Equal(new Rect(2, 3, 8, 7), SelectionOps.Area(new XYPosition(10, 3), new XYPosition(2, 10)));

        Assert.Equal("Node X sélectionné", SelectionOps.AnnounceNode(new Node("n", XYPosition.Zero, Data: "X"), null));
        Assert.Equal("Node n sélectionné", SelectionOps.AnnounceNode(new Node("n", XYPosition.Zero), null));
        Assert.Equal("Mon node sélectionné", SelectionOps.AnnounceNode(new Node("n", XYPosition.Zero), _ => "Mon node"));
        Assert.Equal("Direct sélectionné", SelectionOps.AnnounceNode(new Node("n", XYPosition.Zero, AriaLabel: "Direct"), _ => "Mon node"));
        Assert.Equal("Edge e sélectionné", SelectionOps.AnnounceEdge(new Edge("e", "a", "b"), null));
        Assert.Equal("Lien sélectionné", SelectionOps.AnnounceEdge(new Edge("e", "a", "b"), _ => "Lien"));
    }

    [Fact]
    public void ResizeInfo_and_FitViewOptions_are_plain_records()
    {
        var info = new ResizeInfo("n", ResizeDirection.Left, new Rect(0, 0, 1, 1), new Rect(0, 0, 2, 2));
        Assert.Equal(ResizeDirection.Left, info.Direction);
        Assert.Equal(new FitViewOptions(0.2), new FitViewOptions(0.2));
    }
}
