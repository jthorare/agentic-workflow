using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>
/// The setup-only requests of spec §5.2 (the dedicated user and its token, docs/sindri/onyx.md section 7), used by `sindri onyx up` in the human's process and by nothing else. Its allowlist is separate from the query adapter's (which stays at five requests) and lists ten;
/// neither type uses the other. A request off the list is refused before it is sent. The login cookie lives in the session object only.
/// </summary>
public interface IOnyxProvisioning
{
    /// <summary>Logs the owner in (`POST /api/auth/login`, a form).</summary>
    /// <param name="origin">Where Onyx answers; a port converts to its loopback origin.</param>
    /// <param name="email">The owner's Onyx email.</param>
    /// <param name="password">The owner's Onyx password; used for this call and not kept.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The session, or SND-ONYX-008 when the login is refused.</returns>
    public ValueTask<Result<IOnyxSession>> SignIn(OnyxOrigin origin, string email, string password, CancellationToken cancellationToken);

    /// <summary>Asks whether a token is the dedicated user's admin token (`GET /api/me` with the token as a Bearer).</summary>
    /// <param name="origin">Where Onyx answers; a port converts to its loopback origin.</param>
    /// <param name="token">The personal access token.</param>
    /// <param name="email">The email of the user the token must belong to.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>True when Onyx knows the token as that user with the admin permission, false when it refuses the token or the account is another or not an admin; an error when Onyx can't be reached.</returns>
    public ValueTask<Result<bool>> TokenIsAdmin(OnyxOrigin origin, string token, string email, CancellationToken cancellationToken);
}
