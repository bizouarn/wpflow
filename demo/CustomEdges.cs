using System.Globalization;
using System.Text;
using Wpflow.Core;

namespace demo;

/// <summary>
///     Type d'edge personnalisé « wave » : une ligne qui ondule. Un <see cref="IEdgePathProvider" /> ne produit que le
///     chemin ; la bibliothèque s'occupe du reste (trait, sélection, flèches, animation, clic).
///     Enregistré comme ressource : <c>&lt;demo:WaveEdge x:Key="wpflow-edge-wave" /&gt;</c>.
/// </summary>
public sealed class WaveEdge : IEdgePathProvider
{
    public EdgePath GetPath(EdgeGeometry g)
    {
        var (s, t) = (g.Source, g.Target);
        double dx = t.X - s.X, dy = t.Y - s.Y;
        var length = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
        var (nx, ny) = (-dy / length, dx / length); // normale au segment
        var steps = Math.Max(12, (int)(length / 6));
        var waves = Math.Max(1, (int)(length / 70));
        var amplitude = Math.Min(14, length / 8);

        var path = new StringBuilder();
        for (var i = 0; i <= steps; i++)
        {
            var u = (double)i / steps;
            var offset = amplitude * Math.Sin(2 * Math.PI * waves * u) * Math.Sin(Math.PI * u); // nulle aux extrémités
            var (x, y) = (s.X + dx * u + nx * offset, s.Y + dy * u + ny * offset);
            path.Append(i == 0 ? "M" : " L").Append(x.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                .Append(y.ToString("0.##", CultureInfo.InvariantCulture));
        }

        return new EdgePath(path.ToString(), (s.X + t.X) / 2, (s.Y + t.Y) / 2, Math.Abs(dx) / 2, Math.Abs(dy) / 2);
    }
}
