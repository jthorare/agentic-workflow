using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>
/// IOnyxProvisioning over HTTP to the origin it is given (spec §5.2): http://127.0.0.1:&lt;port&gt; for the managed stack, or the cluster address `onyx attach` has already checked against the environment. Ten requests are allowed (nine exact routes and one delete pattern) and nothing else; one that isn't on the list is refused before anything is sent (<see cref="Allowed"/>).
/// The login cookie lives in the session's own container and goes with the session; no password and no token is put in an error message or a log. Not used by the query adapter, and it does not use it.
/// Why these ten (docs/sindri/onyx.md section 7): Onyx's Community Edition gates `/admin/api-key` and `/manage/admin/user-group` at the Business tier (`backend/ee/onyx/configs/license_enforcement_config.py`,
/// `PATH_PREFIX_MIN_TIER`), so Sindri can't have a service-account key. It gets a dedicated admin user and a personal access token of that user instead, through routes the tiers leave open.
/// </summary>
internal sealed class HttpOnyxProvisioning : IOnyxProvisioning
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    // Each route with its source at the pinned commit f1738ed (the paths the app serves under /api).
    private static readonly (HttpMethod Method, string Path)[] _allowed =
    [
        (HttpMethod.Post, "/api/auth/login"),                          // main.py:652, fastapi-users auth router: the cookie of the owner, then of the dedicated user
        (HttpMethod.Post, "/api/auth/logout"),                         // main.py:652
        (HttpMethod.Post, "/api/auth/register"),                       // main.py:660, fastapi-users register router; UserManager.create (auth/users.py:696) enforces invite-only
        (HttpMethod.Post, "/api/password/reset_password"),             // server/features/password/api.py:42: FULL_ADMIN_PANEL_ACCESS; the server makes the new password
        (HttpMethod.Patch, "/api/manage/admin/users/admin-access"),    // server/manage/users.py:162: FULL_ADMIN_PANEL_ACCESS; the CE route that sets the admin permission
        (HttpMethod.Patch, "/api/manage/admin/deactivate-user"),       // server/manage/users.py:800: FULL_ADMIN_PANEL_ACCESS; retires the dedicated user of an earlier build (its tokens stop working)
        (HttpMethod.Get, "/api/user/pats"),                            // server/pat/api.py (GET ""): BASIC_ACCESS; the dedicated user's own tokens, so old sindri-onyx ones can be revoked
        (HttpMethod.Post, "/api/user/pats"),                           // server/pat/api.py (POST ""): CREATE_USER_API_KEYS, which admins hold
        (HttpMethod.Get, "/api/me"),                                   // server/manage/users.py:1081: whose token this is
    ];

    private static readonly TimeSpan _logoutTimeout = TimeSpan.FromSeconds(5);

    private readonly Func<HttpMessageHandler> _handler;
    private readonly TimeSpan _logout;
    private readonly TimeSpan _request;

    /// <summary>Initializes the adapter for production: a handler per session, no redirect, no proxy.</summary>
    public HttpOnyxProvisioning()
        : this(static () => new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = true }, _logoutTimeout)
    {
    }

    /// <summary>Initializes the adapter over a handler factory (a test hands one pointing at a fake server).</summary>
    /// <param name="handler">Makes the handler a session's client uses; it must keep cookies.</param>
    /// <param name="logoutTimeout">How long disposing a session waits for the logout; null takes 5 s.</param>
    /// <param name="requestTimeout">How long one request waits for Onyx; null takes 15 s.</param>
    internal HttpOnyxProvisioning(Func<HttpMessageHandler> handler, TimeSpan? logoutTimeout = null, TimeSpan? requestTimeout = null)
    {
        _handler = handler;
        _logout = logoutTimeout ?? _logoutTimeout;
        _request = requestTimeout ?? _timeout;
    }

    /// <summary>Gets the JSON options every provisioning request and reply goes through.</summary>
    internal static JsonSerializerOptions Json { get; } = AwJson.CreateOptions(OnyxHealthJsonContext.Default);

    /// <summary>Whether a request is on the provisioning allowlist: the method and the path (no query), exactly.</summary>
    /// <param name="method">The method.</param>
    /// <param name="path">The absolute path.</param>
    /// <returns>True when it may be sent.</returns>
    internal static bool Allowed(HttpMethod method, string path)
        => _allowed.Any(a => a.Method == method && string.Equals(a.Path, path, StringComparison.Ordinal)) || (method == HttpMethod.Delete && IsTokenPath(path));

    // The one route with a parameter: `DELETE /api/user/pats/{id}` (server/pat/api.py; a user can revoke only their own tokens), the id one to nine ASCII numerals and nothing after it.
    private static bool IsTokenPath(string path)
    {
        const string prefix = "/api/user/pats/";
        return path.StartsWith(prefix, StringComparison.Ordinal) && path.Length - prefix.Length is >= 1 and <= 9 && path.AsSpan(prefix.Length).IndexOfAnyExceptInRange('0', '9') < 0;
    }

    /// <inheritdoc />
#pragma warning disable CA2000 // the session owns its client and disposes both when the caller disposes the session
    public async ValueTask<Result<IOnyxSession>> SignIn(OnyxOrigin origin, string email, string password, CancellationToken cancellationToken)
    {
        var session = new OnyxSession(NewClient(), origin.Url, _logout, _request);
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal) { ["username"] = email, ["password"] = password });
        var sent = await session.Send(HttpMethod.Post, "/api/auth/login", body, cancellationToken).ConfigureAwait(false);
        if (sent.IsError || !sent.Value.Success)
        {
            await session.DisposeAsync().ConfigureAwait(false);
            return sent.IsError
                ? sent.Error
                : SindriErrors.OnyxKeyFailed.With($"Onyx refused that login (HTTP {OnyxSession.Code(sent.Value.Status)}); check the email and password, then run `sindri onyx up` again");
        }

        return Results.Success<IOnyxSession>(session);
    }

#pragma warning restore CA2000

    /// <inheritdoc />
    public async ValueTask<Result<bool>> TokenIsAdmin(OnyxOrigin origin, string token, string email, CancellationToken cancellationToken)
    {
        using var client = NewClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
#pragma warning disable CA2000 // a session only borrows the client, which the using above disposes
        var session = new OnyxSession(client, origin.Url, _logout, _request);
#pragma warning restore CA2000
        var sent = await session.Send(HttpMethod.Get, "/api/me", content: null, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error;
        }

        if (!sent.Value.Success)
        {
            return false;   // refused: not a token Onyx knows
        }

        var me = AwJson.Deserialize<OnyxMeWire>(Encoding.UTF8.GetBytes(sent.Value.Body), Json);
        return me is { IsSuccess: true, Value: { } wire }
            && string.Equals(wire.Email, email, StringComparison.OrdinalIgnoreCase)
            && wire.EffectivePermissions?.Contains("admin", StringComparer.Ordinal) == true;
    }

    private HttpClient NewClient() => new(_handler(), disposeHandler: true) { Timeout = _request + TimeSpan.FromSeconds(5) };

}
