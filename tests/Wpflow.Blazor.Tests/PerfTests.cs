using System.Diagnostics;
using Bunit;
using Microsoft.AspNetCore.Components;
using Wpflow.Blazor;
using Wpflow.Core;
using Xunit.Abstractions;

namespace Wpflow.Blazor.Tests;

/// <summary>Mesure du coût d'un rendu après un déplacement dans un grand graphe (affiché, jamais en échec).</summary>
public sealed class PerfTests : FlowViewTestContext
{
    private readonly ITestOutputHelper _output;

    public PerfTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Move_one_node_in_a_large_graph()
    {
        const int count = 1500;
        var nodes = Enumerable.Range(0, count).Select(i => new Node("n" + i, new XYPosition(i % 50 * 200, i / 50 * 100), Data: "N" + i)).ToList();
        var edges = Enumerable.Range(1, count - 1).Select(i => new Edge("e" + i, "n" + (i - 1), "n" + i)).ToList();
        IReadOnlyList<Node> current = nodes;
        var cut = RenderComponent<FlowView>(p =>
        {
            p.Add(f => f.Nodes, current);
            p.Add(f => f.Edges, edges);
            p.Add(f => f.FitViewOnInit, false);
            p.Add(f => f.NodesChanged, EventCallback.Factory.Create<IReadOnlyList<Node>>(this, n => current = n));
        });
        await cut.InvokeAsync(() => cut.Instance.OnRootRect(0, 0, 1280, 800));
        await cut.InvokeAsync(() => cut.Instance.OnMeasured(nodes.Select(n => Measured(n.Id, width: 150)).ToList()));

        var sw = Stopwatch.StartNew();
        for (var i = 1; i <= 20; i++)
            {
            await cut.InvokeAsync(() => cut.Instance.ApplyNodeChanges([new NodePositionChange("n10", new XYPosition(i, i))]));
            cut.SetParametersAndRender(p => p.Add(f => f.Nodes, current)); // le parent re-rend avec les nouveaux nodes
        }
        sw.Stop();
        _output.WriteLine($"{count} nodes, {count - 1} edges : {sw.ElapsedMilliseconds / 20.0:F1} ms par déplacement de node");

        sw.Restart();
        for (var i = 1; i <= 20; i++)
            await cut.InvokeAsync(() => cut.Instance.SetViewport(new Viewport(i, i, 1)));
        sw.Stop();
        _output.WriteLine($"{count} nodes : {sw.ElapsedMilliseconds / 20.0:F1} ms par pas de pan");
    }
}
