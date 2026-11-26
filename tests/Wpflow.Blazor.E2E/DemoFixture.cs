using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Wpflow.Blazor.E2E;

/// <summary>
///     Lance la démo Blazor (processus réel) et un vrai Chrome piloté par Playwright : ces tests exercent le pont JS
///     (<c>wpflow.js</c>) et les gestes de bout en bout, ce que bUnit ne peut pas faire. Deux hôtes : Blazor Server et WebAssembly.
/// </summary>
public abstract class DemoFixture : IAsyncLifetime
{
    private Process? _server;
    private IPlaywright? _playwright;
    private readonly List<string> _log = [];

    /// <summary>La commande qui lance la démo sur <paramref name="url" />.</summary>
    protected abstract ProcessStartInfo CreateStartInfo(string root, string config, string url);

    /// <summary>Nombre d'edges tracés qui dit que l'application est prête.</summary>
    protected virtual int ReadyEdges => 5;

    public string Url { get; private set; } = "";

    public IBrowser Browser { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        var root = FindRepoRoot();
        var config = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
        var port = FreePort();
        Url = $"http://127.0.0.1:{port}/";
        var start = CreateStartInfo(root, config, Url);
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.UseShellExecute = false;
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; // sert les ressources statiques des bibliothèques
        _server = Process.Start(start)!;
        _server.OutputDataReceived += (_, e) => { if (e.Data != null) lock (_log) _log.Add(e.Data); };
        _server.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (_log) _log.Add(e.Data); };
        _server.BeginOutputReadLine();
        _server.BeginErrorReadLine();
        await WaitForServer();

        _playwright = await Playwright.CreateAsync();
        // Un navigateur installé (aucun téléchargement) : Chrome par défaut, WPFLOW_BROWSER_CHANNEL=msedge pour Edge.
        var channel = Environment.GetEnvironmentVariable("WPFLOW_BROWSER_CHANNEL") is { Length: > 0 } c ? c : "chrome";
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = channel, Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (Browser != null) await Browser.CloseAsync();
        _playwright?.Dispose();
        if (_server is { HasExited: false })
        {
            _server.Kill(true);
            await _server.WaitForExitAsync();
        }

        // La sortie du serveur : une exception de circuit Blazor Server n'apparaît que là.
        lock (_log) File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, GetType().Name + ".server.log"), _log);
    }

    private async Task WaitForServer()
    {
        using var http = new HttpClient();
        for (var i = 0; i < 150; i++)
        {
            if (_server!.HasExited) throw new InvalidOperationException("La démo s'est arrêtée : " + string.Join("\n", _log));
            try
            {
                if ((await http.GetAsync(Url)).StatusCode == HttpStatusCode.OK) return;
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(200);
        }

        throw new TimeoutException("La démo ne répond pas : " + string.Join("\n", _log));
    }

    protected static string FindRepoRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "wpflow.sln"))) return d.FullName;
        throw new DirectoryNotFoundException("wpflow.sln introuvable au-dessus de " + AppContext.BaseDirectory);
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    /// <summary>Ouvre la démo (taille fixe) et attend qu'elle soit interactive et mesurée (les edges sont tracés).</summary>
    public async Task<IPage> OpenAsync(bool touch = false, string query = "")
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            HasTouch = touch,
            AcceptDownloads = true
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync(Url + query);
        // WebAssembly télécharge d'abord le runtime : délai large.
        await page.WaitForFunctionAsync("document.querySelectorAll('.wf-edge-path').length >= " + ReadyEdges, null, new PageWaitForFunctionOptions { Timeout = 60000 });
        await page.WaitForTimeoutAsync(300); // cadrage initial
        return page;
    }
}

/// <summary>La démo hébergée par Blazor Server : les évènements du navigateur passent par le circuit SignalR.</summary>
public class ServerFixture : DemoFixture
{
    protected virtual string Project => Path.Combine("demo", "blazor");
    protected virtual string Assembly => "demo-blazor.dll";

    protected override ProcessStartInfo CreateStartInfo(string root, string config, string url)
    {
        var dir = Path.Combine(root, Project);
        // La même variable que dans les csproj des hôtes Server : elle doit être définie à la construction et au test.
        var tfm = Environment.GetEnvironmentVariable("WPFLOW_SERVER_DEMO_TFM") is { Length: > 0 } t ? t : "net8.0";
        var dll = Path.Combine(dir, "bin", config, tfm, Assembly);
        if (!File.Exists(dll)) throw new FileNotFoundException("L'application n'est pas construite : " + dll);
        return new ProcessStartInfo("dotnet", $"\"{dll}\" --urls {url}") { WorkingDirectory = dir };
    }
}

/// <summary>La démo hébergée par Blazor WebAssembly : tout s'exécute dans le navigateur.</summary>
public class WasmFixture : DemoFixture
{
    protected virtual string Project => Path.Combine("demo", "blazor-wasm");

    protected override ProcessStartInfo CreateStartInfo(string root, string config, string url) =>
        new("dotnet", $"run --no-build -c {config} --project \"{Path.Combine(root, Project)}\" --urls {url}") { WorkingDirectory = root };
}

public static class PageExtensions
{
    private static readonly Regex Translate = new(@"translate\((-?[\d.]+)px,\s*(-?[\d.]+)px\)");
    private static readonly Regex Scale = new(@"scale\((-?[\d.]+)\)");

    /// <summary>Le viewport (X, Y, zoom) lu dans le style du calque.</summary>
    public static async Task<(double X, double Y, double Zoom)> ViewportAsync(this IPage page)
    {
        var style = await page.Locator(".wf-viewport").GetAttributeAsync("style") ?? "";
        var t = Translate.Match(style);
        var s = Scale.Match(style);
        return (Number(t.Groups[1]), Number(t.Groups[2]), Number(s.Groups[1]));
    }

    /// <summary>La position absolue (flow) d'un node, lue dans son style.</summary>
    public static async Task<(double X, double Y)> NodePosAsync(this IPage page, string id)
    {
        var style = await page.Locator($"[data-node-id='{id}']").GetAttributeAsync("style") ?? "";
        var t = Translate.Match(style);
        return (Number(t.Groups[1]), Number(t.Groups[2]));
    }

    private static double Number(Group group) => double.Parse(group.Value, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Les erreurs de la console et les exceptions de la page à partir de maintenant ; un test les attend vides.</summary>
    public static List<string> CollectErrors(this IPage page)
    {
        var errors = new List<string>();
        page.Console += (_, m) => { if (m.Type == "error") errors.Add(m.Text); };
        page.PageError += (_, e) => errors.Add(e);
        return errors;
    }

    public static async Task<(double X, double Y)> CenterAsync(this IPage page, string selector)
    {
        var box = await page.Locator(selector).First.BoundingBoxAsync() ?? throw new InvalidOperationException("Introuvable : " + selector);
        return (box.X + box.Width / 2, box.Y + box.Height / 2);
    }

    public static async Task DragAsync(this IPage page, (double X, double Y) from, (double X, double Y) to, int steps = 12, int pauseMs = 0)
    {
        await page.Mouse.MoveAsync((float)from.X, (float)from.Y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync((float)to.X, (float)to.Y, new MouseMoveOptions { Steps = steps });
        if (pauseMs > 0) await page.WaitForTimeoutAsync(pauseMs);
        await page.Mouse.UpAsync();
    }

    /// <summary>Attend qu'une condition devienne vraie (les mises à jour passent par le serveur ou le runtime).</summary>
    public static async Task UntilAsync(this IPage page, Func<Task<bool>> condition, string because, int timeoutMs = 5000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < until)
        {
            if (await condition()) return;
            await page.WaitForTimeoutAsync(50);
        }

        Assert.Fail("Délai dépassé : " + because);
    }

    public static async Task<int> CountAsync(this IPage page, string selector) => await page.Locator(selector).CountAsync();
}
