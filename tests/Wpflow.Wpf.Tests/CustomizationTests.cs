using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Rect = System.Windows.Rect;
using Path = System.Windows.Shapes.Path;
using Wpflow.Core;
using Wpflow.Wpf;

namespace Wpflow.Wpf.Tests;

public class CustomizationTests
{
    internal static FlowView Realize(Action<FlowView>? configure = null, IReadOnlyList<Node>? nodes = null, IReadOnlyList<Edge>? edges = null)
    {
        // Hors fenêtre, le style par défaut (thème) n'est pas appliqué tout seul : on le prend dans les ressources de l'application.
        var flow = new FlowView { Width = 800, Height = 600, Style = (Style)Application.Current.TryFindResource(typeof(FlowView)) };
        configure?.Invoke(flow);
        flow.Measure(new Size(800, 600));
        flow.Arrange(new Rect(0, 0, 800, 600));
        flow.UpdateLayout();
        flow.Nodes = nodes ??
        [
            new Node("a", new XYPosition(0, 0), Width: 100, Height: 40, FixedWidth: 100, FixedHeight: 40),
            new Node("b", new XYPosition(300, 100), Width: 100, Height: 40, FixedWidth: 100, FixedHeight: 40)
        ];
        flow.Edges = edges ?? [new Edge("e", "a", "b")];
        flow.UpdateLayout();
        return flow;
    }

    private static DataTemplate Template(string text)
    {
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetValue(TextBlock.TextProperty, text);
        return new DataTemplate { VisualTree = factory };
    }

    [Fact]
    public void Built_in_node_types_can_be_redefined_by_resources()
    {
        Sta.Run(() =>
        {
            var flow = Realize(f => f.Resources["wpflow-node-default"] = Template("PERSONNALISÉ"));

            var texts = FlowView.FindDescendants<NodeHost>(flow).SelectMany(FlowView.FindDescendants<TextBlock>).Select(t => t.Text);
            Assert.Contains("PERSONNALISÉ", texts);
        });
    }

    [Fact]
    public void Custom_edge_type_uses_the_path_provider_from_resources()
    {
        Sta.Run(() =>
        {
            var provider = new DelegateEdgePathProvider(g => new EdgePath($"M{g.Source.X},{g.Source.Y} L{g.Target.X},{g.Source.Y} L{g.Target.X},{g.Target.Y}", 1, 2, 0, 0));
            var flow = Realize(f => f.Resources["wpflow-edge-elbow"] = provider, edges: [new Edge("e", "a", "b", Type: "elbow")]);

            var visual = Assert.Single(FlowView.FindDescendants<EdgeVisual>(flow));
            Assert.Matches(@"^M[\d.]+,[\d.]+ L[\d.]+,[\d.]+ L[\d.]+,[\d.]+$", visual.PathData);
        });
    }

    [Fact]
    public void A_built_in_edge_type_can_be_overridden()
    {
        Sta.Run(() =>
        {
            var flow = Realize(f => f.Resources["wpflow-edge-straight"] = new DelegateEdgePathProvider(_ => new EdgePath("M0,0 L1,1", 0, 0, 0, 0)),
                edges: [new Edge("e", "a", "b", Type: "straight")]);

            Assert.Equal("M0,0 L1,1", Assert.Single(FlowView.FindDescendants<EdgeVisual>(flow)).PathData);
        });
    }

    [Fact]
    public void Edge_template_renders_the_edge_with_a_context()
    {
        Sta.Run(() =>
        {
            var flow = Realize(f => f.Resources["wpflow-edge-template-fancy"] = Template("edge"),
                edges: [new Edge("e", "a", "b", Type: "fancy"), new Edge("plain", "a", "b")]);

            var templated = Assert.Single(FlowView.FindDescendants<EdgeTemplateVisual>(flow));
            var context = Assert.IsType<EdgeRenderContext>(templated.Content);
            Assert.Equal("e", context.Edge.Id);
            Assert.StartsWith("M", context.PathData);
            Assert.Single(FlowView.FindDescendants<EdgeVisual>(flow)); // l'autre edge garde le rendu direct
        });
    }

    [Fact]
    public void Changing_an_edge_type_switches_the_rendering_mode()
    {
        Sta.Run(() =>
        {
            var flow = Realize(f => f.Resources["wpflow-edge-template-fancy"] = Template("edge"));
            Assert.Single(FlowView.FindDescendants<EdgeVisual>(flow));

            flow.Edges = [new Edge("e", "a", "b", Type: "fancy")];
            Assert.Empty(FlowView.FindDescendants<EdgeVisual>(flow));
            Assert.Single(FlowView.FindDescendants<EdgeTemplateVisual>(flow));
        });
    }

    [Fact]
    public void Custom_marker_comes_from_a_geometry_resource()
    {
        Sta.Run(() =>
        {
            var flow = Realize(f => f.Resources["wpflow-marker-dot"] = Geometry.Parse("M-6,0 A3,3 0 1 1 0,0 A3,3 0 1 1 -6,0 Z"),
                edges: [new Edge("e", "a", "b", MarkerEndKey: "dot"), new Edge("f", "a", "b", MarkerEndKey: "absent")]);

            var visuals = FlowView.FindDescendants<EdgeVisual>(flow).ToDictionary(v => v.EdgeId);
            Assert.True(visuals["e"].HasCustomEndMarker);
            Assert.False(visuals["f"].HasCustomEndMarker); // clé inconnue : on retombe sur MarkerEnd
        });
    }

    [Fact]
    public void Closed_marker_geometries_are_filled_open_ones_are_not()
    {
        Sta.Run(() =>
        {
            Assert.True(MarkerShape.From(Geometry.Parse("M-6,-4 L0,0 L-6,4 Z")).Filled);
            Assert.True(MarkerShape.From(new EllipseGeometry(new Point(-3, 0), 3, 3)).Filled);
            Assert.False(MarkerShape.From(Geometry.Parse("M-6,-4 L0,0 L-6,4")).Filled);
        });
    }

    [Fact]
    public void Label_template_can_be_chosen_per_edge_type()
    {
        Sta.Run(() =>
        {
            var flow = Realize(f => f.Resources["wpflow-edge-label-fancy"] = Template("LABEL"),
                edges: [new Edge("e", "a", "b", Type: "fancy"), new Edge("plain", "a", "b")]);

            var label = Assert.Single(FlowView.FindDescendants<EdgeLabel>(flow));
            Assert.Equal("e", label.EdgeId); // seul le type « fancy » a un label
        });
    }

    [Fact]
    public void Handle_size_and_template_are_customizable()
    {
        Sta.Run(() =>
        {
            var handle = new Handle { Position = Position.Left };
            Assert.Equal(8, handle.Width);
            Assert.Equal(-4, handle.Margin.Left);

            handle.Width = handle.Height = 14;
            Assert.Equal(-7, handle.Margin.Left);

            Assert.Null(handle.Template);
            handle.Template = new ControlTemplate(typeof(Handle)) { VisualTree = new FrameworkElementFactory(typeof(Border)) };
            Assert.NotNull(handle.Template);
        });
    }

    [Fact]
    public void Connection_line_style_and_hit_width_are_exposed()
    {
        Sta.Run(() =>
        {
            var flow = Realize();
            var line = FlowView.FindDescendants<Path>(flow).First(p => p.Name == "PART_Connection");
            Assert.Equal(1.5, line.StrokeThickness);

            var style = new Style(typeof(Path));
            style.Setters.Add(new Setter(Shape.StrokeThicknessProperty, 4.0));
            flow.ConnectionLineStyle = style;
            Assert.Equal(4.0, line.StrokeThickness);

            Assert.Equal(10, flow.EdgeHitWidth);
            flow.EdgeHitWidth = 20;
            Assert.Equal(20, flow.EdgeHitWidth);
        });
    }
}
