using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Wpflow.Blazor;
using Wpflow.Core;

namespace Wpflow.Blazor.Tests;

/// <summary>
///     Un pas de pan ne doit pas reconstruire la couche des nodes et des edges, sinon un grand graphe devient inutilisable :
///     ni parce que le parent écoute le viewport (<c>@bind-Viewport</c>), ni à cause de la virtualisation. Le témoin est le
///     modèle d'edge : il est appelé pour chaque edge à chaque reconstruction.
/// </summary>
public sealed class LayerReuseTests : FlowViewTestContext
{
    /// <summary>Un parent comme celui d'une application : il écoute la vue, et se redessine à chacun de ses évènements.</summary>
    private sealed class Host : ComponentBase
    {
        [Parameter] public Dictionary<string, object> Settings { get; set; } = new();
        [Parameter] public bool Listens { get; set; }

        /// <summary>Appelé à chaque changement de viewport, avant que le parent se redessine.</summary>
        [Parameter] public Func<Viewport, Task>? Changed { get; set; }

        public FlowView View = default!;
        public int Renders;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            Renders++;
            builder.OpenComponent<FlowView>(0);
            builder.AddMultipleAttributes(1, Settings);
            // Comme @bind-Nodes : le parent garde les nodes mesurés, sans quoi il rendrait à la vue les anciens.
            builder.AddAttribute(4, nameof(FlowView.NodesChanged),
                new EventCallback<IReadOnlyList<Node>>(null, (Action<IReadOnlyList<Node>>)(n => Settings[nameof(FlowView.Nodes)] = n)));
            if (Listens)
                builder.AddAttribute(2, nameof(FlowView.ViewportChanged),
                    EventCallback.Factory.Create<Viewport>(this, v => Changed?.Invoke(v) ?? Task.CompletedTask));
            builder.AddComponentReferenceCapture(3, view => View = (FlowView)view);
            builder.CloseComponent();
        }
    }

    private int _drawn; // appels au modèle d'edge

    private static readonly Node Far = new("far", new XYPosition(5000, 0), Data: "F");
    private static readonly Edge ToFar = new("e-far", "b", "far") { Type = "counted" };

    private Dictionary<string, object> Settings(bool virtualize, IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges)
    {
        var templates = new Dictionary<string, RenderFragment<EdgeRenderContext>>
        {
            ["counted"] = context => builder =>
            {
                _drawn++;
                builder.OpenElement(0, "path");
                builder.AddAttribute(1, "class", "counted");
                builder.AddAttribute(2, "d", context.Path.Path);
                builder.CloseElement();
            }
        };
        return new Dictionary<string, object>
        {
            [nameof(FlowView.Nodes)] = nodes, [nameof(FlowView.Edges)] = edges, [nameof(FlowView.EdgeTemplates)] = templates,
            [nameof(FlowView.OnlyRenderVisibleElements)] = virtualize, [nameof(FlowView.FitViewOnInit)] = true
        };
    }

    private async Task<IRenderedComponent<Host>> RenderHost(bool listens, bool virtualize, Func<Viewport, Task>? changed = null,
        IReadOnlyList<Node>? nodes = null, IReadOnlyList<Edge>? edges = null)
    {
        nodes ??= [A, B];
        var settings = Settings(virtualize, nodes, edges ?? [AB with { Type = "counted" }]);
        var host = RenderComponent<Host>(p =>
        {
            p.Add(h => h.Settings, settings);
            p.Add(h => h.Listens, listens);
            p.Add(h => h.Changed, changed);
        });
        await host.InvokeAsync(() => host.Instance.View.OnRootRect(0, 0, 800, 600));
        await host.InvokeAsync(() => host.Instance.View.OnMeasured(nodes.Select(n => Measured(n.Id)).ToList()));
        return host;
    }

    private static Task Pan(IRenderedComponent<Host> host, double dx, double dy = 0)
    {
        var v = host.Instance.View.Viewport;
        return host.InvokeAsync(() => host.Instance.View.SetViewport(v with { X = v.X + dx, Y = v.Y + dy }));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task A_pan_step_keeps_the_layer(bool listens, bool virtualize)
    {
        var host = await RenderHost(listens, virtualize);
        var (drawn, renders) = (_drawn, host.Instance.Renders);

        for (var i = 0; i < 5; i++) await Pan(host, 3, 1);

        Assert.Equal(drawn, _drawn);
        if (listens) Assert.True(host.Instance.Renders > renders, "le parent se redessine bien");
    }

    /// <summary>Un gestionnaire asynchrone du parent le redessine une seconde fois, après coup.</summary>
    [Fact]
    public async Task A_slow_viewport_handler_of_the_parent_keeps_the_layer()
    {
        var host = await RenderHost(listens: true, virtualize: false, changed: _ => Task.Delay(20));
        var drawn = _drawn;

        await Pan(host, 3);
        await Pan(host, 3);

        Assert.Equal(drawn, _drawn);
    }

    /// <summary>Un parent qui suit l'état de la vue (zoom affiché, boutons grisés) se redessine aussi à chaque pas.</summary>
    [Fact]
    public async Task A_parent_following_the_state_of_the_view_keeps_the_layer()
    {
        var changes = 0;
        var settings = Settings(false, [A, B], [AB with { Type = "counted" }]);
        var host = RenderComponent<Host>(p => p.Add(h => h.Settings, settings));
        settings[nameof(FlowView.OnStateChange)] = EventCallback.Factory.Create(host.Instance, () => changes++);
        host.SetParametersAndRender(p => p.Add(h => h.Settings, settings));
        await host.InvokeAsync(() => host.Instance.View.OnRootRect(0, 0, 800, 600));
        await host.InvokeAsync(() => host.Instance.View.OnMeasured([Measured("a"), Measured("b")]));
        var (drawn, seen) = (_drawn, changes);

        for (var i = 0; i < 5; i++) await Pan(host, 3, 1);

        Assert.True(changes >= seen + 5, "le parent est prévenu à chaque pas");
        Assert.Equal(drawn, _drawn);
    }

    /// <summary>
    ///     Les rectangles de la minimap sont en coordonnées flow : un pan ne déplace que son cadre et son voile. Un changement
    ///     des nodes les redessine.
    /// </summary>
    [Fact]
    public async Task The_minimap_redraws_its_nodes_only_when_they_change()
    {
        var colored = 0;
        Func<Node, string?> color = _ =>
        {
            colored++;
            return "red";
        };
        var settings = Settings(true, [A, B], [AB with { Type = "counted" }]);
        settings[nameof(FlowView.ChildContent)] = (RenderFragment)(b =>
        {
            b.OpenComponent<FlowMiniMap>(0);
            b.AddAttribute(1, nameof(FlowMiniMap.NodeColor), color);
            b.CloseComponent();
        });
        var host = RenderComponent<Host>(p =>
        {
            p.Add(h => h.Settings, settings);
            p.Add(h => h.Listens, true);
        });
        await host.InvokeAsync(() => host.Instance.View.OnRootRect(0, 0, 800, 600));
        await host.InvokeAsync(() => host.Instance.View.OnMeasured([Measured("a"), Measured("b")]));
        var before = colored;
        var frame = host.Find(".wf-minimap path").GetAttribute("d");

        for (var i = 0; i < 5; i++) await Pan(host, 3, 1);

        Assert.Equal(before, colored);
        Assert.NotEqual(frame, host.Find(".wf-minimap path").GetAttribute("d")); // le voile suit la vue
        var first = host.Find(".wf-minimap-node").GetAttribute("x");

        await host.InvokeAsync(() => host.Instance.View.ApplyNodeChanges([new NodePositionChange("a", new XYPosition(-40, 0))]));
        host.Render();

        Assert.True(colored > before);
        Assert.NotEqual(first, host.Find(".wf-minimap-node").GetAttribute("x"));
    }

    /// <summary>
    ///     La virtualisation refait la couche quand des nodes apparaissent : la minimap, qui les montre tous, n'a rien à
    ///     redessiner. Sans <c>OnNodeClick</c>, ses rectangles n'ont pas non plus de gestionnaire à renouveler.
    /// </summary>
    [Fact]
    public async Task The_minimap_ignores_what_virtualisation_adds_or_removes()
    {
        var colored = 0;
        Func<Node, string?> color = _ =>
        {
            colored++;
            return "red";
        };
        var corner = new Node("corner", new XYPosition(7000, 0), Data: "C"); // l'emprise, donc l'échelle, ne change pas
        var settings = Settings(true, [A, B, Far, corner], [AB with { Type = "counted" }, ToFar]);
        settings[nameof(FlowView.ChildContent)] = (RenderFragment)(b =>
        {
            b.OpenComponent<FlowMiniMap>(0);
            b.AddAttribute(1, nameof(FlowMiniMap.NodeColor), color);
            b.CloseComponent();
        });
        var host = RenderComponent<Host>(p => p.Add(h => h.Settings, settings));
        await host.InvokeAsync(() => host.Instance.View.OnRootRect(0, 0, 800, 600));
        await host.InvokeAsync(() => host.Instance.View.OnMeasured([Measured("a"), Measured("b"), Measured("far"), Measured("corner")]));
        await host.InvokeAsync(() => host.Instance.View.SetViewport(new Viewport(0, 0, 1)));
        var before = colored;
        var framing = host.Find(".wf-minimap svg").GetAttribute("viewBox");

        // La vue passe sur le node lointain puis revient : la couche est refaite, les nodes sont les mêmes.
        await host.InvokeAsync(() => host.Instance.View.SetViewport(new Viewport(-4800, 0, 1)));
        Assert.NotEmpty(host.FindAll("[data-node-id='far']"));
        await host.InvokeAsync(() => host.Instance.View.SetViewport(new Viewport(0, 0, 1)));

        Assert.Equal(before, colored);
        Assert.Equal(framing, host.Find(".wf-minimap svg").GetAttribute("viewBox"));
        Assert.DoesNotContain(host.FindAll(".wf-minimap-node"), r => r.Attributes.Any(a => a.Name == "blazor:onpointerdown"));
    }

    [Fact]
    public async Task A_listened_click_on_the_minimap_still_reaches_the_application()
    {
        var clicked = new List<string>();
        var settings = Settings(false, [A, B], [AB with { Type = "counted" }]);
        settings[nameof(FlowView.ChildContent)] = (RenderFragment)(b =>
        {
            b.OpenComponent<FlowMiniMap>(0);
            b.AddAttribute(1, nameof(FlowMiniMap.OnNodeClick), EventCallback.Factory.Create<Node>(this, n => clicked.Add(n.Id)));
            b.CloseComponent();
        });
        var host = RenderComponent<Host>(p => p.Add(h => h.Settings, settings));
        await host.InvokeAsync(() => host.Instance.View.OnRootRect(0, 0, 800, 600));
        await host.InvokeAsync(() => host.Instance.View.OnMeasured([Measured("a"), Measured("b")]));

        await host.FindAll(".wf-minimap-node")[1].TriggerEventAsync("onpointerdown", new PointerEventArgs());

        Assert.Equal(["b"], clicked);
    }

    /// <summary>L'appui sur le fond, le déplacement et le relâchement : aucune reconstruction, début et fin compris.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dragging_the_pane_keeps_the_layer_from_press_to_release(bool virtualize)
    {
        var host = await RenderHost(listens: true, virtualize);
        var view = host.Instance.View;
        var drawn = _drawn;
        var start = view.Viewport;

        await host.Find(".wf-flow").TriggerEventAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientX = 700, ClientY = 500 });
        for (var i = 1; i <= 5; i++) await host.InvokeAsync(() => view.OnPointerMove(700 + i * 4, 500, false, false));
        await host.InvokeAsync(() => view.OnPointerUp(720, 500, false, false));

        Assert.Equal(start.X + 20, view.Viewport.X, 3);
        Assert.Equal(drawn, _drawn);
    }

    /// <summary>Avec la virtualisation, un déplacement qui change ce qui est visible reconstruit bien la couche.</summary>
    [Fact]
    public async Task Panning_to_other_elements_redraws_them()
    {
        var host = await RenderHost(listens: true, virtualize: true, nodes: [A, B, Far], edges: [AB with { Type = "counted" }, ToFar]);
        await host.InvokeAsync(() => host.Instance.View.SetViewport(new Viewport(0, 0, 1)));
        Assert.Empty(host.FindAll("[data-node-id='far']"));

        await host.InvokeAsync(() => host.Instance.View.SetViewport(new Viewport(-4800, 0, 1)));

        Assert.NotEmpty(host.FindAll("[data-node-id='far']"));
        Assert.Empty(host.FindAll("[data-node-id='a']"));
        Assert.Single(host.FindAll(".counted")); // l'edge vers le node lointain, pas celui des deux nodes sortis de la vue

        await host.InvokeAsync(() => host.Instance.View.SetViewport(new Viewport(0, 0, 1)));

        Assert.NotEmpty(host.FindAll("[data-node-id='a']"));
        Assert.Empty(host.FindAll("[data-node-id='far']"));
    }

    /// <summary>Un vrai changement fait par le parent à l'occasion d'un pas de pan est bien dessiné.</summary>
    [Fact]
    public async Task Data_changed_by_the_parent_during_a_pan_is_drawn()
    {
        Dictionary<string, object>? settings = null;
        var host = await RenderHost(listens: true, virtualize: false, changed: _ =>
        {
            if (settings != null) settings[nameof(FlowView.Nodes)] = new[] { A, B, new Node("added", new XYPosition(100, 200), Data: "N") };
            return Task.CompletedTask;
        });
        settings = host.Instance.Settings;

        await Pan(host, 3);

        Assert.NotEmpty(host.FindAll("[data-node-id='added']"));
    }

    /// <summary>Un modèle redéclaré (nouveau délégué) redessine, comme le dit la documentation.</summary>
    [Fact]
    public async Task A_template_redeclared_by_the_parent_during_a_pan_is_drawn()
    {
        Dictionary<string, object>? settings = null;
        var host = await RenderHost(listens: true, virtualize: false, changed: _ =>
        {
            if (settings != null)
                settings[nameof(FlowView.NodeTemplate)] = (RenderFragment<Node>)(n => b => b.AddMarkupContent(0, "<i class='redone'></i>"));
            return Task.CompletedTask;
        });
        settings = host.Instance.Settings;

        await Pan(host, 3);

        Assert.Equal(2, host.FindAll(".redone").Count);
    }

    /// <summary>Le fil tiré suit la souris, y compris quand la vue défile toute seule près d'un bord.</summary>
    [Fact]
    public async Task A_pulled_wire_is_still_drawn()
    {
        var host = await RenderHost(listens: true, virtualize: true);
        var view = host.Instance.View;
        await host.InvokeAsync(() => view.SetViewport(new Viewport(100, 100, 1)));

        await host.Find("[data-node-id='a'] .wf-handle-bottom").TriggerEventAsync("onpointerdown",
            new PointerEventArgs { Button = 0, ClientX = 150, ClientY = 140 });
        await host.InvokeAsync(() => view.OnPointerMove(400, 300, false, false));
        var first = host.Find(".wf-connection-line").GetAttribute("d");
        await host.InvokeAsync(() => view.OnPointerMove(420, 330, false, false));

        Assert.NotEqual(first, host.Find(".wf-connection-line").GetAttribute("d"));
        await host.InvokeAsync(() => view.OnPointerUp(420, 330, false, false));
        Assert.Empty(host.FindAll(".wf-connection-line"));
    }
}
