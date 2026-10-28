using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>
/// #1170: the golden's desk requires fact:output (a file), and the engine refuses a 'done' report without it ("step.produce reported 'done' without fact:output").
/// The action bar has no file to give, so it must not send: the person completes it from the row's sheet, which carries the file inputs.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouBarRequiredOutputTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new();

    [Fact]
    public async Task Approve_on_a_row_whose_step_needs_an_output_file_sends_nothing_and_does_not_show_it_done()
    {
        var bar = new NeedsYouBarViewModel(new DeskReporter(_server, () => _api), _clock);
        bar.Show(new NeedsYouRowViewModel(NeedsYouRows.Desk(), seen: true, reducedMotion: false, arrived: false, _clock));
        await bar.Loaded;
        bar.ReadToEnd = true;

        await bar.PressApprove();

        Assert.Empty(_server.CompleteCalls);
        Assert.False(bar.IsApproved);
    }
}
