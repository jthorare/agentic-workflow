using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.GateRun;

/// <summary>
/// Runs the merge gate: the pull request's head merged onto origin/main in the gate tree, then each selected scope's steps
/// in order, every step logging to gates/&lt;pr&gt;/&lt;run&gt;/&lt;step&gt;.log. The run is recorded whatever its verdict.
/// </summary>
/// <param name="config">The team config.</param>
/// <param name="workspace">Builds the tree.</param>
/// <param name="steps">Runs a step.</param>
/// <param name="gates">Stores the run.</param>
/// <param name="reservation">Computes the heavy-slot reservation from the merge-queue depth before the run.</param>
/// <param name="clock">Dates the run.</param>
internal sealed class GateRunService(IMetaConfig config, GateWorkspace workspace, GateStepRunner steps, IGateStore gates, HeavyReservation reservation, TimeProvider clock)
    : IService<GateRunRequest, GateRunResponse>
{
    public async ValueTask<Result<GateRunResponse>> Handle(GateRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        if (request.Scopes.FirstOrDefault(scope => !team.Value.Gate.Scopes.ContainsKey(scope)) is { } unknown)
        {
            return MetaErrors.Invalid.With($"no gate scope '{unknown}' in the team config (scopes: {string.Join(", ", team.Value.Gate.Scopes.Keys)})");
        }

        // Advisory: a reservation that can't be kept (unwritable state directory) must not stop the gate.
        var reserved = await reservation.Apply(team.Value, cancellationToken).ConfigureAwait(false);

        var plan = await Plan(team.Value, request, cancellationToken).ConfigureAwait(false);
        if (plan.IsError)
        {
            return plan.Error;
        }

        var ran = plan.Value.Prepared.Merged ? await RunSteps(plan.Value, team.Value.Gate, team.Value.Heavy.Cap, cancellationToken).ConfigureAwait(false) : Results.Success<IReadOnlyList<GateStep>>([]);
        return ran.IsError ? ran.Error : await Record(plan.Value, ran.Value, reserved.IsSuccess ? reserved.Value : null, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<GatePlan>> Plan(TeamConfig team, GateRunRequest request, CancellationToken cancellationToken)
    {
        var prepared = await workspace.Prepare(team, request.Pr, listChanged: request.Scopes.Count == 0, cancellationToken: cancellationToken).ConfigureAwait(false);
        var latest = await gates.Latest(request.Pr, cancellationToken).ConfigureAwait(false);
        if (prepared.IsError || latest.IsError)
        {
            return prepared.IsError ? prepared.Error : latest.Error;
        }

        var run = await workspace.NextRun(request.Pr, latest.Value?.Run ?? 0, cancellationToken).ConfigureAwait(false);
        if (run.IsError)
        {
            return run.Error;
        }

        return new GatePlan(request.Pr, run.Value, Scopes(request, team.Gate, prepared.Value.Changed), prepared.Value);
    }

    private static IReadOnlyList<string> Scopes(GateRunRequest request, GateConfig gate, IReadOnlyList<string> changed) =>
        request.Scopes.Count > 0 ? [.. request.Scopes.Distinct(StringComparer.Ordinal)] : ScopePlanner.Plan(changed, gate);

    private async ValueTask<Result<IReadOnlyList<GateStep>>> RunSteps(GatePlan plan, GateConfig gate, int heavyCap, CancellationToken cancellationToken)
    {
        var graph = GateGraphs.Build(plan.Scopes, gate);
        if (graph.IsError)
        {
            return graph.Error;
        }

        var ran = await StepScheduler.Run(
            graph.Value,
            Math.Max(1, gate.ParallelSteps ?? heavyCap),
            (step, token) => steps.Run(step, plan.Prepared.Tree, workspace.LogPath(plan.Pr, plan.Run, step.Name), Math.Max(1, gate.ParallelSteps ?? heavyCap), token),
            cancellationToken).ConfigureAwait(false);
        return ran;
    }

    private async ValueTask<Result<GateRunResponse>> Record(GatePlan plan, IReadOnlyList<GateStep> ran, HeavySlots? heavy, CancellationToken cancellationToken)
    {
        var verdict = !plan.Prepared.Merged ? GateVerdict.Conflict : ran.All(s => s.Verdict == GateVerdict.Pass) ? GateVerdict.Pass : GateVerdict.Fail;
        var record = new GateRecord(plan.Pr, plan.Prepared.Pr.Head, plan.Prepared.Main, plan.Scopes, ran, verdict, clock.GetUtcNow()) { Run = plan.Run, Tree = plan.Prepared.TreeSha, Stack = [] };
        var saved = await gates.Save(record, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : new GateRunResponse(saved.Value, heavy);
    }
}
