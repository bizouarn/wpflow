using Wpflow.Core;
using Presence = Wpflow.Core.VirtualizationRule.Presence;

namespace Wpflow.Core.Tests;

public class VirtualizationRuleTests
{
    private static readonly Rect Show = new(0, 0, 100, 100);
    private static readonly Rect Keep = new(-50, -50, 200, 200);

    private static Rect At(double x) => new(x, 10, 10, 10);

    private static string Name(Presence presence) => presence.ToString();

    [Theory]
    [InlineData(50, "Shown")]
    [InlineData(120, "Kept")]
    [InlineData(400, "Absent")]
    public void A_node_is_shown_kept_or_absent(double x, string expected) =>
        Assert.Equal(expected, Name(VirtualizationRule.OfNode(false, At(x), Show, Keep)));

    [Fact]
    public void A_selected_node_is_always_shown() =>
        Assert.Equal(Presence.Shown, VirtualizationRule.OfNode(true, At(4000), Show, Keep));

    [Theory]
    [InlineData(-400, 400, "Shown")]   // les deux bouts au loin, mais le lien traverse la vue
    [InlineData(115, 130, "Kept")]
    [InlineData(400, 500, "Absent")]
    public void An_edge_follows_the_box_around_its_two_nodes(double from, double to, string expected) =>
        Assert.Equal(expected, Name(VirtualizationRule.OfEdge(At(from), At(to), Show, Keep)));

    [Fact]
    public void The_drawing_changes_only_when_an_element_appears_or_leaves_the_kept_zone()
    {
        Assert.True(VirtualizationRule.IsOutdated(false, Presence.Shown));
        Assert.True(VirtualizationRule.IsOutdated(true, Presence.Absent));
        Assert.False(VirtualizationRule.IsOutdated(true, Presence.Shown));
        Assert.False(VirtualizationRule.IsOutdated(true, Presence.Kept));
        Assert.False(VirtualizationRule.IsOutdated(false, Presence.Kept));
        Assert.False(VirtualizationRule.IsOutdated(false, Presence.Absent));
    }
}
