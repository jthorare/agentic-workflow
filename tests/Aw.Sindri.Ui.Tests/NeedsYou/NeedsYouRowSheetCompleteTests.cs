using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The sheet of a desk task's Needs-you row offers Complete as a button named with the instance's address (the UX pass's A-1), and only for a desk task the app can complete.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouRowSheetCompleteTests
{
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private NeedsYouRowViewModel Row(NeedsYouEntry entry) => new(entry, seen: true, reducedMotion: false, arrived: false, _clock);

    [Fact]
    public void A_desk_task_offers_a_complete_button_named_with_its_address()
    {
        var row = Row(NeedsYouRows.Desk());

        Assert.Equal((true, "Complete", "Complete ITM-01JL1/dev-desk-round-trip/1", "CompleteStep"), (row.ShowsComplete, row.CompleteLabel, row.CompleteName, row.SheetComplete?.CatalogId));
    }

    [Fact]
    public void A_desk_task_whose_complete_is_not_ready_offers_no_button()
    {
        var desk = NeedsYouRows.Desk();
        var row = Row(desk with { Actions = [desk.Actions[0] with { Readiness = ActionReadiness.Pending }] });

        Assert.False(row.ShowsComplete);
    }

    [Fact]
    public void Any_other_row_offers_no_complete_button() =>
        Assert.All([NeedsYouRows.Park(), NeedsYouRows.Failure(), NeedsYouRows.Recipe()], entry => Assert.False(Row(entry).ShowsComplete));
}
