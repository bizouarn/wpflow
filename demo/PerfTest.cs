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

        var baseline = Heap();
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
        var loaded = Heap();
        log($"mémoire : tas managé {baseline.Managed / 1048576.0:0.0} -> {loaded.Managed / 1048576.0:0.0} Mo (+{(loaded.Managed - baseline.Managed) / 1024.0 / count:0.0} Ko/node), working set +{(loaded.WorkingSet - baseline.WorkingSet) / 1048576.0:0} Mo");

        const int frames = 60;
        var (a0, g0) = (GC.GetAllocatedBytesForCurrentThread(), (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)));
        var id = "n0";
        sw.Restart();
        for (var i = 1; i <= frames; i++)
        {
            flow.Nodes = Changes.Apply([new NodePositionChange(id, new XYPosition(i * 3, i * 2))], flow.Nodes);
            await flow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        }

        log($"déplacement d'un node : {sw.ElapsedMilliseconds / (double)frames:0.0} ms / image, {(GC.GetAllocatedBytesForCurrentThread() - a0) / frames / 1024.0:0} Ko alloués / image, GC gen0/1/2 = {GC.CollectionCount(0) - g0.Item1}/{GC.CollectionCount(1) - g0.Item2}/{GC.CollectionCount(2) - g0.Item3}");
        var a1 = GC.GetAllocatedBytesForCurrentThread();

        sw.Restart();
        for (var i = 1; i <= frames; i++)
        {
            flow.Viewport = new Viewport(i * 5, i * 3, 0.5);
            await flow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        }

        log($"pan : {sw.ElapsedMilliseconds / (double)frames:0.0} ms / image, {(GC.GetAllocatedBytesForCurrentThread() - a1) / frames / 1024.0:0} Ko alloués / image");
    }

    // Tas managé après collecte complète, et mémoire résidente du processus.
    private static (long Managed, long WorkingSet) Heap()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return (GC.GetTotalMemory(true), Process.GetCurrentProcess().WorkingSet64);
    }
}
