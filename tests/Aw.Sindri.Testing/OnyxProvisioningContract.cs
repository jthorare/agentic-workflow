using Aw.Sindri.Core.Onyx;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="IOnyxProvisioning"/> honours: the owner's session finds no earlier build's user and no dedicated user yet, registers it, then resets its password and makes it an admin;
/// that user's own login makes a token that Onyx then takes as the user's admin token and a stranger's is not; revoking its sindri-onyx tokens leaves exactly the one made after; disposing a session logs out.
/// Run against the fake and HttpOnyxProvisioning over a scripted Onyx.
/// </summary>
public abstract class OnyxProvisioningContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A provisioning over an Onyx that accepts the owner's login.</summary>
    /// <returns>The subject and a count of its logouts.</returns>
    protected abstract (IOnyxProvisioning Provisioning, Func<int> Logouts) Create();

    /// <summary>The whole dedicated-user flow.</summary>
    [Fact]
    public async Task A_user_made_by_the_owner_gets_an_admin_token_of_its_own_and_a_stranger_does_not()
    {
        var (provisioning, _) = Create();
        string password;
        var owner = (await provisioning.SignIn(3189, "owner@example.net", "pw", Token)).Value;
        await using (owner)
        {
            Expect.True(!(await owner.RetireLegacyUser(Token)).Value);
            Expect.True((await owner.ResetDedicatedPassword(Token)).Value is null);   // no such user yet
            Expect.True((await owner.RegisterDedicatedUser("first-pw", Token)).IsSuccess);
            password = (await owner.ResetDedicatedPassword(Token)).Value!;   // why: the user exists now, so Onyx answers a password
            Expect.True(password.Length > 0);
            Expect.True((await owner.MakeDedicatedAdmin(Token)).IsSuccess);
        }

        string old;
        string token;
        var dedicated = (await provisioning.SignIn(3189, OnyxAccounts.DedicatedEmail, password, Token)).Value;
        await using (dedicated)
        {
            Expect.Equal(0, (await dedicated.RevokeTokens("sindri-onyx", Token)).Value);
            old = (await dedicated.CreateToken("sindri-onyx", Token)).Value;
            Expect.Equal(1, (await dedicated.RevokeTokens("sindri-onyx", Token)).Value);
            token = (await dedicated.CreateToken("sindri-onyx", Token)).Value;
        }

        Expect.True(token.Length > 0 && !string.Equals(old, token, StringComparison.Ordinal));
        Expect.True((await provisioning.TokenIsAdmin(3189, token, OnyxAccounts.DedicatedEmail, Token)).Value);
        Expect.True(!(await provisioning.TokenIsAdmin(3189, old, OnyxAccounts.DedicatedEmail, Token)).Value);
        Expect.True(!(await provisioning.TokenIsAdmin(3189, token, "someone@example.net", Token)).Value);
        Expect.True(!(await provisioning.TokenIsAdmin(3189, "stranger-token", OnyxAccounts.DedicatedEmail, Token)).Value);
    }

    /// <summary>Disposing a session logs it out, once.</summary>
    [Fact]
    public async Task Disposing_the_session_logs_out_once()
    {
        var (provisioning, logouts) = Create();
        var session = (await provisioning.SignIn(3189, "owner@example.net", "pw", Token)).Value;

        await session.DisposeAsync();

        Expect.Equal(1, logouts());
    }
}
