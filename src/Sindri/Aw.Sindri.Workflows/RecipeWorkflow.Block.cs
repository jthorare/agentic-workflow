using System.Globalization;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The block runtime of <see cref="RecipeWorkflow"/> (DT spec §8.2; DT-J2): a Step the build registered with AddBlockStep runs in one
/// 'RunBlockStep' activity, attempted once, and its result becomes the trigger the engine reports for it, with no desk task. The activity is
/// bounded by the descriptor's 'maxDuration' (BlockLimits); RCP-TIME-003 keeps the state's timeout longer than that, so the Step's own bound always ends it first and running out is step.failed 'timeout', never a timer park.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    private static readonly TimeSpan _blockHeartbeat = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Runs the open block run: starts the activity, applies the commands an Update submits while it runs (a cancel or takeover ends the run, so
    /// the activity is cancelled), and decides the Step's result like a desk report. The activity's id is 'block:{token}:{n}'.
    /// </summary>
    private async Task<WorkflowInstance> RunBlockStep(WorkflowInstance instance, OpenRun open, Interpreting context)
    {
        var state = context.Recipe.States[instance.Frame.State];
        var step = state.Step ?? throw new InvalidOperationException($"'{instance.Frame.State}' has a block run but no Step"); // why: only an active state opens a run
        using var cancel = new CancellationTokenSource();
        var running = Start(instance, open, state, step, cancel.Token);
        var (awaited, ended) = await AwaitBlock(instance, open, running, cancel, context).ConfigureAwait(true);
        if (ended)
        {
            return awaited;
        }

        instance = awaited;
        var trigger = await Resulting(instance, open, step, running, context).ConfigureAwait(true);
        var decided = instance;
        var decision = TransitionDecision.Decide(new DecisionInput(instance, context.Recipe, trigger, GateAnswers.None, context.Decisions));
        instance = After(instance, decision);
        if (ReferenceEquals(decided, instance))
        {
            // why: a result that changes nothing would leave the run open and run the Step again; fail the instance loudly instead
            throw new ApplicationFailureException($"the block result of {step.Use.Value} on run {open.Token.Value} changed nothing ({decision.GetType().Name})", nonRetryable: true);
        }

        instance = Enter(instance, trigger.Actor);
        await Mirror(DeskRow(decided, instance, trigger, open, decision), NeedsYou(instance)).ConfigureAwait(true);
        Emit(decided, instance, EffectsOf(decision));
        return instance;
    }

    private Task<BlockStepOutcome> Start(WorkflowInstance instance, OpenRun open, RecipeState state, StepUse step, CancellationToken token)
    {
        _runs++;
        var start = _start ?? throw new InvalidOperationException("the workflow has not begun"); // why: the loop runs only after Begin set it
        var recipe = _recipe ?? throw new InvalidOperationException("the workflow has not begun"); // why: as above
        var run = new BlockStepRun(start.WorkflowId().Value, instance.Frame.State, open.Token.Value, step.Use, step.With, instance.Facts, _repo);
        return Workflow.ExecuteActivityAsync(
            (RunStepActivities a) => a.RunBlockStepAsync(run),
            new ActivityOptions
            {
                ActivityId = string.Create(CultureInfo.InvariantCulture, $"block:{open.Token.Value}:{_runs}"),
                TaskQueue = StepTaskQueue.For(state, Workflow.Info.TaskQueue),
                StartToCloseTimeout = Limit(recipe, state, open, step),
                HeartbeatTimeout = _blockHeartbeat,   // Temporal throttles a heartbeat to 80% of this (8 s), so a cancel or takeover reaches the Step within about that
                RetryPolicy = new RetryPolicy { MaximumAttempts = 1 },   // a judge call is never retried: a second ask is a second decision (DT spec §8.2)
                CancellationToken = token,
            });
    }

    private static bool RunOver(WorkflowInstance instance, OpenRun open) =>
        instance.Ended || instance.Suspension is not null || instance.Frame.Run?.Token != open.Token;

    private async Task<WorkflowInstance> ApplyMeanwhile(WorkflowInstance instance, Interpreting context) =>
        _submitted.Count > 0
            ? await ApplySubmitted(instance, context).ConfigureAwait(true)
            : await Apply(instance, new Submission(Ticket: 0, _timers.Take()!), context).ConfigureAwait(true);   // why: Fired says it holds one

    // Waits for the activity, applying what an Update or a timer delivers meanwhile; Ended is true when that ended the run (the activity is cancelled).
    private async Task<(WorkflowInstance Instance, bool Ended)> AwaitBlock(
        WorkflowInstance instance, OpenRun open, Task<BlockStepOutcome> running, CancellationTokenSource cancel, Interpreting context)
    {
        while (!running.IsCompleted)
        {
            await ExpireHeld(instance, wait: null).ConfigureAwait(true);
            if (_submitted.Count > 0 || _timers.Fired)
            {
                instance = await ApplyMeanwhile(instance, context).ConfigureAwait(true);
                if (RunOver(instance, open))
                {
                    await Abandon(running, cancel).ConfigureAwait(true);   // the run is over: nobody is waiting for its result
                    return (instance, true);
                }

                continue;
            }

            var seen = _arrivals;
            bool Woken() => running.IsCompleted || _submitted.Count > 0 || _timers.Fired || _arrivals != seen;
            _ = await Workflow.WaitConditionAsync(Woken, NextExpiry() ?? Timeout.InfiniteTimeSpan).ConfigureAwait(true);
        }

        return (instance, false);
    }

    /// <summary>The activity's bound: the descriptor's 'maxDuration' (two minutes when it sets none), or the state's timeout when that is shorter (a recipe RCP-TIME-003 refuses, kept as a guard for one that predates the rule).</summary>
    private TimeSpan Limit(InterpretedRecipe recipe, RecipeState state, OpenRun open, StepUse step)
    {
        var max = recipe.Catalog.Find(step.Use) is { } descriptor ? BlockLimits.MaxDuration(descriptor) : BlockLimits.Fallback;
        return StateTimers.Timeout(state, open, _deskTimeout) is { } timeout && timeout < max ? timeout : max;
    }

    /// <summary>The activity's result as the trigger the engine reports: a completed result, or step.failed for a failure, a timeout or an invalid result.</summary>
    private static async Task<Trigger> Resulting(WorkflowInstance instance, OpenRun open, StepUse step, Task<BlockStepOutcome> running, Interpreting context)
    {
        try
        {
            var outcome = await running.ConfigureAwait(true);
            return outcome is { Outcome: { } name, Outputs: { } outputs, Failure: null }
                ? BlockStepTriggers.Completed(instance, context.Recipe, context.Decisions, new BlockCompletion(step.Use, open.Token, name, outputs, outcome.Decision))
                : BlockStepTriggers.Failed(step.Use, open.Token, outcome.Failure ?? "the Step returned nothing");
        }
        catch (ActivityFailureException failed)
        {
            return BlockStepTriggers.Failed(step.Use, open.Token, failed.InnerException switch
            {
                TimeoutFailureException => "timeout",
                ApplicationFailureException application => "failed: " + (application.ErrorType ?? "error"),
                { } other => "failed: " + other.GetType().Name,
                null => "failed",
            });
        }
    }

    // Synchronous on purpose: CancelAsync would run the token's callbacks off the workflow scheduler, which Temporal fails the task for.
    private static void Cancel(CancellationTokenSource cancel) => cancel.Cancel();

    /// <summary>Cancels the activity and lets it settle (Temporal's TryCancel completes the call at once).</summary>
    private static async Task Abandon(Task<BlockStepOutcome> running, CancellationTokenSource cancel)
    {
        Cancel(cancel);
        try
        {
            _ = await running.ConfigureAwait(true);
        }
        catch (Exception e) when (e is OperationCanceledException or FailureException)
        {
            // boundary: the cancelled activity's outcome is not wanted
        }
    }
}
