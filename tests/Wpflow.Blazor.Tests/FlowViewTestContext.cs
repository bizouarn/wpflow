using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Wpflow.Blazor;
using Wpflow.Core;

namespace Wpflow.Blazor.Tests;

/// <summary>
///     Socle des tests de <see cref="FlowView" /> sans navigateur : le pont JS est simulé (mode souple) et les callbacks que
///     JS appellerait (<c>OnMeasured</c>, <c>OnPointerMove</c>…) sont invoqués directement.
/// </summary>
public abstract class FlowViewTestContext : BunitContext
{
    protected static readonly Node A = new("a", new XYPosition(0, 0), Data: "A");
    protected static readonly Node B = new("b", new XYPosition(300, 100), Data: "B");
    protected static readonly Edge AB = new("e-ab", "a", "b");

    protected FlowViewTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/Wpflow.Blazor/wpflow.js").Mode = JSRuntimeMode.Loose;
    }

    /// <summary>La vue rendue et l'état que son parent reçoit par <c>@bind-Nodes</c> / <c>@bind-Edges</c>.</summary>
    protected sealed class Harness
    {
        public IRenderedComponent<FlowView> Cut = default!;
        public IReadOnlyList<Node> Nodes = [];
        public IReadOnlyList<Edge> Edges = [];
        public List<IReadOnlyList<NodeChange>> NodeChanges = [];
        public List<Connection> Connections = [];
        public FlowView Flow => Cut.Instance;
        public IEnumerable<string> Selected => Nodes.Where(n => n.Selected).Select(n => n.Id);
        public Node Node(string id) => Nodes.Single(n => n.Id == id);
    }

    /// <summary>Rend les nodes et edges donnés (par défaut <see cref="A" />, <see cref="B" /> et <see cref="AB" />).</summary>
    protected Harness RenderHarness(IReadOnlyList<Node>? nodes, IReadOnlyList<Edge>? edges,
        Action<Harness, ComponentParameterCollectionBuilder<FlowView>>? configure, bool fit)
    {
        var h = new Harness { Nodes = nodes ?? [A, B], Edges = edges ?? [AB] };
        h.Cut = Render<FlowView>(p =>
        {
            p.Add(f => f.Nodes, h.Nodes);
            p.Add(f => f.Edges, h.Edges);
            p.Add(f => f.FitViewOnInit, fit);
            p.Add(f => f.NodesChanged, EventCallback.Factory.Create<IReadOnlyList<Node>>(this, n => h.Nodes = n));
            p.Add(f => f.EdgesChanged, EventCallback.Factory.Create<IReadOnlyList<Edge>>(this, e => h.Edges = e));
            configure?.Invoke(h, p);
        });
        return h;
    }

    /// <summary>La mesure d'un node, avec un handle cible en haut et un handle source en bas, au milieu.</summary>
    protected static MeasuredNode Measured(string id, double width = 100, double height = 40) => new()
    {
        Id = id, Width = width, Height = height,
        Handles =
        [
            new MeasuredHandle { Type = "target", Position = "top", X = width / 2 - 4, Y = -4, Width = 8, Height = 8 },
            new MeasuredHandle { Type = "source", Position = "bottom", X = width / 2 - 4, Y = height - 4, Width = 8, Height = 8 }
        ]
    };

    /// <summary>Simule le pont JS : conteneur 800×600 en (0,0), puis la mesure des nodes donnés (tous par défaut).</summary>
    protected static async Task Measure(IRenderedComponent<FlowView> cut, params string[] ids)
    {
        await cut.InvokeAsync(() => cut.Instance.OnRootRect(0, 0, 800, 600));
        var measured = (ids.Length == 0 ? cut.Instance.Nodes.Select(n => n.Id) : ids).Select(id => Measured(id)).ToList();
        await cut.InvokeAsync(() => cut.Instance.OnMeasured(measured));
    }

    protected static Task Measure(Harness h, params string[] ids) =>
        Measure(h.Cut, ids.Length == 0 ? h.Nodes.Select(n => n.Id).ToArray() : ids);

    protected static Task Down(AngleSharp.Dom.IElement el, double x, double y, bool shift = false, bool ctrl = false, long button = 0) =>
        el.TriggerEventAsync("onpointerdown", new PointerEventArgs { Button = button, ClientX = x, ClientY = y, ShiftKey = shift, CtrlKey = ctrl });

    protected static Task Move(Harness h, double x, double y, bool shift = false) => h.Cut.InvokeAsync(() => h.Flow.OnPointerMove(x, y, shift, false));

    protected static Task Up(Harness h, double x, double y, bool shift = false) => h.Cut.InvokeAsync(() => h.Flow.OnPointerUp(x, y, shift, false));
}
