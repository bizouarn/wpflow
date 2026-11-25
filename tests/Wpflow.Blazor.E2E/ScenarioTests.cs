using System.Globalization;
using Microsoft.Playwright;

namespace Wpflow.Blazor.E2E;

public sealed class ScenarioServerFixture : ServerFixture
{
    protected override string Project => Path.Combine("tests", "Scenarios.Blazor.Server");
    protected override string Assembly => "Scenarios.Blazor.Server.dll";
    protected override int ReadyEdges => 2;
}

public sealed class ScenarioWasmFixture : WasmFixture
{
    protected override string Project => Path.Combine("tests", "Scenarios.Blazor.Wasm");
    protected override int ReadyEdges => 2;
}

public sealed class ServerScenarioTests(ScenarioServerFixture app) : ScenarioTests(app), IClassFixture<ScenarioServerFixture>;

public sealed class WasmScenarioTests(ScenarioWasmFixture app) : ScenarioTests(app), IClassFixture<ScenarioWasmFixture>;

// Les mêmes scénarios, l'application tenant son schéma dans des ViewModels (NodesSource / EdgesSource).
public sealed class ServerMvvmScenarioTests(ScenarioServerFixture app) : ScenarioTests(app), IClassFixture<ScenarioServerFixture>
{
    protected override string Query => "?mvvm=1";
}

public sealed class WasmMvvmScenarioTests(ScenarioWasmFixture app) : ScenarioTests(app), IClassFixture<ScenarioWasmFixture>
{
    protected override string Query => "?mvvm=1";
}

/// <summary>L'éditeur de schéma témoin, piloté par de vraies entrées (ROADMAP-TESTS, lot 2).</summary>
public abstract class ScenarioTests(DemoFixture app)
{
    protected virtual string Query => "";

    private async Task<(IPage Page, List<string> Errors)> OpenAsync()
    {
        var page = await app.OpenAsync(query: Query);
        var errors = new List<string>();
        page.Console += (_, m) => { if (m.Type == "error") errors.Add(m.Text); };
        page.PageError += (_, e) => errors.Add(e);
        await page.WaitForTimeoutAsync(900); // second cadrage, après le chargement asynchrone
        return (page, errors);
    }

    private static string Column(string table, string column) => $"[data-node-id='{table}'] [data-column='{column}']";

    private static string Handle(string table, string column, string type) => $"{Column(table, column)} .wf-handle[data-type='{type}']";

    private static Task<string> TextAsync(IPage page, string id) => page.Locator("#" + id).InnerTextAsync();

    private static Task ExpectTextAsync(IPage page, string id, string expected) =>
        page.UntilAsync(async () => (await TextAsync(page, id)).Trim() == expected, $"#{id} vaut « {expected} »");

    /// <summary>Les deux bouts de chaque tracé d'edge, en coordonnées écran.</summary>
    private static async Task<double[][]> EndsAsync(IPage page) => await page.EvaluateAsync<double[][]>("""
        () => [...document.querySelectorAll('.wf-edge-path')].map(path => {
            const m = path.getScreenCTM();
            const a = path.getPointAtLength(0).matrixTransform(m);
            const b = path.getPointAtLength(path.getTotalLength()).matrixTransform(m);
            return [a.x, a.y, b.x, b.y];
        })
        """);

    /// <summary>Un edge part du handle de la colonne source et arrive à celui de la colonne cible.</summary>
    private static async Task AssertAttachedAsync(IPage page, string from, string fromColumn, string to, string toColumn)
    {
        var detail = "";
        var until = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < until)
        {
            var ends = await EndsAsync(page);
            var source = await page.CenterAsync(Handle(from, fromColumn, "source"));
            var target = await page.CenterAsync(Handle(to, toColumn, "target"));
            if (ends.Any(e => Math.Abs(e[1] - source.Y) < 4 && Math.Abs(e[0] - source.X) < 10 &&
                              Math.Abs(e[3] - target.Y) < 4 && Math.Abs(e[2] - target.X) < 10)) return;
            detail = string.Create(CultureInfo.InvariantCulture,
                $"handles ({source.X:0},{source.Y:0})→({target.X:0},{target.Y:0}), edges ") + string.Join(" ; ",
                ends.Select(e => string.Create(CultureInfo.InvariantCulture, $"({e[0]:0},{e[1]:0})→({e[2]:0},{e[3]:0})")));
            await page.WaitForTimeoutAsync(100);
        }

        Assert.Fail($"Aucun edge n'est accroché à {from}.{fromColumn} → {to}.{toColumn} : {detail}");
    }

    private static async Task ClickAsync(IPage page, string selector, MouseButton button = MouseButton.Left)
    {
        var p = await page.CenterAsync(selector);
        await page.Mouse.ClickAsync((float)p.X, (float)p.Y, new() { Button = button });
    }

    [Fact]
    public async Task The_schema_loads_asynchronously_and_edges_attach_to_their_columns()
    {
        var (page, errors) = await OpenAsync();

        await ExpectTextAsync(page, "count", "4 tables, 2 relations");
        await AssertAttachedAsync(page, "customers", "id", "orders", "customer_id");
        await AssertAttachedAsync(page, "orders", "id", "lines", "order_id");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_clicked_double_clicked_and_right_clicked_column_is_known()
    {
        var (page, errors) = await OpenAsync();

        await ClickAsync(page, Column("orders", "note") + " span");
        await ExpectTextAsync(page, "log", "clic orders.note");
        await ClickAsync(page, Column("orders", "customer_id") + " span", MouseButton.Right);
        await ExpectTextAsync(page, "menu", "colonne orders.customer_id");
        await ClickAsync(page, "[data-node-id='orders'] .title", MouseButton.Right);
        await ExpectTextAsync(page, "menu", "table orders");
        var zoom = (await page.ViewportAsync()).Zoom;
        var p = await page.CenterAsync(Column("customers", "email") + " em");
        await page.Mouse.DblClickAsync((float)p.X, (float)p.Y);
        await page.UntilAsync(async () => (await TextAsync(page, "events")).Contains("double-clic customers.email"), "double-clic reçu");
        await page.WaitForTimeoutAsync(400);
        Assert.Equal(zoom, (await page.ViewportAsync()).Zoom);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("move-column")]
    [InlineData("add-column")]
    [InlineData("remove-column")]
    [InlineData("collapse")]
    public async Task Edges_follow_their_handles_when_the_columns_change(string action)
    {
        var (page, errors) = await OpenAsync();

        await page.ClickAsync("#" + action);
        await page.WaitForTimeoutAsync(500);

        await AssertAttachedAsync(page, "customers", "id", "orders", "customer_id");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Renaming_a_column_keeps_every_key_inside_the_field()
    {
        var (page, errors) = await OpenAsync();
        var viewport = await page.ViewportAsync();
        var position = await page.NodePosAsync("customers");
        var p = await page.CenterAsync(Column("customers", "id") + " span");
        await page.Mouse.DblClickAsync((float)p.X, (float)p.Y);
        var input = page.Locator("input.rename");
        await input.WaitForAsync();
        await page.WaitForFunctionAsync("() => document.activeElement?.classList.contains('rename')");

        await page.Keyboard.PressAsync("Control+a");
        await page.Keyboard.TypeAsync("ab cd");
        await page.Keyboard.PressAsync("ArrowLeft");
        await page.Keyboard.PressAsync("ArrowLeft");
        await page.Keyboard.PressAsync("Delete");
        await page.Keyboard.PressAsync("Backspace");
        await page.Keyboard.PressAsync("ArrowUp");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal("abd", await input.InputValueAsync());
        await page.Keyboard.PressAsync("Control+z"); // annulation du champ, pas du graphe
        await page.WaitForTimeoutAsync(300);
        await ExpectTextAsync(page, "count", "4 tables, 2 relations");
        Assert.Equal(position, await page.NodePosAsync("customers"));
        Assert.Equal(viewport, await page.ViewportAsync());

        await input.FillAsync("uid");
        await page.Keyboard.PressAsync("Enter");
        await page.Locator(Column("customers", "uid")).WaitForAsync();
        await AssertAttachedAsync(page, "customers", "uid", "orders", "customer_id");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Each_target_has_its_own_context_menu()
    {
        var (page, errors) = await OpenAsync();

        await ClickAsync(page, ".relation-label[data-relation='r-orders']");
        await ExpectTextAsync(page, "log", "label r-orders");

        var point = await page.EvaluateAsync<double[]>("""
            () => {
                const path = document.querySelector('.wf-edge-hit');
                const p = path.getPointAtLength(path.getTotalLength() * 0.25).matrixTransform(path.getScreenCTM());
                return [p.x, p.y];
            }
            """);
        await page.Mouse.ClickAsync((float)point[0], (float)point[1], new() { Button = MouseButton.Right });
        await page.UntilAsync(async () => (await TextAsync(page, "menu")).StartsWith("relation "), "menu d'une relation");

        var graph = await page.Locator(".graph").BoundingBoxAsync();
        await page.Mouse.ClickAsync(graph!.X + graph.Width - 40, graph.Y + 20, new() { Button = MouseButton.Right });
        await ExpectTextAsync(page, "menu", "fond");

        await ClickAsync(page, "[data-node-id='orders'] .title");
        await page.Keyboard.DownAsync("Control");
        await ClickAsync(page, "[data-node-id='lines'] .title");
        await page.Keyboard.UpAsync("Control");
        await page.WaitForTimeoutAsync(300);
        await ClickAsync(page, "[data-node-id='lines'] .title", MouseButton.Right);
        await ExpectTextAsync(page, "selection-menu", "sélection de 2");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_right_click_keeps_the_selection_and_is_not_a_pane_click()
    {
        var (page, errors) = await OpenAsync();
        await ClickAsync(page, "[data-node-id='orders'] .title");
        await ExpectTextAsync(page, "log", "clic orders");

        await ClickAsync(page, "[data-node-id='orders'] .title", MouseButton.Right);
        await ExpectTextAsync(page, "menu", "table orders");
        await page.WaitForTimeoutAsync(500);
        Assert.Equal(1, await page.CountAsync("[data-node-id='orders'][aria-selected='true']"));
        await ExpectTextAsync(page, "log", "clic orders");

        // Sur le fond non plus : le menu s'ouvre, sans clic ni désélection.
        var graph = await page.Locator(".graph").BoundingBoxAsync();
        await page.Mouse.ClickAsync(graph!.X + graph.Width - 40, graph.Y + 20, new() { Button = MouseButton.Right });
        await ExpectTextAsync(page, "menu", "fond");
        await page.WaitForTimeoutAsync(500);
        Assert.Equal(1, await page.CountAsync("[data-node-id='orders'][aria-selected='true']"));
        await ExpectTextAsync(page, "log", "clic orders");
        Assert.Empty(errors);
    }

    /// <summary>La même suite d'évènements qu'en WPF (scénario « ordre des évènements » de Scenarios.Wpf).</summary>
    [Fact]
    public async Task A_double_click_raises_click_double_click_click()
    {
        var (page, errors) = await OpenAsync();
        await page.ClickAsync("#clear-events");

        var p = await page.CenterAsync(Column("customers", "email") + " em");
        await page.Mouse.DblClickAsync((float)p.X, (float)p.Y);
        await page.WaitForTimeoutAsync(900);
        await ExpectTextAsync(page, "events", "clic customers.email > double-clic customers.email > clic customers.email");

        // Un clic simple ensuite n'est pas retardé ni doublé.
        await page.ClickAsync("#clear-events");
        await ClickAsync(page, Column("orders", "note") + " span");
        await page.WaitForTimeoutAsync(900);
        await ExpectTextAsync(page, "events", "clic orders.note");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_right_click_on_a_selection_raises_the_selection_menu_then_the_node_menu()
    {
        var (page, errors) = await OpenAsync();
        await ClickAsync(page, "[data-node-id='orders'] .title");
        await page.Keyboard.DownAsync("Control");
        await ClickAsync(page, "[data-node-id='lines'] .title");
        await page.Keyboard.UpAsync("Control");
        await page.WaitForTimeoutAsync(300);
        await page.ClickAsync("#clear-events");

        await ClickAsync(page, "[data-node-id='lines'] .title", MouseButton.Right);

        await ExpectTextAsync(page, "events", "sélection de 2 > table lines");
        Assert.Empty(errors);
    }

    /// <summary>Un seul type d'edge : le tracé et les marqueurs suivent <c>Edge.Data</c>.</summary>
    [Fact]
    public async Task One_edge_type_draws_each_cardinality_differently()
    {
        var (page, errors) = await OpenAsync();

        var edges = await page.EvaluateAsync<string[][]>("""
            () => [...document.querySelectorAll('.wf-edge')].map(g => {
                const path = g.querySelector('.wf-edge-path');
                return [g.getAttribute('aria-label') ?? '', path.getAttribute('d'), path.getAttribute('marker-end') ?? ''];
            })
            """);

        var many = Assert.Single(edges, e => e[2] != "");   // 1-n : flèche à l'arrivée
        var one = Assert.Single(edges, e => e[2] == "");    // 1-1 : aucune
        Assert.Contains("C", many[1]);                      // bézier
        Assert.DoesNotContain("C", one[1]);                 // angles droits adoucis
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Connections_are_validated_by_column_type()
    {
        var (page, errors) = await OpenAsync();

        // text vers int : refusé ; int vers int : accepté.
        await page.DragAsync(await page.CenterAsync(Handle("customers", "name", "source")), await page.CenterAsync(Handle("lines", "id", "target")));
        await page.WaitForTimeoutAsync(500);
        await ExpectTextAsync(page, "count", "4 tables, 2 relations");

        await page.DragAsync(await page.CenterAsync(Handle("customers", "id", "source")), await page.CenterAsync(Handle("lines", "id", "target")));
        await ExpectTextAsync(page, "count", "4 tables, 3 relations");
        await AssertAttachedAsync(page, "customers", "id", "lines", "id");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_controlled_store_can_refuse_changes()
    {
        var (page, errors) = await OpenAsync();
        var locked = await page.NodePosAsync("audit");
        var free = await page.NodePosAsync("orders");

        var from = await page.CenterAsync("[data-node-id='audit'] .title");
        await page.DragAsync(from, (from.X + 90, from.Y + 40));
        await page.WaitForTimeoutAsync(400);
        Assert.Equal(locked, await page.NodePosAsync("audit"));
        await page.UntilAsync(async () => (await TextAsync(page, "refused")).Contains("déplacement de audit"), "refus noté");

        await ClickAsync(page, "[data-node-id='audit'] .title");
        await page.Keyboard.PressAsync("Delete");
        await page.WaitForTimeoutAsync(400);
        await ExpectTextAsync(page, "count", "4 tables, 2 relations");

        from = await page.CenterAsync("[data-node-id='orders'] .title");
        await page.DragAsync(from, (from.X + 60, from.Y + 30));
        await page.UntilAsync(async () => (await page.NodePosAsync("orders")) != free, "un node libre se déplace");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Loading_another_schema_reframes_and_forgets_selection_and_history()
    {
        var (page, errors) = await OpenAsync();
        var from = await page.CenterAsync("[data-node-id='orders'] .title");
        await page.DragAsync(from, (from.X + 60, from.Y + 30)); // une entrée dans l'historique, un node sélectionné
        await page.WaitForTimeoutAsync(400);

        await page.ClickAsync("#load-library");
        await ExpectTextAsync(page, "count", "2 tables, 1 relations");
        await page.ClickAsync("#fit");
        await AssertAttachedAsync(page, "books", "id", "authors", "book_id");
        var box = await page.Locator("[data-node-id='authors']").BoundingBoxAsync();
        Assert.InRange(box!.X, 0, 1280 - box.Width);
        Assert.Equal(0, await page.CountAsync(".wf-node[aria-selected='true']"));

        // Annuler ne doit pas ramener l'ancien schéma.
        await page.ClickAsync("#undo");
        await page.WaitForTimeoutAsync(500);
        await ExpectTextAsync(page, "count", "2 tables, 1 relations");
        Assert.Equal(2, await page.CountAsync(".wf-node"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Saving_and_reading_json_keeps_typed_data()
    {
        var (page, errors) = await OpenAsync();
        await page.ClickAsync("#save");
        await page.WaitForTimeoutAsync(300);
        await page.ClickAsync("#load-library");
        await ExpectTextAsync(page, "count", "2 tables, 1 relations");

        await page.ClickAsync("#restore");

        await ExpectTextAsync(page, "count", "4 tables, 2 relations");
        await page.ClickAsync("#fit");
        await AssertAttachedAsync(page, "customers", "id", "orders", "customer_id");
        await ClickAsync(page, Column("orders", "note") + " span");
        await ExpectTextAsync(page, "log", "clic orders.note");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Background_updates_do_not_break_a_drag_or_a_connection()
    {
        var (page, errors) = await OpenAsync();
        await page.ClickAsync("#ticks");
        var zoom = (await page.ViewportAsync()).Zoom;
        var start = await page.NodePosAsync("orders");

        var from = await page.CenterAsync("[data-node-id='orders'] .title");
        await page.DragAsync(from, (from.X + 120, from.Y + 60), steps: 40, pauseMs: 300);
        await page.WaitForTimeoutAsync(500);
        var end = await page.NodePosAsync("orders");
        Assert.InRange(end.X - start.X, 120 / zoom - 3, 120 / zoom + 3);
        Assert.InRange(end.Y - start.Y, 60 / zoom - 3, 60 / zoom + 3);

        await page.DragAsync(await page.CenterAsync(Handle("customers", "id", "source")),
            await page.CenterAsync(Handle("lines", "id", "target")), steps: 40, pauseMs: 300);
        await ExpectTextAsync(page, "count", "4 tables, 3 relations");
        Assert.NotEqual("0", await page.Locator("[data-node-id='orders'] .rows").InnerTextAsync());
        await page.ClickAsync("#ticks");
        Assert.Empty(errors);
    }

    /// <summary>La mesure d'une seule table dépasse un message SignalR : le circuit ne doit pas tomber.</summary>
    [Fact]
    public async Task A_table_with_many_columns_does_not_drop_the_circuit()
    {
        var (page, errors) = await OpenAsync();

        await page.ClickAsync("#load-wide");

        await ExpectTextAsync(page, "count", "2 tables, 1 relations");
        await page.ClickAsync("#fit");
        await AssertAttachedAsync(page, "wide", "column_149", "other", "id");
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Two_hundred_tables_and_four_hundred_relations_stay_usable(bool virtualize)
    {
        var (page, errors) = await OpenAsync();
        if (virtualize) await page.CheckAsync("#virtualize");
        // En WebAssembly le chargement occupe le seul thread de la page : le clic ne rend la main qu'ensuite, au bout d'une
        // trentaine de secondes sur une machine de 8 Go, soit le délai par défaut de Playwright.
        await page.ClickAsync("#load-large", new() { Timeout = 90000 });
        await page.UntilAsync(async () => (await TextAsync(page, "count")).Trim() == "200 tables, 400 relations", "chargé", 30000);
        await page.WaitForTimeoutAsync(3000);
        var shown = await page.CountAsync(".wf-node");
        if (virtualize) Assert.InRange(shown, 1, 199);
        else Assert.Equal(200, shown);

        if (!virtualize) await page.ClickAsync("#fit");
        await page.WaitForTimeoutAsync(1000);
        var title = page.Locator(".wf-node .title").First;
        var id = await title.EvaluateAsync<string>("e => e.closest('.wf-node').dataset.nodeId");
        var before = await page.NodePosAsync(id);
        var from = await page.CenterAsync($"[data-node-id='{id}'] .title");
        await page.DragAsync(from, (from.X + 40, from.Y + 25));
        await page.UntilAsync(async () => (await page.NodePosAsync(id)) != before, "le node se déplace", 30000);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Controls_inside_a_node_keep_their_behaviour()
    {
        var (page, errors) = await OpenAsync();
        var position = await page.NodePosAsync("audit");
        var viewport = await page.ViewportAsync();

        await ClickAsync(page, ".w-button");
        await page.UntilAsync(async () => await page.Locator(".w-button").InnerTextAsync() == "clics 1", "bouton");
        await page.SelectOptionAsync(".w-select", "b");
        await ClickAsync(page, ".w-check");
        await page.UntilAsync(async () => await page.Locator(".w-state").InnerTextAsync() == "b True", "liste et case à cocher");

        var scroll = await page.CenterAsync(".w-scroll");
        await page.Mouse.MoveAsync((float)scroll.X, (float)scroll.Y);
        await page.Mouse.WheelAsync(0, 120);
        await page.UntilAsync(async () => await page.Locator(".w-scroll").EvaluateAsync<int>("e => e.scrollTop") > 0, "la zone défile");
        Assert.Equal(viewport, await page.ViewportAsync());

        var text = await page.Locator(".w-text").BoundingBoxAsync();
        await page.DragAsync((text!.X + 2, text.Y + text.Height / 2), (text.X + text.Width - 2, text.Y + text.Height / 2));
        Assert.NotEqual("", await page.EvaluateAsync<string>("() => getSelection().toString().trim()"));
        Assert.Equal(position, await page.NodePosAsync("audit"));

        await ClickAsync(page, ".w-link");
        await page.UntilAsync(() => Task.FromResult(page.Url.EndsWith("#ancre")), "le lien navigue");
        Assert.Empty(errors);
    }
}
