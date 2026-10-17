using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.ProbeLaunchTargets;

/// <summary>The work of a probe job (UI spec E-3): checks the viewer, the host, every catalog target and every repo, and records what changed.</summary>
/// <param name="held">The lease the event is fenced by.</param>
/// <param name="probes">Probes and records.</param>
internal sealed class ProbeLaunchTargetsService(HeldLease held, ILaunchProbes probes) : IService<ProbeLaunchTargetsRequest, ProbeLaunchTargetsResult>
{
    public async ValueTask<Result<ProbeLaunchTargetsResult>> Handle(ProbeLaunchTargetsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var recorded = await probes.Record(grant, cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            return recorded.Error;
        }

        var targets = recorded.Value.Probed.Targets;
        return new ProbeLaunchTargetsResult(recorded.Value.Changed, targets.Count, targets.Count(static t => t.Present));
    }
}
