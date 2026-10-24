using System.Net;

using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Controls;

/// <summary>The control renderer (UI spec §7, U3) and the command lifecycle (§4.1), from catalog rows as the API serves them.</summary>
[Lane(TestLane.Unit)]
public sealed class ControlViewModelTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static CommandRowView Row(string action, string ui, string cli = "sindri views rebuild", bool destructive = false) =>
        new(action, "desk", ["human+terminal", "human"], Terminal: false, "POST", "/sindri/views/rebuild", cli, ui, destructive, ReadsViews: false, Shortcut: null);

    private ControlViewModel Control(CommandRowView row) => new(row, ct => _server.RebuildViews(_api, ct), _clipboard, _clock);

    [Fact]
    public void A_row_with_a_sender_is_a_button_named_by_its_label()
    {
        var control = Control(Row("RebuildViews", "ready"));

        Assert.Equal((false, true, "Rebuild views"), (control.IsCopyOnly, control.CanRun, control.Label));
        Assert.Equal("Rebuild views", control.Name);
    }

    // q93: whatever ui word the row carries, a row with a sender is a button and one without is a copy block.
    [Theory]
    [InlineData("pending:P2")]
    [InlineData("cli-only:typed-confirmation")]
    public void The_ui_word_does_not_gate_a_row_the_app_can_send(string ui)
    {
        var control = Control(Row("RebuildViews", ui));

        Assert.Equal((true, true, "Rebuild views"), (control.CanRun, control.IsSendable, control.Name));
    }

    // §7: a row the app has no form for is a copy block: the cli and Copy; it never offers to run it.
    [Fact]
    public void A_row_without_a_sender_is_a_copy_block_that_never_runs()
    {
        var control = new ControlViewModel(Row("ApproveProfile", "ready", "sindri profile approve [<hash>]"), send: null, _clipboard, _clock);

        Assert.Equal((true, false, true), (control.IsCopyOnly, control.CanRun, control.CanCopy));
        Assert.Equal("sindri profile approve [<hash>]", control.Cli);
    }

    // §7 backstop: a cli with a control or bidi character is neither shown nor copied (E-16 makes this never happen).
    [Theory]
    [InlineData("sindri stop\nrm -rf ~")]
    [InlineData("sindri stop ‮evil")]
    public async Task A_cli_with_a_control_or_bidi_character_is_never_shown_or_copied(string cli)
    {
        var control = Control(Row("StopProcess", "cli-only:typed-confirmation", cli));

        await control.Copy(Token);

        Assert.Equal((string.Empty, false), (control.Cli, control.CanCopy));
        Assert.Null(_clipboard.Text);
    }

    // §4.1 Copy: "Copied" for 1.5 s, announced.
    [Fact]
    public async Task Copy_puts_the_cli_on_the_clipboard_and_says_copied_for_1_5_s()
    {
        var control = Control(Row("ApproveProfile", "cli-only:typed-confirmation", "sindri profile approve [<hash>]"));

        await control.Copy(Token);

        Assert.Equal("sindri profile approve [<hash>]", _clipboard.Text);
        Assert.Equal(("Copied", "Copied"), (control.CopyLabel, control.Announcement));
        _clock.Advance(TimeSpan.FromMilliseconds(1499));
        Assert.Equal("Copied", control.CopyLabel);
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal("Copy", control.CopyLabel);
    }

    // §4.1 sending: disabled with its label kept; "Sending…" after 300 ms; a second click does nothing.
    [Fact]
    public async Task While_sending_the_control_is_disabled_a_second_click_does_nothing_and_sending_is_announced_after_300_ms()
    {
        _server.RebuildGate = new TaskCompletionSource();
        var control = Control(Row("RebuildViews", "ready"));

        var first = control.Press(Token);
        var second = control.Press(Token);
        await second;

        Assert.Equal((CommandPhase.Sending, false, "Rebuild views", 1), (control.Phase, control.CanRun, control.Label, _server.RebuildCalls));
        Assert.False(control.IsAnnouncingSending);
        _clock.Advance(TimeSpan.FromMilliseconds(300));
        Assert.Equal((true, "Sending…"), (control.IsAnnouncingSending, control.Announcement));

        _server.RebuildGate.SetResult();
        await first;
        Assert.False(control.IsAnnouncingSending);
    }

    // §4.1 accepted: an inline check and the outcome in words for 2 s, announced; then the control is ready again.
    [Fact]
    public async Task Accepted_shows_the_outcome_for_2_s_then_the_control_is_ready_again()
    {
        var control = Control(Row("RebuildViews", "ready"));

        await control.Press(Token);

        Assert.Equal((CommandPhase.Accepted, "Rebuild views: done", "Rebuild views: done"), (control.Phase, control.Outcome, control.Announcement));
        _clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal((CommandPhase.Idle, true), (control.Phase, control.CanRun));
    }

    // §4.1 refused / failed: the API's title (the SND code) and detail as served, inline; the control stays usable.
    [Fact]
    public async Task A_refusal_shows_the_apis_code_and_detail_as_served()
    {
        _server.Rebuild = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-010", "Views are rebuilding (fix: retry after the rebuild)");
        var control = Control(Row("RebuildViews", "ready"));

        await control.Press(Token);

        Assert.Equal((CommandPhase.Failed, "SND-API-010", "Views are rebuilding (fix: retry after the rebuild)"), (control.Phase, control.ErrorCode, control.ErrorDetail));
        Assert.True(control.CanRun);
        _clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(CommandPhase.Failed, control.Phase);
    }

    // A ready row the app has nothing wired to (StopProcess before Health's confirm, U4) never runs.
    [Fact]
    public async Task A_row_without_a_sender_never_runs()
    {
        var control = new ControlViewModel(Row("StopProcess", "ready", "sindri stop", destructive: true), send: null, _clipboard, _clock);

        await control.Press(Token);

        Assert.Equal((true, false, CommandPhase.Idle), (control.IsCopyOnly, control.CanRun, control.Phase));
    }

    // The lane decision of 2026-10-12: Grant is two protected rows, each with its own label.
    [Theory]
    [InlineData("ApplyCommand:answer-grant", "Grant rounds")]
    [InlineData("ApplyCommand:answer-grant-tokens", "Grant tokens")]
    [InlineData("SomeFutureCommand:variant", "Some future command variant")]
    public void Labels_are_keyed_by_action_id_and_an_unknown_one_shows_it_humanised(string action, string label) =>
        Assert.Equal(label, Control(Row(action, "cli-only:typed-confirmation", "sindri workflow answer <address> --grant <n>")).Label);

    // F4 (corr-2): a refusal is announced with its code and detail, so a screen reader hears what the eyes read.
    [Fact]
    public async Task A_refusal_is_announced_with_its_code_and_detail()
    {
        _server.Rebuild = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-API-010", "Views are rebuilding (fix: retry after the rebuild)");
        var control = Control(Row("RebuildViews", "ready"));

        await control.Press(Token);

        Assert.Equal("SND-API-010: Views are rebuilding (fix: retry after the rebuild)", control.Announcement);
    }

    // G4 (a11y-1): the live region is live only while the control announces something (flag, then text, as the shell's
    // regions do), so the resting label is never spoken: not at rest, not as the reset before a run, not after.
    [Fact]
    public async Task The_live_region_is_gated_so_the_resting_label_is_never_spoken()
    {
        var control = Control(Row("RebuildViews", "ready"));
        var spoken = new List<string>();
        control.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ControlViewModel.Announcement) && control.IsAnnouncing)
            {
                spoken.Add(control.Announcement);
            }
        };
        Assert.False(control.IsAnnouncing);

        await control.Press(Token);
        Assert.True(control.IsAnnouncing);
        _clock.Advance(ControlViewModel.AcceptedFor);

        Assert.Equal(["Rebuild views: done"], spoken);
        Assert.False(control.IsAnnouncing);
    }

    [Fact]
    public async Task A_copy_is_spoken_through_the_gate_and_the_gate_closes_with_the_label()
    {
        var control = Control(Row("RunDoctor", "cli-only:no-protection", "sindri doctor"));

        await control.Copy(Token);
        Assert.Equal((true, "Copied"), (control.IsAnnouncing, control.Announcement));
        _clock.Advance(ControlViewModel.CopiedFor);

        Assert.False(control.IsAnnouncing);
    }

    // F4 (corr-2): the same outcome twice is two announcements; the live region changes between them.
    [Fact]
    public async Task The_same_outcome_twice_changes_the_announcement_each_time()
    {
        var control = Control(Row("RebuildViews", "ready"));
        var heard = new List<string>();
        control.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ControlViewModel.Announcement))
            {
                heard.Add(control.Announcement);
            }
        };

        await control.Press(Token);
        _clock.Advance(ControlViewModel.AcceptedFor);
        await control.Press(Token);

        Assert.Equal(2, heard.Count(text => text == "Rebuild views: done"));
        Assert.All(heard, Assert.NotEmpty);
    }

    // F4 (a11y-5): while it sends the control stays enabled for the keyboard (focus stays on it); the view model refuses a second run.
    [Fact]
    public async Task While_sending_the_control_stays_focusable_and_a_second_run_is_refused()
    {
        _server.RebuildGate = new TaskCompletionSource();
        var control = Control(Row("RebuildViews", "ready"));

        var first = control.Press(Token);

        Assert.Equal((true, false), (control.IsSendable, control.CanRun));
        _server.RebuildGate.SetResult();
        await first;
    }

    // F11 (a11y-8): a name read out of context says whose it is.
    [Fact]
    public async Task Copy_button_names_carry_the_controls_label()
    {
        var cliOnly = new ControlViewModel(Row("RunDoctor", "ready", "sindri doctor"), send: null, _clipboard, _clock);

        Assert.Equal("Copy: Run doctor", cliOnly.CopyName);
        var heard = new List<string?>();
        cliOnly.PropertyChanged += (_, e) => heard.Add(e.PropertyName);

        await cliOnly.Copy(Token);

        Assert.Equal("Copied", cliOnly.CopyName);
        Assert.Contains(nameof(ControlViewModel.CopyName), heard, StringComparer.Ordinal);
        _clock.Advance(ControlViewModel.CopiedFor);
        Assert.Equal("Copy: Run doctor", cliOnly.CopyName);
    }

    // §8 (E-8): a destructive row confirms in-app first; Confirm sends once, Keep sends nothing.
    [Fact]
    public async Task A_destructive_control_asks_before_it_sends()
    {
        var control = Control(Row("RebuildViews", "ready", destructive: true));

        await control.Press(Token);
        Assert.Equal((true, 0), (control.IsConfirming, _server.RebuildCalls));

        control.Keep();
        Assert.Equal((false, 0), (control.IsConfirming, _server.RebuildCalls));

        await control.Press(Token);
        await control.Confirm(Token);
        Assert.Equal((false, 1, CommandPhase.Accepted), (control.IsConfirming, _server.RebuildCalls, control.Phase));
    }

    // L11: Confirm and Keep are named for what they answer, and the question is spoken once when the row appears.
    [Fact]
    public async Task The_confirm_row_names_its_buttons_and_speaks_its_question_once()
    {
        var control = new ControlViewModel(Row("CloseItem:cancelled", "ready", destructive: true), ct => _server.RebuildViews(_api, ct), _clipboard, _clock, "ITM-1");
        var spoken = new List<string>();
        control.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ControlViewModel.Announcement) && control.IsAnnouncing)
            {
                spoken.Add(control.Announcement);
            }
        };
        Assert.Equal(("Confirm: Close as cancelled ITM-1?", "Keep ITM-1"), (control.ConfirmName, control.KeepName));

        await control.Press(Token);
        Assert.Equal(["Close as cancelled ITM-1?"], spoken);

        control.Keep();
        Assert.False(control.IsAnnouncing);
    }

    [Fact]
    public async Task A_control_that_isnt_destructive_sends_on_the_first_press()
    {
        var control = Control(Row("RebuildViews", "ready"));

        await control.Press(Token);

        Assert.Equal((false, 1), (control.IsConfirming, _server.RebuildCalls));
    }

    // §11.2: a control on a subject is named with it ("Hand back ITM-01JA1").
    [Fact]
    public void A_control_on_a_subject_is_named_with_it()
    {
        var control = new ControlViewModel(Row("RebuildViews", "ready"), send: null, _clipboard, _clock, subject: "ITM-01JA1");

        Assert.Equal("Rebuild views ITM-01JA1", control.Name);
        Assert.Equal("Rebuild views ITM-01JA1?", control.ConfirmQuestion);
    }
}
