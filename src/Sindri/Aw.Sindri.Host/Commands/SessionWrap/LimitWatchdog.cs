using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>
/// The one clock and the memory watch of a sandboxed session (sandbox spec §3.3, SB7, SB16). On every tick it reads the scope's counters (keeping the peaks and the last
/// reading, since the files go with the scope), asks the engine whether the run is suspended (a failed answer counts as not suspended, so a runaway is still stopped),
/// counts only unsuspended time against the wall limit, moves the scope's loose ceiling forward when a suspension ends, and stops the session when the wall is used up:
/// SIGTERM, ten seconds, then SIGKILL. A memory kill is the kernel's; the watchdog sees it in memory.events, or after the scope in systemd's Result.
/// </summary>
/// <param name="limits">The session's limits.</param>
/// <param name="scope">The session's scope.</param>
/// <param name="calls">The control plane calls (the suspension read).</param>
/// <param name="clock">The time the ticks and the grace run on.</param>
/// <param name="errors">Where wrap writes its own messages.</param>
internal sealed class LimitWatchdog(WrapLimits limits, IScopeControl scope, IWrapCalls calls, TimeProvider clock, TextWriter errors)
{
    /// <summary>How often the scope and the suspension are read (a memory limit shows within this).</summary>
    internal static readonly TimeSpan Tick = TimeSpan.FromSeconds(5);

    /// <summary>How long a SIGTERM may take before the scope is killed outright.</summary>
    internal static readonly TimeSpan TermGrace = TimeSpan.FromSeconds(10);

    /// <summary>Watches the session until <paramref name="stop"/>.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="proof">The launch secret for the suspension read, or null (then nothing is ever suspended).</param>
    /// <param name="childExited">Completes when the agent exits.</param>
    /// <param name="stop">Ends the watch.</param>
    /// <returns>A task that completes when the watch ends.</returns>
    internal Task Run(LaunchId launch, LaunchSecret? proof, Task childExited, CancellationToken stop) =>
        throw new NotSupportedException($"{limits} {scope} {calls} {clock} {errors} {launch} {proof} {childExited} {stop}");

    /// <summary>Tells whether a sandbox limit ended the session, once the agent has exited.</summary>
    /// <param name="cancellationToken">Cancels the read of the failed scope.</param>
    /// <returns>The limit, its value and the peaks, or null for an ordinary exit.</returns>
    internal ValueTask<LimitEnd?> Conclude(CancellationToken cancellationToken) => throw new NotSupportedException();
}
