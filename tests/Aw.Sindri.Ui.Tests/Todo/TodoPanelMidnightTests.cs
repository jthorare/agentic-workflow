using Aw.Kernel.Testing;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The panel regroups at local midnight and keeps the selection on the same todo (todo spec §5, REQ-E3).</summary>
[Lane(TestLane.Unit)]
public sealed class TodoPanelMidnightTests
{
    private const string Flaky = "td_01jc5d4m7p2q9r5s8t1v3w6x0y";

    private readonly TodoFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task At_midnight_a_todo_due_today_becomes_overdue_and_one_due_tomorrow_becomes_today()
    {
        var panel = await _f.Expanded(Token);
        Assert.Equal("Today", panel.Groups.Single(g => g.Rows.Any(r => r.Id == Flaky)).Heading);

        _f.Clock.Advance(TimeSpan.FromHours(24));

        Assert.Equal("Overdue", panel.Groups.Single(g => g.Rows.Any(r => r.Id == Flaky)).Heading);
        Assert.Equal(["reply to Sam about the offsite"], panel.Groups.Single(g => g.Heading == "Today").Rows.Select(r => r.Text), StringComparer.Ordinal);
        Assert.Equal("5 open · 2 overdue", panel.CountLine);
    }

    [Fact]
    public async Task The_due_words_follow_the_new_day()
    {
        var panel = await _f.Read(Token);
        var flaky = panel.Rows.Single(r => r.Id == Flaky);
        Assert.Equal("Today", flaky.DueWord);

        _f.Clock.Advance(TimeSpan.FromHours(24));

        Assert.Equal("Overdue · Thu", flaky.DueWord);
        Assert.Same(flaky, panel.Rows.Single(r => r.Id == Flaky));
    }

    [Fact]
    public async Task The_selection_stays_on_the_same_todo_across_the_regroup()
    {
        var panel = await _f.Expanded(Token);
        panel.SelectedId = Flaky;

        _f.Clock.Advance(TimeSpan.FromHours(24));

        Assert.Equal(Flaky, panel.SelectedId);
    }

    [Fact]
    public async Task A_regroup_makes_no_new_read()
    {
        var panel = await _f.Read(Token);
        var reads = _f.Server.TodosCalls;

        _f.Clock.Advance(TimeSpan.FromHours(24));

        Assert.Equal(reads, _f.Server.TodosCalls);
        Assert.Equal(5, panel.Rows.Count);
    }

    [Fact]
    public async Task The_panel_regroups_again_at_the_next_midnight()
    {
        var panel = await _f.Expanded(Token);

        _f.Clock.Advance(TimeSpan.FromHours(24));
        _f.Clock.Advance(TimeSpan.FromHours(24));

        Assert.Equal("Overdue", panel.Groups.Single(g => g.Rows.Any(r => r.Id == "td_01jc5a8xq0n4w7ms2t9r3vbk6e")).Heading);
    }

    [Fact]
    public async Task Nothing_changes_before_midnight()
    {
        var panel = await _f.Expanded(Token);

        _f.Clock.Advance(TimeSpan.FromHours(23));

        Assert.Equal("Today", panel.Groups.Single(g => g.Rows.Any(r => r.Id == Flaky)).Heading);
    }
}
