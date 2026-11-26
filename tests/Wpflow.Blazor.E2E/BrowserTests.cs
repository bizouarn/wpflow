using Microsoft.Playwright;

namespace Wpflow.Blazor.E2E;

/// <summary>Gestes de bout en bout dans un vrai navigateur : pont JS, mesure, pointeur, molette, clavier, tactile, export.</summary>
public abstract class BrowserTests(DemoFixture demo) : PagesTest(demo)
{
    private static async Task<(double X, double Y)> HandleAsync(IPage page, string node, string kind) =>
        await page.CenterAsync($"[data-node-id='{node}'] .wf-handle[data-type='{kind}']");

    [Fact]
    public async Task The_demo_measures_its_nodes_and_draws_the_edges()
    {
        var page = await App.OpenAsync();

        Assert.Equal(7, await page.CountAsync(".wf-node"));
        Assert.Equal(5, await page.CountAsync(".wf-edge-path"));
        // les handles sont mesurés : les edges aboutissent au bord des nodes, pas au coin haut gauche du conteneur
        var d = await page.Locator(".wf-edge-path").First.GetAttributeAsync("d");
        Assert.DoesNotContain("M0,0", d);
        Assert.Empty((await page.QuerySelectorAllAsync(".wf-minimap-node")).Skip(7));
    }

    [Fact]
    public async Task Double_clicking_a_node_reaches_the_node_and_does_not_zoom()
    {
        var page = await App.OpenAsync();
        var zoom = (await page.ViewportAsync()).Zoom;
        var centre = await page.CenterAsync("[data-node-id='2']");

        await page.Mouse.DblClickAsync((float)centre.X, (float)centre.Y);

        await page.UntilAsync(async () => await page.Locator(".demo-double").InnerTextAsync() == "double-clic sur 2", "le node a reçu le double-clic");
        await page.WaitForTimeoutAsync(400); // le zoom du fond serait animé sur 200 ms
        Assert.Equal(zoom, (await page.ViewportAsync()).Zoom);
    }

    [Fact]
    public async Task The_data_attributes_of_the_clicked_element_reach_the_node_event()
    {
        var page = await App.OpenAsync();
        // une « colonne » avec un enfant : l'attribut est lu sur un parent de l'élément réellement cliqué
        await page.EvaluateAsync("""
            () => {
                const row = document.createElement('div');
                row.dataset.column = 'client_id';
                row.innerHTML = '<span id="cell">cellule</span>';
                document.querySelector("[data-node-id='2']").appendChild(row);
            }
            """);
        var cell = await page.CenterAsync("#cell");

        await page.Mouse.DblClickAsync((float)cell.X, (float)cell.Y);

        await page.UntilAsync(async () => await page.Locator(".demo-double").InnerTextAsync() == "double-clic sur 2 (client_id)", "la colonne est identifiée");
    }

    [Fact]
    public async Task Double_clicking_an_edge_does_not_zoom()
    {
        var page = await App.OpenAsync();
        var zoom = (await page.ViewportAsync()).Zoom;
        // un point du tracé lui-même (le centre du rectangle d'une courbe peut être à côté)
        var point = await page.EvaluateAsync<double[]>("""
            () => {
                const path = document.querySelector('.wf-edge-hit');
                const p = path.getPointAtLength(path.getTotalLength() / 2).matrixTransform(path.getScreenCTM());
                return [p.x, p.y];
            }
            """);

        await page.Mouse.DblClickAsync((float)point[0], (float)point[1]);

        await page.WaitForTimeoutAsync(500);
        Assert.Equal(zoom, (await page.ViewportAsync()).Zoom);
    }

    [Fact]
    public async Task A_drag_released_outside_the_view_still_ends_the_gesture()
    {
        var page = await App.OpenAsync();
        var before = await page.NodePosAsync("2");
        var from = await page.CenterAsync("[data-node-id='2']");

        await page.Mouse.MoveAsync((float)from.X, (float)from.Y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(-50, -50); // sort d'un seul bond
        await page.UntilAsync(async () => (await page.NodePosAsync("2")) != before, "le node a suivi");
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(300);
        var after = await page.NodePosAsync("2");

        await page.Mouse.MoveAsync(640, 400);
        await page.WaitForTimeoutAsync(300);
        Assert.Equal(after, await page.NodePosAsync("2")); // le geste est bien terminé
    }

    [Fact]
    public async Task Dragging_a_node_moves_it_and_undo_brings_it_back()
    {
        var page = await App.OpenAsync();
        var before = await page.NodePosAsync("2");
        var from = await page.CenterAsync("[data-node-id='2']");

        await page.DragAsync(from, (from.X + 120, from.Y + 60));
        await page.UntilAsync(async () => (await page.NodePosAsync("2")) != before, "le node a bougé");
        var after = await page.NodePosAsync("2");
        var zoom = (await page.ViewportAsync()).Zoom;
        Assert.Equal(before.X + 120 / zoom, after.X, 0);
        Assert.Equal(before.Y + 60 / zoom, after.Y, 0);

        await page.Keyboard.PressAsync("Control+z");
        await page.UntilAsync(async () => (await page.NodePosAsync("2")) == before, "annuler remet le node en place");
    }

    [Fact]
    public async Task Dragging_from_a_handle_to_another_handle_creates_an_edge()
    {
        var page = await App.OpenAsync();
        var edges = await page.CountAsync(".wf-edge-path");

        await page.DragAsync(await HandleAsync(page, "1", "source"), await HandleAsync(page, "4", "target"), steps: 25);

        await page.UntilAsync(async () => await page.CountAsync(".wf-edge-path") == edges + 1, "un edge de plus");
    }

    [Fact]
    public async Task The_wheel_zooms_around_the_cursor_and_the_pane_pans()
    {
        var page = await App.OpenAsync();
        var start = await page.ViewportAsync();

        await page.Mouse.MoveAsync(640, 400);
        await page.Mouse.WheelAsync(0, -300);
        await page.UntilAsync(async () => (await page.ViewportAsync()).Zoom > start.Zoom, "le zoom augmente");

        var zoomed = await page.ViewportAsync();
        await page.DragAsync((100, 500), (160, 540));
        await page.UntilAsync(async () => (await page.ViewportAsync()).X > zoomed.X + 50, "la vue a défilé");
        var panned = await page.ViewportAsync();
        Assert.Equal(zoomed.X + 60, panned.X, 0);
        Assert.Equal(zoomed.Y + 40, panned.Y, 0);
    }

    [Fact]
    public async Task A_slider_inside_a_node_keeps_its_behaviour_and_does_not_drag_the_node()
    {
        var page = await App.OpenAsync();
        var node = await page.NodePosAsync("s");
        var slider = page.Locator("[data-node-id='s'] input[type=range]");
        var value = await slider.InputValueAsync();
        var box = (await slider.BoundingBoxAsync())!;

        await page.DragAsync((box.X + box.Width * 0.5, box.Y + box.Height / 2), (box.X + box.Width * 0.9, box.Y + box.Height / 2));

        await page.UntilAsync(async () => await slider.InputValueAsync() != value, "le curseur a changé");
        Assert.Equal(node, await page.NodePosAsync("s"));
    }

    [Fact]
    public async Task A_drag_handle_restricts_dragging_to_the_handle()
    {
        var page = await App.OpenAsync();
        var before = await page.NodePosAsync("g");

        var body = await page.CenterAsync("[data-node-id='g'] .body");
        await page.DragAsync(body, (body.X + 80, body.Y + 80));
        await page.WaitForTimeoutAsync(300);
        Assert.Equal(before, await page.NodePosAsync("g")); // le corps ne déplace pas

        var grip = await page.CenterAsync("[data-node-id='g'] .grip");
        await page.DragAsync(grip, (grip.X + 80, grip.Y + 80));
        await page.UntilAsync(async () => (await page.NodePosAsync("g")) != before, "la poignée déplace le node");
    }

    [Fact]
    public async Task Selecting_resizing_and_rotating_work_with_the_pointer()
    {
        var page = await App.OpenAsync();
        var node = page.Locator("[data-node-id='r']");

        var centre = await page.CenterAsync("[data-node-id='r']");
        await page.Mouse.ClickAsync((float)centre.X, (float)centre.Y);
        await page.UntilAsync(async () => await page.CountAsync("[data-node-id='r'] .wf-resize") == 8, "les poignées apparaissent");

        var before = (await node.BoundingBoxAsync())!;
        var corner = await page.CenterAsync("[data-node-id='r'] .wf-resize-bottomright");
        await page.DragAsync(corner, (corner.X + 60, corner.Y + 40));
        await page.UntilAsync(async () => (await node.BoundingBoxAsync())!.Width > before.Width + 20, "le node s'élargit");

        var rotator = await page.CenterAsync("[data-node-id='r'] .wf-rotator");
        var box = (await node.BoundingBoxAsync())!;
        await page.DragAsync(rotator, (box.X + box.Width + 100, box.Y + box.Height / 2));
        await page.UntilAsync(async () => (await node.GetAttributeAsync("style"))!.Contains("rotate("), "le node pivote");
    }

    [Fact]
    public async Task Keyboard_select_all_arrows_and_delete()
    {
        var page = await App.OpenAsync();
        await page.Mouse.ClickAsync(60, 700); // fond : donne le focus au graphe

        await page.Keyboard.PressAsync("Control+a");
        await page.UntilAsync(async () => await page.CountAsync(".wf-node.wf-selected") == 7, "tout est sélectionné");

        var before = await page.NodePosAsync("2");
        await page.Keyboard.PressAsync("ArrowRight");
        await page.UntilAsync(async () => (await page.NodePosAsync("2")).X == before.X + 5, "les flèches déplacent de 5 px");

        await page.Keyboard.PressAsync("Delete");
        await page.UntilAsync(async () => await page.CountAsync(".wf-node") == 0, "tout est supprimé");
        Assert.Equal(0, await page.CountAsync(".wf-edge-path"));
    }

    [Fact]
    public async Task Shift_drag_draws_a_selection_box()
    {
        var page = await App.OpenAsync();
        var a = await page.CenterAsync("[data-node-id='2']");
        var b = await page.CenterAsync("[data-node-id='3']");

        await page.Keyboard.DownAsync("Shift");
        await page.DragAsync((Math.Min(a.X, b.X) - 150, a.Y - 80), (Math.Max(a.X, b.X) + 150, a.Y + 80));
        await page.Keyboard.UpAsync("Shift");

        await page.UntilAsync(async () => await page.CountAsync(".wf-node.wf-selected") >= 2, "le rectangle sélectionne les nodes");
        Assert.Contains("wf-selected", await page.Locator("[data-node-id='2']").GetAttributeAsync("class"));
        Assert.DoesNotContain("wf-selected", await page.Locator("[data-node-id='4']").GetAttributeAsync("class"));
    }

    [Fact]
    public async Task Dragging_near_the_edge_scrolls_the_view_automatically()
    {
        var page = await App.OpenAsync();
        var start = await page.ViewportAsync();
        var from = await page.CenterAsync("[data-node-id='3']");

        // le curseur reste immobile près du bord droit : seul le défilement automatique fait bouger la vue
        await page.DragAsync(from, (1272, from.Y), steps: 15, pauseMs: 800);

        Assert.True((await page.ViewportAsync()).X < start.X - 20, "la vue a défilé vers la droite");
    }

    [Fact]
    public async Task A_two_finger_pinch_zooms()
    {
        var page = await App.OpenAsync(touch: true);
        var start = (await page.ViewportAsync()).Zoom;
        var cdp = await page.Context.NewCDPSessionAsync(page);

        object[] Points(double spread) =>
        [
            new { x = 640 - spread, y = 400, id = 1 },
            new { x = 640 + spread, y = 400, id = 2 }
        ];

        async Task Touch(string type, object[] points) =>
            await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object> { ["type"] = type, ["touchPoints"] = points });

        await Touch("touchStart", Points(60));
        for (var spread = 70; spread <= 200; spread += 10)
        {
            await Touch("touchMove", Points(spread));
            await page.WaitForTimeoutAsync(30);
        }

        await Touch("touchEnd", []);

        await page.UntilAsync(async () => (await page.ViewportAsync()).Zoom > start * 1.1, "le pincement zoome");
    }

    [Fact]
    public async Task The_dark_theme_and_the_controls_work()
    {
        var page = await App.OpenAsync();

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sombre" }).ClickAsync();
        await page.UntilAsync(async () => (await page.Locator(".wf-flow").GetAttributeAsync("class"))!.Contains("wf-dark"), "thème sombre");

        var start = (await page.ViewportAsync()).Zoom;
        await page.GetByLabel("Zoom avant").ClickAsync();
        await page.UntilAsync(async () => (await page.ViewportAsync()).Zoom > start, "zoom avant");

        await page.GetByLabel("Verrouiller").ClickAsync();
        var before = await page.NodePosAsync("2");
        var from = await page.CenterAsync("[data-node-id='2']");
        await page.DragAsync(from, (from.X + 100, from.Y + 100));
        await page.WaitForTimeoutAsync(300);
        Assert.Equal(before, await page.NodePosAsync("2")); // verrouillé
    }

    // Ignoré : après le téléchargement, fermer le contexte arrête le pilote Playwright (voir work-doc/TESTS-E2E-IGNORES.md).
    [Fact(Skip = "Plante le pilote Playwright 1.49 avec Edge : work-doc/TESTS-E2E-IGNORES.md")]
    public async Task The_png_export_downloads_a_real_image()
    {
        var page = await App.OpenAsync();

        var download = await page.RunAndWaitForDownloadAsync(() => page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "PNG" }).ClickAsync());

        Assert.Equal("graphe.png", download.SuggestedFilename);
        var path = await download.PathAsync();
        var bytes = await File.ReadAllBytesAsync(path!);
        Assert.True(bytes.Length > 8000, "image de " + bytes.Length + " octets"); // le rendu réel, pas un cadre vide
        if (Environment.GetEnvironmentVariable("WPFLOW_E2E_OUT") is { Length: > 0 } copy) File.Copy(path!, copy, true);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes[..4]); // signature PNG
    }

    [Fact]
    public async Task Minimap_click_recentres_the_view()
    {
        var page = await App.OpenAsync();
        var start = await page.ViewportAsync();
        var box = (await page.Locator(".wf-minimap svg").BoundingBoxAsync())!;

        await page.Mouse.ClickAsync((float)(box.X + 10), (float)(box.Y + 10)); // coin haut gauche de la minimap

        await page.UntilAsync(async () => (await page.ViewportAsync()).X != start.X, "la vue s'est recentrée");
    }
}

/// <summary>Les mêmes gestes avec Blazor Server.</summary>
public sealed class ServerBrowserTests(ServerFixture demo) : BrowserTests(demo), IClassFixture<ServerFixture>;

/// <summary>Les mêmes gestes avec Blazor WebAssembly.</summary>
public sealed class WasmBrowserTests(WasmFixture demo) : BrowserTests(demo), IClassFixture<WasmFixture>;
