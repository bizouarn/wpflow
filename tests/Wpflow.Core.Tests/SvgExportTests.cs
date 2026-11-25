using System.Xml.Linq;
using Wpflow.Core;

namespace Wpflow.Core.Tests;

public class SvgExportTests
{
    private static readonly Node A = new("a", new XYPosition(0, 0), Data: "A <& \"B\"", Width: 100, Height: 40);

    [Fact]
    public void The_drawing_is_valid_xml_even_with_hostile_texts_and_colors()
    {
        var edge = new Edge("e", "a", "a", Label: "<x>", Style: new EdgeStyleOptions(Stroke: "red\"/><script/>"));

        var svg = SvgExport.Build([A], [new SvgEdge(edge, EdgePaths.Straight(new XYPosition(0, 0), new XYPosition(10, 10)), false, true)],
            background: "\"><script/>");

        var document = XDocument.Parse(svg);
        Assert.DoesNotContain(document.Descendants(), e => e.Name.LocalName == "script");
    }

    [Fact]
    public void The_frame_holds_the_visible_nodes_and_hidden_ones_are_left_out()
    {
        var svg = SvgExport.Build([A, new Node("hidden", new XYPosition(900, 900), Width: 10, Height: 10, Hidden: true)], [], padding: 10);

        Assert.Contains("viewBox=\"-10 -10 120 60\"", svg);
        Assert.Equal(2, XDocument.Parse(svg).Descendants().Count(e => e.Name.LocalName == "rect")); // le fond et « a »
    }
}
