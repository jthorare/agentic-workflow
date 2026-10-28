using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>#1170: the bar has no file to give, so Approve on a step that needs one asks the shell for the row's sheet and says why (the sends-nothing check is NeedsYouBarRequiredOutputTests).</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouBarSheetRequestTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new();

    [Fact]
    public async Task Approve_on_a_step_that_needs_an_output_file_asks_for_the_sheet_and_says_why()
    {
        var bar = new NeedsYouBarViewModel(new DeskReporter(_server, () => _api), _clock);
        bar.Show(new NeedsYouRowViewModel(NeedsYouRows.Desk(), seen: true, reducedMotion: false, arrived: false, _clock));
        await bar.Loaded;
        bar.ReadToEnd = true;
        var asked = new List<string>();
        bar.SheetRequested += (_, e) => asked.Add(e.RowId);

        await bar.PressApprove();

        Assert.Equal([NeedsYouRows.Desk().Id], asked, StringComparer.Ordinal);
        Assert.Equal(Labels.CompleteNeedsOutputInSheet, bar.Refusal);
    }
}
