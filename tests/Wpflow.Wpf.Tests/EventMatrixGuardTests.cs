using System.Reflection;
using Microsoft.AspNetCore.Components;
using Scenarios;
using WpfView = Wpflow.Wpf.FlowView;
using BlazorView = Wpflow.Blazor.FlowView;

namespace Wpflow.Wpf.Tests;

/// <summary>
///     Garde de la matrice des évènements et des gestes (ROADMAP-TESTS, lot 3) : tout évènement public des deux
///     bibliothèques est inscrit dans <see cref="EventCatalog" />, et tout évènement inscrit est attendu par au moins un
///     geste de <see cref="GestureMatrix" />, joué avec de vraies entrées par les deux applications témoins.
/// </summary>
public class EventMatrixGuardTests
{
    /// <summary>Les <c>XChanged</c> sont des liaisons bidirectionnelles : elles disent un état, pas un évènement.</summary>
    private static readonly HashSet<string> TwoWayBindings =
    [
        "FlowView.NodesChanged", "FlowView.EdgesChanged", "FlowView.ViewportChanged", "FlowView.IsLockedChanged",
        "FlowView.SelectedNodeItemsChanged", "FlowView.SelectedEdgeItemsChanged", "FlowView.SelectedNodeChanged",
        "FlowView.ControllerChanged"
    ];

    private static IEnumerable<string> WpfEvents() => typeof(WpfView).Assembly.GetExportedTypes()
        .SelectMany(t => t.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(e => t.Name + "." + e.Name));

    private static IEnumerable<string> BlazorCallbacks() => typeof(BlazorView).Assembly.GetExportedTypes()
        .Where(t => typeof(IComponent).IsAssignableFrom(t))
        .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.GetCustomAttribute<ParameterAttribute>() != null && IsCallback(p.PropertyType))
            .Select(p => t.Name + "." + p.Name))
        .Where(name => !TwoWayBindings.Contains(name));

    private static bool IsCallback(Type type) =>
        type == typeof(EventCallback) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EventCallback<>));

    private static void Compare(IEnumerable<string> real, IEnumerable<string> listed)
    {
        var (a, b) = (real.ToHashSet(), listed.ToHashSet());
        var problems = a.Except(b).Select(x => "absent du catalogue : " + x)
            .Concat(b.Except(a).Select(x => "dans le catalogue mais introuvable : " + x)).Order().ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Every_wpf_event_is_in_the_catalog() => Compare(WpfEvents(), EventCatalog.All.SelectMany(e => e.Wpf));

    [Fact]
    public void Every_blazor_callback_is_in_the_catalog() => Compare(BlazorCallbacks(), EventCatalog.All.SelectMany(e => e.Blazor));

    /// <summary>
    ///     Dans l'autre sens que <see cref="EventCommandTests" /> : toute commande publique suit un évènement, donc la matrice,
    ///     ou remplace une action de la vue et figure ici avec le test qui la couvre.
    /// </summary>
    [Fact]
    public void Every_wpf_command_follows_an_event_or_is_listed()
    {
        var replacing = new HashSet<string>
        {
            "FlowView.NodesDeleteCommand", // éditeur de schéma témoin en MVVM, à la vraie souris et au clavier
            "FlowView.EdgesDeleteCommand"
        };
        var followers = EventCommandTests.Notified.Values.Concat(EventCommandTests.Dedicated.Values)
            .Select(name => "FlowView." + name).ToHashSet();

        var unknown = typeof(WpfView).Assembly.GetExportedTypes()
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.Name.EndsWith("Command") && typeof(System.Windows.Input.ICommand).IsAssignableFrom(p.PropertyType))
                .Select(p => t.Name + "." + p.Name))
            .Where(name => !followers.Contains(name) && !replacing.Contains(name))
            .Order().ToList();

        Assert.True(unknown.Count == 0, "commandes sans évènement ni couverture : " + string.Join(", ", unknown));
    }

    [Fact]
    public void Every_event_of_the_catalog_is_expected_by_a_gesture()
    {
        foreach (var blazor in new[] { false, true })
        {
            var expected = GestureMatrix.All.SelectMany(g => g.ExpectedFor(blazor)).Select(x => x.Split(' ')[0]).ToHashSet();
            var missing = EventCatalog.All.Select(e => e.Name).Where(name => !expected.Contains(name)).ToList();
            Assert.True(missing.Count == 0, $"sans geste ({(blazor ? "Blazor" : "WPF")}) : " + string.Join(", ", missing));
        }
    }

    [Fact]
    public void The_matrix_only_names_known_events_and_explains_every_difference()
    {
        var names = EventCatalog.All.Select(e => e.Name).ToHashSet();
        Assert.Equal(GestureMatrix.All.Count, GestureMatrix.All.Select(g => g.Name).Distinct().Count());
        foreach (var g in GestureMatrix.All)
        {
            var used = g.Expected.Concat(g.Blazor ?? []).Select(x => x.Split(' ')[0]).Concat(g.Also).Concat(g.Only);
            Assert.All(used, name => Assert.Contains(name, names));
            Assert.True(g.Blazor == null || !string.IsNullOrWhiteSpace(g.Why), g.Name + " : écart sans raison");
        }
    }

    [Fact]
    public void Continuous_events_are_grouped_and_background_events_ignored()
    {
        var gesture = new Gesture("essai");
        Assert.Equal(["NodeDragStart a", "NodeDrag a", "SelectionDrag a", "NodeDragStop a", "NodeDrag a"],
            gesture.Reduce([
                "NodeDragStart a", "NodeDrag a", "NodesChange", "SelectionDrag a", "NodeDrag a", "SelectionDrag a",
                "NodeDragStop a", "NodeDrag a"
            ]));
        Assert.Equal(["NodeClick a", "NodeClick a"], gesture.Reduce(["NodeClick a", "NodeClick a"])); // deux appels restent deux
    }
}
