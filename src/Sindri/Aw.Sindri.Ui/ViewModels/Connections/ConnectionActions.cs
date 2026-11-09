using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Connections;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>
/// The calls a connection row, the sheet and the add form make, one method per MCP tool (G8: no more actions than the tools have), and the browser sign-in
/// they share: open the consent page, then read the connection until it says 'connected' or the wait runs out. A write is sent once, never retried.
/// </summary>
/// <param name="api">The API.</param>
/// <param name="url">The API's base URL, or null while it isn't known.</param>
/// <param name="launcher">Opens the consent page.</param>
/// <param name="clock">The clock the poll waits on.</param>
internal sealed class ConnectionActions(ISindriApi api, Func<Uri?> url, ILauncher launcher, TimeProvider clock)
{
    /// <summary>How often the sign-in poll reads the connection.</summary>
    internal static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(2);

    /// <summary>How long the poll waits: Sindri's own listener gives up after 5 minutes.</summary>
    internal static readonly TimeSpan SignInTimeout = TimeSpan.FromMinutes(5);

    // The consent page goes through the one launch helper, so the system's answer is kept.
    private readonly LinkLaunch _launch = new(launcher);

    /// <summary>Reads one connection (connection_get).</summary>
    /// <param name="name">The connection's name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The connection, or why not.</returns>
    internal Task<Result<ConnectionOne>> Get(string name, CancellationToken cancellationToken) => With(u => api.GetConnection(u, name, cancellationToken));

    /// <summary>Adds a connection (connection_add). The body is sent once.</summary>
    /// <param name="body">The body, with any pasted secret.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The connection and its consent address, or why not.</returns>
    internal Task<Result<ConnectionAdded>> Add(AddConnectionBody body, CancellationToken cancellationToken) => With(u => api.AddConnection(u, body, cancellationToken));

    /// <summary>Tests a connection (connection_test).</summary>
    /// <param name="name">The connection's name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome, or why not.</returns>
    internal Task<Result<ConnectionTested>> Test(string name, CancellationToken cancellationToken) => With(u => api.TestConnection(u, name, cancellationToken));

    /// <summary>Starts a sign-in again (connection_signin; oauth and mcp connections only).</summary>
    /// <param name="name">The connection's name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The connection and its consent address, or why not.</returns>
    internal Task<Result<ConnectionAdded>> SignIn(string name, CancellationToken cancellationToken) => With(u => api.SignInConnection(u, name, cancellationToken));

    /// <summary>Removes a connection (connection_remove).</summary>
    /// <param name="name">The connection's name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What was removed, or why not.</returns>
    internal Task<Result<ConnectionRemoved>> Remove(string name, CancellationToken cancellationToken) => With(u => api.RemoveConnection(u, name, cancellationToken));

    /// <summary>
    /// Follows a started sign-in to its end: opens the consent page and reads the connection every <see cref="PollEvery"/> until it is connected, reads 'error', can't be read, or
    /// <see cref="SignInTimeout"/> passes. A key connection has no consent page, so it ends at once on what it reads.
    /// </summary>
    /// <param name="added">What the add or the sign-in answered.</param>
    /// <param name="say">Told what the app is waiting on (the note shown while it waits).</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>How it ended.</returns>
    internal async Task<SignInEnd> Finish(ConnectionAdded added, Action<string> say, CancellationToken cancellationToken)
    {
        var name = added.Connection.Name;
        if (added.ConsentUrl is null)
        {
            return Ended(added.Connection);
        }

        if (!Uri.TryCreate(added.ConsentUrl, UriKind.Absolute, out var consent) || consent.Scheme is not ("https" or "http"))
        {
            return new SignInEnd(SignInState.Failed, added.Connection, Labels.ConnectionBadConsent);
        }

        var opened = await _launch.Open(consent, cancellationToken).ConfigureAwait(true);
        say(opened ? Labels.ConnectionWaitingBrowser : Labels.ConnectionBrowserRefused(added.ConsentUrl));
        var (last, deadline) = (added.Connection, clock.GetUtcNow() + SignInTimeout);
        while (clock.GetUtcNow() < deadline)
        {
            await Task.Delay(PollEvery, clock, cancellationToken).ConfigureAwait(true);
            var read = await Get(name, cancellationToken).ConfigureAwait(true);
            if (read.IsError)
            {
                return new SignInEnd(SignInState.Failed, last, Labels.InstanceRefused(read.Error));
            }

            last = read.Value.Connection;
            if (!string.Equals(last.Status, "needs-sign-in", StringComparison.Ordinal))
            {
                return Ended(last);
            }
        }

        return new SignInEnd(SignInState.TimedOut, last, string.Empty);
    }

    private static SignInEnd Ended(ConnectionView view) =>
        string.Equals(view.Status, "connected", StringComparison.Ordinal)
            ? new SignInEnd(SignInState.Connected, view, string.Empty)
            : new SignInEnd(SignInState.Failed, view, view.StatusReason ?? string.Empty);

    private Task<Result<T>> With<T>(Func<Uri, Task<Result<T>>> call) =>
        url() is { } target ? call(target) : Task.FromResult<Result<T>>(SindriUiErrors.Unreachable.With("Sindri isn't reachable yet"));
}
