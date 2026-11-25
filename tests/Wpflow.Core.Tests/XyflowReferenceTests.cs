using System.Text.Json;
using System.Text.RegularExpressions;
using Wpflow.Core;

namespace Wpflow.Core.Tests;

/// <summary>
///     Compare les chemins d'edges à la sortie réelle de <c>@xyflow/system</c> (fichier <c>xyflow-reference.json</c>, produit
///     par un script Node : <c>getBezierPath</c>, <c>getSmoothStepPath</c>…, 7 écarts × 2 origines × 16 combinaisons de côtés = 224 cas).
/// </summary>
public class XyflowReferenceTests
{
    private static readonly Regex Token = new(@"[A-Za-z]|-?\d+(?:\.\d+)?(?:e-?\d+)?", RegexOptions.Compiled);

    private static (List<string> Commands, List<double> Numbers) Parse(string path)
    {
        var cmds = new List<string>();
        var nums = new List<double>();
        foreach (Match m in Token.Matches(path))
        {
            if (char.IsLetter(m.Value[0])) cmds.Add(m.Value);
            else nums.Add(double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        return (cmds, nums);
    }

    private static void AssertSamePath(string expected, string actual, string where)
    {
        var (ec, en) = Parse(expected);
        var (ac, an) = Parse(actual);
        Assert.True(ec.SequenceEqual(ac), $"{where}: commandes {string.Join("", ec)} / {string.Join("", ac)}\n  attendu {expected}\n  obtenu  {actual}");
        Assert.True(en.Count == an.Count, $"{where}: {en.Count} / {an.Count} nombres\n  attendu {expected}\n  obtenu  {actual}");
        for (var i = 0; i < en.Count; i++)
            Assert.True(Math.Abs(en[i] - an[i]) < 0.011, $"{where}: nombre {i}: {en[i]} / {an[i]}\n  attendu {expected}\n  obtenu  {actual}");
    }

    private static Position Side(string s) => s switch
    {
        "left" => Position.Left, "top" => Position.Top, "right" => Position.Right, _ => Position.Bottom
    };

    [Fact]
    public void Edge_paths_match_xyflow_system_output()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "xyflow-reference.json")));
        var root = doc.RootElement;
        var straight = root.GetProperty("straight");
        var sp = EdgePaths.Straight(new XYPosition(10, 20), new XYPosition(110, 90));
        AssertSamePath(straight.GetProperty("path").GetString()!, sp.Path, "straight");
        Assert.Equal(straight.GetProperty("lx").GetDouble(), sp.LabelX, 3);

        var count = 0;
        foreach (var c in root.GetProperty("cases").EnumerateArray())
        {
            var s = new XYPosition(c.GetProperty("sx").GetDouble(), c.GetProperty("sy").GetDouble());
            var t = new XYPosition(c.GetProperty("tx").GetDouble(), c.GetProperty("ty").GetDouble());
            var (ps, pt) = (Side(c.GetProperty("sp").GetString()!), Side(c.GetProperty("tp").GetString()!));
            var where = $"{s}->{t} {ps}/{pt}";

            var bezier = EdgePaths.Bezier(s, ps, t, pt);
            AssertSamePath(c.GetProperty("bezier").GetString()!, bezier.Path, "bezier " + where);
            Assert.Equal(c.GetProperty("bx").GetDouble(), bezier.LabelX, 3);
            Assert.Equal(c.GetProperty("by").GetDouble(), bezier.LabelY, 3);
            AssertSamePath(c.GetProperty("curved").GetString()!, EdgePaths.Bezier(s, ps, t, pt, 0.6).Path, "curvature " + where);

            AssertSamePath(c.GetProperty("step").GetString()!, EdgePaths.Step(s, ps, t, pt).Path, "step " + where);
            var smooth = EdgePaths.SmoothStep(s, ps, t, pt);
            AssertSamePath(c.GetProperty("smooth").GetString()!, smooth.Path, "smoothstep " + where);
            Assert.Equal(c.GetProperty("smx").GetDouble(), smooth.LabelX, 3);
            Assert.Equal(c.GetProperty("smy").GetDouble(), smooth.LabelY, 3);
            AssertSamePath(c.GetProperty("smooth2").GetString()!, EdgePaths.SmoothStep(s, ps, t, pt, 12, 35, 0.3).Path, "smoothstep options " + where);
            count++;
        }

        Assert.Equal(224, count);
    }
}
