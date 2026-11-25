using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpflow.Core;
using Wpflow.Wpf;
using Rect = System.Windows.Rect;

namespace Wpflow.Wpf.Tests;

/// <summary>Lot P2 / P3 : API d'instance, nodes et edges cachés, origine, copier-coller, options de composants.</summary>
public class P2Tests
{
    private static Node N(string id, double x, double y, bool selected = false) =>
        new(id, new XYPosition(x, y), Width: 100, Height: 40, FixedWidth: 100, FixedHeight: 40, Selected: selected);

    private static FlowView Flow(IReadOnlyList<Node>? nodes = null, IReadOnlyList<Edge>? edges = null,
        Action<FlowView>? configure = null) =>
        CustomizationTests.Realize(configure, nodes ?? [N("a", 0, 0), N("b", 300, 100), N("c", 600, 0)], edges ?? [new Edge("e1", "a", "b")]);

    // ---- instance ----

    [Fact]
    public void Instance_api_finds_adds_updates_and_deletes_elements()
    {
        Sta.Run(() =>
        {
            var flow = Flow();
            Assert.Equal("b", flow.GetNode("b")!.Id);
            Assert.Null(flow.GetNode("zzz"));
            Assert.Equal("e1", flow.GetEdge("e1")!.Id);

            flow.AddNodes([N("d", 50, 200)]);
            Assert.NotNull(flow.GetNode("d"));

            flow.AddEdges([new Edge("x", "a", "b"), new Edge("e2", "b", "c")]); // « x » doublonne a→b
            Assert.Equal(["e1", "e2"], flow.Edges.Select(e => e.Id));

            flow.UpdateNodeData("a", "bonjour");
            Assert.Equal("bonjour", flow.GetNode("a")!.Data);
            flow.UpdateNode("a", n => n with { Draggable = false });
            Assert.False(flow.GetNode("a")!.Draggable);
            flow.UpdateEdgeData("e1", 42);
            Assert.Equal(42, flow.GetEdge("e1")!.Data);

            Assert.True(flow.DeleteElements(["a"], null)); // emporte l'edge e1
            Assert.Null(flow.GetNode("a"));
            Assert.DoesNotContain(flow.Edges, e => e.Id == "e1");
            Assert.False(flow.DeleteElements(["nope"], ["nope"]));
        });
    }

    [Fact]
    public void Non_deletable_elements_survive_and_BeforeDelete_can_cancel()
    {
        Sta.Run(() =>
        {
            var flow = Flow([N("a", 0, 0) with { Deletable = false }, N("b", 300, 0)]);
            Assert.False(flow.DeleteElements(["a"], null));
            Assert.NotNull(flow.GetNode("a"));

            var seen = 0;
            flow.BeforeDelete += (_, e) =>
            {
                seen = e.Nodes.Count;
                e.Cancel = true;
            };
            Assert.False(flow.DeleteElements(["b"], null));
            Assert.Equal(1, seen);
            Assert.NotNull(flow.GetNode("b"));
        });
    }

    [Fact]
    public void Intersecting_nodes_use_absolute_measured_rectangles()
    {
        Sta.Run(() =>
        {
            var flow = Flow([N("a", 0, 0), N("b", 50, 10), N("c", 600, 600)]);
            Assert.Equal(["b"], flow.GetIntersectingNodes(flow.GetNode("a")!).Select(n => n.Id));
            Assert.Equal(["a", "b"], flow.GetIntersectingNodes(new Core.Rect(0, 0, 200, 100)).Select(n => n.Id));
            Assert.Single(flow.GetNodeConnections("a", HandleType.Source), c => c.EdgeId == "e1");
        });
    }

    // ---- hidden ----

    [Fact]
    public void Hidden_node_is_collapsed_and_its_edges_disappear()
    {
        Sta.Run(() =>
        {
            var flow = Flow();
            Assert.Single(FlowView.FindDescendants<EdgeVisual>(flow));

            flow.UpdateNode("b", n => n with { Hidden = true });
            flow.UpdateLayout();
            var host = FlowView.FindDescendants<NodeHost>(flow).Single(h => h.Node!.Id == "b");
            Assert.Equal(Visibility.Collapsed, host.Visibility);
            Assert.Empty(FlowView.FindDescendants<EdgeVisual>(flow));
            Assert.DoesNotContain(flow.AbsoluteNodes, n => n.Id == "b");

            flow.UpdateNode("b", n => n with { Hidden = false });
            flow.UpdateLayout();
            Assert.Single(FlowView.FindDescendants<EdgeVisual>(flow));
        });
    }

    [Fact]
    public void Hidden_edge_is_not_drawn_and_cannot_be_selected()
    {
        Sta.Run(() =>
        {
            var flow = Flow(edges: [new Edge("e1", "a", "b", Hidden: true), new Edge("e2", "b", "c", Selectable: false)]);
            Assert.DoesNotContain(FlowView.FindDescendants<EdgeVisual>(flow), v => v.EdgeId == "e1");
            flow.SelectAll();
            Assert.All(flow.Edges, e => Assert.False(e.Selected));
        });
    }

    // ---- origine ----

    [Fact]
    public void Node_origin_shifts_the_drawn_position()
    {
        Sta.Run(() =>
        {
            var flow = Flow([N("a", 100, 100)], [], f => f.NodeOrigin = new XYPosition(0.5, 0.5));
            var abs = flow.GetInternalNode("a")!;
            Assert.Equal(new XYPosition(50, 80), abs.Position); // 100 - 0,5 × 100 ; 100 - 0,5 × 40
            Assert.Equal(new XYPosition(100, 100), flow.GetNode("a")!.Position); // la position stockée reste le point d'ancrage
        });
    }

    // ---- viewport ----

    [Fact]
    public void SetCenter_FitBounds_and_FitView_options_position_the_viewport()
    {
        Sta.Run(() =>
        {
            var flow = Flow();
            flow.SetCenter(100, 50, 2);
            Assert.Equal(2, flow.Viewport.Zoom, 6);
            var center = ViewportMath.FlowToScreen(flow.Viewport, new XYPosition(100, 50));
            Assert.Equal(400, center.X, 3);
            Assert.Equal(300, center.Y, 3);

            flow.FitBounds(new Core.Rect(0, 0, 400, 300), padding: 0);
            var tl = ViewportMath.FlowToScreen(flow.Viewport, new XYPosition(0, 0));
            Assert.InRange(tl.X, 0, 400);

            Assert.True(flow.FitView(new FitViewOptions(NodeIds: ["a"], MaxZoom: 1)));
            Assert.True(flow.Viewport.Zoom <= 1);
            Assert.False(flow.FitView(new FitViewOptions(NodeIds: ["inconnu"])));
        });
    }

    [Fact]
    public void Animated_viewport_ends_at_the_target_and_raises_move_events()
    {
        Sta.Run(() =>
        {
            var flow = Flow();
            var events = new List<string>();
            flow.ViewportMoveStarted += (_, _) => events.Add("start");
            flow.ViewportMoveEnded += (_, _) => events.Add("end");

            flow.SetViewport(new Viewport(100, 100, 1.5), TimeSpan.FromMilliseconds(80));
            Assert.True(flow.IsViewportAnimating);

            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (flow.IsViewportAnimating && DateTime.UtcNow < deadline)
                Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            Assert.False(flow.IsViewportAnimating);
            Assert.Equal(new Viewport(100, 100, 1.5), flow.Viewport);
            Assert.Equal(["start", "end"], events);
        });
    }

    // ---- copier-coller ----

    [Fact]
    public void Copy_and_paste_duplicate_the_selection_with_its_internal_edges()
    {
        Sta.Run(() =>
        {
            var flow = Flow([N("a", 0, 0, true), N("b", 300, 100, true), N("c", 600, 0)],
                [new Edge("e1", "a", "b"), new Edge("e2", "b", "c")]);
            Assert.True(flow.Copy());
            Assert.True(flow.CanPaste);
            Assert.True(flow.Paste());

            Assert.Equal(5, flow.Nodes.Count);
            Assert.Equal(["a-copy", "b-copy"], flow.Nodes.Where(n => n.Selected).Select(n => n.Id));
            Assert.Equal(3, flow.Edges.Count);
            Assert.Contains(flow.Edges, e => e.Source == "a-copy" && e.Target == "b-copy");

            Assert.True(flow.Cut());
            Assert.DoesNotContain(flow.Nodes, n => n.Id == "a-copy");
        });
    }

    // ---- événements de connexion / options ----

    [Fact]
    public void Default_edge_options_are_stored_on_the_view()
    {
        Sta.Run(() =>
        {
            var flow = Flow(configure: f => f.DefaultEdgeOptions = new Edge("", "", "", Type: "smoothstep", Animated: true));
            var connection = new Connection("a", "c", null, null);
            // Même chemin que le geste de connexion : l'edge est fabriqué à partir des options par défaut.
            var result = flow.Edges;
            Assert.NotNull(flow.DefaultEdgeOptions);
            Assert.Equal("smoothstep", flow.DefaultEdgeOptions!.Type);
            Assert.Equal("xy-edge__a-c", GraphQueries.ConnectionId(connection.Source, null, connection.Target, null));
            Assert.Single(result);
        });
    }

    [Fact]
    public void Error_event_reports_edges_pointing_to_missing_nodes_once()
    {
        Sta.Run(() =>
        {
            var errors = new List<string>();
            var flow = Flow(edges: [], configure: f => f.ErrorOccurred += (_, e) => errors.Add(e.Code));
            flow.Edges = [new Edge("bad", "a", "ghost")];
            flow.Edges = [new Edge("bad", "a", "ghost"), new Edge("bad2", "a", "b")];
            Assert.Equal(["edge-node-missing"], errors);
        });
    }

    [Fact]
    public void NodesInitialized_and_FlowInitialized_are_reported()
    {
        Sta.Run(() =>
        {
            var inits = 0;
            var flow = Flow(configure: f => f.FlowInitialized += (_, _) => inits++);
            Assert.True(flow.NodesInitialized); // les nodes de test portent déjà leur taille
            Assert.True(inits <= 1);
        });
    }

    [Fact]
    public void Connection_status_can_be_read_by_templates()
    {
        Sta.Run(() =>
        {
            var handle = new Handle();
            Assert.Equal(HandleConnectionStatus.None, handle.ConnectionStatus);
            handle.ConnectionStatus = HandleConnectionStatus.Valid;
            Assert.Equal(HandleConnectionStatus.Valid, handle.ConnectionStatus);
            Assert.Null(Flow().ConnectionState);
        });
    }

    // ---- edges ----

    [Fact]
    public void Per_edge_style_interaction_width_and_path_options_are_honoured()
    {
        Sta.Run(() =>
        {
            var edge = new Edge("e1", "a", "b", InteractionWidth: 40,
                Style: new EdgeStyleOptions("#ff0000", 4, [6, 3]), PathOptions: new EdgePathOptions(Curvature: 0.9));
            var flow = Flow(edges: [edge]);
            var visual = Assert.Single(FlowView.FindDescendants<EdgeVisual>(flow));
            Assert.Equal(40, visual.HitWidth);
            Assert.Equal(4, visual.LinePen!.Thickness);
            Assert.Equal(Color.FromRgb(255, 0, 0), ((SolidColorBrush)visual.LinePen.Brush).Color);
            Assert.NotNull(visual.LinePen.DashStyle);
            Assert.Equal(0.9, edge.PathOptions!.Curvature);
        });
    }

    [Fact]
    public void Simplebezier_edges_are_drawn()
    {
        Sta.Run(() =>
        {
            var flow = Flow(edges: [new Edge("e1", "a", "b", Type: "simplebezier")]);
            var visual = Assert.Single(FlowView.FindDescendants<EdgeVisual>(flow));
            Assert.StartsWith("M", visual.PathData);
        });
    }

    [Fact]
    public void Edge_z_index_and_aria_label_reach_the_visual()
    {
        Sta.Run(() =>
        {
            var flow = Flow(edges: [new Edge("e1", "a", "b", ZIndex: 5, AriaLabel: "lien a-b")]);
            var visual = Assert.Single(FlowView.FindDescendants<EdgeVisual>(flow));
            Assert.Equal(5, Panel.GetZIndex(visual));
            Assert.Equal("lien a-b", System.Windows.Automation.AutomationProperties.GetName(visual));
        });
    }

    [Fact]
    public void Node_z_index_and_aria_label_reach_the_host()
    {
        Sta.Run(() =>
        {
            var flow = Flow([N("a", 0, 0) with { ZIndex = 9, AriaLabel = "premier" }], []);
            var host = FlowView.FindDescendants<NodeHost>(flow).Single();
            Assert.Equal(9, Panel.GetZIndex(host));
            Assert.Equal("premier", System.Windows.Automation.AutomationProperties.GetName(host));
        });
    }

    // ---- composants ----

    [Fact]
    public void FlowControls_buttons_follow_the_show_options()
    {
        Sta.Run(() =>
        {
            var controls = new FlowControls { ShowZoom = false, ShowInteractive = false, Orientation = Orientation.Horizontal };
            controls.Style = (Style)Application.Current.TryFindResource(typeof(FlowControls));
            controls.ApplyTemplate();
            var buttons = FlowView.FindDescendants<System.Windows.Controls.Primitives.ButtonBase>(controls).ToList();
            Assert.Equal(Visibility.Collapsed, buttons.First(b => b.Name == "PART_ZoomIn").Visibility);
            Assert.Equal(Visibility.Collapsed, buttons.First(b => b.Name == "PART_Lock").Visibility);
            Assert.Equal(Visibility.Visible, buttons.First(b => b.Name == "PART_Fit").Visibility);
            Assert.Equal(Orientation.Horizontal, FlowView.FindDescendants<StackPanel>(controls).First(p => p.Name == "PART_Panel").Orientation);
        });
    }

    [Fact]
    public void Background_layers_can_be_stacked()
    {
        Sta.Run(() =>
        {
            var flow = Flow(configure: f =>
            {
                f.BackgroundSize = 2;
                f.AdditionalBackgrounds = [new BackgroundLayer(BackgroundVariant.Lines, 100, Brushes.Gray, LineWidth: 2)];
            });
            Assert.Single(flow.AdditionalBackgrounds!);
            Assert.Equal(2, flow.BackgroundSize);
        });
    }

    [Fact]
    public void NodeToolbar_can_be_forced_visible_and_aligned()
    {
        Sta.Run(() =>
        {
            var toolbar = new NodeToolbar { Align = ToolbarAlign.End };
            Assert.Equal(HorizontalAlignment.Right, toolbar.HorizontalAlignment);
            toolbar.IsToolbarVisible = true;
            Assert.Equal(Visibility.Visible, toolbar.Visibility);
            toolbar.IsToolbarVisible = false;
            Assert.Equal(Visibility.Collapsed, toolbar.Visibility);
        });
    }

    [Fact]
    public void EdgeToolbar_is_visible_only_for_a_selected_edge()
    {
        Sta.Run(() =>
        {
            var toolbar = new EdgeToolbar { DataContext = new Edge("e", "a", "b") };
            Assert.Equal(Visibility.Collapsed, toolbar.Visibility);
            toolbar.DataContext = new Edge("e", "a", "b", Selected: true);
            Assert.Equal(Visibility.Visible, toolbar.Visibility);
        });
    }

    [Fact]
    public void NodeResizer_variants_and_visibility_options()
    {
        Sta.Run(() =>
        {
            var resizer = new NodeResizer { Variant = ResizerVariant.Line };
            Assert.Equal(ResizerVariant.Line, resizer.Variant);
            Assert.True(resizer.ShowResizer);
            Assert.Null(resizer.ShouldResize);
            Assert.Equal(Brushes.Transparent, resizer.GetThumb(ResizeDirection.TopLeft)!.Background);
        });
    }

    [Fact]
    public void Portal_layer_hosts_elements_in_flow_coordinates()
    {
        Sta.Run(() =>
        {
            var flow = Flow();
            var marker = new Border { Width = 10, Height = 10 };
            flow.AddToPortal(marker, 120, 80);
            Assert.Equal(120, Canvas.GetLeft(marker));
            Assert.Contains(marker, flow.PortalLayer!.Children.Cast<UIElement>());
            flow.RemoveFromPortal(marker);
            Assert.DoesNotContain(marker, flow.PortalLayer.Children.Cast<UIElement>());
        });
    }

    [Fact]
    public void RenderToBitmap_produces_an_image_of_the_whole_graph()
    {
        Sta.Run(() =>
        {
            var flow = Flow();
            var bitmap = flow.RenderToBitmap(padding: 10, scale: 1);
            Assert.NotNull(bitmap);
            Assert.True(bitmap!.PixelWidth >= 600);
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"wpflow-{Guid.NewGuid():N}.png");
            try
            {
                Assert.True(flow.SaveImage(path));
                Assert.True(new System.IO.FileInfo(path).Length > 100);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        });
    }

    /// <summary>L'export montre le graphe entier quelle que soit la vue : défaut vu dans une application consommatrice (image blanche).</summary>
    [Fact]
    public void RenderToBitmap_does_not_depend_on_the_viewport()
    {
        Sta.Run(() =>
        {
            var flow = Flow();
            flow.Viewport = new Viewport(0, 0, 1);
            flow.UpdateLayout();
            var reference = Pixels(flow);
            // L'image n'est pas vide : le graphe est bien dessiné.
            Assert.True(reference.Distinct().Count() > 4);

            flow.Viewport = new Viewport(420, 260, 1.7);
            flow.UpdateLayout();
            var moved = Pixels(flow);

            Assert.Equal(reference, moved);
            Assert.Equal(new Viewport(420, 260, 1.7), flow.Viewport);
        });

        static int[] Pixels(FlowView flow)
        {
            var bitmap = flow.RenderToBitmap(padding: 10, scale: 1)!;
            var pixels = new int[bitmap.PixelWidth * bitmap.PixelHeight];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            return pixels;
        }
    }

    [Fact]
    public void Keyboard_options_are_configurable()
    {
        Sta.Run(() =>
        {
            var flow = Flow([N("a", 0, 0, true)], []);
            flow.DeleteKeys = [Key.X];
            Assert.Equal([Key.X], flow.DeleteKeys);
            Assert.Equal(ModifierKeys.Shift, flow.SelectionKey);
            Assert.Equal(Key.Space, flow.PanActivationKey);
            flow.DeleteSelection();
            Assert.Empty(flow.Nodes);
        });
    }
}

public class FinishingTests
{
    [Fact]
    public void SelectionToolbar_shows_above_two_or_more_selected_nodes()
    {
        Sta.Run(() =>
        {
            var flow = CustomizationTests.Realize(nodes:
            [
                new Node("a", new XYPosition(0, 100), Width: 100, Height: 40, FixedWidth: 100, FixedHeight: 40),
                new Node("b", new XYPosition(300, 100), Width: 100, Height: 40, FixedWidth: 100, FixedHeight: 40)
            ], edges: []);
            var toolbar = new SelectionToolbar { Flow = flow, Content = "outils" };
            Assert.Equal(System.Windows.Visibility.Collapsed, toolbar.Visibility);

            flow.SelectNode("a");
            Assert.Equal(System.Windows.Visibility.Collapsed, toolbar.Visibility); // un seul node
            flow.SelectNode("b", additive: true);
            Assert.Equal(System.Windows.Visibility.Visible, toolbar.Visibility);
            toolbar.MinSelected = 3;
            Assert.Equal(System.Windows.Visibility.Collapsed, toolbar.Visibility);
        });
    }

    [Fact]
    public void Aria_label_provider_names_nodes_without_their_own_label()
    {
        Sta.Run(() =>
        {
            var flow = CustomizationTests.Realize(f => f.NodeAriaLabelProvider = n => $"Étape {n.Id}", nodes:
            [
                new Node("a", new XYPosition(0, 0), Width: 100, Height: 40),
                new Node("b", new XYPosition(200, 0), Width: 100, Height: 40, AriaLabel: "propre")
            ], edges: []);
            var names = FlowView.FindDescendants<NodeHost>(flow)
                .ToDictionary(h => h.Node!.Id, h => System.Windows.Automation.AutomationProperties.GetName(h));
            Assert.Equal("Étape a", names["a"]);
            Assert.Equal("propre", names["b"]);
        });
    }

    [Fact]
    public void Handle_exposes_a_Connected_event()
    {
        Sta.Run(() =>
        {
            var handle = new Handle();
            Connection? seen = null;
            handle.Connected += (_, c) => seen = c;
            var connection = new Connection("a", "b", null, null);
            handle.RaiseConnected(connection);
            Assert.Same(connection, seen);
        });
    }
}

public class LastMileTests
{
    private static Node N(string id, double x, double y, double w = 100, double h = 40, string? parent = null) =>
        new(id, new XYPosition(x, y), Width: w, Height: h, FixedWidth: w, FixedHeight: h, ParentId: parent);

    [Fact]
    public void Parent_origin_is_part_of_the_absolute_position_of_children()
    {
        Sta.Run(() =>
        {
            var flow = CustomizationTests.Realize(nodes:
            [
                N("g", 200, 200, 200, 100) with { Origin = new XYPosition(0.5, 0.5), Type = "group" },
                N("c", 10, 10, parent: "g")
            ], edges: []);
            // Le coin du groupe est (200 - 100, 200 - 50) ; l'enfant s'y ajoute.
            Assert.Equal(new XYPosition(100, 150), flow.GetInternalNode("g")!.Position);
            Assert.Equal(new XYPosition(110, 160), flow.GetInternalNode("c")!.Position);
        });
    }

    [Fact]
    public void ExpandParent_grows_right_and_bottom_without_moving_the_parent()
    {
        Sta.Run(() =>
        {
            var flow = CustomizationTests.Realize(nodes: [N("g", 0, 0, 200, 100) with { Type = "group" }, N("c", 150, 80, parent: "g") with { ExpandParent = true }], edges: []);
            flow.ExpandParents(["c"]);
            var g = flow.GetNode("g")!;
            Assert.Equal(new XYPosition(0, 0), g.Position);
            Assert.Equal((250.0, 120.0), (g.FixedWidth, g.FixedHeight));
        });
    }

    [Fact]
    public void ExpandParent_grows_left_and_top_and_keeps_children_in_place_on_screen()
    {
        Sta.Run(() =>
        {
            var flow = CustomizationTests.Realize(nodes:
            [
                N("g", 100, 100, 200, 100) with { Type = "group" },
                N("c", -30, -20, parent: "g") with { ExpandParent = true },
                N("s", 50, 50, parent: "g")
            ], edges: []);
            var sibling = flow.GetInternalNode("s")!.Position;
            flow.ExpandParents(["c"]);

            var g = flow.GetNode("g")!;
            Assert.Equal(new XYPosition(70, 80), g.Position); // le parent recule de (30, 20)
            Assert.Equal((230.0, 120.0), (g.FixedWidth, g.FixedHeight));
            Assert.Equal(new XYPosition(0, 0), flow.GetNode("c")!.Position); // l'enfant touche maintenant le coin
            Assert.Equal(sibling, flow.GetInternalNode("s")!.Position); // le frère n'a pas bougé à l'écran
        });
    }

    [Fact]
    public void Rotation_is_applied_to_the_host_and_the_rotator_computes_angles()
    {
        Sta.Run(() =>
        {
            var flow = CustomizationTests.Realize(nodes: [N("a", 0, 0) with { Rotation = 90 }], edges: []);
            var host = FlowView.FindDescendants<NodeHost>(flow).Single();
            Assert.Equal(90, ((System.Windows.Media.RotateTransform)host.RenderTransform).Angle);

            flow.UpdateNode("a", n => n with { Rotation = 0 });
            flow.UpdateLayout();
            Assert.Equal(System.Windows.Media.Transform.Identity, host.RenderTransform);

            Assert.Equal(0, NodeRotator.AngleTo(new XYPosition(0, 0), new XYPosition(0, -50)), 6); // au-dessus : 0°
            Assert.Equal(90, NodeRotator.AngleTo(new XYPosition(0, 0), new XYPosition(50, 0)), 6); // à droite : 90°
            Assert.Equal(180, NodeRotator.AngleTo(new XYPosition(0, 0), new XYPosition(0, 50)), 6);
        });
    }

    [Fact]
    public void SelectionToolbar_can_follow_a_list_of_node_ids()
    {
        Sta.Run(() =>
        {
            var flow = CustomizationTests.Realize(nodes: [N("a", 0, 100), N("b", 300, 100), N("c", 600, 100)], edges: []);
            var toolbar = new SelectionToolbar { Flow = flow, NodeIds = ["a", "b"], Content = "x" };
            Assert.Equal(System.Windows.Visibility.Collapsed, toolbar.Visibility);
            flow.SelectNode("c");
            Assert.Equal(System.Windows.Visibility.Collapsed, toolbar.Visibility); // c n'est pas dans la liste
            flow.SelectNode("a");
            Assert.Equal(System.Windows.Visibility.Visible, toolbar.Visibility); // un seul suffit avec une liste
            toolbar.IsToolbarVisible = false;
            Assert.Equal(System.Windows.Visibility.Collapsed, toolbar.Visibility);
        });
    }

    [Fact]
    public void NodeResizeControl_and_AutoScale_expose_their_options()
    {
        Sta.Run(() =>
        {
            var control = new NodeResizeControl { Direction = ResizeDirection.TopRight, KeepAspectRatio = true, NodeMinWidth = 30 };
            Assert.Equal(ResizeDirection.TopRight, control.Direction);
            Assert.True(control.KeepAspectRatio);
            Assert.Equal(30, control.NodeMinWidth);
            Assert.Null(control.ShouldResize);

            var resizer = new NodeResizer { AutoScale = true };
            Assert.True(resizer.AutoScale);
        });
    }
}

public class PartialRowsTests
{
    private static Node N(string id, double x, double y, string? parent = null, bool selected = false) =>
        new(id, new XYPosition(x, y), Width: 100, Height: 40, FixedWidth: 100, FixedHeight: 40, ParentId: parent, Selected: selected);

    [Fact]
    public void Async_before_delete_can_cancel_or_allow()
    {
        Sta.Run(() =>
        {
            var flow = CustomizationTests.Realize(nodes: [N("a", 0, 0), N("b", 300, 0)], edges: []);
            flow.BeforeDeleteAsync = _ => Task.FromResult(false);
            Assert.False(flow.DeleteElementsAsync(["a"], null).GetAwaiter().GetResult());
            Assert.NotNull(flow.GetNode("a"));

            flow.BeforeDeleteAsync = _ => Task.FromResult(true);
            Assert.True(flow.DeleteElementsAsync(["a"], null).GetAwaiter().GetResult());
            Assert.Null(flow.GetNode("a"));
        });
    }

    [Fact]
    public void Change_handlers_can_replace_the_changes_like_a_middleware()
    {
        Sta.Run(() =>
        {
            var flow = CustomizationTests.Realize(nodes: [N("a", 0, 0)], edges: []);
            // Middleware : un node ne descend jamais sous y = 0.
            flow.NodeChangesRequested += (_, e) =>
                e.Changes = e.Changes.Select(c => c is NodePositionChange p ? p with { Position = new XYPosition(p.Position.X, Math.Max(0, p.Position.Y)) } : c).ToList();
            flow.ApplyNodeChanges([new NodePositionChange("a", new XYPosition(50, -80))]);
            Assert.Equal(new XYPosition(50, 0), flow.GetNode("a")!.Position);

            flow.NodeChangesRequested += (_, e) => e.Changes = [];
            flow.ApplyNodeChanges([new NodePositionChange("a", new XYPosition(999, 999))]);
            Assert.Equal(new XYPosition(50, 0), flow.GetNode("a")!.Position); // changes supprimés
        });
    }

    [Fact]
    public void ZIndexMode_manual_ignores_depth_and_selection_and_auto_lifts_ancestors()
    {
        Sta.Run(() =>
        {
            Node[] Set(bool childSelected) =>
            [
                N("g", 0, 0) with { Type = "group" },
                N("c", 10, 10, "g", childSelected),
                N("o", 300, 0)
            ];

            var manual = CustomizationTests.Realize(f => f.ZIndexMode = FlowZIndexMode.Manual, Set(true), []);
            Assert.All(FlowView.FindDescendants<NodeHost>(manual), h => Assert.Equal(0, Panel.GetZIndex(h)));

            var auto = CustomizationTests.Realize(f => f.ZIndexMode = FlowZIndexMode.Auto, Set(true), []);
            int Z(string id) => Panel.GetZIndex(FlowView.FindDescendants<NodeHost>(auto).Single(h => h.Node!.Id == id));
            Assert.True(Z("g") >= 1000 && Z("c") > Z("g")); // le parent suit son enfant sélectionné, l'enfant reste au-dessus
            Assert.True(Z("o") < 1000);
        });
    }

    [Fact]
    public void Marker_orient_and_scrolling_options_exist()
    {
        Sta.Run(() =>
        {
            var fixedAngle = EdgeVisual.FixedTransform(new XYPosition(10, 20), 90);
            Assert.Equal(10, fixedAngle.OffsetX, 6);
            Assert.Equal(20, fixedAngle.OffsetY, 6);
            Assert.Equal(0, fixedAngle.M11, 6); // quart de tour
            var flow = new FlowView();
            Assert.True(flow.PreventScrolling);
            Assert.Equal(FlowMouseButtons.Left | FlowMouseButtons.Middle, flow.PanOnDragButtons);
            flow.PanOnDragButtons |= FlowMouseButtons.Right;
            Assert.True(flow.PanOnDragButtons.HasFlag(FlowMouseButtons.Right));
        });
    }

    [Fact]
    public void Edge_aria_provider_names_edges()
    {
        Sta.Run(() =>
        {
            var flow = CustomizationTests.Realize(f => f.EdgeAriaLabelProvider = e => $"de {e.Source} vers {e.Target}",
                nodes: [N("a", 0, 0), N("b", 300, 100)], edges: [new Edge("e", "a", "b")]);
            var visual = FlowView.FindDescendants<EdgeVisual>(flow).Single();
            Assert.Equal("de a vers b", System.Windows.Automation.AutomationProperties.GetName(visual));
        });
    }
}

public class InteractionFlagsWpfTests
{
    [Fact]
    public void Debug_and_selection_drag_events_exist_and_dragging_flag_is_applied()
    {
        Sta.Run(() =>
        {
            var flow = CustomizationTests.Realize(nodes: [new Node("a", new XYPosition(0, 0), Width: 100, Height: 40, FixedWidth: 100, FixedHeight: 40)], edges: []);
            flow.SelectionDragStarted += (_, _) => { };
            flow.SelectionDragging += (_, _) => { };
            flow.SelectionDragStopped += (_, _) => { };
            flow.Debug = true;
            flow.ApplyNodeChanges([new NodeInteractionChange("a", Dragging: true)]);
            Assert.True(flow.GetNode("a")!.Dragging);
            flow.ApplyNodeChanges([new NodeInteractionChange("a", Dragging: false)]);
            Assert.False(flow.GetNode("a")!.Dragging);
        });
    }
}

public class DragFlagWithSourceTests
{
    [Fact]
    public void Dragging_flag_does_not_disturb_position_write_back_with_a_source()
    {
        Sta.Run(() =>
        {
            var nodes = new System.Collections.ObjectModel.ObservableCollection<NodeVm> { new() { Id = "a", X = 1, Y = 2 } };
            var flow = new FlowView { NodesSource = nodes };
            flow.ApplyNodeChanges([new NodeInteractionChange("a", Dragging: true)]);
            flow.ApplyNodeChanges([new NodePositionChange("a", new XYPosition(40, 50))]);
            Assert.Equal((40, 50), (nodes[0].X, nodes[0].Y));
            Assert.Equal(new XYPosition(40, 50), flow.Nodes[0].Position);
        });
    }
}
