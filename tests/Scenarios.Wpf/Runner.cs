using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using demo;
using Wpflow.Core;
using Wpflow.Wpf;
using Rect = System.Windows.Rect;
using static demo.Sim;

namespace Scenarios.Wpf;

/// <summary>
///     Les scénarios de l'éditeur de schéma, à la vraie souris et au vrai clavier : les mêmes lignes que les scénarios Blazor.
///     Chaque scénario repart d'un schéma neuf ; une exception n'arrête que lui.
/// </summary>
internal static class Runner
{
    private static MainWindow _window = null!;
    private static Action<string> _log = _ => { };

    private static FlowView F => _window.View;
    private static SchemaStore Store => _window.Store;

    private static void Check(string name, bool ok, string detail = "") => _log(SelfTestReport.Line(name, ok, detail));

    public static async Task Run(MainWindow window, Action<string> log)
    {
        (_window, _log) = (window, log);
        var scenarios = new (string Name, Func<Task> Body)[]
        {
            ("chargement", Loading), ("élément visé", Targets), ("colonnes", Columns), ("renommage", Renaming),
            ("menus", Menus), ("relations", Relations), ("connexions", Connections), ("contrôlé", Controlled),
            ("rechargement", Reload), ("json", Json), ("mises à jour de fond", Background), ("contrôles WPF", Widgets),
            ("palette", DragFromPalette), ("200 tables", () => Large(false)), ("200 tables virtualisées", () => Large(true)),
            ("mvvm", Mvvm)
        };
        var only = Environment.GetEnvironmentVariable("WPFLOW_SCENARIO");
        foreach (var (name, body) in scenarios.Where(s => string.IsNullOrEmpty(only) || s.Name == only))
        {
            log("INFO  == " + name);
            try
            {
                await Fresh();
                await body();
            }
            catch (Exception ex)
            {
                Check(name + " : le scénario va au bout", false, ex.Message);
            }

            Check(name + " : aucune exception non gérée", _window.Errors.Count == 0, _window.Errors.FirstOrDefault() ?? "");
            _window.Errors.Clear();
        }
    }

    // ---------------- outils ----------------

    private static async Task Fresh()
    {
        Key(VkEscape);
        F.OnlyRenderVisibleElements = false;
        _window.Open(Schemas.Shop());
        await Task.Delay(700);
        F.FitView();
        await Task.Delay(500);
        _window.SelectionMenus.Clear();
    }

    private static NodeHost Host(string id) => Find<NodeHost>(F).First(h => h.Node?.Id == id);

    private static Grid Row(string table, string column) => Find<Grid>(Host(table)).First(g => g.Tag as string == column);

    private static Handle HandleOf(string table, string column, HandleType type) =>
        Find<Handle>(Row(table, column)).First(h => h.Type == type);

    private static T Tagged<T>(string table, string tag) where T : FrameworkElement => Find<T>(Host(table)).First(e => e.Tag as string == tag);

    private static Point Title(string table) => Screen(Tagged<Border>(table, "title"), new Point(60, 12));

    private static XYPosition Position(string id) => Store.Nodes.First(n => n.Id == id).Position;

    private static Point Offset(Point p, double dx, double dy) => new(p.X + dx, p.Y + dy);

    private static Task DragTo(Point from, Point to) => Drag(from, (to.X, to.Y));

    private static Point Empty() => Screen(F, new Point(F.ActualWidth - 30, 30));

    /// <summary>
    ///     L'extrémité d'edge que la vue calcule pour une colonne (handles mesurés) coïncide avec le handle affiché : sinon les
    ///     edges sont dessinés à l'ancienne place. On compare les handles mesurés par la vue à leur position réelle.
    /// </summary>
    private static (bool Ok, string Detail) Measured(string table, string column, HandleType type)
    {
        var host = Host(table);
        var handle = HandleOf(table, column, type);
        var real = handle.TransformToAncestor(host).Transform(new Point(handle.ActualWidth / 2, handle.ActualHeight / 2));
        var known = host.Handles.FirstOrDefault(h => h.Id == column && h.Type == type);
        if (known == null) return (false, $"{table}.{column} : handle inconnu de la vue");
        var (x, y) = (known.X + known.Width / 2, known.Y + known.Height / 2);
        return (Math.Abs(x - real.X) < 1.5 && Math.Abs(y - real.Y) < 1.5, $"{table}.{column} : mesuré ({x:0},{y:0}), affiché ({real.X:0},{real.Y:0})");
    }

    private static void CheckAttached(string name)
    {
        var bad = Store.Edges.SelectMany(e => new[]
        {
            Measured(e.Source, e.SourceHandle!, HandleType.Source), Measured(e.Target, e.TargetHandle!, HandleType.Target)
        }).Where(r => !r.Ok).ToList();
        Check(name, bad.Count == 0, string.Join(" ; ", bad.Select(b => b.Detail)));
    }

    // ---------------- scénarios ----------------

    private static Task Loading()
    {
        Check("chargement : 4 tables et 2 relations affichées", Find<NodeHost>(F).Count() == 4 && Store.Edges.Count == 2);
        CheckAttached("chargement : les edges partent de leurs colonnes");
        return Task.CompletedTask;
    }

    private static async Task Targets()
    {
        await Click(Center(Row("orders", "note")));
        Check("clic : la colonne est connue", _window.LogText == "clic orders.note", _window.LogText);
        await Task.Delay(600);
        await RightClick(Center(Row("orders", "customer_id")));
        Check("menu contextuel : la colonne est connue", _window.MenuText == "colonne orders.customer_id", _window.MenuText);
        var zoom = F.Viewport.Zoom;
        _window.DoubleClicks.Clear();
        await DoubleClick(Center(Row("customers", "email")));
        // En WPF, NodeClick suit le double-clic (au second relâchement) : les deux sont enregistrés à part.
        Check("double-clic : la colonne est connue, une fois, sans zoom",
            _window.DoubleClicks.SequenceEqual(["customers.email"]) && F.Viewport.Zoom == zoom,
            string.Join(", ", _window.DoubleClicks));
        Key(VkEscape);
    }

    private static async Task Columns()
    {
        Store.MoveColumn("customers", "id", 2);
        await Task.Delay(500);
        CheckAttached("colonnes réordonnées : les edges suivent");
        Store.AddColumn("customers", new Column("added", "int"), 0);
        await Task.Delay(500);
        CheckAttached("colonne ajoutée : les edges suivent");
        Store.RemoveColumn("customers", "name");
        await Task.Delay(500);
        CheckAttached("colonne retirée : les edges suivent");
        Store.ToggleCollapsed("customers");
        await Task.Delay(500);
        CheckAttached("table repliée : les edges suivent");
        Store.ToggleCollapsed("customers");
        await Task.Delay(500);
        CheckAttached("table dépliée : les edges suivent");

        // Des handles déplacés SANS changement de taille du node ni rechargement (déclencheur de style, par exemple).
        Store.Apply([new NodeResizeChange("orders", 222, 150)]);
        await Task.Delay(500);
        var first = Row("orders", "id");
        var second = Row("orders", "customer_id");
        var size = Host("orders").RenderSize;
        first.Margin = new Thickness(0, 20, 0, 0);
        await Task.Delay(600);
        F.UpdateNodeInternals("orders");
        await Task.Delay(400);
        var r = Measured("orders", "customer_id", HandleType.Target);
        Check("handles déplacés sans changement de taille : UpdateNodeInternals les remesure", r.Ok && Host("orders").RenderSize == size,
            r.Detail + $" (taille {size} -> {Host("orders").RenderSize}, ligne {second.ActualHeight:0})");
        CheckAttached("après UpdateNodeInternals : les edges suivent");
    }

    private static async Task Renaming()
    {
        var position = Position("customers");
        await Click(Title("customers")); // le node est sélectionné : les flèches du graphe le déplaceraient
        await Task.Delay(600);
        await DoubleClick(Center(Row("customers", "id")));
        await Task.Delay(300);
        var box = Find<TextBox>(Row("customers", "id")).First();
        Check("renommage : le champ a le focus", box.IsKeyboardFocused, $"{System.Windows.Input.Keyboard.FocusedElement}");

        foreach (var c in "ABCD") Key((byte)c);
        Key(VkLeft);
        Key(VkLeft);
        Key(VkDelete);
        Key(VkBack);
        Key(VkSpace);
        await Task.Delay(300);
        Check("renommage : saisie, flèches, Suppr, Retour et Espace restent dans le champ", box.Text == "a d", $"« {box.Text} »");
        Key(VkUp);
        Key(VkDown);
        await Task.Delay(300);
        Check("renommage : flèches haut et bas ne déplacent pas le node", Position("customers") == position, $"{position} -> {Position("customers")}");
        Key(VkA, ctrl: true);
        await Task.Delay(150);
        Check("renommage : Ctrl+A sélectionne le texte du champ", box.SelectedText == box.Text && Store.Nodes.Count(n => n.Selected) <= 1,
            $"« {box.SelectedText} », {Store.Nodes.Count(n => n.Selected)} nodes sélectionnés");
        Key(VkDelete);
        await Task.Delay(200);
        for (var i = 0; i < 12; i++) Key(VkZ, ctrl: true); // y compris au-delà de l'historique du champ
        await Task.Delay(300);
        Check("renommage : Suppr et Ctrl+Z ne touchent pas le graphe", Store.Nodes.Count == 4 && Store.Edges.Count == 2 && Position("customers") == position,
            $"{Store.Nodes.Count} nodes, {Store.Edges.Count} edges");

        box.Text = "";
        foreach (var c in "UID") Key((byte)c);
        Key(VkReturn);
        await Task.Delay(600);
        Check("renommage : Entrée valide, la relation suit le nouveau nom",
            Store.Table("customers").Columns[0].Name == "uid" && Store.Edges.First(e => e.Id == "r-orders").SourceHandle == "uid",
            Store.Table("customers").Columns[0].Name);
        CheckAttached("renommage : les edges restent accrochés");
    }

    private static async Task Menus()
    {
        var label = Find<Button>(F).First(b => b.Tag as string == "r-orders");
        await Click(Center(label));
        Check("label de relation cliquable", _window.LogText == "label r-orders", _window.LogText);

        await RightClick(Empty());
        Check("menu du fond", _window.MenuText == "fond", _window.MenuText);

        await Click(Title("orders"));
        KeyDown(VkControl);
        await Task.Delay(200);
        await Click(Title("lines"));
        KeyRelease(VkControl);
        await Task.Delay(400);
        await RightClick(Title("lines"));
        Check("menu d'une sélection", _window.SelectionMenus.LastOrDefault() == "sélection de 2",
            $"{_window.SelectionMenus.LastOrDefault()}, sélection={string.Join(",", Store.Nodes.Where(n => n.Selected).Select(n => n.Id))}");
        Check("clic droit : la sélection est gardée", Store.Nodes.Count(n => n.Selected) == 2);
        Key(VkEscape);
        await Task.Delay(300);

        // La relation r-lines (angles droits) part horizontalement du handle source : on vise ce premier segment.
        var start = Center(HandleOf("orders", "id", HandleType.Source));
        await RightClick(Offset(start, 18 * F.Viewport.Zoom, 0));
        Check("menu d'une relation", _window.MenuText == "relation r-lines", _window.MenuText);

        // Un ContextMenu WPF posé dans le modèle : il s'ouvre, son entrée agit, le node ne bouge pas.
        var title = Tagged<Border>("lines", "title");
        var position = Position("lines");
        await RightClick(Title("lines"));
        Check("menu d'une table", _window.MenuText == "table lines", _window.MenuText);
        var menu = title.ContextMenu!;
        Check("ContextMenu du modèle : ouvert", menu.IsOpen);
        if (menu.IsOpen)
        {
            var item = (MenuItem)menu.Items[0];
            var before = _window.TemplateMenuClicks;
            await Click(Center(item));
            Check("ContextMenu du modèle : l'entrée agit sans déplacer le node",
                _window.TemplateMenuClicks == before + 1 && Position("lines") == position, $"{_window.TemplateMenuClicks - before} clic(s)");
        }

        MoveTo(Center(Row("lines", "label")));
        await Task.Delay(1500);
        Check("ToolTip du modèle : affiché au survol",
            PresentationSource.CurrentSources.OfType<PresentationSource>().Any(s => s.RootVisual is FrameworkElement root && Find<ToolTip>(root).Any(t => t.IsVisible)));
    }

    private static Task Relations()
    {
        var visuals = Find<FrameworkElement>(F).Where(e => e.GetType().Name == "EdgeVisual").ToList();
        Check("relations : un visuel par edge", visuals.Count == Store.Edges.Count, $"{visuals.Count}");
        var provider = new RelationPath();
        string Path(string cardinality) => provider.GetPath(new EdgeGeometry(new XYPosition(0, 0), Wpflow.Core.Position.Right,
            new XYPosition(200, 100), Wpflow.Core.Position.Left) { Edge = Schemas.RelationEdge("e", "a", "x", "b", "y", cardinality) }).Path;
        Check("relations : le tracé dépend de la cardinalité", Path("1-n") != Path("1-1") && Path("1-1") != Path("n-n"));
        Check("relations : le fournisseur de l'application est celui de la ressource", F.TryFindResource("wpflow-edge-relation") is RelationPath
            || _window.TryFindResource("wpflow-edge-relation") is RelationPath);
        return Task.CompletedTask;
    }

    private static async Task Connections()
    {
        await DragTo(Center(HandleOf("customers", "name", HandleType.Source)), Center(HandleOf("lines", "id", HandleType.Target)));
        await Task.Delay(300);
        Check("connexion text -> int refusée", Store.Edges.Count == 2, $"{Store.Edges.Count} edges");
        await DragTo(Center(HandleOf("customers", "id", HandleType.Source)), Center(HandleOf("lines", "id", HandleType.Target)));
        await Task.Delay(500);
        Check("connexion int -> int acceptée", Store.Edges.Count == 3, $"{Store.Edges.Count} edges");
        CheckAttached("connexion : le nouvel edge est accroché");
    }

    private static async Task Controlled()
    {
        var locked = Position("audit");
        var free = Position("orders");
        var from = Title("audit");
        await DragTo(from, Offset(from, 90, 40));
        Check("contrôlé : le service refuse de déplacer la table verrouillée", Position("audit") == locked && Store.Refused.Contains("déplacement de audit"),
            string.Join(", ", Store.Refused));
        await Task.Delay(600);
        await Click(Title("audit"));
        Key(VkDelete);
        await Task.Delay(400);
        Check("contrôlé : le service refuse de la supprimer", Store.Nodes.Count == 4);
        from = Title("orders");
        await DragTo(from, Offset(from, 60, 30));
        await Task.Delay(300);
        Check("contrôlé : saisir une autre table la déplace, elle seule", Position("orders") != free && Position("audit") == locked,
            $"orders {free} -> {Position("orders")}, audit {locked} -> {Position("audit")}");
    }

    private static async Task Reload()
    {
        var from = Title("orders");
        await DragTo(from, Offset(from, 60, 30));
        _window.Open(Schemas.Library());
        await Task.Delay(900);
        var hosts = Find<NodeHost>(F).ToList();
        var inside = hosts.All(h =>
        {
            var r = h.TransformToAncestor(F).TransformBounds(new Rect(0, 0, h.ActualWidth, h.ActualHeight));
            return r.Left >= -1 && r.Top >= -1 && r.Right <= F.ActualWidth + 1 && r.Bottom <= F.ActualHeight + 1;
        });
        Check("rechargement : le nouveau schéma est affiché et cadré", hosts.Count == 2 && inside, $"{hosts.Count} nodes");
        Check("rechargement : aucune sélection", Store.Nodes.All(n => !n.Selected));
        F.Undo();
        await Task.Delay(400);
        Check("rechargement : annuler ne ramène pas l'ancien schéma", Store.Nodes.Count == 2 && Find<NodeHost>(F).Count() == 2);
        CheckAttached("rechargement : les edges sont accrochés");
    }

    private static async Task Json()
    {
        var json = Schemas.ToJson(Store.Nodes, Store.Edges, F.Viewport);
        _window.Open(Schemas.Library());
        await Task.Delay(600);
        _window.Open(Schemas.FromJson(json));
        await Task.Delay(900);
        Check("JSON : le schéma revient avec des données typées", Store.Nodes.Count == 4 && Store.Nodes.All(n => n.Data is Table)
            && Store.Edges.All(e => e.Data is Relation));
        CheckAttached("JSON : les edges sont accrochés");
        await Click(Center(Row("orders", "note")));
        Check("JSON : la colonne reste identifiable", _window.LogText == "clic orders.note", _window.LogText);
    }

    private static async Task Background()
    {
        var tick = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        timer.Tick += (_, _) => Store.Tick(++tick % 2 == 0 ? "customers" : "orders");
        timer.Start();
        try
        {
            var zoom = F.Viewport.Zoom;
            var start = Position("orders");
            var from = Title("orders");
            await Drag(from, (from.X + 120, from.Y + 60), steps: 40);
            var end = Position("orders");
            var scale = VisualTreeHelper.GetDpi(F).DpiScaleX;
            Check("mises à jour de fond : le glisser arrive au bon endroit",
                Math.Abs(end.X - start.X - 120 / scale / zoom) < 3 && Math.Abs(end.Y - start.Y - 60 / scale / zoom) < 3,
                $"{start} -> {end}, zoom {zoom:0.00}, échelle {scale}");
            await Drag(Center(HandleOf("customers", "id", HandleType.Source)),
                (Center(HandleOf("lines", "id", HandleType.Target)).X, Center(HandleOf("lines", "id", HandleType.Target)).Y), steps: 40);
            await Task.Delay(300);
            Check("mises à jour de fond : la connexion aboutit", Store.Edges.Count == 3 && tick > 20, $"{Store.Edges.Count} edges, {tick} mises à jour");
        }
        finally
        {
            timer.Stop();
        }
    }

    private static async Task Widgets()
    {
        // La table verrouillée porte les contrôles ; on la cadre seule pour qu'ils soient tous à l'écran.
        F.Viewport = new Viewport(-380 + 200, -300 + 20, 1);
        await Task.Delay(500);
        var position = Position("audit");
        var viewport = F.Viewport;
        bool Still() => Position("audit") == position && F.Viewport == viewport;

        var button = Tagged<Button>("audit", "w-button");
        await Click(Center(button));
        Check("Button : le clic agit", (string)button.Content == "clics 1" && Still(), $"{button.Content}");

        var check = Tagged<CheckBox>("audit", "w-check");
        await Click(Screen(check, new Point(8, check.ActualHeight / 2)));
        Check("CheckBox : cochée", check.IsChecked == true && Still());

        var combo = Tagged<ComboBox>("audit", "w-combo");
        await Click(Center(combo));
        await Task.Delay(500);
        var open = combo.IsDropDownOpen;
        if (open) await Click(Center((ComboBoxItem)combo.Items[1]));
        await Task.Delay(300);
        Check("ComboBox : le menu s'ouvre, le choix s'applique, le graphe ne bouge pas", open && combo.SelectedIndex == 1 && !combo.IsDropDownOpen && Still(),
            $"ouvert={open}, index={combo.SelectedIndex}");

        var text = Tagged<TextBox>("audit", "w-text");
        await Click(Screen(text, new Point(text.ActualWidth - 6, text.ActualHeight / 2)));
        Key(VkA, ctrl: true);
        Key((byte)'Z');
        Key(VkSpace);
        Key((byte)'Z');
        Key(VkLeft);
        await Task.Delay(300);
        Check("TextBox : saisie et focus gardés", text.IsKeyboardFocused && text.Text == "z z" && Still(), $"« {text.Text} »");
        await Drag(Screen(text, new Point(4, text.ActualHeight / 2)), (Screen(text, new Point(40, 0)).X, Screen(text, new Point(0, text.ActualHeight / 2)).Y));
        Check("TextBox : glisser sélectionne du texte sans déplacer le node", text.SelectionLength > 0 && Still(), $"{text.SelectionLength} caractère(s)");

        var list = Tagged<ListBox>("audit", "w-list");
        await Click(Center((ListBoxItem)list.Items[0]));
        var scroll = Find<ScrollViewer>(list).First();
        MoveTo(Center(list));
        await Task.Delay(150);
        Wheel(-240);
        await Task.Delay(400);
        Check("ListBox : sélection et molette gardées", list.SelectedIndex >= 0 && scroll.VerticalOffset > 0 && Still(),
            $"index {list.SelectedIndex}, défilement {scroll.VerticalOffset}, vue {viewport} -> {F.Viewport}");

        var viewer = Tagged<ScrollViewer>("audit", "w-scroll");
        MoveTo(Center(viewer));
        await Task.Delay(150);
        Wheel(-240);
        await Task.Delay(400);
        Check("ScrollViewer : la molette défile sans zoomer", viewer.VerticalOffset > 0 && Still(), $"défilement {viewer.VerticalOffset}");

        var grid = Tagged<DataGrid>("audit", "w-grid");
        grid.ItemsSource = new ObservableCollection<Column>(Enumerable.Range(0, 8).Select(i => new Column($"ligne{i}", "int")));
        await Task.Delay(500);
        var cell = Find<DataGridCell>(grid).First();
        await Click(Center(cell));
        var gridScroll = Find<ScrollViewer>(grid).First();
        MoveTo(Center(grid));
        await Task.Delay(150);
        Wheel(-240);
        await Task.Delay(400);
        Check("DataGrid : sélection et molette gardées", grid.SelectedIndex >= 0 && gridScroll.VerticalOffset > 0 && Still(),
            $"index {grid.SelectedIndex}, défilement {gridScroll.VerticalOffset}");
    }

    private static async Task DragFromPalette()
    {
        var item = (ListBoxItem)_window.Tables.Items[0];
        var target = Screen(F, new Point(80, F.ActualHeight - 80));
        await Drag(Center(item), (target.X, target.Y), steps: 25, delay: 30);
        if (Store.Nodes.Count == 4)
        {
            await Task.Delay(500);
            await Drag(Center(item), (target.X, target.Y), steps: 25, delay: 40);
        }

        await Task.Delay(500);
        var added = Store.Nodes.Skip(4).Select(n => n.Id).ToList();
        var hosts = Find<NodeHost>(F).Select(h => h.Node?.Id).ToList();
        Check("palette : le dépôt ajoute la table", added.SequenceEqual(["invoices"]) && hosts.Contains("invoices"),
            $"ajoutées : {string.Join(",", added)} ; affichées : {string.Join(",", hosts)}");
    }

    private static async Task Large(bool virtualize)
    {
        F.OnlyRenderVisibleElements = virtualize;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        Store.Load(Schemas.Large());
        F.Viewport = new Viewport(20, 20, 0.5);
        await _window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await Task.Delay(1500);
        var shown = Find<NodeHost>(F).Count();
        var name = virtualize ? "200 tables virtualisées" : "200 tables";
        _log($"INFO  {name} : {shown} nodes créés, {clock.ElapsedMilliseconds} ms (attente comprise)");
        Check(name + " : affichées", virtualize ? shown is > 0 and < 200 : shown == 200, $"{shown}");
        // Loin des bords : sinon le défilement automatique ajoute son propre déplacement.
        F.Viewport = new Viewport(150, 150, 0.5);
        await Task.Delay(1000);
        var before = Position("t0");
        var from = Title("t0");
        await DragTo(from, Offset(from, 50, 30));
        await Task.Delay(500);
        var scale = VisualTreeHelper.GetDpi(F).DpiScaleX;
        var after = Position("t0");
        Check(name + " : un glisser déplace la table de la bonne distance",
            Math.Abs(after.X - before.X - 100 / scale) < 4 && Math.Abs(after.Y - before.Y - 60 / scale) < 4, $"{before} -> {after}");
    }

    private static async Task Mvvm()
    {
        var tables = new ObservableCollection<TableViewModel>
        {
            new("customers", 0, 0, [new("id", "int", true), new("name", "text"), new("email", "text")]),
            new("orders", 380, 60, [new("id", "int", true), new("customer_id", "int")])
        };
        var links = new ObservableCollection<RelationViewModel> { new("r", "customers", "id", "orders", "customer_id") };
        var window = new Window { Width = 900, Height = 600, Topmost = true, Resources = _window.Resources, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        var view = new FlowView
        {
            FitViewOnInit = true,
            NodeMapping = new NodeMapping { SelectedPath = "IsSelected", TypePath = "Type" },
            EdgeMapping = new EdgeMapping { SourceHandlePath = "SourceColumn", TargetHandlePath = "TargetColumn", TypePath = "Type" }
        };
        view.NodesSource = tables;
        view.EdgesSource = links;
        window.Content = view;
        window.Show();
        try
        {
            await Task.Delay(1200);
            NodeHost Of(string id) => Find<NodeHost>(view).First(h => h.Node?.Id == id);
            (bool, string) Customer()
            {
                var host = Of("customers");
                var row = Find<Grid>(host).First(g => g.Tag as string == "id");
                var handle = Find<Handle>(row).First(h => h.Type == HandleType.Source);
                var real = handle.TransformToAncestor(host).Transform(new Point(handle.ActualWidth / 2, handle.ActualHeight / 2));
                var known = host.Handles.FirstOrDefault(h => h.Id == "id" && h.Type == HandleType.Source);
                var y = known == null ? double.NaN : known.Y + known.Height / 2;
                return (Math.Abs(y - real.Y) < 1.5, $"mesuré {y:0}, affiché {real.Y:0}");
            }

            tables[0].Columns.Move(0, 2); // même taille de node, mêmes conteneurs
            await Task.Delay(600);
            var (moved, detail) = Customer();
            Check("MVVM : colonnes réordonnées (ObservableCollection.Move), les edges suivent", moved, detail);

            Exception? failure = null;
            await Task.Run(() =>
            {
                try
                {
                    window.Dispatcher.Invoke(() =>
                    {
                        tables[0].Columns.Insert(0, new Column("from_thread", "int"));
                        tables[0].Rows = 7;
                    });
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            await Task.Delay(600);
            var (followed, detail2) = Customer();
            Check("MVVM : une modification d'un autre thread, renvoyée sur le thread d'interface, est suivie", failure == null && followed, failure?.Message ?? detail2);

            var title = Find<Border>(Of("orders")).First(b => b.Tag as string == "title");
            var from = Screen(title, new Point(60, 12));
            var x = tables[1].X;
            await Drag(from, (from.X + 60, from.Y + 30));
            Check("MVVM : un glisser écrit dans le ViewModel", tables[1].X > x + 20, $"{x} -> {tables[1].X:0}");
        }
        finally
        {
            window.Close();
        }
    }
}
