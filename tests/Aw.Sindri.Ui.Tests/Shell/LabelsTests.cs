using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>UI spec §12.4's sentences that take a value.</summary>
[Lane(TestLane.Unit)]
public sealed class LabelsTests
{
    [Theory]
    [InlineData(0, "updated 0s ago")]
    [InlineData(2.9, "updated 2s ago")]
    [InlineData(59, "updated 59s ago")]
    [InlineData(60, "updated 1m ago")]
    [InlineData(3599, "updated 59m ago")]
    [InlineData(3600, "updated 1h ago")]
    [InlineData(-5, "updated 0s ago")]
    public void Updated_ago_counts_seconds_then_minutes_then_hours(double seconds, string words) =>
        Assert.Equal(words, Labels.UpdatedAgo(TimeSpan.FromSeconds(seconds)));

    // F3/F4: the queue's words are the guide's, never the spec's: stage for step, paused for parked, run for instance.
    [Fact]
    public void The_queue_and_item_words_are_plain()
    {
        Assert.Equal(["Title", "Repo", "Status", "Stage", "Age"], [Labels.ColumnTitle, Labels.ColumnRepo, Labels.ColumnState, Labels.ColumnStep, Labels.ColumnSince], StringComparer.Ordinal);
        Assert.Equal(("Paused: needs a decision", "Paused", "paused"), (Labels.ItemState("parked"), Labels.Group("parked"), Labels.ParkedBadge));
        Assert.Equal(("History", "Run", "Waiting for"), (Labels.Timeline, Labels.Instance, Labels.WaitingOn));
        Assert.Equal("Cancel run", Labels.Action("ApplyCommand:cancel"));
    }

    [Fact]
    public void The_versions_line_names_both() => Assert.Equal("Sindri speaks API v2; this app speaks v1", Labels.Versions(2, 1));

    // I7 (§12.4): every Not running reason is a sentence from S1b's SND-UI-001/002 wording, capitalised, with no
    // internals in it; the detail (path, address, pid and epoch) is its own line.
    [Fact]
    public void Not_running_reasons_are_sentences() =>
        Assert.All([Labels.NoRecord, Labels.RecordUnreadable, Labels.StaleRecord, Labels.NothingAnswered], r =>
        {
            Assert.True(char.IsUpper(r[0]), r);
            Assert.DoesNotMatch("[0-9()]", r);
        });

    // §12.4: the Not running card's last line states the follower's real retry wait, so the copy can't drift from it.
    [Fact]
    public void The_not_running_card_looks_again_as_often_as_the_follower_retries() =>
        Assert.Equal(Labels.LooksAgain, string.Create(CultureInfo.InvariantCulture, $"Looks again every {HealthViewModel.RetryEvery.TotalSeconds} s"));

    [Theory]
    [InlineData(69190, 4, 69190, 0, "recorded pid 69190 · epoch 4, answering epoch 0")]
    [InlineData(1, 7, 4242, 7, "recorded pid 1 · epoch 7, answering pid 4242 · epoch 7")]
    public void The_stale_record_detail_names_what_differs(int pid, long epoch, int answeringPid, long answeringEpoch, string detail) =>
        Assert.Equal(detail, Labels.StaleRecordDetail(pid, epoch, answeringPid, answeringEpoch));
}
