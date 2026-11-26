using System.Globalization;
using Wpflow.Core;

namespace Wpflow.Core.Tests;

/// <summary>
///     Une application en français écrit « 1,5 » : les tracés, le JSON et l'export ne doivent pas en dépendre
///     (ROADMAP-TESTS, lot 4).
/// </summary>
public class FrenchCultureTests
{
    /// <summary>Le résultat sous une culture à virgule décimale, et le même sous la culture invariante.</summary>
    private static (T French, T Invariant) Both<T>(Func<T> build)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            // Sans les réglages de l'utilisateur : un Windows en français peut garder le point décimal.
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR", false);
            Assert.Equal("1,5", 1.5.ToString());
            var french = build();
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            return (french, build());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Every_edge_path_uses_the_decimal_point()
    {
        var (a, b) = (new XYPosition(0.5, 1.25), new XYPosition(120.75, 90.5));

        var (french, invariant) = Both(() => new[]
        {
            EdgePaths.Straight(a, b).Path,
            EdgePaths.Bezier(a, Position.Right, b, Position.Left).Path,
            EdgePaths.SimpleBezier(a, Position.Right, b, Position.Left).Path,
            EdgePaths.SmoothStep(a, Position.Right, b, Position.Left).Path,
            EdgePaths.Step(a, Position.Bottom, b, Position.Top).Path
        });

        Assert.Equal(invariant, french);
    }

    [Fact]
    public void Json_written_in_french_reads_back_the_same()
    {
        Node[] nodes = [new("a", new XYPosition(1.5, -2.25), Width: 100.5, Height: 40.25, Rotation: 12.5)];
        Edge[] edges = [new("e", "a", "a")];
        var viewport = new Viewport(10.5, -3.75, 1.25);

        var (french, invariant) = Both(() => GraphSerializer.ToJson(nodes, edges, viewport));

        Assert.Equal(invariant, french);
        var (read, _) = Both(() => GraphSerializer.FromJson(french));
        Assert.Equal(nodes[0].Position, read.Nodes[0].Position);
        Assert.Equal(viewport, read.Viewport);
    }

    [Fact]
    public void The_svg_export_uses_the_decimal_point()
    {
        Node[] nodes = [new("a", new XYPosition(0.5, 1.25), Width: 100.5, Height: 40.25)];
        var edge = new SvgEdge(new Edge("e", "a", "a", Label: "x"), EdgePaths.Straight(new XYPosition(0.5, 0), new XYPosition(10.25, 10)),
            false, true);

        var (french, invariant) = Both(() => SvgExport.Build(nodes, [edge], padding: 10.5));

        Assert.Equal(invariant, french);
    }
}
