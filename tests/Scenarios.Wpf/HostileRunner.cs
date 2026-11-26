using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using demo;
using Wpflow.Core;
using Wpflow.Wpf;
using Rect = System.Windows.Rect;
using static demo.Sim;

namespace Scenarios.Wpf;

/// <summary>
///     Conditions d'intégration hostiles (ROADMAP-TESTS, lot 4), à la vraie souris : les mêmes gestes doivent viser juste
///     quel que soit le conteneur de la vue. WPFLOW_HOSTILE=chemin.log ; WPFLOW_CONDITION=préfixe n'en joue qu'une partie.
/// </summary>
internal static class HostileRunner
{
    private static Action<string> _log = _ => { };
    private static MainWindow _main = null!;
    private static string _condition = "";

    private static void Check(string name, bool ok, string detail = "") => _log(SelfTestReport.Line(_condition + " : " + name, ok, detail));

    public static async Task Run(MainWindow main, Action<string> log)
    {
        _log = log;
        _main = main;
        var conditions = new (string Name, Func<Task> Body)[]
        {
            ("onglet rechargé", TabReload),
            ("onglet jamais affiché", TabNeverShown),
            ("ScrollViewer défilé", Scrolled),
            ("Expander fermé", Collapsed),
            ("Viewbox", InViewbox),
            ("LayoutTransform", () => Transformed(layout: true, 0.6)),
            ("RenderTransform", () => Transformed(layout: false, 1.3)),
            ("Popup", InPopup),
            ("fenêtre secondaire", SecondaryWindow),
            ("rendu logiciel", SoftwareRendering),
            ("deux vues", TwoViews),
            ("styles implicites", ImplicitStyles),
            ("culture fr-FR", French),
            ("exception au début d'un glisser", () => Throwing("début")),
            ("exception à la fin d'un glisser", () => Throwing("fin")),
            ("exception au clic", () => Throwing("clic")),
            ("exception à la connexion demandée", () => Throwing("connexion")),
            ("exception au début d'une connexion", () => Throwing("départ")),
            ("cinquante fenêtres", FiftyWindows)
        };

        var only = Environment.GetEnvironmentVariable("WPFLOW_CONDITION");
        main.Hide();
        try
        {
            foreach (var (name, body) in conditions.Where(c => string.IsNullOrEmpty(only) || c.Name.StartsWith(only)))
            {
                _condition = name;
                _log("INFO  == " + name);
                _main.Errors.Clear();
                var windows = Application.Current.Windows.OfType<Window>().ToList();
                try
                {
                    await body();
                    Check("aucune exception non gérée", _main.Errors.Count == 0, _main.Errors.FirstOrDefault() ?? "");
                }
                catch (Exception ex)
                {
                    Check("la condition va au bout", false, ex.ToString().ReplaceLineEndings(" "));
                }
                finally
                {
                    Mouse.Capture(null);
                    foreach (var w in Application.Current.Windows.OfType<Window>().Except(windows).ToList()) w.Close();
                }
            }
        }
        finally
        {
            main.Show();
        }
    }

    // ---------------- outils ----------------

    /// <summary>Une vue et ce que l'application en reçoit.</summary>
    private sealed class Probe
    {
        public FlowView View { get; }
        public string Last = "";

        public Probe(bool chrome = false)
        {
            View = new FlowView
            {
                Nodes =
                [
                    new Node("a", new XYPosition(0, 0), "input", Data: "A"),
                    new Node("b", new XYPosition(0, 150), Data: "B"),
                    new Node("c", new XYPosition(250, 80), "output", Data: "C")
                ],
                Edges = [new Edge("e", "a", "b")],
                MaxZoom = 1,
                FitViewOnInit = true
            };
            if (chrome)
            {
                var overlay = new Grid();
                overlay.Children.Add(new FlowControls { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom });
                overlay.Children.Add(new MiniMap
                {
                    Width = 160, Height = 110, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom
                });
                View.Content = overlay;
            }

            View.NodeClick += (_, e) => Last = "clic " + e.Id;
            View.NodeDragStopped += (_, e) => Last = "glissé " + string.Join(",", e.NodeIds);
        }

        public NodeHost Host(string id) => Find<NodeHost>(View).First(h => h.Node?.Id == id);

        public Handle Handle(string id, HandleType type) => Find<Handle>(Host(id)).First(h => h.Type == type);

        public XYPosition Position(string id) => View.Nodes.First(n => n.Id == id).Position;

        public Point Pane() => View.PointToScreen(new Point(View.ActualWidth * 0.12, View.ActualHeight * 0.15));

        /// <summary>Pixels physiques de l'écran par unité de la vue : transformations des parents et échelle d'affichage comprises.</summary>
        public double Scale() => (View.PointToScreen(new Point(100, 0)).X - View.PointToScreen(new Point(0, 0)).X) / 100;

        public XYPosition FlowAt(Point screen)
        {
            var local = View.PointFromScreen(screen);
            var v = View.Viewport;
            return new XYPosition((local.X - v.X) / v.Zoom, (local.Y - v.Y) / v.Zoom);
        }
    }

    private static Window Show(UIElement content, double width = 1100, double height = 700, Window? owner = null)
    {
        var window = new Window
        {
            Title = "hostile " + _condition, Content = content, Width = width, Height = height, Left = 40, Top = 40,
            WindowStartupLocation = WindowStartupLocation.Manual, Topmost = true, Owner = owner
        };
        window.Show();
        window.Activate();
        return window;
    }

    private static Task Settle(int ms = 700) => Task.Delay(ms);

    private static bool Close(double a, double b, double tolerance = 2) => Math.Abs(a - b) <= tolerance;

    private static string Text(XYPosition p) => string.Create(CultureInfo.InvariantCulture, $"({p.X:0.#}, {p.Y:0.#})");

    /// <summary>Clic, glisser d'un node, pan, molette et connexion visent juste.</summary>
    private static async Task AimRight(Probe p, string when = "")
    {
        var f = p.View;
        p.Last = "";
        await Click(Center(p.Host("b")));
        Check(when + "le clic atteint le node visé", p.Last == "clic b", p.Last);
        await Task.Delay(600); // le clic suivant ne doit pas faire un double-clic

        var per = p.Scale() * f.Viewport.Zoom;
        var before = p.Position("b");
        var from = Center(p.Host("b"));
        await Drag(from, (from.X + 80, from.Y + 40));
        await Task.Delay(200);
        var after = p.Position("b");
        Check(when + "le node suit la souris", Close(after.X, before.X + 80 / per, 3) && Close(after.Y, before.Y + 40 / per, 3),
            $"attendu {Text(new XYPosition(before.X + 80 / per, before.Y + 40 / per))}, obtenu {Text(after)}");

        var scale = p.Scale();
        var viewport = f.Viewport;
        var pane = p.Pane();
        await Drag(pane, (pane.X + 40, pane.Y + 24));
        await Task.Delay(200);
        Check(when + "le fond suit la souris",
            Close(f.Viewport.X, viewport.X + 40 / scale, 3) && Close(f.Viewport.Y, viewport.Y + 24 / scale, 3),
            $"attendu {Text(new XYPosition(viewport.X + 40 / scale, viewport.Y + 24 / scale))}, obtenu {Text(new XYPosition(f.Viewport.X, f.Viewport.Y))}");

        var anchor = Center(p.Host("a"));
        MoveTo(anchor);
        await Task.Delay(200);
        var under = p.FlowAt(anchor);
        var zoom = f.Viewport.Zoom;
        Wheel(-120);
        await Task.Delay(500);
        var still = p.FlowAt(anchor);
        Check(when + "la molette zoome sous la souris", f.Viewport.Zoom < zoom - 0.01 && Close(still.X, under.X, 3) && Close(still.Y, under.Y, 3),
            $"zoom {zoom:0.##} → {f.Viewport.Zoom:0.##}, point {Text(under)} → {Text(still)}");

        var edges = f.Edges.Count;
        await Drag(Center(p.Handle("a", HandleType.Source)), Tuple(Center(p.Handle("c", HandleType.Target))));
        await Task.Delay(400);
        Check(when + "la connexion se branche", f.Edges.Count == edges + 1 && f.Edges[^1] is { Source: "a", Target: "c" },
            $"{f.Edges.Count} edges");
        Check(when + "aucun geste ne reste en cours", Mouse.Captured == null && !f.Nodes.Any(n => n.Dragging),
            Mouse.Captured?.ToString() ?? "");
    }

    private static (double, double) Tuple(Point p) => (p.X, p.Y);

    private static bool AllVisible(Probe p, out string detail)
    {
        var outside = new List<string>();
        foreach (var id in p.View.Nodes.Select(n => n.Id))
        {
            var host = Find<NodeHost>(p.View).FirstOrDefault(h => h.Node?.Id == id);
            if (host == null || !host.IsVisible)
            {
                outside.Add(id + " absent");
                continue;
            }

            var box = host.TransformToAncestor(p.View).TransformBounds(new Rect(host.RenderSize));
            if (!new Rect(p.View.RenderSize).Contains(box)) outside.Add($"{id} en {box}");
        }

        detail = string.Join(" ; ", outside) + $" ; zoom {p.View.Viewport.Zoom:0.##}";
        return outside.Count == 0;
    }

    // ---------------- conteneurs ----------------

    private static (TabControl Tabs, Probe Probe) Tabbed(int selected)
    {
        var probe = new Probe();
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "graphe", Content = probe.View });
        tabs.Items.Add(new TabItem { Header = "autre", Content = new TextBlock { Text = "autre onglet" } });
        tabs.SelectedIndex = selected;
        Show(tabs);
        return (tabs, probe);
    }

    private static async Task TabReload()
    {
        var (tabs, p) = Tabbed(0);
        await Settle();
        var viewport = p.View.Viewport;
        for (var i = 0; i < 3; i++)
        {
            tabs.SelectedIndex = 1;
            await Settle(300);
            // L'application continue de modifier le graphe pendant que la vue est déchargée.
            if (i == 2) p.View.Nodes = [.. p.View.Nodes, new Node("d", new XYPosition(250, 200), Data: "D")];
            tabs.SelectedIndex = 0;
            await Settle(400);
        }

        Check("le viewport est gardé", p.View.Viewport == viewport, p.View.Viewport.ToString()!);
        Check("le node ajouté pendant l'absence est affiché", AllVisible(p, out var detail), detail);
        await AimRight(p);
    }

    private static async Task TabNeverShown()
    {
        var (tabs, p) = Tabbed(1);
        await Settle();
        tabs.SelectedIndex = 0;
        await Settle(900);
        Check("la vue est cadrée à son premier affichage", AllVisible(p, out var detail), detail);
        await AimRight(p);
    }

    private static async Task Scrolled()
    {
        var p = new Probe();
        p.View.Width = 1000;
        p.View.Height = 560;
        p.View.Margin = new Thickness(0, 500, 0, 500);
        var scroll = new ScrollViewer { Content = p.View };
        Show(scroll);
        await Settle(300);
        scroll.ScrollToVerticalOffset(480);
        await Settle(600);
        await AimRight(p);
        Check("la molette sur la vue ne fait pas défiler le parent", Close(scroll.VerticalOffset, 480, 0.5), $"défilement {scroll.VerticalOffset}");
    }

    private static async Task Collapsed()
    {
        var p = new Probe();
        p.View.Height = 560;
        var expander = new Expander { Header = "graphe", Content = p.View, VerticalAlignment = VerticalAlignment.Top };
        Show(expander);
        await Settle();
        expander.IsExpanded = true;
        await Settle(900);
        Check("la vue est cadrée à son ouverture", AllVisible(p, out var detail), detail);
        await AimRight(p);
    }

    private static async Task InViewbox()
    {
        var p = new Probe();
        p.View.Width = 1600;
        p.View.Height = 1000;
        Show(new Viewbox { Child = p.View }, 900, 600);
        await Settle(900);
        Check("l'échelle de la Viewbox est bien appliquée", p.Scale() < 0.8 * Dpi(p.View), $"échelle {p.Scale():0.##}");
        await AimRight(p);
    }

    private static double Dpi(Visual v) => VisualTreeHelper.GetDpi(v).DpiScaleX;

    private static async Task Transformed(bool layout, double factor)
    {
        var p = new Probe();
        p.View.Width = 800;
        p.View.Height = 480;
        p.View.HorizontalAlignment = HorizontalAlignment.Left;
        p.View.VerticalAlignment = VerticalAlignment.Top;
        var parent = new Grid();
        parent.Children.Add(p.View);
        var window = Show(parent);
        await Settle(600);
        // La transformation arrive après la mesure et le cadrage.
        if (layout) parent.LayoutTransform = new ScaleTransform(factor, factor);
        else parent.RenderTransform = new ScaleTransform(factor, factor);
        await Settle(600);
        Check("l'échelle du parent est bien appliquée", Close(p.Scale(), factor * Dpi(window), 0.01), $"échelle {p.Scale():0.##}");
        await AimRight(p);
    }

    private static async Task InPopup()
    {
        var p = new Probe();
        p.View.Width = 800;
        p.View.Height = 480;
        var anchor = new Border { Background = Brushes.White };
        var window = Show(anchor);
        var popup = new Popup { Child = p.View, PlacementTarget = anchor, Placement = PlacementMode.Relative, HorizontalOffset = 60, VerticalOffset = 60 };
        window.Closed += (_, _) => popup.IsOpen = false;
        popup.IsOpen = true;
        try
        {
            await Settle(900);
            await AimRight(p);
        }
        finally
        {
            popup.IsOpen = false;
        }
    }

    private static async Task SecondaryWindow()
    {
        var owner = Show(new Border { Background = Brushes.White }, 400, 300);
        var p = new Probe();
        Show(p.View, 1000, 640, owner);
        await Settle(900);
        await AimRight(p);
    }

    private static async Task SoftwareRendering()
    {
        var p = new Probe(chrome: true);
        var window = Show(p.View);
        ((HwndSource)PresentationSource.FromVisual(window)!).CompositionTarget!.RenderMode = RenderMode.SoftwareOnly;
        await Settle(900);
        await AimRight(p);
    }

    private static async Task TwoViews()
    {
        var left = new Probe();
        var right = new Probe();
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(right.View, 1);
        left.View.ClipToBounds = right.View.ClipToBounds = true;
        grid.Children.Add(left.View);
        grid.Children.Add(right.View);
        Show(grid, 1200, 700);
        await Settle(900);

        var (viewport, nodes, edges) = (right.View.Viewport, right.View.Nodes, right.View.Edges);
        await AimRight(left);
        Check("les gestes à gauche ne touchent pas la vue de droite",
            right.View.Viewport == viewport && ReferenceEquals(right.View.Nodes, nodes) && ReferenceEquals(right.View.Edges, edges));

        await Click(Center(left.Host("c")));
        await Task.Delay(600);
        await Click(Center(right.Host("a")));
        await Task.Delay(300);
        Check("chaque vue garde sa sélection", left.View.Nodes.Count(n => n.Selected) == 1 && right.View.Nodes.Count(n => n.Selected) == 1);
        Key(VkDelete);
        await Task.Delay(400);
        Check("Suppr n'agit que sur la vue qui a le clavier", right.View.Nodes.Count == 2 && left.View.Nodes.Count == 3,
            $"{left.View.Nodes.Count} / {right.View.Nodes.Count} nodes");
        Key(VkZ, ctrl: true);
        await Task.Delay(400);
        Check("Ctrl+Z annule après la suppression du node qui avait le clavier", right.View.Nodes.Count == 3 && left.View.Nodes.Count == 3,
            $"{left.View.Nodes.Count} / {right.View.Nodes.Count} nodes");

        // Le presse-papier est partagé entre les vues WPF (documenté) : coller agit sur la seule vue qui a le clavier.
        await Click(Center(right.Host("a")));
        await Task.Delay(300);
        Key(VkC, ctrl: true);
        await Task.Delay(200);
        await Click(left.Pane());
        await Task.Delay(600);
        Key(VkV, ctrl: true);
        await Task.Delay(500);
        Check("coller n'agit que sur la vue qui a le clavier", left.View.Nodes.Count == 4 && right.View.Nodes.Count == 3,
            $"{left.View.Nodes.Count} / {right.View.Nodes.Count} nodes");

        var (leftEdges, rightEdges) = (left.View.Edges.Count, right.View.Edges.Count);
        await Drag(Center(left.Handle("b", HandleType.Source)), Tuple(Center(right.Handle("c", HandleType.Target))));
        await Task.Delay(400);
        Check("un fil ne se branche pas dans l'autre vue",
            left.View.Edges.Count == leftEdges && right.View.Edges.Count == rightEdges && Mouse.Captured == null);
    }

    private static Style Implicit(Type type, params (DependencyProperty Property, object Value)[] setters)
    {
        var style = new Style(type);
        foreach (var (property, value) in setters) style.Setters.Add(new Setter(property, value));
        return style;
    }

    private static Dictionary<string, string> Chrome(Probe p)
    {
        var sizes = new Dictionary<string, string>();
        var i = 0;
        foreach (var button in Find<ButtonBase>(p.View)) sizes["bouton " + i++] = $"{button.ActualWidth:0}×{button.ActualHeight:0}";
        var handle = p.Handle("a", HandleType.Source);
        sizes["handle"] = $"{handle.ActualWidth:0}×{handle.ActualHeight:0}";
        var map = Find<MiniMap>(p.View).First();
        sizes["minimap"] = $"{map.ActualWidth:0}×{map.ActualHeight:0}";
        return sizes;
    }

    private static async Task ImplicitStyles()
    {
        var resources = new ResourceDictionary
        {
            [typeof(Button)] = Implicit(typeof(Button), (Control.PaddingProperty, new Thickness(24, 14, 24, 14)),
                (FrameworkElement.MarginProperty, new Thickness(6)), (FrameworkElement.MinWidthProperty, 90d),
                (FrameworkElement.MinHeightProperty, 40d), (Control.FontSizeProperty, 22d)),
            [typeof(RepeatButton)] = Implicit(typeof(RepeatButton), (FrameworkElement.MinWidthProperty, 90d),
                (FrameworkElement.MarginProperty, new Thickness(6))),
            [typeof(ToggleButton)] = Implicit(typeof(ToggleButton), (FrameworkElement.MinWidthProperty, 90d),
                (FrameworkElement.MarginProperty, new Thickness(6))),
            [typeof(TextBlock)] = Implicit(typeof(TextBlock), (FrameworkElement.MarginProperty, new Thickness(8)),
                (TextBlock.FontSizeProperty, 22d), (TextBlock.ForegroundProperty, Brushes.Red)),
            [typeof(ContentControl)] = Implicit(typeof(ContentControl), (FrameworkElement.MarginProperty, new Thickness(12))),
            [typeof(Canvas)] = Implicit(typeof(Canvas), (FrameworkElement.MarginProperty, new Thickness(9))),
            [typeof(Border)] = Implicit(typeof(Border), (Border.CornerRadiusProperty, new CornerRadius(12)))
        };

        var plain = new Probe(chrome: true);
        var first = Show(plain.View);
        await Settle(900);
        var expected = Chrome(plain);
        first.Close();

        Application.Current.Resources.MergedDictionaries.Add(resources);
        try
        {
            var p = new Probe(chrome: true);
            Show(p.View);
            await Settle(900);
            var actual = Chrome(p);
            var changed = expected.Where(e => actual.GetValueOrDefault(e.Key) != e.Value)
                .Select(e => $"{e.Key} {e.Value} → {actual.GetValueOrDefault(e.Key)}").ToList();
            Check("contrôles, handles et minimap gardent leur taille", changed.Count == 0, string.Join(" ; ", changed));
            await AimRight(p);

            // Thème changé à chaud, plusieurs fois.
            foreach (var mode in new[] { ColorMode.Dark, ColorMode.Light, ColorMode.Dark })
            {
                p.View.ColorMode = mode;
                await Settle(300);
            }

            var background = (p.View.Background as SolidColorBrush)?.Color;
            Check("le thème sombre est appliqué", background is { R: < 60, G: < 60, B: < 60 }, background?.ToString() ?? "pas de fond");
            p.View.Nodes = [.. new Probe().View.Nodes];
            p.View.Edges = [new Edge("e", "a", "b")];
            p.View.FitView();
            await Settle(500);
            await AimRight(p, "après le changement de thème, ");
        }
        finally
        {
            Application.Current.Resources.MergedDictionaries.Remove(resources);
        }
    }

    private static async Task French()
    {
        var thread = Thread.CurrentThread;
        var (culture, ui) = (thread.CurrentCulture, thread.CurrentUICulture);
        // Sans les réglages de l'utilisateur : un Windows en français peut garder le point décimal.
        var french = new CultureInfo("fr-FR", false);
        CultureInfo.DefaultThreadCurrentCulture = thread.CurrentCulture = thread.CurrentUICulture = french;
        try
        {
            var p = new Probe(chrome: true);
            Show(p.View);
            await Settle(900);
            Check("la virgule décimale est en vigueur", 1.5.ToString() == "1,5", 1.5.ToString());
            await AimRight(p);
            var json = p.View.ToJson();
            var (nodes, viewport) = (p.View.Nodes, p.View.Viewport);
            p.View.Nodes = [];
            p.View.Edges = [];
            p.View.LoadJson(json);
            await Settle(500);
            Check("le JSON relu rend les mêmes positions et le même viewport",
                p.View.Nodes.Select(n => n.Position).SequenceEqual(nodes.Select(n => n.Position)) &&
                Close(p.View.Viewport.X, viewport.X, 0.001) && Close(p.View.Viewport.Zoom, viewport.Zoom, 0.0001), json[..Math.Min(200, json.Length)]);
            Check("l'export en image fonctionne", p.View.RenderToBitmap() is { PixelWidth: > 100 });
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = null;
            thread.CurrentCulture = culture;
            thread.CurrentUICulture = ui;
        }
    }

    // ---------------- exceptions de l'application ----------------

    private static async Task Throwing(string where)
    {
        var p = new Probe();
        var armed = true;
        var thrown = 0;

        void Throw(string at)
        {
            if (!armed || at != where) return;
            thrown++;
            throw new InvalidOperationException("Exception de l'application : " + at);
        }

        p.View.NodeDragStarted += (_, _) => Throw("début");
        p.View.NodeDragStopped += (_, _) => Throw("fin");
        p.View.NodeClick += (_, _) => Throw("clic");
        p.View.ConnectRequested += (_, _) => Throw("connexion");
        p.View.ConnectStarted += (_, _) => Throw("départ");
        Show(p.View);
        await Settle(900);

        switch (where)
        {
            case "clic":
                await Click(Center(p.Host("b")));
                break;
            case "connexion" or "départ":
                await Drag(Center(p.Handle("b", HandleType.Source)), Tuple(Center(p.Handle("c", HandleType.Target))));
                break;
            default:
                var from = Center(p.Host("b"));
                await Drag(from, (from.X + 60, from.Y + 30));
                break;
        }

        await Settle(500);
        armed = false;
        Check("l'exception est bien levée", thrown > 0 && _main.Errors.Count > 0, $"{thrown} levées");
        _main.Errors.Clear();
        Check("la souris n'est plus capturée", Mouse.Captured == null, Mouse.Captured?.ToString() ?? "");

        // Le geste est fini : un mouvement sans bouton ne déplace plus rien.
        var (position, viewport) = (p.Position("b"), p.View.Viewport);
        MoveTo(p.Pane());
        await Task.Delay(150);
        MoveTo(p.View.PointToScreen(new Point(p.View.ActualWidth / 2, p.View.ActualHeight / 2)));
        await Task.Delay(400);
        Check("bouger sans bouton ne déplace rien", p.Position("b") == position && p.View.Viewport == viewport);
        Check("aucun node ne reste en cours de glisser", !p.View.Nodes.Any(n => n.Dragging));

        // Une connexion a pu être faite avant l'exception : le graphe repart de son état initial.
        p.View.Edges = [new Edge("e", "a", "b")];
        await Task.Delay(300);
        await AimRight(p, "ensuite, ");
    }

    // ---------------- durée de vie ----------------

    /// <summary>Un historique du ViewModel, partagé par les vues successives du même document.</summary>
    private sealed class SharedHistory : IFlowHistory, System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public bool CanUndo => false;
        public bool CanRedo => false;
        public void Checkpoint() => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(CanUndo)));
        public void Undo() { }
        public void Redo() { }
    }

    private static readonly SharedHistory History = new();

    /// <summary>La sélection du document, liée à chaque vue qui l'affiche.</summary>
    private static readonly System.Collections.ObjectModel.ObservableCollection<object> Selection = [];

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task<(WeakReference View, WeakReference Window)> OpenAndClose(MvvmSchema schema, bool drag)
    {
        var view = new FlowView
        {
            NodeMapping = new NodeMapping { SelectedPath = "IsSelected", TypePath = "Type", WidthPath = "Width", HeightPath = "Height" },
            EdgeMapping = new EdgeMapping { SourceHandlePath = "SourceColumn", TargetHandlePath = "TargetColumn", SelectedPath = "IsSelected" },
            NodesSource = schema.Tables, EdgesSource = schema.Relations,
            NodeFactory = schema.NodeFactory, EdgeFactory = schema.EdgeFactory, FitViewOnInit = true,
            SelectedNodes = Selection, History = History
        };
        var overlay = new Grid();
        overlay.Children.Add(new FlowControls());
        overlay.Children.Add(new MiniMap { Width = 160, Height = 110 });
        view.Content = overlay;
        var window = Show(view, 900, 600);
        await Task.Delay(drag ? 500 : 60);
        if (drag)
        {
            // Un glisser jusqu'au bord démarre le défilement automatique et sa minuterie.
            var host = Find<NodeHost>(view).First();
            await Drag(host.PointToScreen(new Point(20, 8)), Tuple(view.PointToScreen(new Point(view.ActualWidth - 4, view.ActualHeight - 4))));
            view.FitView(new FitViewOptions { Duration = TimeSpan.FromSeconds(5) }); // fermée pendant une transition
        }

        window.Close();
        return (new WeakReference(view), new WeakReference(window));
    }

    private static async Task FiftyWindows()
    {
        // Les ViewModels vivent plus longtemps que les vues : c'est le cas d'une application qui rouvre un document.
        var schema = new MvvmSchema(_ => { });
        schema.Load(Schemas.Shop());
        var opened = new List<(WeakReference View, WeakReference Window)>();
        for (var i = 0; i < 50; i++) opened.Add(await OpenAndClose(schema, drag: i % 10 == 0));

        // Une dernière fenêtre ailleurs : le focus et la souris ne retiennent plus les précédentes.
        var other = Show(new TextBox(), 300, 200);
        await Task.Delay(300);
        await Click(Center((TextBox)other.Content));
        await Task.Delay(500);
        for (var i = 0; i < 6; i++)
        {
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(250);
        }

        var views = opened.Count(o => o.View.IsAlive);
        var windows = opened.Count(o => o.Window.IsAlive);
        // WPF peut garder la dernière fenêtre active quelque temps.
        Check("les vues fermées sont libérées malgré des ViewModels encore vivants", views <= 2, $"{views} vues et {windows} fenêtres vivantes sur 50");
        GC.KeepAlive(schema);
    }
}
