using System.Reflection;
using Wpflow.Core;
using Wpflow.Wpf;

namespace Wpflow.Wpf.Tests;

/// <summary>Les méthodes publiques du <c>FlowView</c> Blazor le sont aussi en WPF.</summary>
public class ApiParityTests
{
    // La réflexion, et non un simple appel : ce projet voit aussi les membres internes (InternalsVisibleTo).
    [Theory]
    [InlineData("ApplyNodeChanges")]
    [InlineData("ApplyEdgeChanges")]
    [InlineData("ClearHistory")]
    [InlineData("ClearSelection")]
    [InlineData("SelectAll")]
    [InlineData("SelectOnly")]
    [InlineData("DeleteSelected")]
    [InlineData("CenterOn")]
    [InlineData("FlowToScreen")]
    [InlineData("GetIncomers")]
    [InlineData("GetOutgoers")]
    [InlineData("SetLocked")]
    public void The_method_is_public(string name) =>
        Assert.Contains(typeof(FlowView).GetMethods(BindingFlags.Instance | BindingFlags.Public), m => m.Name == name);

    [Fact]
    public void The_graph_exports_as_a_vector_drawing() => Sta.Run(() =>
    {
        var flow = CustomizationTests.Realize();

        var svg = System.Xml.Linq.XDocument.Parse(flow.ExportSvg(padding: 5, background: "#123456"));

        var shapes = svg.Descendants().ToList();
        Assert.Equal(3, shapes.Count(e => e.Name.LocalName == "rect")); // le fond et les deux nodes
        var path = Assert.Single(shapes, e => e.Name.LocalName == "path");
        Assert.StartsWith("M", path.Attribute("d")!.Value);
        Assert.Contains(shapes, e => e.Attribute("fill")?.Value == "#123456");
    });

    [Fact]
    public void The_public_methods_select_delete_and_query_the_graph()
    {
        Sta.Run(() =>
        {
            var flow = CustomizationTests.Realize(); // nodes « a » et « b », edge « e » de a vers b
            string Selected() => string.Join(",", flow.Nodes.Where(n => n.Selected).Select(n => n.Id)
                .Concat(flow.Edges.Where(e => e.Selected).Select(e => e.Id)));

            Assert.Equal(["b"], flow.GetOutgoers("a").Select(n => n.Id));
            Assert.Equal(["a"], flow.GetIncomers("b").Select(n => n.Id));
            Assert.Empty(flow.GetIncomers("inconnu"));
            Assert.Empty(flow.GetOutgoers("inconnu"));

            flow.SelectAll();
            Assert.Equal("a,b,e", Selected());
            flow.SelectOnly("b", null);
            Assert.Equal("b", Selected());
            flow.SelectOnly(null, "e");
            Assert.Equal("e", Selected());
            flow.ClearSelection();
            Assert.Equal("", Selected());

            // ElementsSelectable faux : SelectOnly ne peut que désélectionner.
            flow.SelectOnly("a", null);
            flow.ElementsSelectable = false;
            flow.SelectOnly("b", "e");
            Assert.Equal("", Selected());
            flow.ElementsSelectable = true;

            flow.Viewport = new Viewport(10, 20, 2);
            var screen = flow.FlowToScreen(new XYPosition(5, 6));
            Assert.Equal(new System.Windows.Point(20, 32), screen);
            Assert.Equal(new XYPosition(5, 6), flow.ScreenToFlow(screen));

            flow.CenterOn(new XYPosition(100, 50));
            Assert.Equal(new System.Windows.Point(400, 300), flow.FlowToScreen(new XYPosition(100, 50)));
            Assert.Equal(2, flow.Viewport.Zoom);

            flow.SetLocked(true);
            Assert.True(flow.IsLocked);
            flow.SetLocked(false);
            Assert.False(flow.IsLocked);

            // Changes injectés par l'application : appliqués et annoncés comme ceux d'un geste.
            var announced = 0;
            flow.NodeChangesRequested += (_, _) => announced++;
            flow.ApplyNodeChanges([new NodePositionChange("a", new XYPosition(7, 8))]);
            flow.ApplyEdgeChanges([new EdgeSelectChange("e", true)]);
            Assert.Equal(new XYPosition(7, 8), flow.GetNode("a")!.Position);
            Assert.Equal(1, announced);
            Assert.Equal("e", Selected());

            flow.SelectOnly("a", null);
            var deleted = flow.DeleteSelected();
            Assert.True(deleted.IsCompletedSuccessfully && deleted.Result);
            Assert.Equal(["b"], flow.Nodes.Select(n => n.Id));
            Assert.Empty(flow.Edges);
            Assert.False(flow.DeleteSelected().Result); // plus rien de sélectionné

            Assert.True(flow.CanUndo);
            flow.ClearHistory();
            Assert.False(flow.CanUndo);
            flow.Undo();
            Assert.Equal(["b"], flow.Nodes.Select(n => n.Id));
        });
    }
}
