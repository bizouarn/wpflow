using Microsoft.Playwright;

namespace Wpflow.Blazor.E2E;

/// <summary>
///     Blazor Server loin de son serveur (ROADMAP-TESTS, lot 4) : latence, coupure puis reprise du circuit, page fermée
///     pendant un geste. WebAssembly n'est pas concerné.
/// </summary>
public sealed class ServerNetworkTests(ScenarioServerFixture app) : IClassFixture<ScenarioServerFixture>
{
    private const string Orders = "[data-node-id='orders']";

    private async Task<(IPage Page, List<string> Errors)> OpenAsync(DelayProxy proxy)
    {
        var page = await app.OpenAsync(url: proxy.Url);
        var errors = page.CollectErrors();
        await page.WaitForTimeoutAsync(1500); // second cadrage, après le chargement asynchrone
        return (page, errors);
    }

    private static Task<(double X, double Y)> TitleAsync(IPage page) => page.CenterAsync(Orders + " .title");

    private static async Task ExpectEventsAsync(IPage page, string expected, int oneWay)
    {
        await page.WaitForTimeoutAsync(oneWay * 4 + 900);
        Assert.Equal(expected, (await page.Locator("#events").InnerTextAsync()).Trim());
    }

    /// <summary>L'aller-retour vaut 100 ou 300 ms : le node ne saute pas pendant le glisser et finit sous la souris.</summary>
    [Theory]
    [InlineData(50)]
    [InlineData(150)]
    public async Task A_slow_server_drags_without_jumps_and_keeps_the_event_order(int oneWay)
    {
        await using var proxy = new DelayProxy(app.Url);
        var (page, errors) = await OpenAsync(proxy);
        proxy.Delay = TimeSpan.FromMilliseconds(oneWay);
        var zoom = (await page.ViewportAsync()).Zoom;
        var start = await page.NodePosAsync("orders");

        // Toutes les positions affichées pendant le geste sont relevées dans la page.
        await page.EvaluateAsync("""
            () => {
                window.seen = [];
                const node = document.querySelector("[data-node-id='orders']");
                new MutationObserver(() => {
                    const t = /translate\((-?[\d.]+)px,\s*(-?[\d.]+)px\)/.exec(node.getAttribute('style'));
                    window.seen.push([+t[1], +t[2]]);
                }).observe(node, { attributes: true, attributeFilter: ['style'] });
            }
            """);

        var from = await TitleAsync(page);
        await page.Mouse.MoveAsync((float)from.X, (float)from.Y);
        await page.Mouse.DownAsync();
        for (var i = 1; i <= 30; i++)
        {
            await page.Mouse.MoveAsync((float)from.X + i * 4, (float)from.Y + i * 2);
            await page.WaitForTimeoutAsync(20);
        }

        await page.Mouse.UpAsync(); // relâché avant que le serveur ait répondu aux derniers mouvements
        await page.WaitForTimeoutAsync(oneWay * 6 + 500);

        var end = await page.NodePosAsync("orders");
        Assert.InRange(end.X - start.X, 120 / zoom - 2, 120 / zoom + 2);
        Assert.InRange(end.Y - start.Y, 60 / zoom - 2, 60 / zoom + 2);
        var seen = await page.EvaluateAsync<double[][]>("() => window.seen");
        Assert.True(seen.Length > 3, "le node a suivi le pointeur pendant le geste : " + seen.Length);
        var previous = (start.X, start.Y);
        foreach (var p in seen)
        {
            Assert.True(p[0] >= previous.X - 0.5 && p[1] >= previous.Y - 0.5, $"retour en arrière : {previous} puis ({p[0]}, {p[1]})");
            previous = (p[0], p[1]);
        }

        // Double-clic : clic, double-clic, clic, comme sans latence. Un clic simple ensuite n'est ni perdu ni doublé.
        await page.ClickAsync("#clear-events");
        await page.WaitForTimeoutAsync(oneWay * 4 + 300);
        var column = await page.CenterAsync("[data-node-id='customers'] [data-column='email'] em");
        await page.Mouse.DblClickAsync((float)column.X, (float)column.Y);
        await ExpectEventsAsync(page, "clic customers.email > double-clic customers.email > clic customers.email", oneWay);
        await page.ClickAsync("#clear-events");
        await page.WaitForTimeoutAsync(oneWay * 4 + 600);
        var note = await page.CenterAsync("[data-node-id='orders'] [data-column='note'] span");
        await page.Mouse.ClickAsync((float)note.X, (float)note.Y);
        await ExpectEventsAsync(page, "clic orders.note", oneWay);
        Assert.Empty(errors);
    }

    /// <summary>Le réseau tombe puis revient : le circuit reprend et la vue reste utilisable, même coupée en plein geste.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_view_survives_a_lost_connection(bool duringADrag)
    {
        await using var proxy = new DelayProxy(app.Url);
        var (page, _) = await OpenAsync(proxy);
        var from = await TitleAsync(page);
        if (duringADrag)
        {
            await page.Mouse.MoveAsync((float)from.X, (float)from.Y);
            await page.Mouse.DownAsync();
            await page.Mouse.MoveAsync((float)from.X + 40, (float)from.Y + 20, new MouseMoveOptions { Steps = 5 });
            await page.WaitForTimeoutAsync(400);
            Assert.Equal(1, await page.CountAsync(Orders + ".wf-dragging"));
        }

        proxy.Cut();
        var modal = page.Locator("#components-reconnect-modal");
        await modal.WaitForAsync(new LocatorWaitForOptions { Timeout = 20000 });
        // Relâché pendant la coupure : le serveur n'en sait rien encore.
        if (duringADrag) await page.Mouse.UpAsync();

        await page.WaitForTimeoutAsync(500);
        proxy.Restore();
        await page.UntilAsync(async () => !await modal.IsVisibleAsync(), "le circuit reprend", 60000);
        await page.WaitForTimeoutAsync(1500);

        // Aucun geste ne reste en cours : bouger sans bouton ne déplace rien.
        await page.UntilAsync(async () => await page.CountAsync(".wf-node.wf-dragging") == 0, "le geste interrompu se termine", 8000);
        var position = await page.NodePosAsync("orders");
        var viewport = await page.ViewportAsync();
        await page.Mouse.MoveAsync(400, 300, new MouseMoveOptions { Steps = 4 });
        await page.Mouse.MoveAsync(1270, 790, new MouseMoveOptions { Steps = 4 }); // au bord : pas de défilement automatique
        await page.WaitForTimeoutAsync(600);
        Assert.Equal(position, await page.NodePosAsync("orders"));
        Assert.Equal(viewport, await page.ViewportAsync());

        from = await TitleAsync(page);
        await page.DragAsync(from, (from.X + 50, from.Y + 25));
        await page.UntilAsync(async () => await page.NodePosAsync("orders") != position, "un nouveau glisser déplace le node");
        await page.UntilAsync(async () => await page.CountAsync(".wf-node.wf-dragging") == 0, "et se termine");
    }

    /// <summary>La page se ferme pendant un geste (défilement automatique en cours) : rien ne remonte dans le journal du serveur.</summary>
    [Fact]
    public async Task Closing_the_page_during_a_gesture_leaves_no_exception_on_the_server()
    {
        await using var proxy = new DelayProxy(app.Url);
        var (page, _) = await OpenAsync(proxy);
        var logged = app.ServerLog.Length;
        var from = await TitleAsync(page);
        await page.Mouse.MoveAsync((float)from.X, (float)from.Y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(1275, 790, new MouseMoveOptions { Steps = 10 });
        await page.WaitForTimeoutAsync(300);

        await page.CloseAsync(new PageCloseOptions { RunBeforeUnload = true });
        await Task.Delay(1500);
        proxy.Cut();
        await Task.Delay(1500);

        var log = app.ServerLog[logged..];
        Assert.DoesNotContain("JSDisconnectedException", log);
        Assert.DoesNotContain("Unhandled exception", log);
        Assert.DoesNotContain("fail:", log);
    }
}
