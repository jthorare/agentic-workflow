using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// How an agent run ends and the one retry of a refused or check-failed run on a stronger model (model-pick spec 7.1): the same run token, its own launch id and worktree, no round counted and no cap of
/// its own on the tokens (owner decision M3, 2026-10-27). What the failed first launch spent is charged with the retry's result, so the state's and the recipe's budgets count both.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    private (RunToken Run, int Tokens)? _carried;   // the tokens of a run's failed first launch, until the retry's result decides the run
    private bool? _escalation;
    private bool EscalationPatched => _escalation ??= Workflow.Patched("model-pick-escalation-v1");   // read only when a run failed on a model with a stronger one allowed; a history without it fails the run as it did

    // The session ended with no report: found ended by the first poll after a suspension it was kept through, the Step runs again (sandbox spec §7); else its output folder may prove a result,
    // a refused or check-failed run is retried once on a stronger model, and any other end fails the Step with how it ended.
    private async Task<WorkflowInstance> Ended(WorkflowInstance instance, AgentRunning running, StepUse step, AgentSessionState ended, Interpreting context)
    {
        var open = running.Open;
        if (ended.FoundEnded)
        {
            var gone = await Close(running, measure: true).ConfigureAwait(true);   // the session is gone: this reads what it spent
            return await Decided(instance, open, new SessionLost("service:agent:" + step.Use.Value, open.Token) { Tokens = gone.Tokens }, reportId: null, context).ConfigureAwait(true);
        }

        var recovery = await RecoveryFor(running, ended).ConfigureAwait(true);
        var closed = await Close(running, measure: true).ConfigureAwait(true);
        if (recovery?.Outcome is { } outcome)
        {
            var recovered = new StepCompleted("agent:recovered", open.Token, OutcomeName.From(outcome), recovery.Outputs, closed.Tokens, StepRuntime.Agent, recovery.Sha!, EmptyDiff: closed.EmptyDiff);   // why: an outcome comes with its digest
            return await Decided(instance, open, recovered, reportId: null, context).ConfigureAwait(true);
        }

        var kind = AgentFailureKinds.Of(ended.Refused, checkFailed: recovery?.Reason is not null);
        if (RetryAfter(running, kind, closed.Tokens) is { } previous)
        {
            return await Escalate(instance, open, previous, context).ConfigureAwait(true);
        }

        var failed = AgentFailed(open, step, AgentEndReasons.Of(ended.EndedAs), closed.Tokens, recovery?.Reason ?? ended.Detail) with { FailureKind = kind };
        return await Decided(instance, open, failed, reportId: null, context).ConfigureAwait(true);
    }

    // A refused run (its report was judged and refused, and the second refusal ended the session) is not recovered from the output folder: that is the output just refused (issue 1468). The
    // skip changes the commands of a history that recovered a refused run, so it has its own patch, read only when the run was refused; a history without it recovers as it did.
    private async Task<AgentRecovered?> RecoveryFor(AgentRunning running, AgentSessionState ended) =>
        ended.Refused && RefusedSkipsRecovery ? null : await Recover(running).ConfigureAwait(true);

    private bool? _refusedSkipsRecovery;
    private bool RefusedSkipsRecovery => _refusedSkipsRecovery ??= Workflow.Patched("refused-skips-recovery-v1");

    // The failed run the one retry follows, or null when there is none: only a refused or check-failed first launch whose pick says a stronger model is allowed is retried.
    private PreviousRun? RetryAfter(AgentRunning running, string kind, int tokens) =>
        running.Open.Escalation == 0 && !string.Equals(kind, AgentFailureKinds.Crashed, StringComparison.Ordinal) && running.Launched is { CanEscalate: true, Model: { } failedModel } && EscalationPatched
            ? new PreviousRun(failedModel, kind, Check: null, tokens)
            : null;

    // Launches the run again on a stronger model, then watches and closes it as usual. The reset: no held launch, no launch time, and the run's launch is the retry's.
    private async Task<WorkflowInstance> Escalate(WorkflowInstance instance, OpenRun open, PreviousRun previous, Interpreting context)
    {
        _agent = null;
        _held = null;
        _launchedAt = null;
        _carried = (open.Token, previous.Tokens);
        var retry = open with { Escalation = 1, Launch = null };
        instance = instance with { Frame = instance.Frame with { Run = retry } };
        _instance = instance;   // why: the Query shows the retry's launch id from here on
        var (launched, done) = await LaunchPhase(instance, retry, context, previous).ConfigureAwait(true);
        return done ? launched : await WatchPhase(launched, retry, context).ConfigureAwait(true);
    }

    // Adds what an escalated run's failed first launch spent to the result that decides the run.
    private Trigger Carrying(OpenRun open, Trigger trigger)
    {
        if (_carried is not { } carried || carried.Run != open.Token)
        {
            return trigger;
        }

        _carried = null;
        int Sum(int tokens) => (int)Math.Min(int.MaxValue, (long)tokens + carried.Tokens);
        return trigger switch
        {
            StepCompleted completed => completed with { Tokens = Sum(completed.Tokens) },
            StepFailed failed => failed with { Tokens = Sum(failed.Tokens) },
            _ => trigger,
        };
    }
}
