using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>
/// Onyx's setup-only requests, scripted as a small Onyx: it knows the users registered through it (with their current passwords), which of them are admins, and the tokens it made (with their names and owners).
/// A login as an email it doesn't know is the owner's and is accepted; a login as a registered user needs that user's current password. Remembers whether each session logged out.
/// </summary>
public sealed class FakeOnyxProvisioning : IOnyxProvisioning
{
    private readonly Dictionary<string, (string Email, string Name)> _tokens = new(StringComparer.Ordinal);
    private int _minted;

    /// <summary>Gets or sets the token the first mint makes; later mints add a counter.</summary>
    public string Token { get; set; } = "onyx_pat_sindri-0123456789";

    /// <summary>Gets or sets an error SignIn returns for any login (the owner's, in a test of a refused login).</summary>
    public Error? SignInError { get; set; }

    /// <summary>Gets or sets an error RegisterDedicatedUser returns (invite-only, a password Onyx won't take).</summary>
    public Error? RegisterError { get; set; }

    /// <summary>Gets or sets an error ResetDedicatedPassword returns.</summary>
    public Error? ResetError { get; set; }

    /// <summary>Gets or sets an error MakeDedicatedAdmin returns (SND-ONYX-007 for a login that isn't an admin).</summary>
    public Error? AdminError { get; set; }

    /// <summary>Gets or sets an error RevokeTokens returns.</summary>
    public Error? RevokeError { get; set; }

    /// <summary>Gets or sets an error CreateToken returns.</summary>
    public Error? TokenError { get; set; }

    /// <summary>Gets or sets an error RetireLegacyUser returns.</summary>
    public Error? RetireError { get; set; }

    /// <summary>Gets or sets a value indicating whether Onyx takes a made token as its user's admin token.</summary>
    public bool TokenIsAdminAnswer { get; set; } = true;

    /// <summary>Gets the registered users by email, with the password each has now.</summary>
    public IDictionary<string, string> Registered { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets the emails that were made admins.</summary>
    public ISet<string> Admins { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Gets the emails of users that were deactivated.</summary>
    public ISet<string> Deactivated { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Gets the tokens Onyx has made and not had revoked, by the email of the user each belongs to.</summary>
    public IDictionary<string, string> Made => _tokens.ToDictionary(static t => t.Key, static t => t.Value.Email, StringComparer.Ordinal);

    /// <summary>Makes Onyx forget every token it made (a stored token that no longer works).</summary>
    public void RevokeAll() => _tokens.Clear();

    /// <summary>Gets the origins the sign-ins and token checks were aimed at, in order.</summary>
    public IList<OnyxOrigin> Origins { get; } = [];

    /// <summary>Gets the number of sign-ins.</summary>
    public int SignIns { get; private set; }

    /// <summary>Gets the number of sessions that were logged out.</summary>
    public int LoggedOut { get; private set; }

    /// <summary>Gets the number of password resets.</summary>
    public int Resets { get; private set; }

    /// <summary>Gets the number of tokens revoked.</summary>
    public int Revoked { get; private set; }

    /// <summary>Gets the emails the requests that change a user were aimed at, in order.</summary>
    public IList<string> Targets { get; } = [];

    /// <inheritdoc />
    public ValueTask<Result<IOnyxSession>> SignIn(OnyxOrigin origin, string email, string password, CancellationToken cancellationToken)
    {
        SignIns++;
        Origins.Add(origin);
        if (SignInError is { } error)
        {
            return ValueTask.FromResult<Result<IOnyxSession>>(error);
        }

        if (Registered.TryGetValue(email, out var current) && !string.Equals(current, password, StringComparison.Ordinal))
        {
            return ValueTask.FromResult<Result<IOnyxSession>>(SindriErrors.OnyxKeyFailed.With("Onyx refused that login"));
        }

        return ValueTask.FromResult(Results.Success<IOnyxSession>(new Session(this, email)));
    }

    /// <inheritdoc />
    public ValueTask<Result<bool>> TokenIsAdmin(OnyxOrigin origin, string token, string email, CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<bool>>(_tokens.TryGetValue(token, out var owner) && string.Equals(owner.Email, email, StringComparison.Ordinal) && Admins.Contains(email) && !Deactivated.Contains(email) && TokenIsAdminAnswer);

    private sealed class Session(FakeOnyxProvisioning onyx, string email) : IOnyxSession
    {
        public ValueTask<Result<bool>> RetireLegacyUser(CancellationToken cancellationToken)
        {
            onyx.Targets.Add(OnyxAccounts.LegacyEmail);
            if (onyx.RetireError is { } error)
            {
                return ValueTask.FromResult<Result<bool>>(error);
            }

            return ValueTask.FromResult<Result<bool>>(onyx.Registered.ContainsKey(OnyxAccounts.LegacyEmail) && onyx.Deactivated.Add(OnyxAccounts.LegacyEmail));
        }

        public ValueTask<Result<string?>> ResetDedicatedPassword(CancellationToken cancellationToken)
        {
            onyx.Targets.Add(OnyxAccounts.DedicatedEmail);
            if (onyx.ResetError is { } error)
            {
                return ValueTask.FromResult<Result<string?>>(error);
            }

            if (!onyx.Registered.ContainsKey(OnyxAccounts.DedicatedEmail))
            {
                return ValueTask.FromResult(Results.Success<string?>(value: null));
            }

            var fresh = "reset-" + (++onyx.Resets).ToString(CultureInfo.InvariantCulture) + "-Pw1!";
            onyx.Registered[OnyxAccounts.DedicatedEmail] = fresh;
            return ValueTask.FromResult(Results.Success<string?>(fresh));
        }

        public ValueTask<Result<Unit>> RegisterDedicatedUser(string password, CancellationToken cancellationToken)
        {
            onyx.Targets.Add(OnyxAccounts.DedicatedEmail);
            if (onyx.RegisterError is { } error)
            {
                return ValueTask.FromResult<Result<Unit>>(error);
            }

            onyx.Registered[OnyxAccounts.DedicatedEmail] = password;
            return ValueTask.FromResult(Results.Ok());
        }

        public ValueTask<Result<Unit>> MakeDedicatedAdmin(CancellationToken cancellationToken)
        {
            onyx.Targets.Add(OnyxAccounts.DedicatedEmail);
            if (onyx.AdminError is { } error)
            {
                return ValueTask.FromResult<Result<Unit>>(error);
            }

            _ = onyx.Admins.Add(OnyxAccounts.DedicatedEmail);
            return ValueTask.FromResult(Results.Ok());
        }

        public ValueTask<Result<int>> RevokeTokens(string name, CancellationToken cancellationToken)
        {
            if (onyx.RevokeError is { } error)
            {
                return ValueTask.FromResult<Result<int>>(error);
            }

            var mine = onyx._tokens.Where(t => string.Equals(t.Value.Email, email, StringComparison.Ordinal) && string.Equals(t.Value.Name, name, StringComparison.Ordinal)).Select(static t => t.Key).ToList();
            foreach (var token in mine)
            {
                _ = onyx._tokens.Remove(token);
            }

            onyx.Revoked += mine.Count;
            return ValueTask.FromResult<Result<int>>(mine.Count);
        }

        public ValueTask<Result<string>> CreateToken(string name, CancellationToken cancellationToken)
        {
            if (onyx.TokenError is { } error)
            {
                return ValueTask.FromResult<Result<string>>(error);
            }

            var token = ++onyx._minted == 1 ? onyx.Token : onyx.Token + "-" + onyx._minted.ToString(CultureInfo.InvariantCulture);
            onyx._tokens[token] = (email, name);
            return ValueTask.FromResult<Result<string>>(token);
        }

        public ValueTask DisposeAsync()
        {
            onyx.LoggedOut++;
            return ValueTask.CompletedTask;
        }
    }
}
