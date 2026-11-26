using static demo.Sim;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpflow.Core;
using Wpflow.Wpf;

namespace demo;

/// <summary>
///     Auto-test de la fenêtre d'exemples (WPFLOW_EXAMPLES=selftest + WPFLOW_SCREENSHOT) : pilote la vraie souris et le
///     vrai clavier Windows pour vérifier groupes, clavier, toolbar, glisser-déposer, modes de connexion, extent, thème,
///     JSON et virtualisation. Chaque vérification est écrite dans le .log sous la forme « OK/ECHEC ».
/// </summary>
internal static class ExamplesSelfTest
{


    private static Action<string> _log = _ => { };
    private static ExamplesWindow _win = null!;
    private static string _base = "";

    private static void Check(string name, bool ok, string detail = "") => _log(SelfTestReport.Line(name, ok, detail));

    public static async Task Run(ExamplesWindow win, string screenshotPath, Action<string> log)
    {
        _win = win;
        _log = log;
        _base = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(screenshotPath)!,
            System.IO.Path.GetFileNameWithoutExtension(screenshotPath));

        await Tab(0);
        await Shot("edges");
        Check("edges: 10 nodes et 5 edges affichés", win.Edges.Nodes.Count == 10 && win.Edges.Edges.Count == 5);
        // Edge animé : les pointillés défilent, donc deux captures espacées dans le temps diffèrent (rien d'autre n'est animé ici).
        var animA = Snapshot(win.Edges);
        await Task.Delay(260);
        var animB = Snapshot(win.Edges);
        Check("edge animé: les pointillés défilent", !animA.SequenceEqual(animB));
        var overridden = (win.Edges.ThemeBrush("Flow.Edge") as SolidColorBrush)?.Color;
        var inherited = (win.Edges.ThemeBrush("Flow.Background") as SolidColorBrush)?.Color;
        Check("thème: la clé surchargée dans FlowView.Resources l'emporte, le reste vient du thème",
            overridden == Color.FromRgb(0x1b, 0x86, 0xd6) && inherited == Colors.White, $"Flow.Edge={overridden}, Flow.Background={inherited}");

        await Groups();
        await Tools();
        await Palette();
        await Layout();
        await Options();
        await Gestures();
        await Gestures2();
        await ClickedElement();
        await Customization();
        await Mvvm();
        await Perf();
    }

    // ---------------- scénarios ----------------

    private static async Task Groups()
    {
        await Tab(1);
        var f = _win.Groups;
        await Shot("groups-initial");

        var g1 = Host(f, "g1");
        var before = Abs(f, "c1");
        var start = Screen(g1, new Point(40, 12));
        await Drag(start, (start.X + 60, start.Y + 40));
        var after = Abs(f, "c1");
        Check("groupe: les enfants suivent le parent",
            Math.Abs((after.X - before.X) - 60 / f.Viewport.Zoom) < 1.5 && Math.Abs((after.Y - before.Y) - 40 / f.Viewport.Zoom) < 1.5,
            $"c1 {before} -> {after}");

        // Un enfant tiré très loin reste dans le groupe (extent: parent).
        var c2 = Host(f, "c2");
        var p = Screen(c2, new Point(c2.ActualWidth / 2, c2.ActualHeight / 2));
        await Drag(p, (p.X + 500, p.Y + 400));
        var rel = f.Nodes.First(n => n.Id == "c2");
        var parent = f.Nodes.First(n => n.Id == "g1");
        Check("groupe: extent parent",
            rel.Position.X <= (parent.FixedWidth!.Value - rel.Width!.Value) + 0.5 &&
            rel.Position.Y <= (parent.FixedHeight!.Value - rel.Height!.Value) + 0.5,
            $"c2 relatif {rel.Position}, parent {parent.FixedWidth}x{parent.FixedHeight}");

        // Clavier : Tab sélectionne, flèches déplacent, Ctrl+Z annule, Echap désélectionne.
        var empty = Screen(f, new Point(f.ActualWidth - 80, 80));
        await Click(empty);
        Key(VkTab);
        await Task.Delay(200);
        var firstSelected = f.Nodes.FirstOrDefault(n => n.Selected)?.Id;
        Check("clavier: Tab sélectionne le premier node", firstSelected == "g1", firstSelected ?? "aucun");
        Key(VkTab);
        await Task.Delay(200);
        var second = f.Nodes.FirstOrDefault(n => n.Selected)?.Id;
        Check("clavier: Tab suivant", second == "c1", second ?? "aucun");

        var posBefore = f.Nodes.First(n => n.Id == "c1").Position;
        Key(VkRight);
        Key(VkRight);
        await Task.Delay(200);
        var posAfter = f.Nodes.First(n => n.Id == "c1").Position;
        Check("clavier: flèches déplacent de 5 px", Math.Abs(posAfter.X - posBefore.X - 10) < 0.01, $"{posBefore} -> {posAfter}");
        Key(VkZ, ctrl: true);
        await Task.Delay(200);
        var undone = f.Nodes.First(n => n.Id == "c1").Position;
        Check("clavier: Ctrl+Z annule un déplacement au clavier", undone.X < posAfter.X, $"{posAfter} -> {undone}");
        Key(VkEscape);
        await Task.Delay(200);
        Check("clavier: Echap désélectionne", f.Nodes.All(n => !n.Selected));

        // Accessibilité : les peers existent et exposent nom + sélection.
        var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(Host(f, "c1"));
        Check("accessibilité: peer de node", peer is NodeHostAutomationPeer && peer.GetName().Contains("Enfant 1"), peer?.GetName() ?? "null");
        var flowPeer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(f);
        Check("accessibilité: peer de la surface", flowPeer is FlowViewAutomationPeer, flowPeer?.GetClassName() ?? "null");

        // Suppression du groupe : les enfants partent avec lui.
        var gp = Screen(Host(f, "g1"), new Point(40, 12));
        await Click(gp);
        Key(VkDelete);
        await Task.Delay(300);
        Check("groupe: supprimer le parent supprime les enfants", f.Nodes.Count == 1 && f.Nodes[0].Id == "free",
            string.Join(",", f.Nodes.Select(n => n.Id)));
        await Shot("groups-after");
    }

    private static async Task Tools()
    {
        await Tab(2);
        var f = _win.Tools;
        var t1 = Host(f, "t1");
        await Click(Screen(t1, new Point(30, 40)));
        await Task.Delay(300);
        var toolbar = Find<NodeToolbar>(t1).First();
        Check("toolbar: visible quand le node est sélectionné", toolbar.Visibility == Visibility.Visible && toolbar.ActualHeight > 0);
        var resizer = Find<NodeResizer>(t1).First();
        Check("resizer: visible quand le node est sélectionné", resizer.Visibility == Visibility.Visible);
        await Shot("tools");

        var dup = Find<Button>(t1).First(b => (string)b.Content == "Dupliquer");
        await Click(Screen(dup, new Point(dup.ActualWidth / 2, dup.ActualHeight / 2)));
        await Task.Delay(400);
        Check("toolbar: le bouton Dupliquer agit", f.Nodes.Count == 3, $"{f.Nodes.Count} nodes");
    }

    private static async Task Palette()
    {
        await Tab(3);
        var f = _win.Palette;
        var item = Find<Border>(_win).First(b => b.Tag is "default");
        var from = Screen(item, new Point(item.ActualWidth / 2, item.ActualHeight / 2));
        var to = Screen(f, new Point(f.ActualWidth / 2, f.ActualHeight / 2));
        await Drag(from, (to.X, to.Y), steps: 25, delay: 30);
        await Task.Delay(500);
        Check("palette: glisser-déposer ajoute un node", f.Nodes.Count == 2, $"{f.Nodes.Count} nodes");
        if (f.Nodes.Count == 2)
        {
            var dropped = f.Nodes[1];
            var expected = f.ScreenToFlow(f.PointFromScreen(to));
            Check("palette: position du dépôt", Math.Abs(dropped.Position.X + 75 - expected.X) < 3, $"{dropped.Position} ~ {expected}");
        }
    }

    private static async Task Layout()
    {
        await Tab(4);
        var f = _win.Layout;
        await Task.Delay(500);
        _win.OnAutoLayout(null, null);
        await Task.Delay(800);
        var x = f.Nodes.ToDictionary(n => n.Id, n => n.Position.X);
        Check("layout: couches de gauche à droite", x["a"] < x["b"] && x["b"] < x["d"] && x["d"] < x["f"] && x["a"] == 0,
            string.Join(" ", x.Select(kv => $"{kv.Key}={kv.Value:0}")));
        await Shot("layout");
    }

    private static async Task Options()
    {
        await Tab(5);
        var f = _win.Options;
        await Task.Delay(500);

        // Strict : source vers source refusé.
        var edgesBefore = f.Edges.Count;
        await ConnectSources(f, "oE", "oA");
        Check("strict: source vers source refusé", f.Edges.Count == edgesBefore, $"{f.Edges.Count} edges, dernier: {f.Edges.Last()}");

        // Loose : accepté.
        _win.Loose.IsChecked = true;
        _win.OnOptionsChanged(null, null);
        await ConnectSources(f, "oE", "oA");
        Check("loose: source vers source accepté", f.Edges.Count == edgesBefore + 1, $"{f.Edges.Count} edges");

        // isValidConnection : refusé avec le prédicat, accepté sans.
        _win.NoSelf.IsChecked = true;
        _win.OnOptionsChanged(null, null);
        var n = f.Edges.Count;
        await ConnectSources(f, "oA", "oD");
        Check("isValidConnection: refuse les connexions vers Node D", f.Edges.Count == n, $"{f.Edges.Count} edges");
        _win.NoSelf.IsChecked = false;
        _win.OnOptionsChanged(null, null);
        await ConnectSources(f, "oA", "oD");
        Check("isValidConnection: la même connexion passe sans prédicat", f.Edges.Count == n + 1, $"{f.Edges.Count} edges");

        // translateExtent : un pan extrême reste dans les limites (ou centré si l'extent est plus petit que la vue).
        _win.Extent.IsChecked = true;
        _win.OnOptionsChanged(null, null);
        var start = Screen(f, new Point(f.ActualWidth - 60, f.ActualHeight - 60));
        await Drag(start, (start.X - 900, start.Y - 700));
        var visible = ViewportMath.GetVisibleRect(f.Viewport, f.ActualWidth, f.ActualHeight);
        var extent = new Wpflow.Core.Rect(-100, -100, 800, 500);
        var xOk = visible.X >= extent.X - 1 && visible.Right <= extent.Right + 1 || visible.Width > extent.Width && Math.Abs(visible.Center.X - extent.Center.X) < 1;
        var yOk = visible.Y >= extent.Y - 1 && visible.Bottom <= extent.Bottom + 1 || visible.Height > extent.Height && Math.Abs(visible.Center.Y - extent.Center.Y) < 1;
        Check("translateExtent: pan limité", xOk && yOk, $"visible {visible} dans {extent}");
        _win.Extent.IsChecked = false;
        _win.OnOptionsChanged(null, null);

        // Espace + glisser sur un node : la vue bouge, le node reste en place.
        var nodeB = Host(f, "oB");
        var nodePos = f.Nodes.First(x => x.Id == "oB").Position;
        var vpBefore = f.Viewport;
        var nb = Screen(nodeB, new Point(nodeB.ActualWidth / 2, 12));
        KeyDown(VkSpace);
        await Drag(nb, (nb.X + 50, nb.Y + 30));
        KeyRelease(VkSpace);
        await Task.Delay(200);
        Check("espace + glisser: déplace la vue sans bouger le node",
            f.Nodes.First(x => x.Id == "oB").Position == nodePos && Math.Abs(f.Viewport.X - vpBefore.X - 50) < 2,
            $"viewport {vpBefore.X:0}->{f.Viewport.X:0}");
        f.FitView();

        // JSON : export puis import redonnent le même graphe.
        var nodesBefore = f.Nodes.Select(x => (x.Id, x.Position)).ToList();
        _win.OnExport(null, null);
        var json = _win.Json.Text;
        f.Nodes = [];
        f.Edges = [];
        _win.OnImport(null, null);
        await Task.Delay(300);
        Check("json: export/import identiques", f.Nodes.Select(x => (x.Id, x.Position)).SequenceEqual(nodesBefore) && json.Contains("\"viewport\""),
            $"{f.Nodes.Count} nodes, {f.Edges.Count} edges");

        // Thème sombre.
        _win.Dark.IsChecked = true;
        _win.OnOptionsChanged(null, null);
        await Task.Delay(600);
        f.FitView();
        await Task.Delay(400);
        var bg = (f.ThemeBrush("Flow.Background") as SolidColorBrush)?.Color;
        Check("thème sombre appliqué", bg == Color.FromRgb(0x14, 0x14, 0x14), $"fond {bg}");
        await Shot("dark");
        _win.Dark.IsChecked = false;
        _win.OnOptionsChanged(null, null);
    }

    /// <summary>Gestes ajoutés par les lots P2/P3 : survol, double-clic, connexion par clics, sélection, copier-coller, auto-défilement, molette.</summary>
    private static async Task Gestures()
    {
        await Tab(5);
        var f = _win.Options;
        f.FitView();
        await Task.Delay(500);
        Point Empty() => Screen(f, new Point(10, 10)); // coin de la vue : vide après un cadrage avec marge (la fenêtre peut bouger)

        // Survol : entrer puis sortir d'un node lève les événements.
        int enter = 0, leave = 0;
        f.NodeMouseEnter += (_, _) => enter++;
        f.NodeMouseLeave += (_, _) => leave++;
        MoveTo(Empty());
        await Task.Delay(150);
        var b = Host(f, "oB");
        MoveTo(Screen(b, new Point(b.ActualWidth / 2, b.ActualHeight / 2)));
        await Task.Delay(200);
        MoveTo(Empty());
        await Task.Delay(200);
        Check("survol: NodeMouseEnter puis NodeMouseLeave", enter >= 1 && leave >= 1, $"enter={enter}, leave={leave}");

        // Double-clic sur un node, puis sur le fond (zoom ×2 animé).
        var doubles = 0;
        f.NodeDoubleClick += (_, _) => doubles++;
        await DoubleClick(Screen(b, new Point(b.ActualWidth / 2, b.ActualHeight / 2)));
        Check("double-clic: NodeDoubleClick levé", doubles == 1, $"{doubles}");
        var zoom0 = f.Viewport.Zoom;
        await DoubleClick(Empty());
        await Task.Delay(500);
        Check("double-clic sur le fond: zoom ×2 (animé)", f.Viewport.Zoom > zoom0 * 1.5 || f.Viewport.Zoom >= f.MaxZoom - 0.01,
            $"zoom {zoom0:0.00} -> {f.Viewport.Zoom:0.00}");
        f.FitView();
        await Task.Delay(400);

        // Connexion par deux clics.
        f.ConnectOnClick = true;
        Wpflow.Core.Node? from = null, to = null;
        foreach (var x in f.Nodes)
        foreach (var y in f.Nodes)
            if (from == null && x.Id != y.Id && !f.Edges.Any(e => (e.Source == x.Id && e.Target == y.Id) || (e.Source == y.Id && e.Target == x.Id)))
                (from, to) = (x, y);
        var hs = Find<Handle>(Host(f, from!.Id)).First(h => h.Type == HandleType.Source);
        var ht = Find<Handle>(Host(f, to!.Id)).First(h => h.Type == HandleType.Target);
        var edgesBefore = f.Edges.Count;
        ClickConnectEndEvent? ended = null;
        f.ClickConnectEnded += (_, e) => ended = new ClickConnectEndEvent(e.IsValid);
        await Click(Screen(hs, new Point(hs.ActualWidth / 2, hs.ActualHeight / 2)));
        var armed = f.IsConnecting && f.ConnectionState != null;
        MoveTo(Screen(ht, new Point(ht.ActualWidth / 2, ht.ActualHeight / 2)));
        await Task.Delay(200);
        var preview = f.ConnectionState;
        await Click(Screen(ht, new Point(ht.ActualWidth / 2, ht.ActualHeight / 2)));
        Check("connectOnClick: deux clics créent l'edge", armed && f.Edges.Count == edgesBefore + 1 && ended is { Valid: true } && !f.IsConnecting,
            $"armé={armed}, edges {edgesBefore}->{f.Edges.Count}, fin={ended}, IsConnecting={f.IsConnecting}");
        Check("connexion: l'état expose la cible et sa validité pendant le geste", preview is { IsValid: true, ToNodeId: not null }, $"{preview}");

        // Échap annule une connexion par clics.
        await Click(Screen(hs, new Point(hs.ActualWidth / 2, hs.ActualHeight / 2)));
        Key(VkEscape);
        await Task.Delay(200);
        Check("connectOnClick: Échap annule", !f.IsConnecting && f.ConnectionState == null && f.Edges.Count == edgesBefore + 1);
        f.ConnectOnClick = false;

        // Rectangle de sélection : partiel puis complet, et sélection des edges entre nodes sélectionnés.
        f.FitView();
        await Task.Delay(400);
        Point BC() => Screen(b, new Point(b.ActualWidth / 2, b.ActualHeight / 2));
        var bc = BC();
        f.AutoPanOnSelection = false; // le coin de départ est à moins de 40 px du bord : sinon la vue défilerait
        f.SelectionMode = FlowSelectionMode.Full;
        KeyDown(VkShift);
        await Task.Delay(200); // laisse Keyboard.Modifiers suivre
        await Drag(Empty(), (bc.X, bc.Y));
        KeyRelease(VkShift);
        var fullSelected = f.Nodes.First(n => n.Id == "oB").Selected;
        f.SelectionMode = FlowSelectionMode.Partial;
        KeyDown(VkShift);
        await Task.Delay(200); // laisse Keyboard.Modifiers suivre
        await Drag(Empty(), (bc.X, bc.Y));
        KeyRelease(VkShift);
        var partialSelected = f.Nodes.First(n => n.Id == "oB").Selected;
        Check("sélection: Full exige le node entier, Partial le touche", !fullSelected && partialSelected, $"full={fullSelected}, partial={partialSelected}, sélection={string.Join(",", f.Nodes.Where(n => n.Selected).Select(n => n.Id))}, bc={bc}, oB={b.PointToScreen(new Point(0, 0))} {b.ActualWidth}x{b.ActualHeight}, vue={Screen(f, new Point(0, 0))} {f.ActualWidth}x{f.ActualHeight}");
        var corner = Screen(f, new Point(f.ActualWidth - 10, f.ActualHeight - 10));
        KeyDown(VkShift);
        await Task.Delay(200); // laisse Keyboard.Modifiers suivre
        await Drag(Empty(), (corner.X, corner.Y));
        KeyRelease(VkShift);
        Check("sélection: les edges entre nodes sélectionnés le sont aussi",
            f.Nodes.All(n => n.Selected) && f.Edges.Count > 0 && f.Edges.All(e => e.Selected),
            $"{f.Nodes.Count(n => n.Selected)}/{f.Nodes.Count} nodes, {f.Edges.Count(e => e.Selected)}/{f.Edges.Count} edges");

        f.AutoPanOnSelection = true;

        // Copier-coller au clavier (le clic donne le focus et sélectionne oB).
        f.FitView();
        await Task.Delay(400);
        bc = BC();
        await Click(Empty()); // désélectionne tout
        await Click(bc);
        var nodesBefore = f.Nodes.Count;
        Key(VkC, ctrl: true);
        Key(VkV, ctrl: true);
        await Task.Delay(300);
        Check("copier-coller: Ctrl+C / Ctrl+V dupliquent le node sélectionné",
            f.Nodes.Count == nodesBefore + 1 && f.Nodes.Any(n => n.Id == "oB-copy" && n.Selected), $"{nodesBefore} -> {f.Nodes.Count} nodes, sélection={string.Join(",", f.Nodes.Where(n => n.Selected).Select(n => n.Id))}, focus={System.Windows.Input.Keyboard.FocusedElement}");
        Key(VkZ, ctrl: true);
        await Task.Delay(250);
        Check("copier-coller: annulable", f.Nodes.Count == nodesBefore, $"{f.Nodes.Count} nodes");

        // Molette : défilement au lieu du zoom avec PanOnScroll.
        f.FitView();
        await Task.Delay(400);
        f.PanOnScroll = true;
        var vp = f.Viewport;
        MoveTo(Empty());
        await Task.Delay(150);
        Wheel(120);
        await Task.Delay(300);
        Check("panOnScroll: la molette déplace la vue sans zoomer",
            Math.Abs(f.Viewport.Zoom - vp.Zoom) < 1e-6 && Math.Abs(f.Viewport.Y - vp.Y - 60) < 2, $"{vp} -> {f.Viewport}");
        f.PanOnScroll = false;

        // Auto-défilement : un node tenu près du bord droit fait défiler la vue.
        f.FitView();
        await Task.Delay(400);
        var before = f.Viewport;
        var nb = Screen(b, new Point(b.ActualWidth / 2, 12));
        var edge = Screen(f, new Point(f.ActualWidth - 8, 0));
        await DragHolding(nb, (edge.X, nb.Y), () => Task.Delay(700));
        Check("auto-défilement: la vue défile quand on tire un node vers le bord", f.Viewport.X < before.X - 20, $"x {before.X:0} -> {f.Viewport.X:0}");
        f.FitView();
        await Task.Delay(300);
    }

    private sealed record ClickConnectEndEvent(bool Valid);

    private static DataTemplate CardTemplate(bool grip, bool noDrag)
    {
        var root = new FrameworkElementFactory(typeof(Grid));
        root.SetValue(FrameworkElement.WidthProperty, 150.0);
        root.SetValue(FrameworkElement.HeightProperty, 60.0);
        var card = new FrameworkElementFactory(typeof(Border));
        card.SetValue(Border.BackgroundProperty, Brushes.White);
        card.SetValue(Border.BorderBrushProperty, Brushes.Black);
        card.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        root.AppendChild(card);
        if (grip)
        {
            var handle = new FrameworkElementFactory(typeof(Border));
            handle.SetValue(FrameworkElement.HeightProperty, 16.0);
            handle.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
            handle.SetValue(Border.BackgroundProperty, Brushes.LightSkyBlue);
            handle.SetValue(NodeHost.DragHandleProperty, "grip");
            root.AppendChild(handle);
        }

        if (noDrag) root.SetValue(FlowView.NoDragProperty, true);
        return new DataTemplate { VisualTree = root };
    }

    /// <summary>Zone de saisie (<c>DragHandle</c>), <c>NoDrag</c>, <c>PanOnDrag</c>, <c>SelectionOnDrag</c> et <c>DefaultEdgeOptions</c>, à la vraie souris.</summary>
    private static async Task Gestures2()
    {
        await Tab(5);
        var f = _win.Options;
        f.Resources["wpflow-node-grip"] = CardTemplate(true, false);
        f.Resources["wpflow-node-nodrag"] = CardTemplate(false, true);
        var added = f.Nodes.ToList();
        added.Add(new Node("gripN", new XYPosition(-250, -140), Type: "grip", DragHandle: "grip"));
        added.Add(new Node("lockN", new XYPosition(-250, -40), Type: "nodrag"));
        f.Nodes = added;
        await Task.Delay(600);
        f.FitView();
        await Task.Delay(500);

        // DragHandle : tirer le corps du node ne le déplace pas (la vue défile), tirer la poignée le déplace.
        var grip = Host(f, "gripN");
        var before = f.Nodes.First(n => n.Id == "gripN").Position;
        var vp = f.Viewport;
        var body = Screen(grip, new Point(75, 40));
        await Drag(body, (body.X + 60, body.Y + 30));
        var afterBody = f.Nodes.First(n => n.Id == "gripN").Position;
        Check("dragHandle: tirer le corps ne déplace pas le node (la vue défile)", afterBody == before && f.Viewport.X > vp.X + 30,
            $"node {before} -> {afterBody}, vue {vp.X:0} -> {f.Viewport.X:0}");
        grip = Host(f, "gripN");
        var handle = Screen(grip, new Point(75, 8));
        await Drag(handle, (handle.X + 60, handle.Y + 30));
        var afterGrip = f.Nodes.First(n => n.Id == "gripN").Position;
        Check("dragHandle: tirer la poignée déplace le node", afterGrip.X > before.X + 30, $"{before} -> {afterGrip}");

        // NoDrag : un node dont le contenu est marqué ne se déplace pas.
        var lockHost = Host(f, "lockN");
        var lockBefore = f.Nodes.First(n => n.Id == "lockN").Position;
        var lp = Screen(lockHost, new Point(75, 30));
        await Drag(lp, (lp.X + 60, lp.Y + 30));
        Check("noDrag: le contenu marqué empêche le déplacement", f.Nodes.First(n => n.Id == "lockN").Position == lockBefore);

        // PanOnDrag faux : glisser sur le fond ne déplace plus la vue.
        f.PanOnDrag = false;
        var empty = Screen(f, new Point(10, f.ActualHeight / 2));
        var vp2 = f.Viewport;
        await Drag(empty, (empty.X + 80, empty.Y + 40));
        Check("panOnDrag faux: la vue reste en place", f.Viewport == vp2, $"{vp2} -> {f.Viewport}");
        f.PanOnDrag = true;

        // SelectionOnDrag : glisser sur le fond trace un rectangle (sans Maj).
        f.SelectionOnDrag = true;
        f.AutoPanOnSelection = false;
        var oB = Host(f, "oB");
        var target = Screen(oB, new Point(oB.ActualWidth / 2, oB.ActualHeight / 2));
        await Drag(Screen(f, new Point(10, 10)), (target.X, target.Y));
        Check("selectionOnDrag: le glisser sur le fond sélectionne", f.Nodes.First(n => n.Id == "oB").Selected);
        f.SelectionOnDrag = false;
        f.AutoPanOnSelection = true;

        // DefaultEdgeOptions : l'edge créé à la souris reprend le type et l'animation par défaut.
        f.DefaultEdgeOptions = new Edge("", "", "", Type: "smoothstep", Animated: true);
        f.ConnectionMode = ConnectionMode.Strict;
        Node? from = null, to = null;
        foreach (var x in f.Nodes.Where(n => n.Type is "default" or "input"))
        foreach (var y in f.Nodes.Where(n => n.Type is "default" or "output"))
            if (from == null && x.Id != y.Id && !f.Edges.Any(e => (e.Source == x.Id && e.Target == y.Id) || (e.Source == y.Id && e.Target == x.Id)))
                (from, to) = (x, y);
        var hs = Find<Handle>(Host(f, from!.Id)).First(h => h.Type == HandleType.Source);
        var ht = Find<Handle>(Host(f, to!.Id)).First(h => h.Type == HandleType.Target);
        var count = f.Edges.Count;
        await Drag(Screen(hs, new Point(hs.ActualWidth / 2, hs.ActualHeight / 2)), (Screen(ht, new Point(ht.ActualWidth / 2, ht.ActualHeight / 2)).X, Screen(ht, new Point(ht.ActualWidth / 2, ht.ActualHeight / 2)).Y));
        var created = f.Edges.LastOrDefault();
        Check("defaultEdgeOptions: le nouvel edge reprend type et animation", f.Edges.Count == count + 1 && created is { Type: "smoothstep", Animated: true },
            $"{count} -> {f.Edges.Count}, {created?.Type}/{created?.Animated}");
        f.DefaultEdgeOptions = null;
        f.Nodes = f.Nodes.Where(n => n.Id is not ("gripN" or "lockN")).ToList();
        f.FitView();
        await Task.Delay(300);
    }

    // Une « table » à deux colonnes : chaque ligne est marquée par FlowView.TargetData, l'en-tête ne l'est pas.
    private static DataTemplate TableTemplate()
    {
        var root = new FrameworkElementFactory(typeof(StackPanel));
        root.SetValue(FrameworkElement.WidthProperty, 160.0);
        root.SetValue(Panel.BackgroundProperty, Brushes.White);
        foreach (var column in new[] { null, "id", "client_id" })
        {
            var row = new FrameworkElementFactory(typeof(Border));
            row.SetValue(FrameworkElement.HeightProperty, 26.0);
            row.SetValue(Border.BackgroundProperty, column == null ? Brushes.LightSkyBlue : Brushes.White);
            if (column != null) row.SetValue(FlowView.TargetDataProperty, column);
            // L'élément réellement cliqué est le texte : la donnée est lue sur son parent.
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetValue(TextBlock.TextProperty, column ?? "commandes");
            text.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            row.AppendChild(text);
            root.AppendChild(row);
        }

        return new DataTemplate { VisualTree = root };
    }

    /// <summary>Savoir quel élément d'un node a été cliqué, double-cliqué ou visé par le menu contextuel (évènements et commandes).</summary>
    private static async Task ClickedElement()
    {
        await Tab(5);
        var f = _win.Options;
        var saved = f.Nodes;
        f.Resources["wpflow-node-table"] = TableTemplate();
        f.Nodes = [..saved, new Node("table", new XYPosition(-250, -140), Type: "table")];
        await Task.Delay(500);
        f.FitView();
        await Task.Delay(500);

        var clicks = new List<ElementEventArgs>();
        var doubles = new List<ElementEventArgs>();
        var menus = new List<ElementEventArgs>();
        var panes = new List<ElementEventArgs>();
        EventHandler<ElementEventArgs> onClick = (_, e) => clicks.Add(e), onDouble = (_, e) => doubles.Add(e),
            onMenu = (_, e) => menus.Add(e), onPane = (_, e) => panes.Add(e);
        f.NodeClick += onClick;
        f.NodeDoubleClick += onDouble;
        f.NodeContextMenuRequested += onMenu;
        f.PaneClick += onPane;

        var texts = Find<TextBlock>(Host(f, "table")).ToList();
        Point Row(int i) => Center(texts[i]);

        await Click(Row(2));
        Check("élément cliqué: NodeClick donne la colonne", clicks.Count == 1 && Equals(clicks[0].TargetData, "client_id") && clicks[0].OriginalSource != null,
            $"{clicks.Count} clic(s), {clicks.LastOrDefault()?.TargetData}");
        await Task.Delay(600); // au-delà du délai de double-clic
        await Click(Row(0));
        Check("élément cliqué: sans marque (en-tête), TargetData est null", clicks.Count == 2 && clicks[1].TargetData == null, $"{clicks.LastOrDefault()?.TargetData}");
        await Task.Delay(600);

        var zoom = f.Viewport.Zoom;
        await DoubleClick(Row(1));
        Check("élément cliqué: NodeDoubleClick donne la colonne, sans zoom",
            doubles.Count == 1 && Equals(doubles[0].TargetData, "id") && Math.Abs(f.Viewport.Zoom - zoom) < 1e-9,
            $"{doubles.Count}, {doubles.LastOrDefault()?.TargetData}");

        await RightClick(Row(2));
        Check("élément cliqué: le menu contextuel donne la colonne", menus.Count == 1 && Equals(menus[0].TargetData, "client_id"),
            $"{menus.Count}, {menus.LastOrDefault()?.TargetData}");

        await Click(Screen(f, new Point(10, 10)));
        Check("élément cliqué: sur le fond, TargetData est null", panes.Count == 1 && panes[0].TargetData == null);

        var before = clicks.Count;
        var from = Row(2);
        await Drag(from, (from.X + 50, from.Y + 30));
        Check("élément cliqué: un glisser n'est pas un clic", clicks.Count == before, $"{before} -> {clicks.Count}");

        object? parameter = null;
        f.NodeClickCommand = new ParameterCommand(p => parameter = p);
        texts = Find<TextBlock>(Host(f, "table")).ToList();
        await Task.Delay(600);
        await Click(Row(1));
        Check("commandes: par défaut, le paramètre reste l'élément", parameter is Node { Id: "table" }, $"{parameter?.GetType().Name}");
        f.CommandsReceiveEventArgs = true;
        await Task.Delay(600);
        await Click(Row(2));
        Check("commandes: avec CommandsReceiveEventArgs, la commande reçoit la colonne",
            parameter is ElementEventArgs { Id: "table", TargetData: "client_id" }, $"{parameter?.GetType().Name}");

        f.CommandsReceiveEventArgs = false;
        f.NodeClickCommand = null;
        f.NodeClick -= onClick;
        f.NodeDoubleClick -= onDouble;
        f.NodeContextMenuRequested -= onMenu;
        f.PaneClick -= onPane;
        f.Nodes = saved;
        f.FitView();
        await Task.Delay(300);
    }

    private sealed class ParameterCommand(Action<object?> execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute(parameter);
    }

    private static async Task Perf()
    {
        await Tab(6);
        _win.OnLoadMany(null, null);
        await Task.Delay(6000);
        _win.UpdatePerfStatus();
        var visibleCount = _win.CountVisibleNodes();
        Check("virtualisation: seuls les nodes visibles sont rendus", visibleCount > 0 && visibleCount < 1000,
            $"{_win.Perf.Nodes.Count} nodes, {visibleCount} rendus");
        await Shot("perf");

        // Après un pan, de nouveaux nodes sont rendus.
        var f = _win.Perf;
        f.Viewport = new Viewport(-3000, -1500, 0.6);
        await Task.Delay(1500);
        var after = _win.CountVisibleNodes();
        Check("virtualisation: le pan affiche d'autres nodes", after > 0 && after < 1000, $"{after} rendus");
        var someMeasured = f.Nodes.Count(n => n.Rect != null);
        Check("virtualisation: les nodes entrés dans la vue sont mesurés", someMeasured > 0, $"{someMeasured} mesurés");
    }

    private static async Task Customization()
    {
        await Tab(7);
        var view = (CustomizationExample)((TabItem)_win.TabsControl.Items[7]).Content;
        var f = view.Flow;
        await Task.Delay(800);
        await Shot("custom-initial");

        var hosts = Find<NodeHost>(f).ToList();
        Check("perso: le template « default » redéfini par une ressource est utilisé par tous les nodes",
            hosts.Count == 4 && hosts.All(h => Find<Border>(h).Any(b => b.Tag as string == "custom-default")), $"{hosts.Count} nodes");
        var handles = Find<Handle>(f).ToList();
        Check("perso: les handles prennent le gabarit et la taille du style implicite",
            handles.Count == 8 && handles.All(h => h.Template != null && h.Width == 14 && h.ActualWidth > 12), $"{handles.Count} handles");

        var wave = Find<EdgeVisual>(f).First(v => v.EdgeId == "wave");
        Check("perso: le type d'edge « wave » vient d'un IEdgePathProvider en ressource",
            wave.PathData!.Count(c => c == 'L') > 10 && wave.HasCustomEndMarker, $"{wave.PathData!.Count(c => c == 'L')} segments");
        Check("perso: les types intégrés restent dessinés directement, « pulse » par son template",
            Find<EdgeVisual>(f).Count() == 3 && Find<EdgeTemplateVisual>(f).Count() == 1);
        Check("perso: le label d'un type vient du template « wpflow-edge-label-pulse »",
            Find<EdgeLabel>(f).Select(l => l.EdgeId).SequenceEqual(["pulse"]));

        // Clic réel sur chaque tracé personnalisé : il doit sélectionner l'edge (zone de clic, template compris).
        Point OnEdge(Geometry g)
        {
            PathGeometry.CreateFromGeometry(g).GetPointAtFractionLength(0.5, out var p, out _);
            var v = f.Viewport;
            return f.PointToScreen(new Point(p.X * v.Zoom + v.X, p.Y * v.Zoom + v.Y));
        }

        await Click(OnEdge(Geometry.Parse(wave.PathData!)));
        await Task.Delay(250);
        Check("perso: un clic sur l'edge « wave » le sélectionne", f.Edges.First(e => e.Id == "wave").Selected);

        var templated = Find<EdgeTemplateVisual>(f).First();
        await Click(OnEdge(((EdgeRenderContext)templated.Content).Geometry));
        await Task.Delay(250);
        Check("perso: un clic sur l'edge à template (« pulse ») le sélectionne", f.Edges.First(e => e.Id == "pulse").Selected);

        // Connexion à la souris entre deux handles en losange.
        var before = f.Edges.Count;
        var src = Find<Handle>(Find<NodeHost>(f).First(h => h.Node?.Id == "b")).First(h => h.Type == HandleType.Source);
        var dst = Find<Handle>(Find<NodeHost>(f).First(h => h.Node?.Id == "c")).First(h => h.Type == HandleType.Target);
        await Drag(Center(src), (Center(dst).X, Center(dst).Y));
        Check("perso: on connecte avec les handles personnalisés", f.Edges.Count == before + 1, $"{before} -> {f.Edges.Count}");
        await Shot("custom-final");
    }

    private static async Task Mvvm()
    {
        await Tab(8);
        var view = (MvvmExample)((TabItem)_win.TabsControl.Items[8]).Content;
        var f = view.Flow;
        var vm = (MvvmViewModel)view.DataContext;
        await Task.Delay(800);
        Check("mvvm: 3 ViewModels projetés en 3 nodes et 2 edges", f.Nodes.Count == 3 && f.Edges.Count == 2 && vm.Tasks.Count == 3,
            $"{f.Nodes.Count} nodes, {f.Edges.Count} edges");
        await Shot("mvvm-initial");

        // Glisser avec la vraie souris : le ViewModel reçoit la nouvelle position.
        var first = vm.Tasks[0];
        var (x0, y0) = (first.X, first.Y);
        var host = Host(f, "1");
        var start = Screen(host, new Point(4, 4));
        await Drag(start, (start.X + 90, start.Y + 40));
        Check("mvvm: le glisser écrit X/Y dans le ViewModel",
            Math.Abs(first.X - (x0 + 90 / f.Viewport.Zoom)) < 1.5 && Math.Abs(first.Y - (y0 + 40 / f.Viewport.Zoom)) < 1.5,
            $"({x0},{y0}) -> ({first.X:0.0},{first.Y:0.0}), zoom {f.Viewport.Zoom:0.00}, départ écran {start}");
        Check("mvvm: le node glissé apparaît dans SelectedNodes (ObservableCollection du ViewModel)",
            vm.Selection.Count == 1 && ReferenceEquals(vm.Selection[0], first) && vm.Summary.Contains("1 sélectionné"), vm.Summary);

        // Commandes de clic : elles reçoivent le ViewModel, ou la position flow pour le fond.
        await Click(Screen(Host(f, "2"), new Point(4, 4)));
        Check("mvvm: NodeClickCommand reçoit le ViewModel cliqué", vm.Hint.Contains("Binder NodesSource"), vm.Hint);
        await Click(Screen(f, new Point(f.ActualWidth - 60, 60)));
        Check("mvvm: PaneClickCommand reçoit la position flow du clic sur le fond", vm.Hint.Contains("clic sur le fond"), vm.Hint);
        var host3 = Host(f, "3");
        var grab = Screen(host3, new Point(4, 4));
        await Drag(grab, (grab.X + 30, grab.Y + 20));
        Check("mvvm: NodeDragStopCommand est appelée à la fin du déplacement", vm.Hint.Contains("déplacé"), vm.Hint);


        // ViewModel -> vue : commande « Replacer », puis changement de titre.
        vm.ShuffleCommand.Execute(null);
        await Task.Delay(300);
        Check("mvvm: modifier X/Y dans le ViewModel déplace les nodes",
            f.Nodes.Select((n, i) => Math.Abs(n.Position.X - 60 * i) < 0.01).All(b => b), string.Join(" ", f.Nodes.Select(n => n.Position.X)));
        first.Title = "Titre depuis le VM";
        await Task.Delay(300);
        Check("mvvm: le titre du ViewModel s'affiche dans le template",
            Find<TextBox>(Host(f, "1")).First().Text == "Titre depuis le VM");

        // Connexion à la souris : un LinkVm est créé par la fabrique.
        var src = Find<Handle>(Host(f, "2")).First(h => h.Type == HandleType.Source);
        var dst = Find<Handle>(Host(f, "3")).First(h => h.Type == HandleType.Target);
        await Drag(Center(src), (Center(dst).X, Center(dst).Y));
        Check("mvvm: connecter à la souris ajoute un LinkVm", vm.Links.Count == 3 && f.Edges.Count == 3, $"{vm.Links.Count} liens");

        // Ajout / sélection / suppression pilotés par le ViewModel.
        vm.AddCommand.Execute(null);
        await Task.Delay(300);
        Check("mvvm: ajouter un ViewModel crée un node", f.Nodes.Count == 4 && f.Nodes[3].Id == "4");
        vm.Selection.Clear();
        vm.Selection.Add(vm.Tasks[3]);
        await Task.Delay(200);
        Check("mvvm: ajouter à Selection sélectionne le node", f.Nodes[3].Selected && vm.Tasks[3].IsSelected && f.Nodes.Count(n => n.Selected) == 1);
        vm.NodesDeleteCommand.Execute(new List<object> { vm.Tasks[3] });
        await Task.Delay(300);
        Check("mvvm: supprimer côté ViewModel retire le node", f.Nodes.Count == 3 && vm.Tasks.Count == 3);

        // Contrôleur remis par liaison, zoom lié, commandes routées.
        Check("mvvm: le contrôle remet un IFlowController au ViewModel, pas le FlowView", vm.Flow != null && vm.Flow is not FlowView);
        vm.Flow!.ZoomTo(1.5);
        await Task.Delay(200);
        Check("mvvm: ZoomTo du contrôleur met à jour Zoom, lié en TwoWay", Math.Abs(f.Viewport.Zoom - 1.5) < 1e-6 && Math.Abs(vm.Zoom - 1.5) < 1e-6,
            $"viewport {f.Viewport.Zoom:0.00}, vm {vm.Zoom:0.00}");
        vm.Zoom = 0.8;
        await Task.Delay(200);
        Check("mvvm: écrire Zoom dans le ViewModel zoome la vue", Math.Abs(f.Viewport.Zoom - 0.8) < 1e-6, $"{f.Viewport.Zoom:0.00}");
        vm.FitCommand.Execute(null);
        await Task.Delay(300);
        Check("mvvm: Cadrer (via le contrôleur) change le zoom et le ViewModel suit", Math.Abs(vm.Zoom - f.Viewport.Zoom) < 1e-6);

        // Connexion : le CanExecute de ConnectCommand est le validateur (règle métier : jamais vers la tâche 1).
        var src3 = Find<Handle>(Host(f, "3")).First(h => h.Type == HandleType.Source);
        var dst1 = Find<Handle>(Host(f, "1")).First(h => h.Type == HandleType.Target);
        var linksBefore = vm.Links.Count;
        await Drag(Center(src3), (Center(dst1).X, Center(dst1).Y));
        Check("mvvm: ConnectCommand.CanExecute faux refuse la connexion", vm.Links.Count == linksBefore && f.Edges.Count == linksBefore,
            $"{linksBefore} -> {vm.Links.Count}");

        var dst2 = Find<Handle>(Host(f, "2")).First(h => h.Type == HandleType.Target);
        var connecting = false;
        await DragHolding(Center(src3), (Center(dst2).X, Center(dst2).Y), () =>
        {
            connecting = f.IsConnecting;
            return Task.CompletedTask;
        });
        Check("mvvm: IsConnecting est vrai pendant le tracé, faux ensuite", connecting && !f.IsConnecting);
        Check("mvvm: ConnectCommand ajoute le lien (et la vue n'en crée pas en double)", vm.Links.Count == linksBefore + 1 && f.Edges.Count == linksBefore + 1 && vm.Hint.Contains("lien 3"),
            $"{vm.Links.Count} liens, {vm.Hint}");

        Check("mvvm: la commande routée Annuler est disponible", FlowCommands.Undo.CanExecute(null, f) && f.CanUndo);
        FlowCommands.Undo.Execute(null, f);
        await Task.Delay(300);
        Check("mvvm: Annuler (commande routée) retire le lien du ViewModel", vm.Links.Count == linksBefore, $"{vm.Links.Count} liens");

        // Touche Suppr : la vue retire le ViewModel de la collection.
        await Click(Screen(Host(f, "2"), new Point(4, 4)));
        Key(VkDelete);
        await Task.Delay(300);
        Check("mvvm: Suppr dans la vue retire le ViewModel et ses liens", vm.Tasks.Count == 2 && vm.Links.All(l => l.Source != "2" && l.Target != "2"),
            $"{vm.Tasks.Count} tâches, {vm.Links.Count} liens");
        await Shot("mvvm-final");
    }

    // ---------------- outils ----------------

    private static async Task ConnectSources(FlowView f, string from, string to)
    {
        var a = Find<Handle>(Host(f, from)).First(h => h.Type == HandleType.Source);
        var b = Find<Handle>(Host(f, to)).First(h => h.Type == HandleType.Source);
        var p1 = Screen(a, new Point(a.ActualWidth / 2, a.ActualHeight / 2));
        var p2 = Screen(b, new Point(b.ActualWidth / 2, b.ActualHeight / 2));
        await Drag(p1, (p2.X, p2.Y));
    }

    private static async Task Tab(int index)
    {
        _win.TabsControl.SelectedIndex = index;
        await Task.Delay(900);
    }

    private static async Task Shot(string name)
    {
        await Task.Delay(300);
        _win.Capture($"{_base}-{name}.png");
    }

    private static NodeHost Host(FlowView f, string id) => Find<NodeHost>(f).First(h => h.Node?.Id == id);

    private static XYPosition Abs(FlowView f, string id) => f.AbsoluteNodes.First(n => n.Id == id).Position;
}
