using Microsoft.Playwright;

namespace Wpflow.Blazor.E2E;

// Le grand graphe occupe un cœur entier pendant des dizaines de secondes : il passe seul.
[Collection(Isolated.Name)]
public sealed class ServerLargeSchemaTests(ScenarioServerFixture app) : LargeSchemaTests(app), IClassFixture<ScenarioServerFixture>;

[Collection(Isolated.Name)]
public sealed class WasmLargeSchemaTests(ScenarioWasmFixture app) : LargeSchemaTests(app), IClassFixture<ScenarioWasmFixture>;

[Collection(Isolated.Name)]
public sealed class ServerMvvmLargeSchemaTests(ScenarioServerFixture app) : LargeSchemaTests(app), IClassFixture<ScenarioServerFixture>
{
    protected override string Query => "?mvvm=1";
}

[Collection(Isolated.Name)]
public sealed class WasmMvvmLargeSchemaTests(ScenarioWasmFixture app) : LargeSchemaTests(app), IClassFixture<ScenarioWasmFixture>
{
    protected override string Query => "?mvvm=1";
}

/// <summary>L'éditeur de schéma avec 200 tables et 400 relations, avec et sans virtualisation.</summary>
public abstract class LargeSchemaTests(DemoFixture app) : PagesTest(app)
{
    protected virtual string Query => "";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Two_hundred_tables_and_four_hundred_relations_stay_usable(bool virtualize)
    {
        var page = await App.OpenAsync(query: Query);
        var errors = page.CollectErrors();
        await page.WaitForTimeoutAsync(900); // second cadrage, après le chargement asynchrone
        if (virtualize) await page.CheckAsync("#virtualize");
        // En WebAssembly le chargement occupe le seul thread de la page : le clic ne rend la main qu'ensuite, au bout d'une
        // trentaine de secondes sur une machine de 8 Go, soit le délai par défaut de Playwright.
        await page.ClickAsync("#load-large", new() { Timeout = 90000 });
        await page.UntilAsync(async () => (await page.Locator("#count").InnerTextAsync()).Trim() == "200 tables, 400 relations", "chargé", 30000);
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
}
