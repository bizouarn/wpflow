// BrowserCheck --url <adresse du graphe> [--serve <dossier>] [--navigate <adresse de l'accueil>] [--reload-renderer <nom>]
// Écrit une ligne OK/ECHEC par vérification ; code de sortie 0 seulement si tout passe.
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

var options = new Dictionary<string, string>();
for (var i = 0; i + 1 < args.Length; i += 2) options[args[i]] = args[i + 1];
var url = options["--url"];

// Publication WebAssembly autonome : un serveur de fichiers statiques, sans rien de plus que ce qu'offre un hébergeur.
WebApplication? files = null;
if (options.TryGetValue("--serve", out var folder))
{
    var builder = WebApplication.CreateBuilder();
    builder.Logging.ClearProviders();
    files = builder.Build();
    files.Urls.Add(new Uri(url).GetLeftPart(UriPartial.Authority));
    var provider = new PhysicalFileProvider(Path.GetFullPath(folder));
    var types = new FileExtensionContentTypeProvider();
    types.Mappings[".dat"] = types.Mappings[".blat"] = "application/octet-stream";
    files.UseStaticFiles(new StaticFileOptions { FileProvider = provider, ContentTypeProvider = types, ServeUnknownFileTypes = true });
    files.MapFallback(async context =>
    {
        // Seules les routes de l'application retombent sur index.html : un fichier manquant doit rester un 404.
        if (Path.HasExtension(context.Request.Path.Value)) context.Response.StatusCode = 404;
        else
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(provider.GetFileInfo("index.html"));
        }
    });
    await files.StartAsync();
}

var failed = false;
void Check(string name, bool ok, string detail = "")
{
    failed |= !ok;
    Console.WriteLine($"{(ok ? "OK    " : "ECHEC ")}{name}{(detail.Length > 0 ? " — " + detail : "")}");
}

try
{
    using var playwright = await Playwright.CreateAsync();
    var channel = Environment.GetEnvironmentVariable("WPFLOW_BROWSER_CHANNEL") is { Length: > 0 } c ? c : "chrome";
    await using var browser = await playwright.Chromium.LaunchAsync(new() { Channel = channel, Headless = true });
    var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 1280, Height = 800 } });
    var errors = new List<string>();
    // « Failed to load resource » ne dit pas quelle ressource : les réponses en échec sont relevées avec leur URL plus bas.
    page.Console += (_, m) => { if (m.Type == "error" && !m.Text.StartsWith("Failed to load resource")) errors.Add("console : " + m.Text); };
    page.RequestFailed += (_, r) => { if (r.Failure != "net::ERR_ABORTED") errors.Add($"{r.Failure} {r.Url}"); };
    page.PageError += (_, e) => errors.Add("page : " + e);
    page.Response += (_, r) => { if (r.Status >= 400 && !r.Url.EndsWith("favicon.ico") && !r.Url.EndsWith("favicon.png")) errors.Add($"{r.Status} {r.Url}"); };

    async Task Graph(string step)
    {
        try
        {
            await page.WaitForSelectorAsync(".wf-edge-path", new() { State = WaitForSelectorState.Attached, Timeout = 60000 });
        }
        catch (TimeoutException)
        {
            Check($"{step} : le graphe s'affiche, edge tracé", false);
            return;
        }

        await page.WaitForTimeoutAsync(800); // interactivité et cadrage
        Check($"{step} : deux nodes et un edge", await page.Locator(".wf-node").CountAsync() == 2 && await page.Locator(".wf-edge-path").CountAsync() == 1);
        Check($"{step} : feuille de style chargée",
            await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('.wf-node')).position") == "absolute");

        // Interactivité réelle : tirer un node le déplace.
        var node = page.Locator("[data-node-id='2']");
        var before = await node.BoundingBoxAsync();
        await page.Mouse.MoveAsync(before!.X + before.Width / 2, before.Y + before.Height / 2);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(before.X + before.Width / 2 + 90, before.Y + before.Height / 2 + 40, new() { Steps = 10 });
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(500);
        var after = await node.BoundingBoxAsync();
        Check($"{step} : un glisser déplace le node", after!.X - before.X > 60, $"x {before.X:0} -> {after.X:0}");
    }

    if (options.TryGetValue("--navigate", out var home))
    {
        // Interactivité par page : arrivée et départ par la navigation améliorée, sans rechargement du document.
        await page.GotoAsync(home);
        await page.EvaluateAsync("() => window.__sameDocument = true");
        await page.ClickAsync("#to-graph");
        await Graph("première visite");
        await page.ClickAsync("#to-home");
        await page.WaitForSelectorAsync(".wf-node", new() { State = WaitForSelectorState.Detached, Timeout = 15000 });
        await page.ClickAsync("#to-graph");
        await Graph("retour sur le graphe");
        Check("navigation améliorée (document conservé)", await page.EvaluateAsync<bool>("() => window.__sameDocument === true"));
    }
    else
    {
        await page.GotoAsync(url);
        await Graph("premier affichage");
        Check("JSON : aller-retour", await page.InnerTextAsync("#json") == "ok", await page.InnerTextAsync("#json"));
        await page.ClickAsync("#export");
        await page.WaitForTimeoutAsync(500);
        Check("export SVG", await page.InnerTextAsync("#svg") == "ok", await page.InnerTextAsync("#svg"));
        Console.WriteLine("INFO  rendu : " + await page.InnerTextAsync("#renderer"));

        if (options.TryGetValue("--reload-renderer", out var expected))
        {
            await page.WaitForTimeoutAsync(8000); // InteractiveAuto : le runtime WebAssembly se télécharge en arrière-plan
            await page.ReloadAsync();
            await Graph("second chargement");
            var renderer = await page.InnerTextAsync("#renderer");
            Check($"second chargement : rendu {expected}", renderer == expected, renderer);
        }
    }

    Check("aucune erreur (console, page, requêtes)", errors.Count == 0, string.Join(" | ", errors.Take(8)));
}
catch (Exception ex)
{
    Check("exécution", false, ex.Message);
}
finally
{
    if (files != null) await files.StopAsync();
}

return failed ? 1 : 0;
