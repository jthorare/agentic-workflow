using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxAccount;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxAccount;

// Spec §4.1 steps 6-9: the owner's sign-up, Sindri's key from the owner's login, its storage and the profile section.
public sealed class OnyxAccountTests
{
    private const string LoginSecret = "owner-login-value-0042";
    private const string MadeKey = "onyx_pat_sindri-0123456789";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Rig
    {
        public Rig()
        {
            Prompts.Answer("owner@example.com", LoginSecret);
        }

        public FakeOnyxWorkspace Workspace { get; } = new();

        public FakeOnyxPrompts Prompts { get; } = new();

        public FakeBrowser Browser { get; } = new();

        public FakeOnyxProvisioning Provisioning { get; } = new();

        public FakeSecretWriter Secrets { get; } = new();

        public FakeOnyxProfileWriter Profile { get; } = new();

        public CapturingOnyxProgress Progress { get; } = new();

        public OnyxAccountService Service(FakeOnyxPrompts? prompts = null)
            => new(Workspace, prompts ?? Prompts, Browser, new OnyxKeyMinter(prompts ?? Prompts, Provisioning, Secrets, Secrets, new FakeRandomHex(), Progress), Profile, Progress);

        public async Task<Kernel.Result<OnyxAccountResponse>> Run() => await Service().Handle(new OnyxAccountRequest(3189, "v4.9.0"), Token);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_first_run_opens_sign_up_makes_the_dedicated_admin_user_and_stores_its_token_and_writes_the_profile()
    {
        var rig = new Rig();

        var response = (await rig.Run()).Value;

        Assert.Equal(["signup", "key", "profile"], response.Steps.Select(static s => s.Step), StringComparer.Ordinal);
        Assert.All(response.Steps, static s => Assert.Equal(OnyxStepOutcome.Ran, s.Outcome));
        Assert.Equal("keychain:onyx/sindri-onyx", response.KeyPointer);
        Assert.True(response.ProfileChanged);
        Assert.Equal(new Uri("http://127.0.0.1:3189/auth/signup"), Assert.Single(rig.Browser.Opened));
        Assert.Equal(("http://127.0.0.1:3189", "keychain:onyx/sindri-onyx", "v4.9.0"), rig.Profile.Section);
        Assert.Equal(MadeKey, rig.Secrets.Stored["keychain:onyx/sindri-onyx"]);
        Assert.Equal(2, rig.Provisioning.LoggedOut);   // the owner's session and the dedicated user's
        Assert.Equal("keychain:onyx/sindri-onyx", rig.Workspace.State!.KeyPointer);
        Assert.Contains(OnyxAccounts.DedicatedEmail, rig.Provisioning.Registered.Keys, StringComparer.Ordinal);
        Assert.Contains(OnyxAccounts.DedicatedEmail, rig.Provisioning.Admins, StringComparer.Ordinal);
        Assert.Equal(OnyxAccounts.DedicatedEmail, rig.Provisioning.Made[MadeKey]);
        Assert.Equal(0, rig.Provisioning.Resets);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Neither_the_login_secret_nor_the_key_reaches_progress_the_state_or_the_response()
    {
        var rig = new Rig();

        var response = (await rig.Run()).Value;

        var shown = string.Join('\n', rig.Progress.Lines) + string.Join(',', rig.Workspace.State!.Steps.Values) + rig.Workspace.State.KeyPointer + response.Url + response.KeyPointer + string.Join(',', response.Steps.Select(static s => s.Step + s.Outcome));
        Assert.DoesNotContain(LoginSecret, shown, StringComparison.Ordinal);
        Assert.DoesNotContain(MadeKey, shown, StringComparison.Ordinal);
        Assert.DoesNotContain(rig.Provisioning.Registered[OnyxAccounts.DedicatedEmail], shown, StringComparison.Ordinal);   // the dedicated user's password is made, used and dropped
        Assert.Equal(1, rig.Prompts.SecretsAsked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rerun_skips_every_step_and_asks_nothing()
    {
        var rig = new Rig();
        _ = await rig.Run();
        var asked = rig.Prompts.Asked.Count;

        var again = (await rig.Run()).Value;

        Assert.Equal([OnyxStepOutcome.Skipped, OnyxStepOutcome.Skipped, OnyxStepOutcome.Skipped], again.Steps.Select(static s => s.Outcome));
        Assert.False(again.ProfileChanged);
        Assert.Equal(asked, rig.Prompts.Asked.Count);
        Assert.Equal(2, rig.Provisioning.SignIns);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_recorded_key_that_no_longer_resolves_is_made_again()
    {
        var rig = new Rig();
        _ = await rig.Run();
        rig.Secrets.Stored.Clear();
        rig.Provisioning.RevokeAll();
        rig.Prompts.Answer("owner@example.com", LoginSecret);

        var again = (await rig.Run()).Value;

        Assert.Equal(OnyxStepOutcome.Skipped, again.Steps[0].Outcome);
        Assert.Equal(OnyxStepOutcome.Ran, again.Steps[1].Outcome);
        Assert.Equal(4, rig.Provisioning.SignIns);
        Assert.Equal(1, rig.Provisioning.Resets);   // the dedicated user already existed: its password was reset, not a second user made
        _ = Assert.Single(rig.Provisioning.Registered);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_terminal_nothing_is_asked_and_the_stage_needs_the_human()
    {
        var rig = new Rig();
        rig.Prompts.Interactive = false;

        var response = (await rig.Run()).Value;

        Assert.True(response.NeedsHuman);
        Assert.Empty(rig.Prompts.Asked);
        Assert.Empty(rig.Browser.Opened);
        Assert.Null(rig.Profile.Section);
        Assert.Equal(0, rig.Provisioning.SignIns);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_login_that_is_not_an_admin_is_sindri_onyx_007_and_stores_nothing()
    {
        var rig = new Rig();
        rig.Provisioning.AdminError = SindriErrors.OnyxNotAdmin.With("not an admin");

        var result = await rig.Run();

        Assert.Equal(SindriErrors.OnyxNotAdmin.Code, result.Error.Code);
        Assert.Empty(rig.Secrets.Stored);
        Assert.Null(rig.Profile.Section);
        Assert.Equal(1, rig.Provisioning.LoggedOut);   // the owner's session still logged out; no dedicated login was made
        Assert.Empty(rig.Provisioning.Made);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_key_onyx_does_not_treat_as_an_admin_is_sindri_onyx_008()
    {
        var rig = new Rig();
        rig.Provisioning.TokenIsAdminAnswer = false;

        var result = await rig.Run();

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, result.Error.Code);
        Assert.Empty(rig.Secrets.Stored);
        Assert.Contains("sindri-onyx", result.Error.Message, StringComparison.Ordinal);   // the token made in Onyx is left there: the owner is told
        Assert.Contains("nobody can read back", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_login_is_sindri_onyx_008()
    {
        var rig = new Rig();
        rig.Provisioning.SignInError = SindriErrors.OnyxKeyFailed.With("Onyx refused that login");

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, (await rig.Run()).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_login_given_is_sindri_onyx_008_and_nothing_is_sent()
    {
        var rig = new Rig();
        var empty = new FakeOnyxPrompts();
        empty.Answer(string.Empty, null);

        var result = await rig.Service(empty).Handle(new OnyxAccountRequest(3189, "v4.9.0"), Token);

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, result.Error.Code);
        Assert.Equal(0, rig.Provisioning.SignIns);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_key_that_cannot_be_stored_is_sindri_onyx_009_and_the_profile_is_not_written()
    {
        var rig = new Rig();
        rig.Secrets.Fails = true;

        var result = await rig.Run();

        Assert.Equal(SindriErrors.OnyxSecretNotStored.Code, result.Error.Code);
        Assert.Null(rig.Profile.Section);
        Assert.Contains("nobody can read back", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_recorded_key_that_stopped_working_tells_the_owner_the_old_one_may_still_be_in_onyx()
    {
        var rig = new Rig();
        _ = await rig.Run();
        rig.Secrets.Stored.Clear();
        rig.Prompts.Answer("owner@example.com", LoginSecret);

        _ = await rig.Run();

        Assert.Contains(rig.Progress.Lines, static l => l.StartsWith("key: the recorded key no longer works", StringComparison.Ordinal) && l.Contains("nobody can read back", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_account_stage_is_refused_while_the_first_holds_the_workspace_lock_and_the_lock_is_released_after()
    {
        var rig = new Rig();
        using var held = rig.Workspace.TryLock().Value;

        var refused = await rig.Run();

        Assert.Equal(SindriErrors.CommandBusy.Code, refused.Error.Code);
        Assert.Equal(0, rig.Provisioning.SignIns);
        held!.Dispose();
        Assert.True((await rig.Run()).IsSuccess);
        Assert.False(rig.Workspace.LockHeld);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_workspace_lock_is_held_while_the_owner_is_at_a_prompt()
    {
        var rig = new Rig();
        var heldAtPrompt = false;
        rig.Prompts.OnPause = () => heldAtPrompt = rig.Workspace.LockHeld;

        _ = await rig.Run();

        Assert.True(heldAtPrompt);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_sign_up_page_is_printed_when_no_browser_opens()
    {
        var rig = new Rig();
        rig.Browser.Works = false;

        _ = await rig.Run();

        Assert.Contains(rig.Progress.Lines, static l => l.Contains("http://127.0.0.1:3189/auth/signup", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_that_cannot_be_written_returns_its_error_and_a_rerun_does_not_make_a_second_key()
    {
        var rig = new Rig();
        rig.Profile.Fails = SindriErrors.InvalidInput.With("profile.yaml is not writable");

        var failed = await rig.Run();

        Assert.Equal(SindriErrors.InvalidInput.Code, failed.Error.Code);
        Assert.Equal("keychain:onyx/sindri-onyx", rig.Workspace.State!.KeyPointer);   // the key is kept
        rig.Profile.Fails = null;

        var again = (await rig.Run()).Value;

        Assert.Equal(OnyxStepOutcome.Skipped, again.Steps[0].Outcome);
        Assert.Equal(OnyxStepOutcome.Skipped, again.Steps[1].Outcome);
        Assert.Equal(OnyxStepOutcome.Ran, again.Steps[2].Outcome);
        Assert.Equal(2, rig.Provisioning.SignIns);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_mint_after_sign_up_keeps_the_sign_up_so_a_rerun_does_not_ask_for_it_again()
    {
        var rig = new Rig();
        rig.Provisioning.SignInError = SindriErrors.OnyxKeyFailed.With("Onyx refused that login");
        _ = await rig.Run();
        rig.Provisioning.SignInError = null;
        rig.Prompts.Answer("owner@example.com", LoginSecret);
        rig.Browser.Opened.Clear();

        var again = (await rig.Run()).Value;

        Assert.Equal(OnyxStepOutcome.Skipped, again.Steps[0].Outcome);
        Assert.Equal(OnyxStepOutcome.Ran, again.Steps[1].Outcome);
        Assert.Empty(rig.Browser.Opened);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_terminal_after_sign_up_the_key_step_needs_the_human_and_writes_no_profile()
    {
        var rig = new Rig();
        _ = await rig.Run();
        rig.Secrets.Stored.Clear();
        rig.Prompts.Interactive = false;

        var response = (await rig.Run()).Value;

        Assert.True(response.NeedsHuman);
        Assert.Null(response.KeyPointer);
        Assert.Equal(OnyxStepOutcome.NeedsHuman, response.Steps[1].Outcome);
        Assert.Equal(2, response.Steps.Count);   // no profile step was reached
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_recorded_key_Onyx_now_rejects_is_made_again()
    {
        var rig = new Rig();
        _ = await rig.Run();
        rig.Provisioning.TokenIsAdminAnswer = false;
        rig.Prompts.Answer("owner@example.com", LoginSecret);

        var result = await rig.Run();

        Assert.Equal(4, rig.Provisioning.SignIns);   // the stored key was checked, found wanting, and a new one made
        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, result.Error.Code);   // the fake still rejects the new one
    }
}
