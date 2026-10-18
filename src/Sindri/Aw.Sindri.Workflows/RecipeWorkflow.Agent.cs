using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
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
/// The agent runtime of <see cref="RecipeWorkflow"/> (engine P8 U3; spec §5.3, §6.5): a Step the build runs on an agent session is launched once
/// ('LaunchAgentStep', id 'agent-launch:{token}:{n}'), watched ('WatchAgentStep'), and closed ('CloseAgentStep') when the session reports with the
/// 'complete' Update (validated like a desk report) or ends. Updates and timers are applied meanwhile as the block runtime does. A park or a takeover
/// leaves the session running and the watch resumes after it; a cancel or an end of the run stops the session.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    private static readonly AgentCall _launchCall = new("agent-launch", TimeSpan.FromMinutes(2), Heartbeat: null, new RetryPolicy { InitialInterval = TimeSpan.FromSeconds(1), MaximumAttempts = 3 });
    private static readonly AgentCall _watchCall = new("agent-watch", TimeSpan.FromDays(7), TimeSpan.FromSeconds(10), new RetryPolicy { InitialInterval = TimeSpan.FromSeconds(1), MaximumInterval = TimeSpan.FromSeconds(10) });   // a heartbeat timeout or a restarted worker retries the watch, which attaches to the same session
    private static readonly AgentCall _closeCall = new("agent-close", TimeSpan.FromMinutes(2), Heartbeat: null, new RetryPolicy { InitialInterval = TimeSpan.FromSeconds(1), MaximumAttempts = 3 });
    private (string Token, AgentLaunched Launched)? _agent;   // the open run's launch, kept across a park or a takeover so the watch resumes without a second launch

    private enum AgentEnd
    {
        Finished,
        Reported,
        Over,
    }

    private async Task<WorkflowInstance> RunAgentStep(WorkflowInstance instance, OpenRun open, Interpreting context)
    {
        if (_agent is not { } held || !string.Equals(held.Token, open.Token.Value, StringComparison.Ordinal))
        {
            var (launched, done) = await LaunchPhase(instance, open, context).ConfigureAwait(true);
            if (done)
            {
                return launched;   // the run is over or its launch failed: the instance is already decided
            }

            instance = launched;
        }

        return await WatchPhase(instance, open, context).ConfigureAwait(true);
    }

    private async Task<WorkflowInstance> WatchPhase(WorkflowInstance instance, OpenRun open, Interpreting context)
    {
        var (state, step) = StateOf(instance, context);
        var running = new AgentRunning(open, state, AgentRunOf(instance, open, step), _agent!.Value.Launched);   // why: LaunchPhase set it for this run's token before the watch
        var launch = AgentLaunches.LaunchIdFor(running.Run).Value;
        using var cancel = new CancellationTokenSource();
        var watching = Workflow.ExecuteActivityAsync((AgentStepActivities a) => a.WatchAsync(launch), AgentOptions(_watchCall, open, state, cancel.Token));
        var (applied, end) = await AwaitAgent(instance, open, new Awaited(watching, cancel, Reports: true), context).ConfigureAwait(true);
        if (end == AgentEnd.Over)
        {
            return await StopIfClosed(applied, running).ConfigureAwait(true);
        }

        if (end == AgentEnd.Reported)
        {
            await Abandon(watching, cancel).ConfigureAwait(true);   // the report ends the watch
            return await Reported(applied, running, context).ConfigureAwait(true);
        }

        var ended = await watching.ConfigureAwait(true);
        var closed = await Close(running, measure: true).ConfigureAwait(true);
        var reason = ended switch
        {
            { EndedAs: AgentSessionEnd.Limit, Limit: { } limit } => limit.Text,
            { EndedAs: AgentSessionEnd.Lost } => "lost",
            _ => "exit",
        };
        return await Decided(applied, open, AgentFailed(open, step, reason, closed.Tokens), reportId: null, context).ConfigureAwait(true);
    }

    // The session's own report: closes the run (usage, diff signal, stop), then decides it like a desk report and answers the Update that carried it.
    private async Task<WorkflowInstance> Reported(WorkflowInstance instance, AgentRunning running, Interpreting context)
    {
        var report = _report ?? throw new InvalidOperationException("an agent report was signalled but is gone"); // why: AwaitAgent returned Reported only with _report set
        var reportId = _reportId;
        _report = null;
        _reportId = null;
        var closed = await Close(running, measure: true).ConfigureAwait(true);
        return await Decided(instance, running.Open, report with { Tokens = closed.Tokens, EmptyDiff = closed.EmptyDiff }, reportId, context).ConfigureAwait(true);
    }

    // Decides the trigger like RunDeskStep and RunBlockStep: mirror the change, emit its effects, and hand the result to the 'complete' Update that carried a report.
    private async Task<WorkflowInstance> Decided(WorkflowInstance instance, OpenRun open, Trigger trigger, string? reportId, Interpreting context)
    {
        var before = instance;
        var decision = TransitionDecision.Decide(new DecisionInput(instance, context.Recipe, trigger, GateAnswers.None, context.Decisions));
        if (decision is Rejected rejected)
        {
            Settle(reportId, new Reported(Result: null, rejected.Error));   // the validator saw the same instance, so this is a bug guard
            return instance;
        }

        instance = After(instance, decision);
        if (ReferenceEquals(before, instance))
        {
            Settle(reportId, new Reported(new CommandResult(GetInstance(), Happened(before, instance, decision)), Refusal: null));
            return reportId is null
                ? throw new ApplicationFailureException($"the agent result of run {open.Token.Value} changed nothing ({decision.GetType().Name})", nonRetryable: true)   // why: leaving the run open would run the Step again
                : instance;
        }

        instance = Enter(instance, trigger.Actor);
        await Mirror(DeskRow(before, instance, trigger, open, decision), NeedsYou(instance)).ConfigureAwait(true);
        Emit(before, instance, EffectsOf(decision));
        Settle(reportId, new Reported(new CommandResult(GetInstance(), Happened(before, instance, decision)), Refusal: null));
        return instance;
    }

    // A run that was cancelled or replaced closes: its session is stopped. A park or a takeover keeps the session (D11).
    private async Task<WorkflowInstance> StopIfClosed(WorkflowInstance instance, AgentRunning running)
    {
        if (instance.Ended || (instance.Suspension is null && instance.Frame.Run?.Token != running.Open.Token))
        {
            _ = await Close(running, measure: false).ConfigureAwait(true);
        }

        return instance;
    }

    private async Task<AgentClosed> Close(AgentRunning running, bool measure)
    {
        var close = new AgentCloseRun(AgentLaunches.LaunchIdFor(running.Run).Value, running.Launched?.Worktree, running.Launched?.StartHead, measure);
        try
        {
            return await Workflow.ExecuteActivityAsync((AgentStepActivities a) => a.CloseAsync(close), AgentOptions(_closeCall, running.Open, running.State, CancellationToken.None)).ConfigureAwait(true);
        }
        catch (ActivityFailureException)
        {
            return new AgentClosed(Tokens: 0, EmptyDiff: false);   // boundary: a close that can't read the usage must not wedge the instance; the run is decided with 0 tokens
        }
    }

    private ActivityOptions AgentOptions(AgentCall call, OpenRun open, RecipeState state, CancellationToken token)
    {
        _runs++;
        return new ActivityOptions
        {
            ActivityId = string.Create(CultureInfo.InvariantCulture, $"{call.Kind}:{open.Token.Value}:{_runs}"),   // the run token is in every id
            TaskQueue = StepTaskQueue.For(state, Workflow.Info.TaskQueue),
            StartToCloseTimeout = call.Limit,
            HeartbeatTimeout = call.Heartbeat,
            RetryPolicy = call.Retry,
            CancellationToken = token,
        };
    }

    private static (RecipeState State, StepUse Step) StateOf(WorkflowInstance instance, Interpreting context)
    {
        var state = context.Recipe.States[instance.Frame.State];
        return (state, state.Step ?? throw new InvalidOperationException($"'{instance.Frame.State}' has an agent run but no Step")); // why: only an active state opens a run
    }

    private AgentStepRun AgentRunOf(WorkflowInstance instance, OpenRun open, StepUse step)
    {
        var address = _start!.WorkflowId(); // why: the loop runs only after RunAsync set _start
        var descriptor = _recipe!.Catalog.Find(step.Use); // why: as above
        var writes = descriptor is not null && descriptor.Capabilities.HasFlag(Capability.WriteScope);
        return new AgentStepRun(WorkflowInstanceId.Of(address), address, open.Token, new StepRef(instance.Frame.State, step.Use.Value), writes, descriptor?.LoginSource);
    }

    private StepFailed AgentFailed(OpenRun open, StepUse step, string reason, int tokens)
    {
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"failed\n{reason}\n{_runs}"))));
        return new StepFailed("service:agent:" + step.Use.Value, open.Token, reason, tokens, digest);
    }

    // Waits for the activity, an accepted report (when the wait takes reports), or what ends the run, applying the commands and timers an Update delivers meanwhile.
    private async Task<(WorkflowInstance Instance, AgentEnd End)> AwaitAgent(WorkflowInstance instance, OpenRun open, Awaited waiting, Interpreting context)
    {
        while (true)
        {
            await ExpireHeld(instance, wait: null).ConfigureAwait(true);
            if (waiting.Reports && _report is not null)
            {
                return (instance, AgentEnd.Reported);
            }

            if (_submitted.Count > 0 || _timers.Fired)
            {
                instance = await ApplyMeanwhile(instance, context).ConfigureAwait(true);
                if (RunOver(instance, open))
                {
                    await Abandon(waiting.Running, waiting.Cancel).ConfigureAwait(true);
                    return (instance, AgentEnd.Over);
                }

                continue;
            }

            if (waiting.Running.IsCompleted)
            {
                return (instance, AgentEnd.Finished);
            }

            await WaitForAgent(waiting).ConfigureAwait(true);
        }
    }

    private async Task WaitForAgent(Awaited waiting)
    {
        var seen = _arrivals;
        bool Woken() => waiting.Running.IsCompleted || (waiting.Reports && _report is not null) || _submitted.Count > 0 || _timers.Fired || _arrivals != seen;
        _ = await Workflow.WaitConditionAsync(Woken, NextExpiry() ?? Timeout.InfiniteTimeSpan).ConfigureAwait(true);
    }

    /// <summary>Cancels the activity and lets it settle.</summary>
    private static async Task Abandon(Task running, CancellationTokenSource cancel)
    {
        Cancel(cancel);
        try
        {
            await running.ConfigureAwait(true);
        }
        catch (Exception e) when (e is OperationCanceledException or FailureException)
        {
            // boundary: the cancelled activity's outcome is not wanted
        }
    }

    private sealed record AgentCall(string Kind, TimeSpan Limit, TimeSpan? Heartbeat, RetryPolicy Retry);

    private sealed record Awaited(Task Running, CancellationTokenSource Cancel, bool Reports);

    private sealed record AgentRunning(OpenRun Open, RecipeState State, AgentStepRun Run, AgentLaunched? Launched);
}
