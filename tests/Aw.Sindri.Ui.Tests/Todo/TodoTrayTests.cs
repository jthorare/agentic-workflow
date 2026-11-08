using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The tray's tooltip carries the todo counts (todo spec §3 table, Tray row).</summary>
[Lane(TestLane.Unit)]
public sealed class TodoTrayTests
{
    private static AttentionSummary Running() => AttentionSummary.For(feed: null, ShellState.Normal, A.Noon);

    [Theory]
    [InlineData(1, 0, "Sindri · running\n1 overdue todo")]
    [InlineData(3, 0, "Sindri · running\n3 overdue todos")]
    [InlineData(0, 1, "Sindri · running\n1 todo to sort out")]
    [InlineData(0, 2, "Sindri · running\n2 todos to sort out")]
    [InlineData(2, 1, "Sindri · running\n2 overdue todos · 1 todo to sort out")]
    [InlineData(0, 0, "Sindri · running")]
    public void The_tooltip_gains_each_nonzero_todo_count(int overdue, int toSortOut, string tooltip) =>
        Assert.Equal(tooltip, Running().WithTodos(overdue, toSortOut).Tray.Tooltip);

    [Theory]
    [InlineData(ShellState.NotRunning)]
    [InlineData(ShellState.Stale)]
    internal void A_tray_that_is_not_running_or_stale_adds_no_todo_line(ShellState state)
    {
        var summary = AttentionSummary.For(feed: null, state, A.Noon);

        Assert.Equal(summary.Tray.Tooltip, summary.WithTodos(overdue: 2, toSortOut: 1).Tray.Tooltip);
    }

    [Fact]
    public void The_badge_and_the_title_stay()
    {
        var plain = Running();

        var with = plain.WithTodos(overdue: 2, toSortOut: 1);

        Assert.Equal((plain.Badge, plain.Title, plain.Tray.State), (with.Badge, with.Title, with.Tray.State));
    }
}
