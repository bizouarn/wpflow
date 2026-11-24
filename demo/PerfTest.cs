using System.Diagnostics;
using System.Windows.Threading;
using Wpflow.Core;
using Wpflow.Wpf;

namespace demo;

/// <summary>
///     Spike de performance (WPFLOW_PERF=N) : N nodes par défaut en grille, ~N edges. Mesure le temps de
///     premier affichage, puis le coût moyen d'une image pendant un déplacement de node et pendant un pan.
/// </summary>
internal static class PerfTest
{
    public static async Task Run(FlowView flow, int count, Action<string> log)
    {
        if (Environment.GetEnvironmentVariable("WPFLOW_PERF_VIRT") == "1") flow.OnlyRenderVisibleElements = true;
        var cols = (int)Math.Ceiling(Math.Sqrt(count));
        var nodes = Enumerable.Range(0, count)
            .Select(i => new Node($"n{i}", new XYPosition(i % cols * 200, i / cols * 80), "default", $"Node {i}"))
            .ToList();
        var edges = Enumerable.Range(1, count - 1)
            .Select(i => new Edge($"e{i}", $"n{i - 1}", $"n{i}"))
            .ToList();

        var sw = Stopwatch.StartNew();
        flow.Edges = edges;
        flow.Nodes = nodes;
        flow.Viewport = new Viewport(0, 0, 0.5);

        // Attente de la mesure de tous les nodes.
        // Avec la virtualisation, les nodes hors vue ne sont jamais mesurés : on attend seulement ceux qui sont visibles.
        var virtualized = flow.OnlyRenderVisibleElements;
        while ((virtualized ? flow.Nodes.Count(n => n.Rect != null) < 50 : flow.Nodes.Any(n => n.Rect is null))
               && sw.Elapsed.TotalSeconds < 120)
            await flow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        await flow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        log($"{count} nodes / {edges.Count} edges : premier affichage + mesure = {sw.ElapsedMilliseconds} ms");

        const int frames = 60;
        var id = "n0";
        sw.Restart();
        for (var i = 1; i <= frames; i++)
        {
            flow.Nodes = Changes.Apply([new NodePositionChange(id, new XYPosition(i * 3, i * 2))], flow.Nodes);
            await flow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        }

        log($"déplacement d'un node : {sw.ElapsedMilliseconds / (double)frames:0.0} ms / image");

        sw.Restart();
        for (var i = 1; i <= frames; i++)
        {
            flow.Viewport = new Viewport(i * 5, i * 3, 0.5);
            await flow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        }

        log($"pan : {sw.ElapsedMilliseconds / (double)frames:0.0} ms / image");
    }
}
