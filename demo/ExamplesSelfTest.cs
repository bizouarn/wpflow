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

    private static void Check(string name, bool ok, string detail = "") =>
        _log($"{(ok ? "OK    " : "ECHEC ")} {name}{(detail.Length > 0 ? " — " + detail : "")}");

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
            overridden == Color.FromRgb(0xff, 0x00, 0x71) && inherited == Colors.White, $"Flow.Edge={overridden}, Flow.Background={inherited}");

        await Groups();
        await Tools();
        await Palette();
        await Layout();
        await Options();
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
        if (f.Nodes.Count != 2)
        {
            // Le glisser-déposer OLE dépend du focus de la fenêtre : un second essai écarte les faux négatifs.
            await Task.Delay(500);
            await Drag(from, (to.X, to.Y), steps: 25, delay: 40);
        }

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
