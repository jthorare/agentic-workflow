using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Linear;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>
/// Issue #2536: a state-change row read "FRN-6177 moved from  to" because the facts port sends null state names when the team's stored state list can't name an id, and the label printed the nulls as empty text.
/// A row names the states it knows and never prints a blank.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class A_state_change_carries_both_state_names
{
    private static readonly DateTimeOffset _at = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    private static string Row(string? from, string? to) => Labels.LinearSinceRow(new LinearSinceItem("state", "FRN-6177", "Retry budget", Actor: null, _at, from, to));

    [Fact]
    public void A_move_with_both_names_says_from_and_to() =>
        Assert.Equal("FRN-6177 moved from Todo to In Progress", Row("Todo", "In Progress"));

    [Fact]
    public void A_move_whose_old_state_is_unknown_says_only_where_it_went() =>
        Assert.Equal("FRN-6177 moved to In Progress", Row(from: null, "In Progress"));

    [Fact]
    public void A_move_with_no_names_says_a_new_state_and_never_prints_a_blank()
    {
        var text = Row(from: null, to: null);

        Assert.DoesNotContain("  ", text, StringComparison.Ordinal);
        Assert.Equal("FRN-6177 moved to a new state", text);
    }
}
