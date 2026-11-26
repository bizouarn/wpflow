using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using demo;
using Wpflow.Core;
using Wpflow.Wpf;
using static demo.Sim;

namespace Scenarios.Wpf;

/// <summary>
///     La matrice des évènements et des gestes (<see cref="GestureMatrix" />) à la vraie souris : chaque geste doit lever
///     exactement la suite attendue, la même qu'en Blazor, et ne laisser aucun geste en cours. <c>WPFLOW_GESTURE=a,b</c>
///     ne joue que les gestes dont le nom commence par a ou b.
/// </summary>
internal static class MatrixRunner
{
    private const byte VkShiftKey = 0x10;
    private static MainWindow _window = null!;
    private static Action<string> _log = _ => { };
    private static EventJournal _journal = null!;
    private static FlowControls _controls = null!;
    private static MiniMap _map = null!;
    private static readonly List<DependencyProperty> Changed = [];

    private static FlowView F => _window.View;

    /// <summary>Un geste : ce qui le prépare (hors journal), le geste lui-même, et ce qui doit encore être vrai après.</summary>
    private sealed record Play(Func<Task> Act, Func<Task>? Prepare = null, Func<string?>? Verify = null);

    public static async Task Run(MainWindow window, Action<string> log)
    {
        _window = window;
        _log = log;
        _journal = window.AttachJournal();
        (_controls, _map) = window.AddCompanions();

        var only = Environment.GetEnvironmentVariable("WPFLOW_GESTURE")?.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var plays = Plays();
        foreach (var gesture in GestureMatrix.All.Where(g => only == null || only.Any(g.Name.StartsWith)))
        {
            try
            {
                if (!plays.TryGetValue(gesture.Name, out var play))
                {
                    _log(SelfTestReport.Line(gesture.Name, false, "geste non joué en WPF"));
                    continue;
                }

                await Reset(gesture);
                if (play.Prepare != null) await play.Prepare();
                await Task.Delay(650); // pas de double-clic avec la préparation
                _journal.Clear();
                await play.Act();
                await Task.Delay(500);
                var got = gesture.Reduce(_journal.Entries);
                var expected = gesture.ExpectedFor(blazor: false);
                var ok = got.SequenceEqual(expected);
                _log(SelfTestReport.Line(gesture.Name, ok,
                    ok ? "" : $"attendu [{string.Join(" > ", expected)}], reçu [{string.Join(" > ", got)}]"));
                var left = play.Verify?.Invoke() ?? Leftover();
                if (left != null) _log(SelfTestReport.Line(gesture.Name + " : état final", false, left));
            }
            catch (Exception ex)
            {
                _log(SelfTestReport.Line(gesture.Name, false, ex.ToString()));
            }

            if (_window.Errors.Count > 0) _log(SelfTestReport.Line(gesture.Name + " : aucune exception non gérée", false, _window.Errors[0]));
            _window.Errors.Clear();
        }

        if (only == null || only.Any("dialog".StartsWith)) await DialogFromDoubleClick();
        window.RemoveCompanions();
    }

    /// <summary>
    ///     Propre à WPF : un gestionnaire de double-clic ouvre une boîte de dialogue, qui reçoit le relâchement. Au retour,
    ///     la vue ne doit pas continuer le geste comme si le bouton était resté enfoncé.
    /// </summary>
    private static async Task DialogFromDoubleClick()
    {
        const string name = "dialog : un double-clic qui ouvre une boîte de dialogue ne laisse aucun geste";
        try
        {
            await Reset(GestureMatrix.Named("node.double-click"));
            var shown = false;

            void Open(object? sender, ElementEventArgs e)
            {
                shown = true;
                var dialog = Dialog();
                var close = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                close.Tick += (_, _) =>
                {
                    close.Stop();
                    dialog.Close();
                };
                close.Start();
                dialog.ShowDialog();
            }

            F.NodeDoubleClick += Open;
            var from = Title("orders");
            try
            {
                await DoubleClick(from);
                await Task.Delay(700);
            }
            finally
            {
                F.NodeDoubleClick -= Open;
            }

            _window.Activate();
            await Task.Delay(300);
            var before = F.Nodes.First(n => n.Id == "orders").Position;
            await Hover(Offset(from, 40, 20), Offset(from, 90, 50)); // bouton relâché
            var after = F.Nodes.First(n => n.Id == "orders").Position;
            var left = Leftover();
            _log(SelfTestReport.Line(name, shown && before == after && left == null,
                $"boîte ouverte={shown}, node {before} -> {after}{(left == null ? "" : ", " + left)}"));
        }
        catch (Exception ex)
        {
            _log(SelfTestReport.Line(name, false, ex.ToString()));
        }
    }

    private static Window Dialog() => new()
    {
        Owner = _window, Width = 340, Height = 120, Title = "Test : interruption d'un geste",
        WindowStartupLocation = WindowStartupLocation.Manual, Left = _window.Left, Top = _window.Top,
        Content = new TextBlock
        {
            Text = "Ouverte par le test : elle se ferme seule.", Margin = new Thickness(16), TextWrapping = TextWrapping.Wrap
        }
    };

    /// <summary>Après un geste, rien ne doit rester « en cours ».</summary>
    private static string? Leftover()
    {
        if (F.IsMouseCaptured) return "la souris est encore capturée";
        var busy = F.Nodes.Where(n => n.Dragging || n.Resizing).Select(n => n.Id).ToList();
        return busy.Count > 0 ? "nodes encore en cours de déplacement : " + string.Join(", ", busy) : null;
    }

    private static async Task Reset(Gesture gesture)
    {
        foreach (var key in new[] { VkControl, VkShiftKey }) KeyRelease(key);
        MoveTo(Screen(F, new Point(F.ActualWidth - 30, 30)));
        Key(VkEscape);
        await Task.Delay(80);
        Key(VkEscape); // un menu ouvert prend la première
        foreach (var property in Changed) F.ClearValue(property);
        Changed.Clear();
        F.IsLocked = false;
        foreach (var (name, value) in GestureMatrix.ViewOptions(gesture.Options)) Apply(name, value);

        // Un schéma vide d'abord : les nodes et leurs contrôles repartent de zéro.
        _window.Open(new Schema([], []));
        await Task.Delay(150);
        _window.Open(GestureMatrix.SchemaFor(gesture.Options));
        await Task.Delay(300);
        F.Viewport = new Viewport(60, 60, 1);
        await Task.Delay(300);
        F.Focus();
    }

    private static void Apply(string name, string value)
    {
        var descriptor = DependencyPropertyDescriptor.FromName(name, typeof(FlowView), typeof(FlowView))
                         ?? throw new ArgumentException("réglage inconnu : " + name);
        var type = descriptor.PropertyType;
        object typed = name == nameof(FlowView.DeleteKeys) ? Array.Empty<Key>()
            : type.IsEnum ? Enum.Parse(type, value)
            : Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        Changed.Add(descriptor.DependencyProperty);
        F.SetValue(descriptor.DependencyProperty, typed);
    }

    // ---------------- les points visés ----------------

    private static NodeHost Host(string id) => Find<NodeHost>(F).First(h => h.Node?.Id == id);

    private static T Tagged<T>(string node, string tag) where T : FrameworkElement => Find<T>(Host(node)).First(e => e.Tag as string == tag);

    private static Point Title(string node)
    {
        var title = Tagged<Border>(node, "title");
        return Screen(title, new Point(Math.Min(60, title.ActualWidth / 2), Math.Min(12, title.ActualHeight / 2)));
    }

    private static Grid Row(string table, string column) => Find<Grid>(Host(table)).First(g => g.Tag as string == column);

    private static Point HandlePoint(string table, string column, HandleType type) =>
        Center(Find<Handle>(Row(table, column)).First(h => h.Type == type));

    private static Point NodeHandle(string node) => Center(Find<Handle>(Host(node)).First());

    private static Point Flow(double x, double y) => Screen(F, F.FlowToScreen(new XYPosition(x, y)));

    private static Point Blank => Flow(270, 450);

    private static string PathOf(string edge) => Find<EdgeVisual>(F).First(v => v.EdgeId == edge).PathData!;

    private static XYPosition PointOn(string path, double fraction)
    {
        System.Windows.Media.Geometry.Parse(path).GetFlattenedPathGeometry().GetPointAtFractionLength(fraction, out var p, out _);
        return new XYPosition(p.X, p.Y);
    }

    private static Point OnEdge(string edge)
    {
        var p = PointOn(PathOf(edge), 0.3);
        return Flow(p.X, p.Y);
    }

    /// <summary>Sur le tracé, près de son arrivée mais hors du handle : de quoi rattraper cette extrémité.</summary>
    private static Point EdgeEnd(string edge)
    {
        var end = PointOn(PathOf(edge), 1);
        return Flow(end.X - 14, end.Y);
    }

    private static Point Label(string edge) => Center(Find<Button>(F).First(b => b.Tag as string == edge));

    private static Point Offset(Point p, double dx, double dy) => new(p.X + dx, p.Y + dy);

    private static Point Outside() => Screen(_window, new Point(-60, 400));

    // ---------------- les actions de base ----------------

    private static Task DragTo(Point from, Point to) => Drag(from, (to.X, to.Y));

    private static Task DragBy(Point from, double dx, double dy) => DragTo(from, Offset(from, dx, dy));

    private static async Task ClickWith(byte modifier, Point at)
    {
        KeyDown(modifier);
        await Task.Delay(120);
        await Click(at);
        KeyRelease(modifier);
        await Task.Delay(120);
    }

    private static Task RightAt(Point p) => RightClick(p);

    private static Task MiddleAt(Point p) => DragWith(MouseButton.Middle, p, p);

    private static async Task Select(params string[] nodes)
    {
        await Click(Title(nodes[0]));
        foreach (var node in nodes.Skip(1))
        {
            await Task.Delay(550);
            await ClickWith(VkControl, Title(node));
        }
    }

    /// <summary>
    ///     Une boîte de dialogue s'ouvre pendant que le bouton est enfoncé, comme depuis un gestionnaire de l'application :
    ///     la vue perd la capture de la souris et ne recevra pas le relâchement.
    /// </summary>
    private static Task Interrupted(Point from, Point to) => DragHolding(from, (to.X, to.Y), async () =>
    {
        var dialog = Dialog();
        // La minuterie est armée AVANT ShowDialog, qui ne rend la main qu'à la fermeture : celle-ci ne doit dépendre
        // d'aucun évènement de la fenêtre (ContentRendered n'est pas levé pour une fenêtre sans contenu).
        var captured = true;
        var close = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        close.Tick += (_, _) =>
        {
            close.Stop();
            captured = F.IsMouseCaptured;
            dialog.Close();
        };
        close.Start();
        dialog.ShowDialog();
        // Sans perte de capture, le geste ne serait pas interrompu et le test ne prouverait rien.
        if (captured) _log(SelfTestReport.Line("interruption : la boîte de dialogue retire la capture", false));
        _window.Activate();
        await Task.Delay(350);
    });

    /// <summary>Un glisser qui revient à son point de départ : ce n'est pas un clic.</summary>
    private static Task ThereAndBack(Point from) => DragHolding(from, (from.X + 60, from.Y + 30), async () =>
    {
        MoveTo(from);
        await Task.Delay(200);
    });

    private static Task Press(ButtonBase button) => Click(Center(button));

    private static Task Control(string part) => Press(Find<ButtonBase>(_controls).First(b => b.Name == part));

    private static Point OnMap(string node)
    {
        var visible = ViewportMath.GetVisibleRect(F.Viewport, F.ActualWidth, F.ActualHeight);
        var layout = MiniMapMath.Compute(GraphUtils.GetNodesBounds(F.AbsoluteNodes), visible, _map.ActualWidth, _map.ActualHeight);
        var rect = F.AbsoluteNodes.First(n => n.Id == node).Rect!.Value;
        return Screen(_map, new Point((rect.X + rect.Width / 2 - layout.X) * layout.Scale,
            (rect.Y + rect.Height / 2 - layout.Y) * layout.Scale));
    }

    private static async Task Hover(params Point[] path)
    {
        foreach (var p in path)
        {
            MoveTo(p);
            await Task.Delay(200);
        }
    }

    private static async Task WheelOnPane(int delta)
    {
        MoveTo(Blank);
        await Task.Delay(150);
        Wheel(delta);
        await Task.Delay(500);
    }

    private static async Task PlayLoad()
    {
        var other = new MainWindow(mvvm: false) { Topmost = true };
        var journal = other.AttachJournal();
        other.Show();
        try
        {
            await Task.Delay(500);
            other.Open(GestureMatrix.Schema());
            await Task.Delay(1000);
            foreach (var entry in journal.Entries)
            {
                var parts = entry.Split(' ', 2);
                _journal.Add(parts[0], parts.Length > 1 ? parts[1] : null);
            }
        }
        finally
        {
            other.Close();
            _window.Activate();
        }
    }

    // ---------------- les gestes ----------------

    private static Dictionary<string, Play> Plays()
    {
        Play P(Func<Task> act, Func<Task>? prepare = null) => new(act, prepare);

        Play ClickOrders() => P(() => Click(Title("orders")));
        Play DragOrders() => P(() => DragBy(Title("orders"), 70, 40));
        Play ClickEdge() => P(() => Click(OnEdge("r-lines")));
        Play DragPane() => P(() => DragBy(Blank, 80, 40));
        Play DragHandle() => P(() => DragTo(HandlePoint("customers", "id", HandleType.Source), HandlePoint("lines", "id", HandleType.Target)));
        Play DeleteKey() => P(async () => { Key(VkDelete); await Task.Delay(200); }, () => Select("orders"));
        Play Reconnect() => P(() => DragTo(EdgeEnd("r-lines"), HandlePoint("lines", "id", HandleType.Target)));

        async Task Arm()
        {
            await Click(HandlePoint("customers", "id", HandleType.Source));
            await Task.Delay(300);
        }

        return new Dictionary<string, Play>
        {
            ["node.click"] = ClickOrders(),
            ["node.click-selected"] = P(() => Click(Title("orders")), () => Select("orders")),
            ["node.click-column"] = P(() => Click(Center(Row("orders", "note")))),
            ["node.double-click"] = P(() => DoubleClick(Title("orders"))),
            ["node.right-click"] = P(() => RightAt(Title("orders"))),
            ["node.middle-click"] = P(() => MiddleAt(Title("orders"))),
            ["node.ctrl-click-adds"] = P(() => ClickWith(VkControl, Title("orders")), () => Select("customers")),
            ["node.ctrl-click-removes"] = P(() => ClickWith(VkControl, Title("orders")), () => Select("orders")),
            ["node.shift-click-adds"] = P(() => ClickWith(VkShiftKey, Title("orders")), () => Select("customers")),
            ["node.drag-under-threshold"] = P(() => DragBy(Title("orders"), 6, 0)),
            ["node.drag"] = DragOrders(),
            ["node.drag-back-to-start"] = P(() => ThereAndBack(Title("orders"))),
            ["node.drag-released-outside"] = P(() => DragTo(Title("orders"), Outside())),
            ["node.drag-escape"] = P(() =>
            {
                var from = Title("orders");
                return DragHolding(from, (from.X + 60, from.Y + 30), async () =>
                {
                    Key(VkEscape);
                    await Task.Delay(200);
                    MoveTo(Offset(from, 90, 50));
                    await Task.Delay(200);
                });
            }),
            ["node.drag-interrupted"] = P(() => Interrupted(Title("orders"), Offset(Title("orders"), 70, 40))),

            ["selection.click-one"] = new Play(() => Click(Title("orders")), () => Select("orders", "lines"),
                () => F.Nodes.Count(n => n.Selected) == 2 ? Leftover() : "la sélection multiple n'est pas gardée"),
            ["selection.drag"] = P(() => DragBy(Title("orders"), 60, 30), () => Select("orders", "lines")),
            ["selection.right-click"] = P(() => RightAt(Title("lines")), () => Select("orders", "lines")),
            ["selection.box"] = P(async () =>
            {
                KeyDown(VkShiftKey);
                await Task.Delay(120);
                await DragTo(Flow(340, 30), Flow(640, 200));
                KeyRelease(VkShiftKey);
            }),

            ["pane.click"] = P(() => Click(Blank), () => Select("orders")),
            ["pane.double-click"] = P(() => DoubleClick(Blank)),
            ["pane.right-click"] = P(() => RightAt(Blank)),
            ["pane.middle-click"] = P(() => MiddleAt(Blank)),
            ["pane.drag-under-threshold"] = P(() => DragBy(Blank, 6, 0)),
            ["pane.drag"] = DragPane(),
            ["pane.drag-with-selection"] = P(() => DragBy(Blank, 80, 40), () => Select("orders")),
            ["pane.click-after-drag-back"] = P(() => ThereAndBack(Blank)),
            ["selection.box-interrupted"] = P(async () =>
            {
                KeyDown(VkShiftKey);
                await Task.Delay(120);
                await Interrupted(Flow(340, 30), Flow(640, 200));
                KeyRelease(VkShiftKey);
            }),
            ["pane.middle-drag"] = P(() => DragWith(MouseButton.Middle, Blank, Offset(Blank, 80, 40))),
            ["pane.right-drag"] = P(() => DragWith(MouseButton.Right, Blank, Offset(Blank, 80, 40))),
            ["pane.drag-released-outside"] = P(() => DragTo(Blank, Outside())),
            ["pane.drag-interrupted"] = P(() => Interrupted(Blank, Offset(Blank, 80, 40))),
            ["pane.wheel"] = P(() => WheelOnPane(120)),

            ["edge.click"] = ClickEdge(),
            ["edge.click-near-end"] = P(() =>
            {
                var start = PointOn(PathOf("r-lines"), 0);
                return Click(Flow(start.X + 14, start.Y));
            }),
            ["edge.double-click"] = P(() => DoubleClick(OnEdge("r-lines"))),
            ["edge.right-click"] = P(() => RightAt(OnEdge("r-lines"))),
            ["edge.middle-click"] = P(() => MiddleAt(OnEdge("r-lines"))),
            ["edge.ctrl-click-adds"] = P(() => ClickWith(VkControl, OnEdge("r-lines")), () => Select("orders")),
            ["edge.drag"] = P(() => DragBy(OnEdge("r-lines"), 0, 60)),
            ["label.click"] = P(() => Click(Label("r-lines"))),
            ["label.double-click"] = P(() => DoubleClick(Label("r-lines"))),

            ["handle.click"] = P(() => Click(HandlePoint("customers", "id", HandleType.Source))),
            ["handle.click-connect"] = P(async () =>
            {
                await Arm();
                await Click(HandlePoint("lines", "id", HandleType.Target));
            }),
            ["handle.click-connect-cancel"] = P(async () =>
            {
                await Arm();
                await Click(Blank);
            }),
            ["handle.click-connect-escape"] = P(async () =>
            {
                await Arm();
                Key(VkEscape);
                await Task.Delay(200);
            }),
            ["handle.drag-connect"] = DragHandle(),
            ["handle.drag-void"] = P(() => DragTo(HandlePoint("customers", "id", HandleType.Source), Blank)),
            ["handle.drag-released-outside"] = P(() => DragTo(HandlePoint("customers", "id", HandleType.Source), Outside())),
            ["handle.drag-interrupted"] = P(() => Interrupted(HandlePoint("customers", "id", HandleType.Source), Blank)),
            ["handle.right-click"] = P(() => RightAt(HandlePoint("customers", "id", HandleType.Source))),
            ["handle.connected"] = P(() => DragTo(NodeHandle("box"), NodeHandle("grip"))),

            ["reconnect.to-handle"] = Reconnect(),
            ["reconnect.to-void"] = P(() => DragTo(EdgeEnd("r-lines"), Blank)),
            ["reconnect.interrupted"] = P(() => Interrupted(EdgeEnd("r-lines"), Blank)),

            ["delete.key"] = DeleteKey(),
            ["drop.palette"] = P(() =>
            {
                var to = Blank;
                return Drag(Center((ListBoxItem)_window.Tables.Items[0]), (to.X, to.Y), steps: 25, delay: 30);
            }),

            ["controls.zoom-in"] = P(() => Control("PART_ZoomIn")),
            ["controls.zoom-out"] = P(() => Control("PART_ZoomOut")),
            ["controls.fit"] = P(() => Control("PART_Fit")),
            ["controls.lock"] = P(() => Control("PART_Lock")),
            ["minimap.node-click"] = P(() => Click(OnMap("orders"))),
            ["resizer.drag"] = P(() => DragBy(Center(Find<NodeResizer>(Host("box")).First().GetThumb(ResizeDirection.BottomRight)!), 40, 20),
                () => Select("box")),
            ["resize-control.drag"] = P(() => DragBy(Center(Find<NodeResizeControl>(Host("grip")).First()), 40, 20)),
            ["toolbar.click"] = new Play(() => Click(Center(Tagged<Button>("box", "tool"))), () => Select("box")),
            ["widget.click"] = P(() => Click(Center(Tagged<Button>("audit", "w-button")))),
            ["widget.double-click"] = P(() => DoubleClick(Center(Tagged<Button>("audit", "w-button")))),

            ["hover.node"] = P(() => Hover(Title("orders"), Offset(Title("orders"), 6, 3), Blank), () => Hover(Blank)),
            ["hover.edge"] = P(() => Hover(OnEdge("r-lines"), Offset(OnEdge("r-lines"), 4, 0), Blank), () => Hover(Blank)),
            ["hover.pane"] = P(() => Hover(Blank, Offset(Blank, 8, 4), Title("orders")), () => Hover(Title("orders"))),

            ["lifecycle.load"] = P(PlayLoad),
            ["lifecycle.error"] = P(async () =>
            {
                var schema = GestureMatrix.Schema();
                _window.Open(schema with { Edges = [.. schema.Edges, new Edge("lost", "orders", "nowhere")] });
                await Task.Delay(400);
            }),
            ["background.node-drag"] = DragOrders(),
            ["background.edge-click"] = ClickEdge(),
            ["background.state"] = DragPane(),

            ["locked.node.click"] = ClickOrders(),
            ["locked.node.drag"] = DragOrders(),
            ["locked.edge.click"] = ClickEdge(),
            ["locked.handle.drag"] = DragHandle(),
            ["locked.delete.key"] = P(async () => { Key(VkDelete); await Task.Delay(200); }),
            ["no-nodes-draggable.node.drag"] = DragOrders(),
            ["node-not-draggable.drag"] = DragOrders(),
            ["no-elements-selectable.node.click"] = ClickOrders(),
            ["node-not-selectable.click"] = ClickOrders(),
            ["edge-not-selectable.click"] = ClickEdge(),
            ["no-nodes-connectable.handle.drag"] = DragHandle(),
            ["node-not-connectable.handle.drag"] = DragHandle(),
            ["no-edges-reconnectable.drag"] = Reconnect(),
            ["edge-not-reconnectable.drag"] = Reconnect(),
            ["node-not-deletable.delete.key"] = DeleteKey(),
            ["no-delete-keys.delete.key"] = DeleteKey(),
            ["no-pan-on-drag.pane.drag"] = DragPane(),
            ["no-zoom-on-scroll.wheel"] = P(() => WheelOnPane(120)),
            ["no-zoom-on-double-click.pane"] = P(() => DoubleClick(Blank)),
            ["no-connect-on-click.handle.click"] = P(() => Click(HandlePoint("customers", "id", HandleType.Source))),
            ["no-select-on-drag.node.drag"] = DragOrders()
        };
    }
}
