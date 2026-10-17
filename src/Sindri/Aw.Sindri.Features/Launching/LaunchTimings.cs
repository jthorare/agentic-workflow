using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.Launching;

/// <summary>The launcher's clock and time limits (UI spec E-1): the request's deadline, the limit on one 'ILauncher.Launch', and the time that must remain to claim a request.</summary>
/// <param name="Clock">The clock every limit is measured on.</param>
/// <param name="Deadline">A request older than this is never launched; the sweep fails it with SND-LAUNCH-007.</param>
/// <param name="LaunchLimit">One 'ILauncher.Launch' that takes longer is SND-LAUNCH-009.</param>
/// <param name="ClaimReserve">A request is claimed only while at least this much of its deadline remains, so a launch never opens a window after the deadline.</param>
internal sealed record LaunchTimings(TimeProvider Clock, TimeSpan Deadline, TimeSpan LaunchLimit, TimeSpan ClaimReserve)
{
    /// <summary>The spec's numbers (30 s, 10 s and 10 s) on <paramref name="clock"/>.</summary>
    /// <param name="clock">The clock.</param>
    /// <returns>The limits.</returns>
    public static LaunchTimings For(TimeProvider clock) => new(clock, LaunchRules.Deadline, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
}
