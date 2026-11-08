using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.LaneRecycle;

/// <summary>
/// Recycles a Claude lane that wrote a digest: the guard refuses without a fresh, non-empty digest or for a lane that isn't Claude Code, then
/// the runner clears the lane, waits for its new session and has it continue from the digest. A dry run lists the steps and sends nothing.
/// </summary>
/// <param name="config">The team config.</param>
/// <param name="guard">The refusals.</param>
/// <param name="runner">The recycle itself.</param>
/// <param name="switcher">The move to another agent, for a lane routing.lanes assigns elsewhere.</param>
internal sealed class LaneRecycleService(IMetaConfig config, RecycleGuard guard, RecycleRunner runner, SwitchRunner switcher) : IService<LaneRecycleRequest, LaneRecycleResponse>
{
    public async ValueTask<Result<LaneRecycleResponse>> Handle(LaneRecycleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var plan = await guard.Check(team.Value, request, cancellationToken).ConfigureAwait(false);
        if (plan.IsError)
        {
            return plan.Error;
        }

        var switching = plan.Value.SwitchTo is not null;
        if (request.DryRun)
        {
            return new LaneRecycleResponse(plan.Value.Lane, DryRun: true, plan.Value.OldSession.Value, NewSession: null, plan.Value.OldTokens, plan.Value.Digest, switching ? SwitchRunner.Steps(plan.Value) : RecycleRunner.Steps(plan.Value));
        }

        return switching
            ? await switcher.Run(plan.Value, cancellationToken).ConfigureAwait(false)
            : await runner.Run(plan.Value, cancellationToken).ConfigureAwait(false);
    }
}
