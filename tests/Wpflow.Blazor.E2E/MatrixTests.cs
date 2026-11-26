using System.Globalization;
using Microsoft.Playwright;
using Scenarios;

namespace Wpflow.Blazor.E2E;

public sealed class ServerMatrixTests(ScenarioServerFixture app) : MatrixTests(app), IClassFixture<ScenarioServerFixture>;

public sealed class WasmMatrixTests(ScenarioWasmFixture app) : MatrixTests(app), IClassFixture<ScenarioWasmFixture>;

/// <summary>
///     La matrice des évènements et des gestes (<see cref="GestureMatrix" />) avec de vraies entrées du navigateur : chaque
///     geste doit lever exactement la suite attendue, la même qu'en WPF, et ne laisser aucun geste en cours.
/// </summary>
public abstract class MatrixTests(DemoFixture app)
{
    public static TheoryData<string> Gestures()
    {
        var data = new TheoryData<string>();
        foreach (var gesture in GestureMatrix.All) data.Add(gesture.Name);
        return data;
    }

    [Theory]
    [MemberData(nameof(Gestures))]
    public async Task A_gesture_raises_exactly_its_events(string name)
    {
        var gesture = GestureMatrix.Named(name);
        var page = await app.OpenAsync(query: "?matrix=" + name);
        var errors = new List<string>();
        page.Console += (_, m) => { if (m.Type == "error") errors.Add(m.Text); };
        page.PageError += (_, e) => errors.Add(e);
        try
        {
            await page.WaitForTimeoutAsync(700); // chargement, mesures et cadrage
            var player = new Player(page);
            var play = player.Plays(name);
            await play.Prepare();
            await page.WaitForTimeoutAsync(650); // pas de double-clic avec la préparation

            var whole = name.StartsWith("lifecycle."); // ces gestes contrôlent le journal depuis l'ouverture
            var before = whole ? 0 : (await player.JournalAsync()).Count;
            await play.Act();
            await page.WaitForTimeoutAsync(700);
            var got = gesture.Reduce((await player.JournalAsync()).Skip(before).ToList());
            var expected = gesture.ExpectedFor(blazor: true);
            Assert.True(got.SequenceEqual(expected),
                $"{name} : attendu [{string.Join(" > ", expected)}], reçu [{string.Join(" > ", got)}]");

            if (name == "selection.click-one") Assert.Equal(2, await page.CountAsync(".wf-node.wf-selected"));
            await player.AssertNothingInProgressAsync();
            Assert.Empty(errors);
        }
        finally
        {
            await page.Context.CloseAsync();
        }
    }

    private sealed record Play(Func<Task> Act, Func<Task> Prepare);

    private sealed class Player(IPage page)
    {
        private IMouse Mouse => page.Mouse;

        public async Task<IReadOnlyList<string>> JournalAsync() =>
            ((await page.Locator("#journal").TextContentAsync()) ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);

        /// <summary>Souris relâchée, la bouger ne doit plus rien déplacer.</summary>
        public async Task AssertNothingInProgressAsync()
        {
            const string state = "() => [document.querySelector('.wf-viewport')?.getAttribute('style'), " +
                                 "...[...document.querySelectorAll('.wf-node')].map(n => n.getAttribute('style'))].join(';')";
            await Move(await Blank());
            await page.WaitForTimeoutAsync(300);
            var before = await page.EvaluateAsync<string>(state);
            var blank = await Blank();
            await Move((blank.X + 60, blank.Y + 40));
            await page.WaitForTimeoutAsync(300);
            Assert.Equal(before, await page.EvaluateAsync<string>(state));
            Assert.Equal(0, await page.CountAsync(".wf-selection-box"));
        }

        // ---------------- les points visés ----------------

        private async Task<(double X, double Y)> Flow(double x, double y)
        {
            var box = await page.Locator(".graph").BoundingBoxAsync() ?? throw new InvalidOperationException(".graph");
            return (box.X + 60 + x, box.Y + 60 + y);
        }

        private Task<(double X, double Y)> Blank() => Flow(270, 450);

        private Task<(double X, double Y)> At(string selector) => page.CenterAsync(selector);

        private static string NodeSelector(string node) => $"[data-node-id='{node}']";

        // Sur le titre des tables ; les deux autres nodes sont visés à leur quart haut, loin de leurs poignées.
        private async Task<(double X, double Y)> Title(string node)
        {
            if (node is not ("box" or "grip")) return await At(NodeSelector(node) + " .title");
            var box = await page.Locator(NodeSelector(node)).BoundingBoxAsync() ?? throw new InvalidOperationException(node);
            return (box.X + box.Width / 4, box.Y + box.Height / 4);
        }

        private Task<(double X, double Y)> Handle(string table, string column, string type) =>
            At($"{NodeSelector(table)} [data-column='{column}'] .wf-handle[data-type='{type}']");

        private Task<(double X, double Y)> NodeHandle(string node) => At(NodeSelector(node) + " .wf-handle");

        private Task<(double X, double Y)> From() => Handle("customers", "id", "source");

        private Task<(double X, double Y)> Into() => Handle("lines", "id", "target");

        /// <summary>
        ///     Un point du tracé de la relation <c>r-lines</c> (la seconde) : à une fraction de sa longueur, ou, en pixels, après
        ///     son départ (positif) ou avant son arrivée (négatif).
        /// </summary>
        private async Task<(double X, double Y)> OnEdge(double fraction = 0.3, double pixels = 0)
        {
            var p = await page.EvaluateAsync<double[]>("""
                ([fraction, pixels]) => {
                    const path = document.querySelectorAll('.wf-edge-hit')[1];
                    const length = path.getTotalLength();
                    const at = pixels > 0 ? pixels : pixels < 0 ? length + pixels : length * fraction;
                    const p = path.getPointAtLength(at).matrixTransform(path.getScreenCTM());
                    return [p.x, p.y];
                }
                """, new object[] { fraction, pixels });
            return (p[0], p[1]);
        }

        // ---------------- les actions de base ----------------

        private Task Move((double X, double Y) p, int steps = 1) =>
            Mouse.MoveAsync((float)p.X, (float)p.Y, new MouseMoveOptions { Steps = steps });

        private Task Click((double X, double Y) p, MouseButton button = MouseButton.Left) =>
            Mouse.ClickAsync((float)p.X, (float)p.Y, new MouseClickOptions { Button = button, Delay = 40 });

        private Task DoubleClick((double X, double Y) p) =>
            Mouse.DblClickAsync((float)p.X, (float)p.Y, new MouseDblClickOptions { Delay = 40 });

        private Task Right((double X, double Y) p) => Click(p, MouseButton.Right);

        private Task Middle((double X, double Y) p) => Click(p, MouseButton.Middle);

        private async Task Drag((double X, double Y) from, (double X, double Y) to, MouseButton button = MouseButton.Left,
            Func<Task>? whileHeld = null, bool release = true)
        {
            await Move(from);
            await Mouse.DownAsync(new MouseDownOptions { Button = button });
            for (var i = 1; i <= 10; i++)
            {
                await Move((from.X + (to.X - from.X) * i / 10, from.Y + (to.Y - from.Y) * i / 10));
                await page.WaitForTimeoutAsync(20);
            }

            await page.WaitForTimeoutAsync(80);
            if (whileHeld != null) await whileHeld();
            if (release) await Mouse.UpAsync(new MouseUpOptions { Button = button });
        }

        private static (double X, double Y) By((double X, double Y) p, double dx, double dy) => (p.X + dx, p.Y + dy);

        private async Task DragBy((double X, double Y) from, double dx, double dy, MouseButton button = MouseButton.Left) =>
            await Drag(from, By(from, dx, dy), button);

        /// <summary>Un glisser qui revient à son point de départ : ce n'est pas un clic.</summary>
        private Task ThereAndBack((double X, double Y) from) => Drag(from, By(from, 60, 30), whileHeld: async () =>
        {
            await Move(from, 6);
            await page.WaitForTimeoutAsync(200);
        });

        /// <summary>
        ///     Le relâchement n'arrive jamais (boîte de dialogue, autre application) : le mouvement suivant n'a plus de bouton
        ///     enfoncé. Envoyé par le protocole de Chrome : c'est une entrée du navigateur, pas un évènement fabriqué.
        /// </summary>
        private async Task Interrupted((double X, double Y) from, (double X, double Y) to)
        {
            await Drag(from, to, release: false);
            var cdp = await page.Context.NewCDPSessionAsync(page);
            await cdp.SendAsync("Input.dispatchMouseEvent", new Dictionary<string, object>
            {
                ["type"] = "mouseMoved", ["x"] = to.X + 4, ["y"] = to.Y + 4, ["button"] = "none", ["buttons"] = 0
            });
            await page.WaitForTimeoutAsync(300);
        }

        private async Task ClickWith(string key, (double X, double Y) p)
        {
            await page.Keyboard.DownAsync(key);
            await Click(p);
            await page.Keyboard.UpAsync(key);
        }

        private async Task Select(params string[] nodes)
        {
            await Click(await Title(nodes[0]));
            foreach (var node in nodes.Skip(1))
            {
                await page.WaitForTimeoutAsync(550);
                await ClickWith("Control", await Title(node));
            }
        }

        private async Task Hover(params (double X, double Y)[] path)
        {
            foreach (var p in path)
            {
                await Move(p);
                await page.WaitForTimeoutAsync(200);
            }
        }

        // ---------------- les gestes ----------------

        public Play Plays(string name)
        {
            static Task Nothing() => Task.CompletedTask;
            Play P(Func<Task> act, Func<Task>? prepare = null) => new(act, prepare ?? Nothing);

            Play ClickOrders() => P(async () => await Click(await Title("orders")));
            Play DragOrders() => P(async () => await DragBy(await Title("orders"), 70, 40));
            Play ClickEdge() => P(async () => await Click(await OnEdge()));
            Play DragPane() => P(async () => await DragBy(await Blank(), 80, 40));
            Play DragHandle() => P(async () => await Drag(await From(), await Into()));
            Play DeleteKey() => P(() => page.Keyboard.PressAsync("Delete"), () => Select("orders"));
            Play Reconnect() => P(async () => await Drag(await OnEdge(pixels: -14), await Into()));
            Play ControlButton(string title) => P(() => page.ClickAsync($".wf-controls button[title='{title}']"));

            async Task Arm()
            {
                await Click(await From());
                await page.WaitForTimeoutAsync(300);
            }

            async Task Wheel()
            {
                await Move(await Blank());
                await Mouse.WheelAsync(0, -120);
                await page.WaitForTimeoutAsync(500);
            }

            return name switch
            {
                "node.click" => ClickOrders(),
                "node.click-selected" => P(async () => await Click(await Title("orders")), () => Select("orders")),
                "node.click-column" => P(async () => await Click(await At("[data-node-id='orders'] [data-column='note'] span"))),
                "node.double-click" => P(async () => await DoubleClick(await Title("orders"))),
                "node.right-click" => P(async () => await Right(await Title("orders"))),
                "node.middle-click" => P(async () => await Middle(await Title("orders"))),
                "node.ctrl-click-adds" => P(async () => await ClickWith("Control", await Title("orders")), () => Select("customers")),
                "node.ctrl-click-removes" => P(async () => await ClickWith("Control", await Title("orders")), () => Select("orders")),
                "node.shift-click-adds" => P(async () => await ClickWith("Shift", await Title("orders")), () => Select("customers")),
                "node.drag-under-threshold" => P(async () => await DragBy(await Title("orders"), 6, 0)),
                "node.drag" => DragOrders(),
                "node.drag-back-to-start" => P(async () => await ThereAndBack(await Title("orders"))),
                "node.drag-released-outside" => P(async () => await Drag(await Title("orders"), (-60, 400))),
                "node.drag-escape" => P(async () =>
                {
                    var from = await Title("orders");
                    await Drag(from, By(from, 60, 30), whileHeld: async () =>
                    {
                        await page.Keyboard.PressAsync("Escape");
                        await page.WaitForTimeoutAsync(200);
                        await Move(By(from, 90, 50), 4);
                        await page.WaitForTimeoutAsync(200);
                    });
                }),
                "node.drag-interrupted" => P(async () =>
                {
                    var from = await Title("orders");
                    await Interrupted(from, By(from, 70, 40));
                }),

                "selection.click-one" => P(async () => await Click(await Title("orders")), () => Select("orders", "lines")),
                "selection.drag" => P(async () => await DragBy(await Title("orders"), 60, 30), () => Select("orders", "lines")),
                "selection.right-click" => P(async () => await Right(await Title("lines")), () => Select("orders", "lines")),
                "selection.box" => P(async () =>
                {
                    await page.Keyboard.DownAsync("Shift");
                    await Drag(await Flow(340, 30), await Flow(640, 200));
                    await page.Keyboard.UpAsync("Shift");
                }),
                "selection.box-interrupted" => P(async () =>
                {
                    await page.Keyboard.DownAsync("Shift");
                    await Interrupted(await Flow(340, 30), await Flow(640, 200));
                    await page.Keyboard.UpAsync("Shift");
                }),

                "pane.click" => P(async () => await Click(await Blank()), () => Select("orders")),
                "pane.double-click" => P(async () => await DoubleClick(await Blank())),
                "pane.right-click" => P(async () => await Right(await Blank())),
                "pane.middle-click" => P(async () => await Middle(await Blank())),
                "pane.drag-under-threshold" => P(async () => await DragBy(await Blank(), 6, 0)),
                "pane.drag" => DragPane(),
                "pane.drag-with-selection" => P(async () => await DragBy(await Blank(), 80, 40), () => Select("orders")),
                "pane.click-after-drag-back" => P(async () => await ThereAndBack(await Blank())),
                "pane.middle-drag" => P(async () => await DragBy(await Blank(), 80, 40, MouseButton.Middle)),
                "pane.right-drag" => P(async () => await DragBy(await Blank(), 80, 40, MouseButton.Right)),
                "pane.drag-released-outside" => P(async () => await Drag(await Blank(), (-60, 400))),
                "pane.drag-interrupted" => P(async () =>
                {
                    var from = await Blank();
                    await Interrupted(from, By(from, 80, 40));
                }),
                "pane.wheel" or "no-zoom-on-scroll.wheel" => P(Wheel),

                "edge.click" or "background.edge-click" or "locked.edge.click" or "edge-not-selectable.click" => ClickEdge(),
                "edge.click-near-end" => P(async () => await Click(await OnEdge(pixels: 14))),
                "edge.double-click" => P(async () => await DoubleClick(await OnEdge())),
                "edge.right-click" => P(async () => await Right(await OnEdge())),
                "edge.middle-click" => P(async () => await Middle(await OnEdge())),
                "edge.ctrl-click-adds" => P(async () => await ClickWith("Control", await OnEdge()), () => Select("orders")),
                "edge.drag" => P(async () => await DragBy(await OnEdge(), 0, 60)),
                "label.click" => P(async () => await Click(await At("[data-relation='r-lines']"))),
                "label.double-click" => P(async () => await DoubleClick(await At("[data-relation='r-lines']"))),

                "handle.click" or "no-connect-on-click.handle.click" => P(async () => await Click(await From())),
                "handle.click-connect" => P(async () =>
                {
                    await Arm();
                    await Click(await Into());
                }),
                "handle.click-connect-cancel" => P(async () =>
                {
                    await Arm();
                    await Click(await Blank());
                }),
                "handle.click-connect-escape" => P(async () =>
                {
                    await Arm();
                    await page.Keyboard.PressAsync("Escape");
                }),
                "handle.drag-connect" or "locked.handle.drag" or "no-nodes-connectable.handle.drag"
                    or "node-not-connectable.handle.drag" => DragHandle(),
                "handle.drag-void" => P(async () => await Drag(await From(), await Blank())),
                "handle.drag-released-outside" => P(async () => await Drag(await From(), (-60, 400))),
                "handle.drag-interrupted" => P(async () => await Interrupted(await From(), await Blank())),
                "handle.right-click" => P(async () => await Right(await From())),
                "handle.connected" => P(async () => await Drag(await NodeHandle("box"), await NodeHandle("grip"))),

                "reconnect.to-handle" or "no-edges-reconnectable.drag" or "edge-not-reconnectable.drag" => Reconnect(),
                "reconnect.to-void" => P(async () => await Drag(await OnEdge(pixels: -14), await Blank())),
                "reconnect.interrupted" => P(async () => await Interrupted(await OnEdge(pixels: -14), await Blank())),

                "delete.key" or "node-not-deletable.delete.key" or "no-delete-keys.delete.key" => DeleteKey(),
                "drop.palette" => P(async () =>
                {
                    var blank = await Blank();
                    var graph = await page.Locator(".graph").BoundingBoxAsync() ?? throw new InvalidOperationException(".graph");
                    await page.DragAndDropAsync("#palette", ".graph",
                        new() { TargetPosition = new() { X = (float)(blank.X - graph.X), Y = (float)(blank.Y - graph.Y) } });
                }),

                "controls.zoom-in" => ControlButton("Zoom avant"),
                "controls.zoom-out" => ControlButton("Zoom arrière"),
                "controls.fit" => ControlButton("Cadrer"),
                "controls.lock" => ControlButton("Verrouiller"),
                "minimap.node-click" => P(async () =>
                {
                    var box = await page.Locator(".wf-minimap-node").Nth(1).BoundingBoxAsync()
                              ?? throw new InvalidOperationException("minimap");
                    await Click((box.X + box.Width / 2, box.Y + box.Height / 2));
                }),
                "resizer.drag" => P(async () => await DragBy(await At("[data-node-id='box'] .wf-resize-bottomright"), 40, 20),
                    () => Select("box")),
                "resize-control.drag" => P(async () => await DragBy(await At(".grip-control"), 40, 20)),
                "toolbar.click" => P(async () => await Click(await At("[data-node-id='box'] .tool")), () => Select("box")),
                "widget.click" => P(async () => await Click(await At("[data-node-id='audit'] .w-button"))),
                "widget.double-click" => P(async () => await DoubleClick(await At("[data-node-id='audit'] .w-button"))),

                "hover.node" => P(async () =>
                {
                    var title = await Title("orders");
                    await Hover(title, By(title, 6, 3), await Blank());
                }, async () => await Hover(await Blank())),
                "hover.edge" => P(async () =>
                {
                    var on = await OnEdge();
                    await Hover(on, By(on, 4, 0), await Blank());
                }, async () => await Hover(await Blank())),
                "hover.pane" => P(async () =>
                {
                    var blank = await Blank();
                    await Hover(blank, By(blank, 8, 4), await Title("orders"));
                }, async () => await Hover(await Title("orders"))),

                "lifecycle.load" or "lifecycle.error" => P(Nothing),
                "background.node-drag" or "locked.node.drag" or "no-nodes-draggable.node.drag" or "node-not-draggable.drag"
                    or "no-select-on-drag.node.drag" => DragOrders(),
                "background.state" or "no-pan-on-drag.pane.drag" => DragPane(),
                "locked.node.click" or "no-elements-selectable.node.click" or "node-not-selectable.click" => ClickOrders(),
                "locked.delete.key" => P(() => page.Keyboard.PressAsync("Delete")),
                "no-zoom-on-double-click.pane" => P(async () => await DoubleClick(await Blank())),
                _ => throw new InvalidOperationException("geste non joué en Blazor : " + name)
            };
        }
    }
}
