using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>Names that say which row (review run 1, K10 / a11y-5, a11y-10; UI spec §11.2, §11.3): urgency word, chip, title, id, "new"; a control is its label and the row's id.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouNamesTests
{
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private NeedsYouRowViewModel Row(NeedsYouEntry entry, bool seen = true, bool reduced = false) => new(entry, seen, reduced, arrived: false, _clock);

    [Fact]
    public void A_row_name_is_urgency_chip_title_id_and_new_when_unseen()
    {
        Assert.Equal("needs you now, PARKED, Retry storm in outbox dispatcher, ITM-01JC8, new", Row(NeedsYouRows.Park(), seen: false).AccessibleName);
        Assert.Equal("needs you now, PARKED, Retry storm in outbox dispatcher, ITM-01JC8", Row(NeedsYouRows.Park(), seen: true).AccessibleName);
        Assert.Equal("failed, FAILED, Nightly index build, failure:service:index", Row(NeedsYouRows.Failure()).AccessibleName);
        Assert.Equal("to look at, PROFILE, Profile changed since approval · 2 keys, profile:drift", Row(NeedsYouRows.Profile()).AccessibleName);
    }

    [Fact]
    public void A_controls_name_is_its_label_and_the_rows_id_and_the_title_button_opens_that_row()
    {
        var park = Row(NeedsYouRows.Park());

        Assert.Equal("Grant rounds ITM-01JC8", park.Controls[0].Name);
        Assert.Equal("Open Retry storm in outbox dispatcher ITM-01JC8", park.OpenName);
    }

    [Fact]
    public void A_decision_options_name_is_its_label_and_the_decisions_id()
    {
        var sheet = new DecisionSheetViewModel(new FakeSindriApi(), () => null, _clock, new DecisionSender(new FakeSindriApi()));
        sheet.Show(Row(NeedsYouRows.Decision()));

        Assert.Equal(["Accept DEC-1", "Decline DEC-1"], sheet.Options.Select(o => o.Name), StringComparer.Ordinal);
    }

}
