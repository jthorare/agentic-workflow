namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Reads and steers one session's cgroup scope (sandbox spec §3.3, SB7). The adapter is <see cref="SystemdScope"/>; the watchdog in 'session wrap' only sees this port.</summary>
internal interface IScopeControl
{
    /// <summary>Reads the scope's counters.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The reading, or null when the scope is gone (its files are removed with it).</returns>
    public ValueTask<ScopeSample?> Sample(CancellationToken cancellationToken);

    /// <summary>Tells how a scope that is gone ended.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>systemd's 'Result' of the unit ('oom-kill', 'timeout', 'success', …), or null while the scope runs or when the unit is unknown.</returns>
    public ValueTask<string?> Result(CancellationToken cancellationToken);

    /// <summary>Moves the scope's loose ceiling (RuntimeMaxSec) to a value counted from the scope's start.</summary>
    /// <param name="runtimeMax">The new ceiling.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when systemd has the new value; a failure is not an error (the ceiling is only a last resort).</returns>
    public ValueTask SetRuntimeMax(TimeSpan runtimeMax, CancellationToken cancellationToken);

    /// <summary>Signals every process of the scope.</summary>
    /// <param name="force">True for SIGKILL, false for SIGTERM.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the signal is sent.</returns>
    public ValueTask Kill(bool force, CancellationToken cancellationToken);
}
