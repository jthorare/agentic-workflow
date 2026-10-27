using Aw.UiDriver.Core.Protocol;
using Aw.UiDriver.Core.Selectors;

namespace Aw.UiDriver.Tests;

public sealed class WaitNotVisibleTests
{
    private static readonly NodeFacts _shown = Node("0/1", visible: true);
    private static readonly NodeFacts _hidden = Node("0/2", visible: false);

    private static NodeFacts Node(string id, bool visible) => new(id, "TextBlock", AutomationId: null, Name: null, Text: "Reading the map", visible, Enabled: true);

    private static bool Holds(WaitState state, params NodeFacts[] matches) => WaitStates.Holds(state, matches, matches.FirstOrDefault(), text: null);

    [Fact, Trait("Lane", "unit")]
    public void NotVisible_holds_when_nothing_matches() => Assert.True(Holds(WaitState.NotVisible));

    [Fact, Trait("Lane", "unit")]
    public void NotVisible_holds_when_every_match_is_hidden() => Assert.True(Holds(WaitState.NotVisible, _hidden, Node("0/3", visible: false)));

    [Fact, Trait("Lane", "unit")]
    public void NotVisible_does_not_hold_while_any_match_is_visible()
    {
        Assert.False(Holds(WaitState.NotVisible, _shown));
        Assert.False(Holds(WaitState.NotVisible, _hidden, _shown));
    }

    [Fact, Trait("Lane", "unit")]
    public void NotVisible_is_a_state_by_name() => Assert.True(WaitStates.TryParse("notVisible", out var state) && state == WaitState.NotVisible);

    [Fact, Trait("Lane", "unit")]
    public void The_other_states_still_read_the_picked_node()
    {
        Assert.False(Holds(WaitState.Gone, _hidden));
        Assert.True(Holds(WaitState.Hidden, _hidden));
        Assert.True(Holds(WaitState.Gone));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failed_gone_wait_says_when_the_match_is_only_hidden()
    {
        Assert.Contains("hidden", WaitStates.Hint(WaitState.Gone, [_hidden]), StringComparison.Ordinal);
        Assert.Contains("notVisible", WaitStates.Hint(WaitState.Gone, [_hidden]), StringComparison.Ordinal);
        Assert.Equal(string.Empty, WaitStates.Hint(WaitState.Gone, [_shown]));
        Assert.Equal(string.Empty, WaitStates.Hint(WaitState.Visible, [_hidden]));
    }
}
