using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The inbox's approvals (needs-you spec §8.3; q93): what the bar offers for a drifted profile, and that one plain click approves with no challenge.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouConfirmBarTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly FakeSindriApi _server = new();

    private async Task<NeedsYouBarViewModel> Bar(NeedsYouEntry entry)
    {
        var confirm = new NeedsYouConfirm(() => _api, _server);
        var bar = new NeedsYouBarViewModel(confirm);
        bar.Show(new NeedsYouRowViewModel(entry, seen: true, reducedMotion: false, arrived: false, _clock));
        await bar.Loaded;
        bar.ReadToEnd = true;
        return bar;
    }

    [Fact]
    public async Task A_drifted_profile_offers_the_approve_button_and_approves_nothing_by_showing_it()
    {
        var bar = await Bar(NeedsYouRows.ProfileReady());

        Assert.Equal((true, true, NeedsYouBarMode.Pending), (bar.ShowsPrimary, bar.PrimaryEnabled, bar.Mode));
        Assert.Empty(_server.ProfileApprovals);
    }

    [Fact]
    public async Task One_press_sends_the_full_hash_once_and_approves_the_row()
    {
        var bar = await Bar(NeedsYouRows.ProfileReady());

        await bar.PressApprove();
        await bar.PressApprove();   // a second press while approved sends nothing

        Assert.Equal(NeedsYouRows.ProfileHash, Assert.Single(_server.ProfileApprovals).Hash);
        Assert.True(bar.IsApproved);
    }

    [Fact]
    public async Task A_refused_approval_shows_the_words_and_leaves_the_row_open()
    {
        _server.ApproveProfileAnswer = RouteAnswer.Refused();
        var entry = NeedsYouRows.ProfileReady();
        var bar = await Bar(entry);

        await bar.PressApprove();

        Assert.Equal((false, true, "SND-AUTH-002: " + Labels.ApproveSessionRefused + " Run: " + entry.Cli), (bar.IsApproved, bar.HasRefusal, bar.Refusal));
        Assert.True(bar.PrimaryEnabled);
    }

    [Fact]
    public async Task Any_refused_profile_approval_names_its_code_and_the_rows_command()
    {
        _server.ApproveProfileAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.Conflict, "SND-PROFILE-009", "The settings changed again.");
        var entry = NeedsYouRows.ProfileReady();
        var bar = await Bar(entry);

        await bar.PressApprove();

        Assert.Equal((false, "SND-PROFILE-009: The settings changed again. Run: sindri profile approve " + NeedsYouRows.ProfileHash[..6]), (bar.IsApproved, bar.Refusal));
        Assert.Equal(entry.Cli, "sindri profile approve " + NeedsYouRows.ProfileHash[..6]);   // the served line, not a built one
    }

    [Fact]
    public async Task An_answer_that_did_not_approve_is_a_refusal_not_an_approval()
    {
        _server.ApproveProfileAnswer = RouteAnswer.Api("profile-approve", "preview");   // 200 with approved:false
        var bar = await Bar(NeedsYouRows.ProfileReady());

        await bar.PressApprove();

        Assert.Equal((false, Labels.NeedsYouNotApproved + " Run: " + NeedsYouRows.ProfileReady().Cli), (bar.IsApproved, bar.Refusal));
    }

    [Fact]
    public async Task A_second_press_while_the_first_is_in_flight_sends_nothing()
    {
        var gate = _server.ApproveProfileGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bar = await Bar(NeedsYouRows.ProfileReady());

        var first = bar.PressApprove();
        await bar.PressApprove();
        gate.SetResult();
        await first;

        _ = Assert.Single(_server.ProfileApprovals);
        Assert.True(bar.IsApproved);
    }

}
