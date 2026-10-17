using Aw.Kernel;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// Probes the launcher and records 'launch.probed' when what it found differs from the newest probe in the ledger (UI spec E-3: a probe never churns the
/// ledger). The 'ProbeLaunchTargets' job and the launcher service (at start, every 10 minutes, and after a failed launch) share it.
/// </summary>
public interface ILaunchProbes
{
    /// <summary>Probes and records.</summary>
    /// <param name="grant">The lease the event is fenced by.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>What was found, and whether it was recorded.</returns>
    public ValueTask<Result<RecordedProbe>> Record(LeaseGrant grant, CancellationToken cancellationToken);
}
