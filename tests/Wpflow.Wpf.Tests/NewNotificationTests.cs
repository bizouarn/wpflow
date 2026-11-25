using Wpflow.Core;
using Wpflow.Wpf;

namespace Wpflow.Wpf.Tests;

/// <summary>Les évènements et réglages ajoutés à la vue WPF pour la parité.</summary>
public class NewNotificationTests
{
    private static FlowView Make() => new()
    {
        Nodes = [new Node("a", new XYPosition(0, 0)), new Node("b", new XYPosition(100, 0), Deletable: false)],
        Edges = [new Edge("e", "a", "b")]
    };

    [Fact]
    public void Selection_changes_are_notified_once_each() => Sta.Run(() =>
    {
        var flow = Make();
        var seen = new List<string>();
        flow.SelectionChanged += (_, e) => seen.Add(string.Join("+", e.Nodes.Select(n => n.Id).Concat(e.Edges.Select(x => x.Id))));

        flow.SelectOnly("a", null);
        flow.SelectOnly("a", null); // rien ne change
        flow.UpdateNode("a", n => n with { Position = new XYPosition(5, 5) }); // la sélection non plus
        flow.ClearSelection();

        Assert.Equal(["a", ""], seen);
    });

    [Fact]
    public void A_deletion_reports_what_actually_went_away() => Sta.Run(() =>
    {
        var flow = Make();
        DeletedEventArgs? deleted = null;
        flow.Deleted += (_, e) => deleted = e;

        flow.DeleteElements(["a", "b"], null);

        Assert.Equal(["a"], deleted!.Nodes.Select(n => n.Id)); // « b » n'est pas supprimable
        Assert.Equal(["e"], deleted.Edges.Select(x => x.Id));
    });

    [Fact]
    public void The_history_can_be_disabled_and_limited() => Sta.Run(() =>
    {
        var flow = Make();
        flow.HistoryLimit = 1;
        flow.DeleteElements(null, ["e"]);
        flow.DeleteElements(["a"], null);

        flow.Undo();
        Assert.False(flow.CanUndo); // un seul état gardé
        flow.HistoryEnabled = false;
        flow.DeleteElements(["a"], null);

        Assert.False(flow.CanUndo);
    });

    [Fact]
    public void In_controlled_mode_the_view_keeps_no_history() => Sta.Run(() =>
    {
        var flow = Make();
        flow.AutoApplyChanges = false;

        flow.DeleteElements(["a"], null);

        Assert.False(flow.CanUndo);
    });
}
