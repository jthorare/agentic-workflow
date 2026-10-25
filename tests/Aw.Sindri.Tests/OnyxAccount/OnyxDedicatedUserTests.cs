using Aw.Context.Core.Onyx;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxAccount;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxAccount;

// Onyx Community Edition has no service-account keys (the route is Business tier), so Sindri gets a dedicated admin user and a personal access token of its own:
// register it, make it an admin, log in as it, mint the token. Every step is safe to repeat, so a rerun after any failure picks up where it stopped.
public sealed class OnyxDedicatedUserTests
{
    private const string OwnerLogin = "owner-login-value-0042";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Rig
    {
        public Rig()
        {
            Answer();
        }

        public FakeOnyxWorkspace Workspace { get; } = new();

        public FakeOnyxPrompts Prompts { get; } = new();

        public FakeOnyxProvisioning Provisioning { get; } = new();

        public FakeSecretWriter Secrets { get; } = new();

        public FakeOnyxProfileWriter Profile { get; } = new();

        public CapturingOnyxProgress Progress { get; } = new();

        public void Answer() => Prompts.Answer("owner@example.com", OwnerLogin);

        public async Task<Kernel.Result<OnyxAccountResponse>> Run()
        {
            var minter = new OnyxKeyMinter(Prompts, Provisioning, Secrets, Secrets, new FakeRandomHex(), Progress);
            return await new OnyxAccountService(Workspace, Prompts, new FakeBrowser(), minter, Profile, Progress).Handle(new OnyxAccountRequest(3189, "v4.9.0"), Token);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_dedicated_user_gets_a_random_password_that_satisfies_a_strict_policy_and_the_dedicated_email()
    {
        var rig = new Rig();

        _ = await rig.Run();

        var password = rig.Provisioning.Registered[OnyxAccounts.DedicatedEmail];
        Assert.True(password.Length is >= 20 and <= 64);
        Assert.Contains(password, static c => char.IsUpper(c));
        Assert.Contains(password, static c => char.IsLower(c));
        Assert.Contains(password, static c => char.IsDigit(c));
        Assert.Contains(password, static c => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(c, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_existing_dedicated_user_is_reused_with_a_reset_password_and_no_second_user()
    {
        var rig = new Rig();
        _ = rig.Provisioning.Registered.TryAdd(OnyxAccounts.DedicatedEmail, "forgotten-password-1A!");

        var response = (await rig.Run()).Value;

        Assert.Equal(OnyxStepOutcome.Ran, response.Steps[1].Outcome);
        Assert.Equal(1, rig.Provisioning.Resets);
        _ = Assert.Single(rig.Provisioning.Registered);
        Assert.Contains(OnyxAccounts.DedicatedEmail, rig.Provisioning.Admins, StringComparer.Ordinal);
        Assert.NotNull(response.KeyPointer);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_registration_is_sindri_onyx_008_makes_no_token_and_logs_out()
    {
        var rig = new Rig();
        rig.Provisioning.RegisterError = SindriErrors.OnyxKeyFailed.With("this Onyx is invite-only");

        var result = await rig.Run();

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, result.Error.Code);
        Assert.Empty(rig.Provisioning.Made);
        Assert.Empty(rig.Secrets.Stored);
        Assert.Equal(1, rig.Provisioning.LoggedOut);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_password_reset_that_fails_is_returned_and_makes_no_token()
    {
        var rig = new Rig();
        _ = rig.Provisioning.Registered.TryAdd(OnyxAccounts.DedicatedEmail, "x");
        rig.Provisioning.ResetError = SindriErrors.OnyxNotAdmin.With("not an admin");

        var result = await rig.Run();

        Assert.Equal(SindriErrors.OnyxNotAdmin.Code, result.Error.Code);
        Assert.Empty(rig.Provisioning.Made);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dedicated_login_that_is_refused_is_sindri_onyx_008_and_names_the_dedicated_user()
    {
        var rig = new Rig();
        rig.Provisioning.RegisterError = null;
        var failing = new FailDedicatedLogin(rig.Provisioning);

        var minter = new OnyxKeyMinter(rig.Prompts, failing, rig.Secrets, rig.Secrets, new FakeRandomHex(), rig.Progress);
        var result = await new OnyxAccountService(rig.Workspace, rig.Prompts, new FakeBrowser(), minter, rig.Profile, rig.Progress).Handle(new OnyxAccountRequest(3189, "v4.9.0"), Token);

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, result.Error.Code);
        Assert.Contains(OnyxAccounts.DedicatedEmail, result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(rig.Secrets.Stored);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_token_that_cannot_be_made_is_returned_and_a_rerun_resumes_at_the_key_step_without_the_sign_up_again()
    {
        var rig = new Rig();
        var pauses = 0;
        rig.Prompts.OnPause = () => pauses++;
        rig.Provisioning.TokenError = SindriErrors.OnyxKeyFailed.With("Onyx didn't make the token");
        var failed = await rig.Run();
        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, failed.Error.Code);
        Assert.True(rig.Workspace.State!.Steps.ContainsKey("signup"));
        Assert.False(rig.Workspace.State.Steps.ContainsKey("key"));
        rig.Provisioning.TokenError = null;
        rig.Answer();

        var again = (await rig.Run()).Value;

        Assert.Equal(OnyxStepOutcome.Skipped, again.Steps[0].Outcome);
        Assert.Equal(OnyxStepOutcome.Ran, again.Steps[1].Outcome);
        Assert.Equal(OnyxStepOutcome.Ran, again.Steps[2].Outcome);
        Assert.Equal(1, pauses);   // the sign-up page wait is not repeated
        _ = Assert.Single(rig.Provisioning.Registered);   // the user made by the first try is reused
        Assert.Equal(1, rig.Provisioning.Resets);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rerun_on_an_invite_only_onyx_resets_the_existing_user_and_never_needs_the_invite_again()
    {
        var rig = new Rig();
        _ = rig.Provisioning.Registered.TryAdd(OnyxAccounts.DedicatedEmail, "created-earlier-1aA!");
        rig.Provisioning.RegisterError = SindriErrors.OnyxKeyFailed.With("this Onyx is invite-only");

        var response = (await rig.Run()).Value;

        Assert.NotNull(response.KeyPointer);
        Assert.Equal(1, rig.Provisioning.Resets);   // the reset came first and needed no invite; register was never tried
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_first_run_on_an_invite_only_onyx_says_to_invite_the_address_and_makes_nothing()
    {
        var rig = new Rig();
        rig.Provisioning.RegisterError = SindriErrors.OnyxKeyFailed.With("invite sindri@example.com once");

        var result = await rig.Run();

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, result.Error.Code);
        Assert.Contains("invite", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(rig.Provisioning.Made);
        Assert.Empty(rig.Provisioning.Admins);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("sindri@example.com")]
    [InlineData("  Sindri@Example.COM ")]
    [InlineData("sindri@sindri.app")]
    public async Task The_dedicated_address_typed_as_the_owners_login_is_refused_before_anything_is_sent(string typed)
    {
        var rig = new Rig();
        var prompts = new FakeOnyxPrompts();
        prompts.Answer(typed, OwnerLogin);
        var minter = new OnyxKeyMinter(prompts, rig.Provisioning, rig.Secrets, rig.Secrets, new FakeRandomHex(), rig.Progress);

        var result = await minter.Mint(3189, Token);

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, result.Error.Code);
        Assert.Contains("Sindri creates itself", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("nothing was sent to Onyx", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(0, rig.Provisioning.SignIns);
        Assert.Empty(rig.Provisioning.Targets);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_login_prompt_asks_for_the_owners_own_admin_account_and_says_sindri_makes_its_own_user()
    {
        var rig = new Rig();

        _ = await rig.Run();

        var prompt = rig.Prompts.Asked.First(static q => q.Contains("login", StringComparison.Ordinal));
        Assert.Equal(OnyxKeyMinter.LoginPrompt, prompt);
        Assert.Contains("Your own Onyx admin login", prompt, StringComparison.Ordinal);
        Assert.Contains("the account you signed up with", prompt, StringComparison.Ordinal);
        Assert.Contains("Sindri creates its own user", prompt, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_user_left_by_the_earlier_build_is_deactivated_and_the_owner_is_told()
    {
        var rig = new Rig();
        _ = rig.Provisioning.Registered.TryAdd(OnyxAccounts.LegacyEmail, "old-1aA!");
        _ = rig.Provisioning.Admins.Add(OnyxAccounts.LegacyEmail);

        var response = (await rig.Run()).Value;

        Assert.Contains(OnyxAccounts.LegacyEmail, rig.Provisioning.Deactivated, StringComparer.Ordinal);
        Assert.Contains(rig.Progress.Lines, static l => l.Contains("deactivated sindri@sindri.app", StringComparison.Ordinal));
        Assert.NotNull(response.KeyPointer);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_earlier_user_means_no_notice()
    {
        var rig = new Rig();

        _ = await rig.Run();

        Assert.DoesNotContain(rig.Progress.Lines, static l => l.Contains("deactivated", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_remint_revokes_the_old_token_first_so_exactly_one_sindri_token_exists_after_the_store()
    {
        var rig = new Rig();
        _ = await rig.Run();
        var first = rig.Secrets.Stored["keychain:onyx/sindri-onyx"];
        rig.Secrets.Stored.Clear();   // the recorded token no longer resolves
        rig.Answer();

        _ = await rig.Run();

        Assert.Equal(1, rig.Provisioning.Revoked);
        var second = rig.Secrets.Stored["keychain:onyx/sindri-onyx"];
        Assert.NotEqual(first, second, StringComparer.Ordinal);
        Assert.Equal([second], rig.Provisioning.Made.Keys.ToList());
        Assert.Contains(rig.Progress.Lines, static l => l.Contains("revoked 1 old sindri-onyx token", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_revoke_that_fails_is_returned_and_makes_no_token()
    {
        var rig = new Rig();
        rig.Provisioning.RevokeError = SindriErrors.OnyxKeyFailed.With("Onyx didn't list the tokens");

        var result = await rig.Run();

        Assert.Equal(SindriErrors.OnyxKeyFailed.Code, result.Error.Code);
        Assert.Empty(rig.Provisioning.Made);
        Assert.Empty(rig.Secrets.Stored);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_token_request_that_fails_after_Onyx_may_have_made_it_still_carries_the_live_token_note()
    {
        var rig = new Rig();
        rig.Provisioning.TokenError = SindriErrors.OnyxKeyFailed.With("Onyx didn't answer POST /api/user/pats within 15 s");

        var result = await rig.Run();

        Assert.Contains("nobody can read back", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("revokes it", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("harmless", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_request_that_changes_a_user_is_aimed_at_the_dedicated_or_the_retired_email()
    {
        var rig = new Rig();

        _ = await rig.Run();

        Assert.All(rig.Provisioning.Targets, static t => Assert.True(string.Equals(t, OnyxAccounts.DedicatedEmail, StringComparison.Ordinal) || string.Equals(t, OnyxAccounts.LegacyEmail, StringComparison.Ordinal)));
        Assert.NotEmpty(rig.Provisioning.Targets);
    }

    [Fact, Trait("Lane", "unit")]
    public void Sindri_and_doctor_name_the_same_dedicated_address_and_it_is_a_reserved_example_domain()
    {
        Assert.Equal(OnyxSettings.DedicatedAccountEmail, OnyxAccounts.DedicatedEmail);
        Assert.EndsWith("@example.com", OnyxAccounts.DedicatedEmail, StringComparison.Ordinal);
    }

    // A provisioning whose login as the dedicated user is refused while the owner's works.
    private sealed class FailDedicatedLogin(FakeOnyxProvisioning inner) : IOnyxProvisioning
    {
        public ValueTask<Kernel.Result<IOnyxSession>> SignIn(int port, string email, string password, CancellationToken cancellationToken)
            => string.Equals(email, OnyxAccounts.DedicatedEmail, StringComparison.Ordinal)
                ? ValueTask.FromResult<Kernel.Result<IOnyxSession>>(SindriErrors.OnyxKeyFailed.With("Onyx refused that login (HTTP 400)"))
                : inner.SignIn(port, email, password, cancellationToken);

        public ValueTask<Kernel.Result<bool>> TokenIsAdmin(int port, string token, string email, CancellationToken cancellationToken)
            => inner.TokenIsAdmin(port, token, email, cancellationToken);
    }
}
