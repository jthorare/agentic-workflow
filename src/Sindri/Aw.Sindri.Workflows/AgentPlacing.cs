using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Where an agent Step runs and, for a Step that must differ from another, who ran that one (#1269): the profile's target, with the agent swapped for the state's own 'agent: id', or for
/// the one <see cref="ChallengerPick"/> chooses when the state says 'agent: { differentFrom: ... }' naming an earlier state.
/// </summary>
/// <param name="targets">The profile's target.</param>
/// <param name="challengers">Picks the agent of a Step that differs from another and carries the other's launch.</param>
internal sealed class AgentPlacing(AgentTargetResolver targets, ChallengerPick challengers)
{
    /// <summary>The place <paramref name="run"/> runs.</summary>
    /// <param name="run">The run.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The target and the other Step's launch (null for a Step that differs from none); SND-LAUNCH-028 when no agent may run a Step that must differ.</returns>
    public async ValueTask<Result<PlacedStep>> Place(AgentLaunchRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var target = await targets.Resolve(run.Item, run.Run.Address, cancellationToken).ConfigureAwait(false);
        if (target.IsError)
        {
            return target.Error;
        }

        if (run.Agent?.Use is { } use)
        {
            return AgentId.TryFrom(use, out var named)
                ? new PlacedStep(target.Value with { Agent = named }, Drafter: null)
                : SindriErrors.LaunchAgentMissing.With($"the state's agent '{use}' isn't an agent id (fix: name an agent of `sindri launch show`)");
        }

        if (run.Agent?.DifferentFrom is not { } other)
        {
            return new PlacedStep(target.Value, Drafter: null);
        }

        // why: the first launch of the run (escalation 0), so an escalation retry keeps the agent the first run was placed on
        var second = await challengers.Pick(new ChallengerAsk(target.Value.Item, run.Run.Address, AgentLaunches.LaunchIdFor(run.Run.Workflow, run.Run.Run), other, target.Value.Agent), cancellationToken).ConfigureAwait(false);
        return second.IsError ? second.Error : new PlacedStep(target.Value with { Agent = second.Value.Agent }, second.Value.Drafter);
    }
}
