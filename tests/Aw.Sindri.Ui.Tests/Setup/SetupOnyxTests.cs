using System.Net;
using System.Reflection;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Setup;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Setup;
using static Aw.Sindri.Ui.Tests.Setup.SetupFixture;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>
/// The Onyx step: Set up and Not now, the login form with its visible labels, show and hide, errors under the field, focus where the person is working, the password gone from the
/// view model the moment it is sent, one press sends once, and the terminal alternative as one line under the list.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class SetupOnyxTests
{
    private const string Sentinel = "hunter2-sentinel-9f3a";
    private readonly SetupFixture _f = new("extra-setup-ready");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static System.Text.Json.Nodes.JsonNode Waiting() => With("first-run", "onyx", "needs-you", "ConnectOnyx");

    private async Task<(SetupViewModel Setup, SetupOnyxViewModel Onyx, SetupStepViewModel Step)> Open(System.Text.Json.Nodes.JsonNode? list = null, SetupFixture? f = null)
    {
        f ??= _f;
        f.Serve(list ?? Waiting());
        var setup = await f.Read(Token);
        var step = setup.Steps.Single(s => s.Id == "onyx");
        return (setup, step.Onyx!, step);
    }

    [Fact]
    public async Task A_waiting_login_opens_the_form_with_the_visible_labels_and_no_other_button()
    {
        var (_, onyx, step) = await Open();

        Assert.Equal((true, false, false, false, false), (onyx.ShowForm, onyx.ShowConnect, onyx.ShowStart, onyx.ShowDefer, step.HasAction));
        Assert.Equal(("Onyx email", "Onyx password", "Show password"), (onyx.EmailLabel, onyx.PasswordLabel, onyx.RevealText));
        Assert.Equal(("Create your Onyx account in the browser first if you haven't, then sign in here.", "Used once to connect Linear and Slack. Never stored, never shown in any log."), (onyx.IntroText, onyx.NoteText));
    }

    [Fact]
    public async Task A_step_that_starts_waiting_puts_focus_in_the_first_field()
    {
        var (setup, onyx, _) = await Open(Golden("first-run"));
        var asked = new List<SetupFocus>();
        onyx.FocusRequested += (_, e) => asked.Add(e.Target);

        _f.Serve(Waiting());
        await setup.Refresh(Url, Token);

        Assert.True(onyx.ShowForm);
        Assert.Equal([SetupFocus.OnyxEmail], asked);
    }

    [Fact]
    public async Task A_todo_step_offers_Set_up_and_Not_now_and_Set_up_starts_it_once()
    {
        var (_, onyx, step) = await Open(Golden("first-run"));
        _f.Server.WriteGate = null;

        await onyx.Start(Token);
        await onyx.Start(Token);   // the button is gone while it works

        Assert.Equal(1, _f.Server.SetupOnyxCalls);
        Assert.Equal(("Working", Labels.SetupOnyxStarting, false, false), (step.ChipWord, step.Note, onyx.ShowStart, onyx.ShowDefer));
    }

    [Fact]
    public async Task Not_now_sets_onyx_aside_once()
    {
        var (_, onyx, _) = await Open(Golden("first-run"));

        await onyx.Defer(Token);

        Assert.Equal([new OnyxDeferRequest(Deferred: true)], _f.Server.OnyxDeferrals);
    }

    [Fact]
    public async Task Without_a_ready_catalog_row_the_step_is_the_command_to_copy_and_nothing_else()
    {
        var (_, onyx, step) = await Open(Golden("first-run"), new SetupFixture("extra-onyx-pending"));   // the served catalog with SetupOnyx, ConnectOnyx and DeferOnyx still pending (all three are ready since api SO-A5)

        Assert.Equal((false, false, false, "sindri onyx up"), (onyx.ShowStart, onyx.ShowDefer, onyx.ShowForm, step.Action?.Cli?.Cli));
    }

    [Fact]
    public async Task A_waiting_login_without_a_ready_row_is_the_terminal_command_not_a_form()
    {
        var (_, onyx, step) = await Open(Waiting(), new SetupFixture("extra-onyx-pending"));

        Assert.Equal((false, "sindri onyx connect"), (onyx.ShowForm, step.Action?.Cli?.Cli));
    }

    [Fact]
    public async Task Docker_missing_shows_the_reason_and_no_control()
    {
        var (_, onyx, step) = await Open(Golden("onyx-no-docker"));

        Assert.Equal((false, false, false, false, "Skipped"), (onyx.ShowStart, onyx.ShowDefer, onyx.ShowForm, step.HasAction, step.ChipWord));
    }

    [Fact]
    public async Task Sending_with_nothing_typed_asks_for_both_under_their_fields_and_sends_nothing()
    {
        var (_, onyx, _) = await Open();
        var asked = new List<SetupFocus>();
        onyx.FocusRequested += (_, e) => asked.Add(e.Target);

        await onyx.Connect(Token);

        Assert.Equal((0, Labels.SetupOnyxEmailNeeded, Labels.SetupOnyxPasswordNeeded), (_f.Server.OnyxLogins.Count, onyx.EmailError, onyx.PasswordError));
        Assert.Equal([SetupFocus.OnyxEmail], asked);
    }

    [Fact]
    public async Task Sending_with_only_the_password_missing_puts_focus_in_the_password()
    {
        var (_, onyx, _) = await Open();
        var asked = new List<SetupFocus>();
        onyx.FocusRequested += (_, e) => asked.Add(e.Target);
        onyx.Email = "me@example.com";

        await onyx.Connect(Token);

        Assert.Equal((string.Empty, Labels.SetupOnyxPasswordNeeded), (onyx.EmailError, onyx.PasswordError));
        Assert.Equal([SetupFocus.OnyxPassword], asked);
    }

    [Fact]
    public async Task The_password_is_gone_from_the_view_model_the_moment_it_is_sent_and_one_press_sends_once()
    {
        var (_, onyx, step) = await Open();
        onyx.Email = " me@example.com ";
        onyx.Password = Sentinel;
        onyx.ToggleReveal();
        _f.Server.WriteGate = new TaskCompletionSource();

        var first = onyx.Connect(Token);
        var whileSending = (onyx.Password, onyx.IsRevealed, onyx.IsSending);
        var second = onyx.Connect(Token);   // a second press while the first is on its way
        _f.Server.WriteGate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal((string.Empty, false, true), whileSending);
        var sent = Assert.Single(_f.Server.OnyxLogins);
        Assert.Equal(("me@example.com", Sentinel), (sent.AdminEmail, sent.AdminPassword));
        Assert.Equal(("Working", Labels.SetupOnyxConnecting, false), (step.ChipWord, step.Note, onyx.IsSending));
        AssertNoSentinelAnywhere(onyx, step);
    }

    [Theory]
    [InlineData("wrong-password", "", "Onyx didn't accept that email and password. Check them and try again.", "")]
    [InlineData("not-waiting", "", "", "Sindri isn't waiting for a login. Set up Onyx first.")]
    [InlineData("missing-field", "", "", "Enter your Onyx email and password.")]
    public async Task A_refusal_is_said_in_words_where_it_belongs_the_password_is_gone_and_the_email_stays(string scenario, string emailError, string passwordError, string formError)
    {
        var (_, onyx, step) = await Open();
        _f.Server.ConnectOnyxAnswer = RouteAnswer.Problem("onyx-account", scenario);
        var asked = new List<SetupFocus>();
        onyx.FocusRequested += (_, e) => asked.Add(e.Target);
        (onyx.Email, onyx.Password) = ("me@example.com", Sentinel);

        await onyx.Connect(Token);

        Assert.Equal((emailError, passwordError, formError, "me@example.com", string.Empty, true), (onyx.EmailError, onyx.PasswordError, onyx.FormError, onyx.Email, onyx.Password, onyx.ShowForm));
        Assert.Equal("Needs you", step.ChipWord);
        Assert.Equal([passwordError.Length > 0 ? SetupFocus.OnyxPassword : SetupFocus.OnyxEmail], asked);
        AssertNoSentinelAnywhere(onyx, step);
        Assert.DoesNotContain("SND-", onyx.EmailError + onyx.PasswordError + onyx.FormError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_session_refusal_and_an_unreachable_sindri_are_plain_sentences_under_the_form()
    {
        var (_, onyx, _) = await Open();
        _f.Server.ConnectOnyxAnswer = RouteAnswer.Problem(HttpStatusCode.Forbidden, "SND-AUTH-002", "An agent session can't do this.");
        (onyx.Email, onyx.Password) = ("me@example.com", Sentinel);
        await onyx.Connect(Token);
        var refused = onyx.FormError;

        _f.Server.GoDown();
        (onyx.Email, onyx.Password) = ("me@example.com", Sentinel);
        await onyx.Connect(Token);

        Assert.Equal(("Only you can do this from the app. An agent session can't.", Labels.SetupOnyxFailed), (refused, onyx.FormError));
    }

    [Fact]
    public async Task Typing_clears_the_error_under_that_field()
    {
        var (_, onyx, _) = await Open();
        await onyx.Connect(Token);

        onyx.Email = "m";
        var afterEmail = (onyx.EmailError, onyx.PasswordError.Length > 0);
        onyx.Password = "p";

        Assert.Equal((string.Empty, true), afterEmail);
        Assert.Equal(string.Empty, onyx.PasswordError);
    }

    [Fact]
    public async Task Show_and_hide_toggle_the_mask_and_their_words()
    {
        var (_, onyx, _) = await Open();

        var masked = (onyx.PasswordChar, onyx.RevealText, onyx.IsRevealed);
        onyx.ToggleReveal();
        var shown = (onyx.PasswordChar, onyx.RevealText, onyx.IsRevealed);
        onyx.ToggleReveal();

        Assert.Equal(('•', "Show password", false), masked);
        Assert.Equal(('\0', "Hide password", true), shown);
        Assert.Equal(('•', false), (onyx.PasswordChar, onyx.IsRevealed));
    }

    [Fact]
    public async Task Esc_forgets_the_password_closes_the_form_and_returns_focus_to_the_Connect_button()
    {
        var (_, onyx, _) = await Open();
        var asked = new List<SetupFocus>();
        onyx.FocusRequested += (_, e) => asked.Add(e.Target);
        onyx.Password = Sentinel;
        onyx.ToggleReveal();

        onyx.Close();
        var closed = (onyx.Password, onyx.IsRevealed, onyx.ShowForm, onyx.ShowConnect);
        onyx.Open();

        Assert.Equal((string.Empty, false, false, true), closed);
        Assert.Equal((true, false), (onyx.ShowForm, onyx.ShowConnect));
        Assert.Equal([SetupFocus.OnyxConnect, SetupFocus.OnyxEmail], asked);
    }

    [Fact]
    public async Task The_terminal_alternative_is_one_line_under_the_list_while_onyx_is_open_and_copies()
    {
        var setup = await ReadOf("first-run");

        var shown = (setup.IsFootnoteShown, setup.Footnote.Cli);
        await setup.Footnote.Copy(Token);

        Assert.Equal((true, "sindri-app onyx up"), shown);
        Assert.Equal("sindri-app onyx up", _f.Clipboard.Text);
    }

    [Theory]
    [InlineData("all-done")]
    [InlineData("onyx-skipped")]
    [InlineData("onyx-no-docker")]
    public async Task The_footnote_goes_when_onyx_is_done_or_set_aside(string scenario)
    {
        var setup = await ReadOf(scenario);

        Assert.False(setup.IsFootnoteShown);
    }

    private async Task<SetupViewModel> ReadOf(string scenario)
    {
        _f.Serve(scenario);
        return await _f.Read(Token);
    }

    // The password must not be in any string the view could bind: not on the onyx view model, not on the step's, not in a note or an error.
    private static void AssertNoSentinelAnywhere(params object[] models)
    {
        foreach (var model in models)
        {
            var strings = model.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0)
                .Select(p => (string?)p.GetValue(model) ?? string.Empty);
            Assert.DoesNotContain(strings, s => s.Contains(Sentinel, StringComparison.Ordinal));
        }
    }
}
