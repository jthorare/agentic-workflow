using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>
/// Keeps each agent-app launch's GitHub token: mints it at the request, writes it into the session home's token folder (a temp file renamed in, so a reader never sees half a token),
/// and renews it every 45 minutes until the launch is released. A failed renewal keeps the old token (valid 60 minutes), says so, and tries again in 5.
/// </summary>
internal sealed class AgentTokenRenewal(IAgentTokenSource source, TimeProvider clock, Action<string>? warn = null)
{
    private static readonly TimeSpan _every = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan _retry = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<LaunchId, Held> _held = new();

    /// <summary>Mints a token for the launch and keeps it for <see cref="Write"/>.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="repo">The repository the token is narrowed to.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Success, or SND-LAUNCH-018 with its reason.</returns>
    internal async ValueTask<Result<Unit>> Mint(LaunchId launch, RepoName repo, CancellationToken cancellationToken)
    {
        var minted = await source.Mint(launch, repo, cancellationToken).ConfigureAwait(false);
        if (minted.IsError)
        {
            return minted.Error;
        }

        _held[launch] = new Held(repo, minted.Value);
        return Unit.Value;
    }

    /// <summary>Writes the launch's token into its session home (nothing when it has none) and starts the renewals.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="sessionHome">The launch's session home.</param>
    /// <returns>Success, or the failure to write the file.</returns>
    internal Result<Unit> Write(LaunchId launch, string sessionHome)
    {
        if (!_held.TryGetValue(launch, out var held))
        {
            return Unit.Value;
        }

        try
        {
            _ = TokenFile.Replace(sessionHome, held.Token.Value);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.FsWrite, "the GitHub token could not be written: " + e.Message);
        }

        held.Home = sessionHome;
        held.Timer = clock.CreateTimer(_ => _ = Renew(launch, held), state: null, _every, Timeout.InfiniteTimeSpan);
        return Unit.Value;
    }

    /// <summary>Stops the launch's renewals and forgets its token.</summary>
    /// <param name="launch">The launch.</param>
    internal void Release(LaunchId launch)
    {
#pragma warning disable CA2000 // Stop disposes it
        if (_held.TryRemove(launch, out var held))
        {
            held.Stop();
        }
#pragma warning restore CA2000
    }

    private async Task Renew(LaunchId launch, Held held)
    {
        if (held.Home is not { } home)
        {
            return;
        }

        TimeSpan? next;
        try
        {
            next = await Attempt(launch, held, home).ConfigureAwait(false);
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
        {
            return;   // released while renewing
        }

        try
        {
            _ = next is { } due ? held.Timer?.Change(due, Timeout.InfiniteTimeSpan) : null;
        }
        catch (ObjectDisposedException)
        {
            // released while renewing
        }
    }

    // When to renew next: 45 minutes after a renewal, 5 after a failed one; null when the launch was released meanwhile.
    private async Task<TimeSpan?> Attempt(LaunchId launch, Held held, string home)
    {
        try
        {
            var minted = await source.Mint(launch, held.Repo, held.Cancel.Token).ConfigureAwait(false);
            if (held.Cancel.IsCancellationRequested)
            {
                return null;
            }

            if (minted.IsError)
            {
                Say(minted.Error);
                return _retry;
            }

            if (!TokenFile.Replace(home, minted.Value.Value, create: false))
            {
                return null;   // the session home is gone: the launch closed, so the token is not written back
            }

            held.Token = minted.Value;
            return _every;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            warn?.Invoke($"{SindriErrors.LaunchGitHubTokenFailed.Code}: the GitHub token could not be renewed ({e.Message}); the old token stays");
            return _retry;
        }
#pragma warning disable CA1031 // why: any other failure (a keychain process that won't start, an unexpected reply) must not end the renewals; it is said and retried
        catch (Exception e) when (e is not OperationCanceledException and not ObjectDisposedException)
        {
            warn?.Invoke($"{SindriErrors.LaunchGitHubTokenFailed.Code}: the GitHub token could not be renewed ({e.GetType().Name}); the old token stays");
            return _retry;
        }
#pragma warning restore CA1031
    }

    private void Say(Error error) =>
        warn?.Invoke($"{error.Code}: the GitHub token could not be renewed ({SandboxRefusal.ReasonOf(error)}); the old token stays");

    private sealed class Held(RepoName repo, AgentToken token) : IDisposable
    {
        internal RepoName Repo { get; } = repo;

        internal CancellationTokenSource Cancel { get; } = new();

        internal AgentToken Token { get; set; } = token;

        internal string? Home { get; set; }

        internal ITimer? Timer { get; set; }

        internal void Stop()
        {
            Cancel.Cancel();
            Dispose();
        }

        public void Dispose()
        {
            Timer?.Dispose();
            Cancel.Dispose();
        }
    }
}
