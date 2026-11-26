using System.Globalization;
using Microsoft.Playwright;

namespace Wpflow.Blazor.E2E;

public sealed class ServerHostileTests(ScenarioServerFixture app) : HostileTests(app), IClassFixture<ScenarioServerFixture>;

public sealed class WasmHostileTests(ScenarioWasmFixture app) : HostileTests(app), IClassFixture<ScenarioWasmFixture>;

/// <summary>Conditions d'intégration hostiles (ROADMAP-TESTS, lot 4), avec de vraies entrées.</summary>
public abstract class HostileTests(DemoFixture app) : PagesTest(app)
{
    private const string Left = "#left";

    private async Task<(IPage Page, List<string> Errors)> OpenAsync(string mode = "two", string extra = "",
        Action<BrowserNewContextOptions>? configure = null, string? css = null, string? url = null)
    {
        var edges = mode == "two" ? 2 : 1;
        var ready = css == null
            ? $"document.querySelectorAll('.wf-edge-path').length >= {edges}"
            : "document.querySelectorAll('.wf-node').length >= 3";
        var page = await App.OpenAsync(query: $"?hostile={mode}{extra}", configure: configure, url: url, ready: ready,
            beforeLoad: css == null ? null : Inject(css));
        var errors = page.CollectErrors();
        return (page, errors);
    }

    /// <summary>Un script qui pose une feuille de style avant le premier rendu.</summary>
    private static string Inject(string css) => $$"""
        document.addEventListener('DOMContentLoaded', () => {
            const style = document.createElement('style');
            style.id = 'hostile';
            style.textContent = {{System.Text.Json.JsonSerializer.Serialize(css)}};
            document.head.appendChild(style);
        });
        """;

    private static string Node(string id, string view = Left) => $"{view} [data-node-id='{id}']";

    private static string Handle(string id, string type, string view = Left) => $"{Node(id, view)} .wf-handle[data-type='{type}']";

    private static Task<string> TextAsync(IPage page, string id) => page.Locator("#" + id).InnerTextAsync();

    private static Task ExpectTextAsync(IPage page, string id, string expected) =>
        page.UntilAsync(async () => (await TextAsync(page, id)).Trim() == expected, $"#{id} vaut « {expected} »");

    /// <summary>La vue telle qu'elle est à l'écran : viewport, coin du conteneur et échelle imposée par un parent.</summary>
    private sealed record Frame(double X, double Y, double Zoom, double Left, double Top, double Scale, double Width, double Height)
    {
        public (double X, double Y) ToClient(double flowX, double flowY) => (Left + (flowX * Zoom + X) * Scale, Top + (flowY * Zoom + Y) * Scale);

        public (double X, double Y) ToFlow(double clientX, double clientY) => (((clientX - Left) / Scale - X) / Zoom, ((clientY - Top) / Scale - Y) / Zoom);
    }

    private static async Task<Frame> FrameAsync(IPage page, string view = Left)
    {
        var values = await page.EvaluateAsync<double[]>("""
            view => {
                const root = document.querySelector(view + ' .wf-flow');
                const r = root.getBoundingClientRect();
                const style = root.querySelector('.wf-viewport').getAttribute('style');
                const t = /translate\((-?[\d.]+)px,\s*(-?[\d.]+)px\)\s*scale\((-?[\d.]+)\)/.exec(style);
                return [+t[1], +t[2], +t[3], r.left, r.top, r.width / root.offsetWidth, root.offsetWidth, root.offsetHeight];
            }
            """, view);
        return new Frame(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7]);
    }

    private static async Task<(double X, double Y)> PosAsync(IPage page, string id, string view = Left)
    {
        var values = await page.EvaluateAsync<double[]>("""
            selector => {
                const t = /translate\((-?[\d.]+)px,\s*(-?[\d.]+)px\)/.exec(document.querySelector(selector).getAttribute('style'));
                return [+t[1], +t[2]];
            }
            """, Node(id, view));
        return (values[0], values[1]);
    }

    private static void Near((double X, double Y) expected, (double X, double Y) actual, double tolerance, string what) =>
        Assert.True(Math.Abs(expected.X - actual.X) <= tolerance && Math.Abs(expected.Y - actual.Y) <= tolerance,
            string.Create(CultureInfo.InvariantCulture, $"{what} : attendu ({expected.X:0.#}, {expected.Y:0.#}), obtenu ({actual.X:0.#}, {actual.Y:0.#})"));

    /// <summary>Chaque edge de la vue part de la poignée source de « a » et arrive à la poignée cible de « b ».</summary>
    private static async Task AssertEdgeAttachedAsync(IPage page, string view = Left)
    {
        var detail = "";
        for (var i = 0; i < 40; i++)
        {
            var end = await page.EvaluateAsync<double[]>("""
                view => {
                    const path = document.querySelector(view + ' .wf-edge-path');
                    const m = path.getScreenCTM();
                    const a = path.getPointAtLength(0).matrixTransform(m);
                    const b = path.getPointAtLength(path.getTotalLength()).matrixTransform(m);
                    return [a.x, a.y, b.x, b.y];
                }
                """, view);
            var source = await page.CenterAsync(Handle("a", "source", view));
            var target = await page.CenterAsync(Handle("b", "target", view));
            // Le tracé touche le bord de la poignée (8 px de côté, au zoom de la vue), pas son centre.
            var frame = await FrameAsync(page, view);
            var reach = 4 * frame.Zoom * frame.Scale + 3;
            if (Math.Abs(end[0] - source.X) < reach && Math.Abs(end[1] - source.Y) < reach &&
                Math.Abs(end[2] - target.X) < reach && Math.Abs(end[3] - target.Y) < reach) return;
            detail = string.Create(CultureInfo.InvariantCulture,
                $"edge ({end[0]:0},{end[1]:0})→({end[2]:0},{end[3]:0}), handles ({source.X:0},{source.Y:0})→({target.X:0},{target.Y:0})");
            await page.WaitForTimeoutAsync(100);
        }

        Assert.Fail("L'edge n'est pas accroché à ses handles : " + detail);
    }

    /// <summary>Un point du fond de la vue, loin des nodes, des contrôles et de la minimap.</summary>
    private static async Task<(double X, double Y)> PanePointAsync(IPage page, string view = Left)
    {
        var box = await page.Locator($"{view} .wf-flow").BoundingBoxAsync();
        return (box!.X + box.Width * 0.12, box.Y + box.Height * 0.15);
    }

    /// <summary>Ce qui se trouve sous les deux bouts d'un fil, et le viewport : pour comprendre une connexion manquée.</summary>
    private static async Task<string> StateAsync(IPage page, string view, (double X, double Y) from, (double X, double Y) to) =>
        await page.EvaluateAsync<string>("""
            ([view, points]) => {
                const root = document.querySelector(view + ' .wf-flow');
                const r = root.getBoundingClientRect();
                const under = ([x, y]) => {
                    const e = document.elementFromPoint(x, y);
                    return `(${x.toFixed(0)},${y.toFixed(0)}) ${e?.closest('[data-node-id]')?.dataset.nodeId ?? ''} ${e?.getAttribute('class')}`;
                };
                return [root.querySelector('.wf-viewport').getAttribute('style'),
                    `vue ${r.left.toFixed(0)},${r.top.toFixed(0)} ${r.width.toFixed(0)}×${r.height.toFixed(0)}`, ...points.map(under),
                    `fil ${root.querySelector('.wf-connection-line')?.getAttribute('d') ?? 'aucun'}`].join(' | ');
            }
            """, new object[] { view, new[] { new[] { from.X, from.Y }, new[] { to.X, to.Y } } });

    /// <summary>Tous les gestes d'une vue visent juste : dépôt, clic, glisser, pan, molette, connexion.</summary>
    private static async Task AssertGesturesAimRightAsync(IPage page, string view = Left)
    {
        var side = view.TrimStart('#');

        // Le dépôt d'abord : rien n'a encore appuyé sur la vue depuis le changement de mise en page.
        var frame = await FrameAsync(page, view);
        var drop = await PanePointAsync(page, view);
        await page.DragAndDropAsync("#palette-item", view + " .wf-flow", new PageDragAndDropOptions
        {
            TargetPosition = new TargetPosition { X = (float)(frame.Width * 0.12 * frame.Scale), Y = (float)(frame.Height * 0.15 * frame.Scale) }
        });
        await page.UntilAsync(async () => (await TextAsync(page, "drops")).Contains(';'), "dépôt reçu");
        frame = await FrameAsync(page, view);
        drop = await PanePointAsync(page, view);
        var parts = (await TextAsync(page, "drops")).Split(';').Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray();
        Near(frame.ToFlow(drop.X, drop.Y), (parts[0], parts[1]), 3, "position du dépôt");

        var b = await page.CenterAsync(Node("b", view));
        await page.Mouse.ClickAsync((float)b.X, (float)b.Y);
        await ExpectTextAsync(page, "log", side + " clic b");
        await page.WaitForTimeoutAsync(600); // le clic suivant ne doit pas faire un double-clic

        frame = await FrameAsync(page, view);
        var before = await PosAsync(page, "b", view);
        b = await page.CenterAsync(Node("b", view));
        await page.DragAsync(b, (b.X + 80, b.Y + 40));
        await ExpectTextAsync(page, "log", side + " glissé b");
        await page.WaitForTimeoutAsync(300);
        var per = frame.Zoom * frame.Scale;
        Near((before.X + 80 / per, before.Y + 40 / per), await PosAsync(page, "b", view), 2, "glisser d'un node");
        await AssertEdgeAttachedAsync(page, view);

        var pane = await PanePointAsync(page, view);
        await page.DragAsync(pane, (pane.X + 40, pane.Y + 24));
        await page.WaitForTimeoutAsync(400);
        var panned = await FrameAsync(page, view);
        Near((frame.X + 40 / frame.Scale, frame.Y + 24 / frame.Scale), (panned.X, panned.Y), 2, "pan");

        var anchor = await page.CenterAsync(Node("a", view));
        var under = panned.ToFlow(anchor.X, anchor.Y);
        await page.Mouse.MoveAsync((float)anchor.X, (float)anchor.Y);
        await page.Mouse.WheelAsync(0, 120);
        await page.UntilAsync(async () => (await FrameAsync(page, view)).Zoom < panned.Zoom - 0.01, "la molette zoome");
        await page.WaitForTimeoutAsync(300);
        Near(under, (await FrameAsync(page, view)).ToFlow(anchor.X, anchor.Y), 2, "le point sous la souris reste en place");

        var count = await page.CountAsync(view + " .wf-edge");
        var source = await page.CenterAsync(Handle("a", "source", view));
        var target = await page.CenterAsync(Handle("c", "target", view));
        var seen = await StateAsync(page, view, source, target);
        await page.DragAsync(source, target);
        for (var i = 0; i < 100 && await page.CountAsync(view + " .wf-edge") != count + 1; i++) await page.WaitForTimeoutAsync(50);
        var edges = await page.CountAsync(view + " .wf-edge");
        if (edges != count + 1)
        {
            var now = await StateAsync(page, view, source, target);
            Assert.Fail($"Pas de connexion de a à c ({edges} edges) ; avant le geste : {seen} ; après : {now}");
        }
    }

    /// <summary>La page change après la mesure et le cadrage, sans que la taille du graphe change.</summary>
    [Theory]
    [InlineData("page défilée", """
        () => { document.querySelector('.graph').style.marginTop = '900px'; document.body.style.height = '2400px';
                scrollTo(0, 900); }
        """)]
    [InlineData("conteneur défilant", """
        () => { const g = document.querySelector('.graph'); g.style.height = '600px'; g.style.overflow = 'auto';
                for (const side of g.children) side.style.cssText = 'flex:1;height:600px;position:relative;margin-top:300px';
                g.scrollTop = 300; }
        """)]
    [InlineData("contenu inséré au-dessus", """
        () => { const bar = document.createElement('div'); bar.style.height = '130px';
                document.querySelector('.graph').before(bar); }
        """)]
    [InlineData("conteneur déplacé de côté", """() => { document.querySelector('.graph').style.marginLeft = '37px'; }""")]
    [InlineData("parent réduit (scale 0,6)", """
        () => { const g = document.querySelector('.graph'); g.style.transformOrigin = '0 0'; g.style.transform = 'scale(0.6)'; }
        """)]
    [InlineData("parent agrandi (scale 1,3)", """
        () => { const g = document.querySelector('.graph'); g.style.height = '560px'; // pas plus étroit : « c » resterait au bord, où la vue défile seule
                g.style.transformOrigin = '0 0'; g.style.transform = 'scale(1.3)'; }
        """)]
    public async Task Gestures_aim_right_after_the_page_changes(string condition, string script)
    {
        var (page, errors) = await OpenAsync();
        _ = condition;

        await page.EvaluateAsync(script);
        await page.WaitForTimeoutAsync(300);

        await AssertGesturesAimRightAsync(page);
        Assert.Empty(errors);
    }

    /// <summary>Un parent à l'échelle dès le chargement : la mesure et le cadrage tiennent compte de la taille réelle du graphe.</summary>
    [Fact]
    public async Task A_scaled_parent_at_load_does_not_skew_measure_and_fit()
    {
        var (page, errors) = await OpenAsync(css: ".graph { transform: scale(0.6); transform-origin: 0 0; }");
        await page.WaitForTimeoutAsync(500);

        await AssertEdgeAttachedAsync(page);
        var frame = await FrameAsync(page);
        var nodes = new List<LocatorBoundingBoxResult>();
        foreach (var id in new[] { "a", "b", "c" }) nodes.Add((await page.Locator(Node(id)).BoundingBoxAsync())!);
        var (left, top) = frame.ToFlow(nodes.Min(n => n.X), nodes.Min(n => n.Y));
        var (right, bottom) = frame.ToFlow(nodes.Max(n => n.X + n.Width), nodes.Max(n => n.Y + n.Height));
        // Le contenu cadré est au centre du graphe (taille de mise en page, avant l'échelle du parent).
        Near((frame.Width / 2, frame.Height / 2), (frame.X + (left + right) / 2 * frame.Zoom, frame.Y + (top + bottom) / 2 * frame.Zoom), 4, "contenu centré");

        await AssertGesturesAimRightAsync(page);
        Assert.Empty(errors);
    }

    /// <summary>Le zoom du navigateur change la taille de la page en pixels CSS et la densité de l'écran.</summary>
    [Theory]
    [InlineData(0.67f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public async Task Gestures_aim_right_at_another_browser_zoom_or_pixel_density(float factor)
    {
        var (page, errors) = await OpenAsync(configure: o =>
        {
            o.DeviceScaleFactor = factor;
            o.ViewportSize = factor == 2f
                ? new ViewportSize { Width = 1280, Height = 800 }
                : new ViewportSize { Width = (int)(1280 / factor), Height = (int)(800 / factor) };
        });

        await AssertEdgeAttachedAsync(page);
        await AssertGesturesAimRightAsync(page);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("onglet masqué", ".graph { display: none; }")]
    [InlineData("conteneur invisible", ".graph { visibility: hidden; }")]
    [InlineData("conteneur de largeur nulle", ".graph > div { flex: none !important; width: 0; overflow: hidden; }")]
    public async Task A_view_shown_after_loading_hidden_is_measured_and_framed(string condition, string css)
    {
        var (page, errors) = await OpenAsync(css: css);
        _ = condition;
        await page.WaitForTimeoutAsync(500);

        await page.EvaluateAsync("() => document.getElementById('hostile').remove()");

        await page.UntilAsync(async () => await page.CountAsync(".wf-edge-path") == 2, "les edges sont tracés");
        await page.WaitForTimeoutAsync(600);
        await AssertEdgeAttachedAsync(page);
        var root = (await page.Locator(Left + " .wf-flow").BoundingBoxAsync())!;
        foreach (var id in new[] { "a", "b", "c" })
        {
            var box = (await page.Locator(Node(id)).BoundingBoxAsync())!;
            Assert.True(box.X >= root.X && box.Y >= root.Y && box.X + box.Width <= root.X + root.Width && box.Y + box.Height <= root.Y + root.Height,
                $"Le node {id} n'est pas cadré dans la vue : ({box.X:0},{box.Y:0}) {box.Width:0}×{box.Height:0}, zoom {(await FrameAsync(page)).Zoom}");
        }

        await AssertGesturesAimRightAsync(page);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_view_hidden_then_shown_again_keeps_working()
    {
        var (page, errors) = await OpenAsync();
        var frame = await FrameAsync(page);

        await page.EvaluateAsync("() => document.querySelector('.graph').style.display = 'none'");
        await page.WaitForTimeoutAsync(600);
        await page.EvaluateAsync("() => document.querySelector('.graph').style.display = 'flex'");
        await page.WaitForTimeoutAsync(600);

        Assert.Equal(frame, await FrameAsync(page));
        await AssertEdgeAttachedAsync(page);
        await AssertGesturesAimRightAsync(page);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Two_views_on_a_page_are_independent()
    {
        var (page, errors) = await OpenAsync();

        // Marqueurs SVG et motifs de fond : des identifiants distincts, sinon une vue emprunte ceux de l'autre.
        var ids = await page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('.graph [id] marker, .graph marker, .graph pattern')].map(e => e.id)");
        Assert.Equal(ids.Distinct().Count(), ids.Length);

        var rightFrame = await FrameAsync(page, "#right");
        var rightB = await PosAsync(page, "b", "#right");
        await AssertGesturesAimRightAsync(page);
        Assert.Equal(rightFrame, await FrameAsync(page, "#right"));
        Assert.Equal(rightB, await PosAsync(page, "b", "#right"));
        Assert.Equal(1, await page.CountAsync("#right .wf-edge"));
        Assert.Equal(0, await page.CountAsync("#right .wf-node[aria-selected='true']"));

        // Sélection et clavier : la touche ne concerne que la vue qui a le focus.
        var c = await page.CenterAsync(Node("c"));
        await page.Mouse.ClickAsync((float)c.X, (float)c.Y);
        await page.WaitForTimeoutAsync(600);
        var other = await page.CenterAsync(Node("a", "#right"));
        await page.Mouse.ClickAsync((float)other.X, (float)other.Y);
        await page.WaitForTimeoutAsync(300);
        Assert.Equal(1, await page.CountAsync("#left .wf-node[aria-selected='true']"));
        await page.Keyboard.PressAsync("Delete");
        await page.UntilAsync(async () => await page.CountAsync("#right .wf-node") == 2, "le node de droite est supprimé");
        Assert.Equal(3, await page.CountAsync("#left .wf-node"));
        await page.Keyboard.PressAsync("Control+z");
        await page.WaitForTimeoutAsync(1000);

        await page.UntilAsync(async () => await page.CountAsync("#right .wf-node") == 3, "annulation à droite");
        Assert.Equal(3, await page.CountAsync("#left .wf-node"));

        // Presse-papier : ce que la vue de droite copie ne se colle pas dans celle de gauche.
        await page.Mouse.ClickAsync((float)other.X, (float)other.Y);
        await page.WaitForTimeoutAsync(300);
        await page.Keyboard.PressAsync("Control+c");
        var leftPane = await PanePointAsync(page);
        await page.Mouse.ClickAsync((float)leftPane.X, (float)leftPane.Y);
        await page.WaitForTimeoutAsync(300);
        await page.Keyboard.PressAsync("Control+v");
        await page.WaitForTimeoutAsync(500);
        Assert.Equal(3, await page.CountAsync("#left .wf-node"));
        Assert.Equal(3, await page.CountAsync("#right .wf-node"));

        // Un fil tiré d'une vue ne se branche pas dans l'autre.
        var leftEdges = await page.CountAsync("#left .wf-edge");
        await page.DragAsync(await page.CenterAsync(Handle("a", "source")), await page.CenterAsync(Handle("c", "target", "#right")));
        await page.WaitForTimeoutAsync(500);
        Assert.Equal(leftEdges, await page.CountAsync("#left .wf-edge"));
        Assert.Equal(1, await page.CountAsync("#right .wf-edge"));
        Assert.Equal(0, await page.CountAsync(".wf-connection-line"));
        Assert.Empty(errors);
    }

    /// <summary>Les styles globaux d'un cadriciel CSS (Bootstrap, Tailwind, bibliothèques d'icônes) ne cassent pas le graphe.</summary>
    [Fact]
    public async Task Aggressive_global_css_does_not_break_the_graph()
    {
        var (page, errors) = await OpenAsync();
        var handle = (await page.Locator(Handle("a", "source")).BoundingBoxAsync())!;
        var control = (await page.Locator(Left + " .wf-controls button").First.BoundingBoxAsync())!;
        var minimap = (await page.Locator(Left + " .wf-minimap svg").BoundingBoxAsync())!;

        await page.AddStyleTagAsync(new PageAddStyleTagOptions
        {
            Content = """
                *, *::before, *::after { box-sizing: border-box; }
                * { margin: 0; padding: 0; }
                img, svg, video, canvas { display: block; vertical-align: middle; max-width: 100%; height: auto; }
                svg { width: 16px; height: 16px; overflow: hidden; }
                path { fill: currentColor; stroke: none; }
                button { padding: 14px 24px; margin: 6px; font-size: 20px; line-height: 2; min-width: 80px; border: 3px solid red;
                         position: relative; top: 2px; }
                div { line-height: 1.8; }
                """
        });
        await page.WaitForTimeoutAsync(600);

        Assert.Equal(handle.Width, (await page.Locator(Handle("a", "source")).BoundingBoxAsync())!.Width, 1);
        var after = (await page.Locator(Left + " .wf-controls button").First.BoundingBoxAsync())!;
        Assert.Equal((control.Width, control.Height), (after.Width, after.Height));
        var map = (await page.Locator(Left + " .wf-minimap svg").BoundingBoxAsync())!;
        Assert.Equal((minimap.Width, minimap.Height), (map.Width, map.Height));
        var background = (await page.Locator(Left + " .wf-background").BoundingBoxAsync())!;
        var root = (await page.Locator(Left + " .wf-flow").BoundingBoxAsync())!;
        Assert.Equal((root.Width, root.Height), (background.Width, background.Height));

        // L'edge est visible (ni masqué ni rogné) et se laisse cliquer, et son tracé n'est pas rempli.
        var state = await page.EvaluateAsync<string[]>("""
            () => {
                const path = document.querySelector('#left .wf-edge-hit');
                const m = path.getScreenCTM();
                const p = path.getPointAtLength(path.getTotalLength() / 2).matrixTransform(m);
                const line = document.querySelector('#left .wf-edge-path');
                const svg = document.querySelector('#left .wf-edges');
                return [String(document.elementFromPoint(p.x, p.y)?.getAttribute('class')), getComputedStyle(line).fill,
                        getComputedStyle(line).stroke, String(svg.getBoundingClientRect().width > 0), getComputedStyle(svg).overflow];
            }
            """);
        Assert.Equal("wf-edge-hit", state[0]);
        Assert.Equal("none", state[1]);
        Assert.NotEqual("none", state[2]);
        Assert.Equal("true", state[3]);
        Assert.Equal("visible", state[4]);

        await AssertGesturesAimRightAsync(page);
        Assert.Empty(errors);
    }

    private static async Task<int> WindowListenersAsync(ICDPSession cdp)
    {
        await cdp.SendAsync("HeapProfiler.collectGarbage");
        var window = await cdp.SendAsync("Runtime.evaluate", new Dictionary<string, object> { ["expression"] = "window" });
        var id = window!.Value.GetProperty("result").GetProperty("objectId").GetString()!;
        var listeners = await cdp.SendAsync("DOMDebugger.getEventListeners", new Dictionary<string, object> { ["objectId"] = id });
        return listeners!.Value.GetProperty("listeners").GetArrayLength();
    }

    private static async Task<double> MetricAsync(ICDPSession cdp, string name)
    {
        var metrics = await cdp.SendAsync("Performance.getMetrics");
        return metrics!.Value.GetProperty("metrics").EnumerateArray().First(m => m.GetProperty("name").GetString() == name)
            .GetProperty("value").GetDouble();
    }

    /// <summary>Cinquante montages et démontages : écouteurs JS, éléments du DOM et références .NET sont libérés.</summary>
    [Fact]
    public async Task Fifty_mounts_release_listeners_and_views()
    {
        var (page, errors) = await OpenAsync("mount");
        var cdp = await page.Context.NewCDPSessionAsync(page);
        await cdp.SendAsync("Performance.enable");

        async Task ToggleAsync(bool shown)
        {
            await page.ClickAsync("#toggle");
            await page.UntilAsync(async () => await page.CountAsync(".wf-edge-path") == (shown ? 1 : 0),
                shown ? "la vue est montée et mesurée" : "la vue est retirée");
        }

        // Un premier aller-retour : ce qui n'est créé qu'une fois (module JS) ne compte pas.
        await ToggleAsync(false);
        await ToggleAsync(true);
        await ToggleAsync(false);
        var listeners = await WindowListenersAsync(cdp);
        var jsListeners = await MetricAsync(cdp, "JSEventListeners");
        var nodes = await MetricAsync(cdp, "Nodes");

        for (var i = 0; i < 25; i++)
        {
            await ToggleAsync(true);
            // Un geste en cours de route laisse des états (minuteries, capture) à libérer aussi.
            var b = await page.CenterAsync(Node("b"));
            await page.DragAsync(b, (b.X + 30, b.Y + 20), steps: 4);
            await ToggleAsync(false);
        }

        Assert.Equal(listeners, await WindowListenersAsync(cdp));
        await page.WaitForTimeoutAsync(500);
        await cdp.SendAsync("HeapProfiler.collectGarbage");
        Assert.InRange(await MetricAsync(cdp, "JSEventListeners"), 0, jsListeners + 5);
        Assert.InRange(await MetricAsync(cdp, "Nodes"), 0, nodes + 200);

        // Les vues retirées sont libérées ; Blazor peut garder la toute dernière (arbre de rendu précédent).
        await page.ClickAsync("#count-alive");
        await page.UntilAsync(async () => (await TextAsync(page, "alive")).Contains('/'), "comptage", 15000);
        var counts = (await TextAsync(page, "alive")).Split('/').Select(int.Parse).ToArray();
        Assert.True(counts[1] >= 27, "vues montées : " + counts[1]);
        Assert.InRange(counts[0], 0, 1);
        Assert.Empty(errors);
    }

    /// <summary>Une exception de l'application pendant un geste : le geste se termine et la vue reste utilisable.</summary>
    [Theory]
    [InlineData("début", "drag")]
    [InlineData("fin", "drag")]
    [InlineData("clic", "click")]
    [InlineData("connexion", "connect")]
    public async Task A_throwing_handler_leaves_the_view_usable(string where, string gesture)
    {
        var (page, errors) = await OpenAsync("throw");
        await page.ClickAsync("#arm");
        await ExpectTextAsync(page, "arm", "exceptions armées");

        var b = await page.CenterAsync(Node("b"));
        switch (gesture)
        {
            case "drag":
                await page.DragAsync(b, (b.X + 60, b.Y + 30));
                break;
            case "click":
                await page.Mouse.ClickAsync((float)b.X, (float)b.Y);
                break;
            default:
                await page.DragAsync(await page.CenterAsync(Handle("b", "source")), await page.CenterAsync(Handle("c", "target")));
                break;
        }

        await page.UntilAsync(async () => (await TextAsync(page, "thrown") + string.Join(" ", errors)).Contains(where) || errors.Count > 0,
            "l'exception est levée");
        await page.WaitForTimeoutAsync(700);
        await page.ClickAsync("#arm");
        await ExpectTextAsync(page, "arm", "armer les exceptions");

        // Le geste est fini : un mouvement sans bouton ne déplace plus rien et aucun fil ne suit la souris.
        var position = await PosAsync(page, "b");
        var frame = await FrameAsync(page);
        await page.Mouse.MoveAsync(300, 300);
        await page.Mouse.MoveAsync(500, 450, new MouseMoveOptions { Steps = 5 });
        await page.WaitForTimeoutAsync(400);
        Assert.Equal(position, await PosAsync(page, "b"));
        Assert.Equal(frame, await FrameAsync(page));
        Assert.Equal(0, await page.CountAsync(".wf-connection-line"));
        Assert.Equal(0, await page.CountAsync(".wf-node.wf-dragging"));

        await AssertGesturesAimRightAsync(page);
    }

    /// <summary>
    ///     Sur une page chargée, l'image suivante tarde : le relâchement arrive avant que le dernier mouvement ait été transmis.
    ///     Le geste doit finir là où la souris a été relâchée.
    /// </summary>
    [Theory]
    [InlineData("node")]
    [InlineData("connexion")]
    [InlineData("fond")]
    public async Task A_release_before_the_next_frame_ends_where_the_mouse_is(string gesture)
    {
        var (page, errors) = await OpenAsync();
        await page.EvaluateAsync("() => { window.requestAnimationFrame = callback => setTimeout(() => callback(performance.now()), 2000); }");
        var frame = await FrameAsync(page);
        var position = await PosAsync(page, "b");
        var edges = await page.CountAsync("#left .wf-edge");

        switch (gesture)
        {
            case "node":
                var b = await page.CenterAsync(Node("b"));
                await page.DragAsync(b, (b.X + 80, b.Y + 40));
                await page.UntilAsync(async () => (await TextAsync(page, "log")).Contains("glissé b"), "fin du glisser");
                Near((position.X + 80 / frame.Zoom, position.Y + 40 / frame.Zoom), await PosAsync(page, "b"), 2, "le node finit sous la souris");
                break;
            case "connexion":
                await page.DragAsync(await page.CenterAsync(Handle("b", "source")), await page.CenterAsync(Handle("c", "target")));
                await page.UntilAsync(async () => await page.CountAsync("#left .wf-edge") == edges + 1, "la connexion est faite");
                break;
            default:
                var pane = await PanePointAsync(page);
                await page.DragAsync(pane, (pane.X + 40, pane.Y + 24));
                await page.WaitForTimeoutAsync(600);
                var panned = await FrameAsync(page);
                Near((frame.X + 40, frame.Y + 24), (panned.X, panned.Y), 2, "la vue finit sous la souris");
                break;
        }

        Assert.Empty(errors);
    }

    /// <summary>La virgule décimale ne passe ni dans les styles, ni dans les tracés, ni dans les attributs SVG.</summary>
    [Fact]
    public async Task A_french_culture_keeps_styles_and_paths_valid()
    {
        var (page, errors) = await OpenAsync(configure: o => o.Locale = "fr-FR");

        Assert.Equal("1,5", await page.Locator("#culture").InnerTextAsync());
        await AssertGesturesAimRightAsync(page);
        var invalid = await page.EvaluateAsync<string[]>("""
            () => {
                const bad = [];
                // Le navigateur écarte une déclaration qu'il ne comprend pas : chaque déclaration écrite doit avoir été comprise.
                for (const e of document.querySelectorAll('.wf-flow [style]')) {
                    for (const declaration of e.getAttribute('style').split(';')) {
                        const at = declaration.indexOf(':');
                        if (at < 0) continue;
                        const name = declaration.slice(0, at).trim(), value = declaration.slice(at + 1).trim();
                        if (value && !name.startsWith('--') && !CSS.supports(name, value)) bad.push(declaration);
                    }
                }
                for (const e of document.querySelectorAll('.wf-flow svg, .wf-flow svg *'))
                    for (const name of ['x', 'y', 'width', 'height', 'r', 'cx', 'cy', 'rx', 'stroke-width', 'markerWidth', 'markerHeight'])
                        if (e.getAttribute(name)?.includes(',')) bad.push(name + '=' + e.getAttribute(name));
                // Dans un tracé, la virgule ne sépare que deux nombres : trois nombres à la suite trahissent une décimale.
                for (const path of document.querySelectorAll('.wf-flow path[d]'))
                    if (/\d,\d+,\d/.test(path.getAttribute('d'))) bad.push('d=' + path.getAttribute('d'));
                return bad;
            }
            """);
        Assert.Empty(invalid);
        await AssertEdgeAttachedAsync(page);
        Assert.Empty(errors);
    }
}
