using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Features.OnyxAccount;

/// <summary>
/// Spec §4.1 steps 7 and 8: Sindri's own Onyx user and its token. Onyx Community Edition has no service-account keys (their route is Business tier), so Sindri gets a dedicated admin user (<see cref="OnyxAccounts.DedicatedEmail"/>)
/// and a personal access token of that user. The owner's own Onyx admin login (the account they signed up with, never Sindri's user) is asked for once at the terminal, held in memory for the calls below and dropped. With it, a user left
/// by an earlier build is deactivated, the dedicated user's password is reset to a new random one (a reset needs no invite, so it works on an invite-only Onyx) or, when Onyx has no such user, the user is registered with a random password,
/// and it is made an admin. Then Sindri logs in as that user with the password it just made, revokes any old sindri-onyx token, mints an unscoped token with no expiry, checks it and stores it, and only a pointer to it is returned: after a
/// successful store exactly one Sindri token exists. The user's password is random, lives in memory for this one call and is never stored or printed. Every step is safe to repeat: a rerun after any failure finds the user and resets
/// its password, so no second user and no second sign-up are needed. A recorded token is trusted again only when it still resolves from where it was stored and Onyx still takes it as that user's admin token.
/// </summary>
internal sealed class OnyxKeyMinter(IOnyxPrompts prompts, IOnyxProvisioning provisioning, ISecretWriter secrets, ISecretPointers pointers, IRandomHex random, IOnyxProgress progress)
{
    private const string KeyName = "sindri-onyx";
    private const string KeyService = "onyx";

    /// <summary>What the owner is told whenever a token named sindri-onyx may be left in Onyx: it is a live admin token that nobody can read back, and the next run revokes it.</summary>
    internal const string LeftoverNote = "A live admin token named sindri-onyx may exist on Onyx's user sindri@example.com that nobody can read back; the next `sindri onyx up` revokes it before making a new one, or deactivate that user in Onyx's Admin → Users page to stop it working now.";

    /// <summary>The prompt for the owner's login: it names whose account it wants, so Sindri's own address isn't typed in.</summary>
    internal const string LoginPrompt = "Your own Onyx admin login (the account you signed up with; Sindri creates its own user from it): ";

    internal async ValueTask<bool> Stands(int port, string? pointer, CancellationToken cancellationToken)
    {
        if (pointer is null || SecretPointer.Parse(pointer) is not { IsSuccess: true } parsed)
        {
            return false;
        }

        var key = pointers.Resolve(parsed.Value);
        if (key.IsError)
        {
            return false;
        }

        var admin = await provisioning.TokenIsAdmin(port, key.Value, OnyxAccounts.DedicatedEmail, cancellationToken).ConfigureAwait(false);
        return admin is { IsSuccess: true, Value: true };
    }

    /// <summary>Makes the dedicated user and its token, and stores the token.</summary>
    /// <param name="port">Onyx's loopback port.</param>
    /// <param name="cancellationToken">Cancels the asking and the requests.</param>
    /// <returns>The pointer to the stored token, or SND-ONYX-007, 008 or 009.</returns>
    internal async ValueTask<Result<string>> Mint(int port, CancellationToken cancellationToken)
    {
        var email = await prompts.Ask(LoginPrompt, cancellationToken).ConfigureAwait(false);
        var password = email is { Length: > 0 } ? await prompts.AskSecret("The password of that account (not shown, not stored): ", cancellationToken).ConfigureAwait(false) : null;
        if (email is not { Length: > 0 } || password is not { Length: > 0 })
        {
            return SindriErrors.OnyxKeyFailed.With("no Onyx login was given; run `sindri onyx up` again and enter the email and password of the account you created");
        }

        return await Mint(port, new OnyxLogin(email, new OnyxSecret(password)), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Makes the dedicated user and its token with a login that was already given (the app's form), and stores the token.</summary>
    /// <param name="port">Onyx's loopback port.</param>
    /// <param name="login">The owner's own Onyx admin login.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>The pointer to the stored token, or SND-ONYX-007, 008 or 009.</returns>
    internal async ValueTask<Result<string>> Mint(int port, OnyxLogin login, CancellationToken cancellationToken)
    {
        var password = await Prepare(port, login, cancellationToken).ConfigureAwait(false);
        if (password.IsError)
        {
            return password.Error;
        }

        var minted = await Token(port, password.Value, cancellationToken).ConfigureAwait(false);
        if (minted.IsError)
        {
            return minted.Error;
        }

        var stored = secrets.Store(KeyService, KeyName, minted.Value);
        return stored.IsError ? Leftover(stored.Error) : stored.Value.ToString();
    }

    // The owner's session: the dedicated user exists, has a password only this call knows, and is an admin.
    private async ValueTask<Result<string>> Prepare(int port, OnyxLogin login, CancellationToken cancellationToken)
    {
        var email = login.Email;
        if (IsSindriAddress(email))
        {
            return SindriErrors.OnyxKeyFailed.With($"{email.Trim()} is the address of Sindri's own Onyx user, which Sindri creates itself; nothing was sent to Onyx. Enter your own Onyx admin login, the account you signed up with at Onyx's sign-up page, and run `sindri onyx up` again");
        }

        var signedIn = await provisioning.SignIn(port, email, login.Password.Reveal(), cancellationToken).ConfigureAwait(false);
        if (signedIn.IsError)
        {
            return signedIn.Error;
        }

        var session = signedIn.Value;
        await using (session.ConfigureAwait(false))   // logs out and drops the cookie
        {
            return await SetUpUser(session, cancellationToken).ConfigureAwait(false);
        }
    }

    // The earlier build's address and this build's: neither is the owner's login, and typing one is the mistake this guards.
    private static bool IsSindriAddress(string email)
        => string.Equals(email.Trim(), OnyxAccounts.DedicatedEmail, StringComparison.OrdinalIgnoreCase)
        || string.Equals(email.Trim(), OnyxAccounts.LegacyEmail, StringComparison.OrdinalIgnoreCase);

    // Retire an earlier build's user, then reset the dedicated user's password; only when Onyx has no such user register it. Then make it an admin.
    private async ValueTask<Result<string>> SetUpUser(IOnyxSession session, CancellationToken cancellationToken)
    {
        var retired = await session.RetireLegacyUser(cancellationToken).ConfigureAwait(false);
        if (retired.IsError)
        {
            return retired.Error;
        }

        if (retired.Value)
        {
            progress.Line($"key: deactivated {OnyxAccounts.LegacyEmail}, the Sindri user of an earlier build (its tokens no longer work)");
        }

        var reset = await session.ResetDedicatedPassword(cancellationToken).ConfigureAwait(false);
        if (reset.IsError)
        {
            return reset.Error;
        }

        var password = reset.Value;
        if (password is null)
        {
            password = NewPassword();
            var created = await session.RegisterDedicatedUser(password, cancellationToken).ConfigureAwait(false);
            if (created.IsError)
            {
                return created.Error;
            }
        }

        var admin = await session.MakeDedicatedAdmin(cancellationToken).ConfigureAwait(false);
        return admin.IsError ? admin.Error : password;
    }

    // The dedicated user's own session: log in with the password just made, mint the token, check it. The token exists in Onyx from the moment it is made.
    private async ValueTask<Result<string>> Token(int port, string password, CancellationToken cancellationToken)
    {
        var signedIn = await provisioning.SignIn(port, OnyxAccounts.DedicatedEmail, password, cancellationToken).ConfigureAwait(false);
        if (signedIn.IsError)
        {
            return SindriErrors.OnyxKeyFailed.With($"Onyx didn't let Sindri's own user {OnyxAccounts.DedicatedEmail} log in right after setting it up (if it was deactivated in Admin → Users, activate it again): {signedIn.Error.Message}");
        }

        var session = signedIn.Value;
        await using (session.ConfigureAwait(false))
        {
            var revoked = await session.RevokeTokens(KeyName, cancellationToken).ConfigureAwait(false);
            if (revoked.IsError)
            {
                return revoked.Error;
            }

            if (revoked.Value > 0)
            {
                progress.Line($"key: revoked {revoked.Value.ToString(CultureInfo.InvariantCulture)} old sindri-onyx token(s) of {OnyxAccounts.DedicatedEmail}");
            }

            var made = await session.CreateToken(KeyName, cancellationToken).ConfigureAwait(false);
            if (made.IsError)
            {
                return Leftover(made.Error);   // a timeout can come after Onyx made the token
            }

            var verified = await Verified(port, made.Value, cancellationToken).ConfigureAwait(false);
            return verified.IsError ? Leftover(verified.Error) : verified;
        }
    }

    // Random enough to be unguessable, and shaped to pass whatever password rules the owner turned on: lower, upper, digit and special character, at most 64 long.
    private string NewPassword() => random.Hex(16) + "aA1!";

    private static Error Leftover(Error error) => error with { Message = $"{error.Message}. {LeftoverNote}" };

    private async ValueTask<Result<string>> Verified(int port, string token, CancellationToken cancellationToken)
    {
        var admin = await provisioning.TokenIsAdmin(port, token, OnyxAccounts.DedicatedEmail, cancellationToken).ConfigureAwait(false);
        if (admin.IsError)
        {
            return admin.Error;
        }

        return admin.Value
            ? token
            : SindriErrors.OnyxKeyFailed.With("Onyx made the token but doesn't treat it as the admin token of Sindri's user; check that user in Onyx's admin users page, then run `sindri onyx up` again");
    }
}
