using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Wpflow.Blazor;
using Wpflow.Core;

namespace Wpflow.Blazor.Tests;

/// <summary>
///     Comme les nodes, un edge n'est redessiné que si ce qui le dessine a changé : lui-même, ses extrémités, ses deux nodes
///     ou les réglages de la vue. Redessiner tous les edges d'un grand graphe à chaque pas d'un geste le rend inutilisable.
///     Le témoin est le modèle d'edge, appelé à chaque dessin d'un edge.
/// </summary>
public sealed class EdgeReuseTests : FlowViewTestContext
{
    /// <summary>Un parent comme celui d'une application : il garde les données et se redessine à chaque évènement reçu.</summary>
    private sealed class Host : ComponentBase
    {
        [Parameter] public Dictionary<string, object> Settings { get; set; } = new();
        public FlowView View = default!;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<FlowView>(0);
            builder.AddMultipleAttributes(1, Settings);
            builder.AddAttribute(2, nameof(FlowView.NodesChanged),
                EventCallback.Factory.Create<IReadOnlyList<Node>>(this, n => Settings[nameof(FlowView.Nodes)] = n));
            builder.AddAttribute(3, nameof(FlowView.EdgesChanged),
                EventCallback.Factory.Create<IReadOnlyList<Edge>>(this, e => Settings[nameof(FlowView.Edges)] = e));
            builder.AddAttribute(4, nameof(FlowView.OnNodeDrag), EventCallback.Factory.Create<NodeDragEventArgs>(this, _ => { }));
            builder.AddAttribute(5, nameof(FlowView.OnPaneMouseMove), EventCallback.Factory.Create<PaneEventArgs>(this, _ => { }));
            builder.AddComponentReferenceCapture(6, view => View = (FlowView)view);
            builder.CloseComponent();
        }
    }

    private static readonly Node C = new("c", new XYPosition(600, 0), Data: "C");
    private static readonly Node D = new("d", new XYPosition(600, 200), Data: "D");
    private static readonly Edge First = new("e-ab", "a", "b") { Type = "counted" };
    private static readonly Edge Second = new("e-cd", "c", "d") { Type = "counted" };

    private readonly List<string> _drawn = [];

    private Dictionary<string, RenderFragment<EdgeRenderContext>> Templates(string css = "counted") => new()
    {
        ["counted"] = context => builder =>
        {
            _drawn.Add(context.Edge.Id);
            builder.OpenElement(0, "path");
            builder.AddAttribute(1, "class", css);
            builder.AddAttribute(2, "d", context.Path.Path);
            // Ce que le modèle lit de l'edge et de ses nodes doit rester à jour.
            builder.AddAttribute(3, "data-edge", context.Edge.Id);
            builder.CloseElement();
        }
    };

    private async Task<IRenderedComponent<Host>> Open(Action<Dictionary<string, object>>? more = null)
    {
        var settings = new Dictionary<string, object>
        {
            [nameof(FlowView.Nodes)] = (IReadOnlyList<Node>)[A, B, C, D],
            [nameof(FlowView.Edges)] = (IReadOnlyList<Edge>)[First, Second],
            [nameof(FlowView.EdgeTemplates)] = Templates(),
            [nameof(FlowView.FitViewOnInit)] = false
        };
        more?.Invoke(settings);
        var host = Render<Host>(p => p.Add(h => h.Settings, settings));
        await host.InvokeAsync(() => host.Instance.View.OnRootRect(0, 0, 800, 600));
        await host.InvokeAsync(() => host.Instance.View.OnMeasured([Measured("a"), Measured("b"), Measured("c"), Measured("d")]));
        _drawn.Clear();
        return host;
    }

    private static Task Move(IRenderedComponent<Host> host, double x, double y) =>
        host.InvokeAsync(() => host.Instance.View.OnPointerMove(x, y, false, false));

    private static Task Release(IRenderedComponent<Host> host, double x, double y) =>
        host.InvokeAsync(() => host.Instance.View.OnPointerUp(x, y, false, false));

    [Fact]
    public async Task Dragging_a_node_redraws_only_its_edges()
    {
        var host = await Open();
        await host.Find("[data-node-id='a']").TriggerEventAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientX = 20, ClientY = 20 });
        await Move(host, 40, 20);
        _drawn.Clear();
        var path = host.Find("[data-edge='e-ab']").GetAttribute("d");

        for (var i = 1; i <= 5; i++) await Move(host, 40 + i * 6, 20);

        Assert.DoesNotContain("e-cd", _drawn);
        Assert.Equal(5, _drawn.Count(id => id == "e-ab")); // une fois par pas, malgré les rendus du parent
        Assert.NotEqual(path, host.Find("[data-edge='e-ab']").GetAttribute("d"));
    }

    [Fact]
    public async Task Hovering_the_pane_redraws_no_edge()
    {
        var host = await Open();

        for (var i = 1; i <= 5; i++)
            await host.Find(".wf-flow").TriggerEventAsync("onpointermove", new PointerEventArgs { ClientX = 500 + i, ClientY = 400 });

        Assert.Empty(_drawn);
    }

    [Fact]
    public async Task A_selection_box_redraws_only_what_it_selects()
    {
        var host = await Open(s => s[nameof(FlowView.SelectionOnDrag)] = true);
        await host.Find(".wf-flow").TriggerEventAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientX = 450, ClientY = 500 });
        await Move(host, 460, 510);
        for (var i = 1; i <= 5; i++) await Move(host, 460 + i * 3, 520);

        Assert.Empty(_drawn); // rien n'est sélectionné

        await Move(host, -10, -10); // le rectangle prend a et b, donc leur edge

        Assert.DoesNotContain("e-cd", _drawn);
        Assert.Contains("wf-selected", host.Find("[data-node-id='a']").ClassName);
        await Release(host, -10, -10);
    }

    [Fact]
    public async Task Pulling_a_wire_redraws_no_edge()
    {
        var host = await Open();
        await host.Find("[data-node-id='a'] .wf-handle-bottom").TriggerEventAsync("onpointerdown",
            new PointerEventArgs { Button = 0, ClientX = 50, ClientY = 40 });
        await Move(host, 200, 200);
        _drawn.Clear();
        var wire = host.Find(".wf-connection-line").GetAttribute("d");

        for (var i = 1; i <= 5; i++) await Move(host, 200 + i * 6, 200);

        Assert.Empty(_drawn);
        Assert.NotEqual(wire, host.Find(".wf-connection-line").GetAttribute("d"));
        await Release(host, 230, 200);
        Assert.Empty(host.FindAll(".wf-connection-line"));
    }

    // Le témoin d'un parcours de la couche : le nom accessible est demandé à chaque node à chaque fois.
    private int _walked;

    private void CountWalks(Dictionary<string, object> settings)
    {
        Func<Node, string?> label = n =>
        {
            _walked++;
            return n.Id;
        };
        settings[nameof(FlowView.NodeAriaLabelProvider)] = label;
    }

    [Fact]
    public async Task Pulling_a_wire_does_not_walk_the_layer()
    {
        var host = await Open(CountWalks);
        await host.Find("[data-node-id='a'] .wf-handle-bottom").TriggerEventAsync("onpointerdown",
            new PointerEventArgs { Button = 0, ClientX = 50, ClientY = 40 });
        await Move(host, 200, 200);
        _walked = 0;

        for (var i = 1; i <= 5; i++) await Move(host, 200 + i * 6, 200);

        Assert.Equal(0, _walked);
        await Release(host, 230, 200);
    }

    [Fact]
    public async Task A_render_of_the_parent_that_changes_nothing_does_not_walk_the_layer()
    {
        var host = await Open(CountWalks);
        _walked = 0;

        for (var i = 0; i < 5; i++) host.Render(p => p.Add(h => h.Settings, host.Instance.Settings));

        Assert.Equal(0, _walked);
    }

    /// <summary>La liste est la même, pas son contenu : l'application l'a modifiée sur place avant de se redessiner.</summary>
    [Fact]
    public async Task A_list_changed_in_place_is_still_drawn()
    {
        var edges = new List<Edge> { First, Second };
        var host = await Open(s => s[nameof(FlowView.Edges)] = (IReadOnlyList<Edge>)edges);

        edges[0] = First with { Animated = true };
        host.Render(p => p.Add(h => h.Settings, host.Instance.Settings));

        Assert.Single(host.FindAll(".wf-edge-animated"));

        edges.RemoveAt(1);
        host.Render(p => p.Add(h => h.Settings, host.Instance.Settings));

        Assert.Single(host.FindAll(".wf-edge"));
    }

    /// <summary>
    ///     Le pont JS découpe les mesures d'un grand graphe en dizaines de messages (limite de Blazor Server). Les appliquer
    ///     un à un parcourrait le graphe à chaque message : elles ne le sont qu'une fois, au dernier.
    /// </summary>
    [Fact]
    public async Task Measures_sent_in_several_messages_are_applied_once()
    {
        var applied = 0;
        var host = await Open(s =>
        {
            CountWalks(s);
            s[nameof(FlowView.OnNodesChange)] = EventCallback.Factory.Create<IReadOnlyList<NodeChange>>(this, _ => applied++);
        });
        (applied, _walked) = (0, 0);
        var view = host.Instance.View;
        var split = Measured("c", 130); // un node à nombreux handles, coupé entre deux messages
        var start = new MeasuredNode { Id = "c", Width = 130, Height = 40, Handles = [split.Handles[0]], Partial = true, More = true };
        var first = Measured("a", 110);
        first.More = true;
        split.Handles.RemoveAt(0);

        await host.InvokeAsync(() => view.OnMeasured([first]));
        await host.InvokeAsync(() => view.OnMeasured([start]));

        Assert.Equal((0, 0), (applied, _walked));

        await host.InvokeAsync(() => view.OnMeasured([split, Measured("b", 120)]));

        Assert.Equal(1, applied);
        Assert.Equal([110, 120, 130], view.Nodes.Take(3).Select(n => n.Width!.Value));
        Assert.Equal(2, host.FindAll(".wf-edge").Count);
    }

    [Fact]
    public async Task Changing_a_node_redraws_the_edges_that_read_it()
    {
        var host = await Open();

        await host.InvokeAsync(() => host.Instance.View.ApplyNodeChanges(
            [new NodeReplaceChange("c", host.Instance.View.Nodes.Single(n => n.Id == "c") with { Data = "autre" })]));

        Assert.Equal(["e-cd"], _drawn.Distinct());
    }

    [Fact]
    public async Task Changing_an_edge_redraws_it_alone()
    {
        var host = await Open();

        await host.InvokeAsync(() => host.Instance.View.ApplyEdgeChanges([new EdgeSelectChange("e-ab", true)]));

        Assert.Equal(["e-ab"], _drawn.Distinct());
        Assert.Contains("wf-selected", host.FindAll(".wf-edge")[1].ClassName + host.FindAll(".wf-edge")[0].ClassName);
        Assert.Equal(2, host.FindAll(".wf-edge-anchor").Count); // les ancres de reconnexion de l'edge sélectionné
    }

    [Fact]
    public async Task A_new_setting_of_the_view_redraws_the_edges()
    {
        var host = await Open();
        await host.InvokeAsync(() => host.Instance.View.ApplyEdgeChanges([new EdgeSelectChange("e-ab", true)]));

        await host.InvokeAsync(() => host.Instance.View.SetLocked(true));

        Assert.Empty(host.FindAll(".wf-edge-anchor"));

        await host.InvokeAsync(() => host.Instance.View.SetLocked(false));
        _drawn.Clear();
        host.Instance.Settings[nameof(FlowView.EdgeHitWidth)] = 37.0;
        host.Render(p => p.Add(h => h.Settings, host.Instance.Settings));

        Assert.Equal(2, _drawn.Distinct().Count());
        Assert.All(host.FindAll(".wf-edge-hit"), hit => Assert.Contains("37", hit.GetAttribute("style")));
    }

    [Fact]
    public async Task A_redeclared_template_redraws_every_edge()
    {
        var host = await Open();
        host.Instance.Settings[nameof(FlowView.EdgeTemplates)] = Templates("autre");

        host.Render(p => p.Add(h => h.Settings, host.Instance.Settings));

        Assert.Equal(2, host.FindAll("path.autre").Count);
    }

    [Fact]
    public async Task A_label_follows_its_edge()
    {
        var host = await Open(s => s[nameof(FlowView.Edges)] = (IReadOnlyList<Edge>)[First with { Label = "un" }, Second]);

        await host.InvokeAsync(() => host.Instance.View.ApplyEdgeChanges(
            [new EdgeReplaceChange("e-ab", host.Instance.View.Edges[0] with { Label = "deux" })]));

        Assert.Equal("deux", host.Find(".wf-edge-label").TextContent.Trim());

        await host.InvokeAsync(() => host.Instance.View.ApplyEdgeChanges([new EdgeRemoveChange("e-ab")]));

        Assert.Empty(host.FindAll(".wf-edge-label"));
        Assert.Single(host.FindAll(".wf-edge"));
    }
}
