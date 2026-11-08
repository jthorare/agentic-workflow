using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Home;

/// <summary>Home's Running rows (F3 home spec §1 Q3): each carries the shared status badge, with the stage and step beside it and the time in the step.</summary>
[Lane(TestLane.Unit)]
public sealed class HomeRunningRowsTests
{
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(FixtureInstances.Rows.Max(r => r.Since) + TimeSpan.FromMinutes(90));

    private StripRowViewModel Row(string word, ChipTone tone) =>
        new(FixtureInstances.Rows[0] with { Chip = new StateChip(word, tone) }, _clipboard, _clock);

    [Fact]
    public void A_running_row_wears_the_running_badge()
    {
        var status = Row("RUNNING", ChipTone.Ok).Status;

        Assert.Equal((RunStatusKind.Running, Labels.WorkflowsStatusRunning), (status.Kind, status.Word));
    }

    [Fact]
    public void A_row_the_user_took_over_wears_the_took_over_badge()
    {
        var status = Row("WITH YOU", ChipTone.Neutral).Status;

        Assert.Equal((RunStatusKind.TookOver, Labels.WorkflowsStatusTookOver), (status.Kind, status.Word));
    }

    [Fact]
    public void A_word_this_build_does_not_know_shows_as_served()
    {
        var status = Row("SOMETHING NEW", ChipTone.Neutral).Status;

        Assert.Equal((RunStatusKind.Unknown, "SOMETHING NEW"), (status.Kind, status.Word));
    }

    [Fact]
    public void The_badge_is_the_rows_accessible_status_and_the_time_in_the_step_stays_on_the_row()
    {
        var row = Row("RUNNING", ChipTone.Ok);

        Assert.Equal(row.Status.Word, row.StatusName);
        Assert.NotEmpty(row.Age);
        Assert.NotEmpty(row.Stage);
    }
}
