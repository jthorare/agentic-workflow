using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>What a launch's viewer came to, in words, and when the attach command is shown beside it.</summary>
[Lane(TestLane.Unit)]
public sealed class RunWordsViewerTests
{
    private const string Command = "tmux -L sindri-launch attach -t =snd-LCH-00000000000000000000000000000001";

    private static LaunchView Started(LaunchViewerView viewer) =>
        AgentRunFixtures.Golden<LaunchView>("launches-id", "started-print") with { Viewer = viewer };

    [Fact]
    public void A_skipped_viewer_with_a_reason_shows_the_reason_and_the_command()
    {
        var launch = Started(new LaunchViewerView("warp", "Warp", "skipped", Code: null, Reason: "background: viewer opens on takeover"));

        Assert.Equal(Labels.LaunchViewerSkipped("background: viewer opens on takeover"), RunWords.LaunchLine(launch));
        Assert.True(RunWords.ShowsCommand(launch));
        Assert.Equal(Command, RunWords.Command(launch));
    }

    [Fact]
    public void A_failed_viewer_shows_its_code_and_detail_and_the_command()
    {
        var launch = Started(new LaunchViewerView("warp", "Warp", "failed", "SND-LAUNCH-001", "warp: no display"));

        Assert.Equal(Labels.LaunchViewerFailed("SND-LAUNCH-001", "warp: no display"), RunWords.LaunchLine(launch));
        Assert.True(RunWords.ShowsCommand(launch));
    }

    [Theory]
    [InlineData("opened")]
    [InlineData("unconfirmed")]
    public void An_opened_or_unconfirmed_viewer_shows_no_command(string outcome)
    {
        var launch = Started(new LaunchViewerView("warp", "Warp", outcome, Code: null));

        Assert.False(RunWords.ShowsCommand(launch));
    }

    [Fact]
    public void A_skipped_viewer_with_no_reason_never_reads_as_none()
    {
        var launch = Started(new LaunchViewerView("warp", "Warp", "skipped", Code: null));

        Assert.Equal(Labels.LaunchRunning, RunWords.LaunchLine(launch));
    }
}
