using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The inbox's action bar (needs-you spec §3 and §5, slice 2): what A, D and the buttons do per kind of request, and what is sent once.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouBarViewModelTests
{
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly List<NeedsYouDecisionEventArgs> _decisions = [];
    private Func<NeedsYouDecisionEventArgs, Task<string>> _decisionAnswer = _ => Task.FromResult("");

    private async Task<NeedsYouBarViewModel> Bar(NeedsYouEntry entry, bool read = false)
    {
        var bar = new NeedsYouBarViewModel();
        bar.DecisionRequested += (_, e) =>
        {
            _decisions.Add(e);
            e.Settled = _decisionAnswer(e);
        };
        bar.Show(new NeedsYouRowViewModel(entry, seen: true, reducedMotion: false, arrived: false, _clock));
        await bar.Loaded;
        bar.ReadToEnd = read;
        return bar;
    }

    [Fact]
    public async Task A_decision_is_accepted_with_its_recommended_option_and_declined_with_a_reason()
    {
        var bar = await Bar(NeedsYouRows.Decision());

        Assert.Equal(("Accept", "Decline"), (bar.ApproveWord, bar.SendBackWord));
        await bar.PressApprove();
        await bar.PressSendBack();
        bar.ReasonText = "out of scope";
        await bar.Send();

        Assert.Equal([("decision:DEC-1", "accept", ""), ("decision:DEC-1", "decline", "out of scope")], _decisions.Select(d => (d.RowId, d.OptionId, d.Note)));
    }

    [Fact]
    public async Task A_settings_request_can_only_be_approved_so_send_back_is_disabled_with_the_reason()
    {
        var bar = await Bar(NeedsYouRows.Profile());

        Assert.Equal((true, false, "These can only be approved. Change the settings or workflow file to undo."), (bar.ShowsSendBack, bar.SendBackEnabled, bar.SendBackTooltip));
        await bar.PressSendBack();
        Assert.Equal(NeedsYouBarMode.Pending, bar.Mode);
    }

    [Fact]
    public async Task A_failure_keeps_the_rows_own_controls_and_has_no_bar()
    {
        var bar = await Bar(NeedsYouRows.Failure());

        Assert.Equal((true, false, false), (bar.ShowsRowControls, bar.ShowsPrimary, bar.ShowsSendBack));
    }

    [Fact]
    public async Task A_declined_decision_the_server_refuses_keeps_the_reason_and_shows_the_refusal_in_the_bar()
    {
        _decisionAnswer = _ => Task.FromResult("That decision was already answered.");
        var bar = await Bar(NeedsYouRows.Decision());
        await bar.PressSendBack();
        bar.ReasonText = "out of scope";

        await bar.Send();

        Assert.Equal((NeedsYouBarMode.SendingBack, "out of scope", "That decision was already answered."), (bar.Mode, bar.ReasonText, bar.Refusal));
    }

    [Fact]
    public async Task A_declined_decision_that_was_accepted_closes_the_box_and_clears_the_reason()
    {
        var bar = await Bar(NeedsYouRows.Decision());
        await bar.PressSendBack();
        bar.ReasonText = "out of scope";

        await bar.Send();

        Assert.Equal((NeedsYouBarMode.Pending, "", ""), (bar.Mode, bar.ReasonText, bar.Refusal));
    }

    [Fact]
    public async Task A_pressed_twice_while_the_decision_is_in_flight_answers_it_once()
    {
        var gate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _decisionAnswer = _ => gate.Task;
        var bar = await Bar(NeedsYouRows.Decision());

        var first = bar.PressApprove();
        await bar.PressApprove();
        gate.SetResult("");
        await first;

        _ = Assert.Single(_decisions);
    }

}
