using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The sheets of a failure row and a profile row (U3' T6): what the API served about them, and only copies for what the app cannot run.</summary>
[Lane(TestLane.Unit)]
public sealed class RowSheetViewModelTests
{
    [Fact]
    public void Failure_shows_service_last_run_why_and_each_action_with_its_cli()
    {
        var details = FailureDetails.Build(NeedsYouRows.Failure(), "9h");

        Assert.Equal(
            [("Service", "index"), ("Failed", "9h"), ("Why", "SND-SCHED-001: A background service failed"), ("Run again", "Not in the app yet · sindri services run index"), ("Pause", "Not in the app yet · sindri services pause index")],
            details.Select(d => (d.Label, d.Value)));
    }

    [Fact]
    public void A_failure_has_no_closed_details_and_keeps_its_command_up_front()
    {
        var row = new NeedsYouRowViewModel(NeedsYouRows.Failure(), seen: false, reducedMotion: false, arrived: false, new FakeTimeProvider(A.Noon));

        Assert.Equal((false, row.Details.Count), (row.HasMoreDetails, row.PrimaryDetails.Count));
        Assert.Equal(row.Cli.Length > 0, row.ShowsCliUpFront);
    }

    [Fact]
    public void A_ready_action_shows_its_command_not_the_not_yet_words()
    {
        var entry = NeedsYouRows.Failure() with { Actions = [new NeedsYouAction("RunService", "Run again", ActionReadiness.Ready, "sindri services run index", RejectLike: false)] };

        Assert.Contains(new SheetDetail("Run again", "sindri services run index", Command: "sindri services run index"), FailureDetails.Build(entry, "1h"));
    }
}
