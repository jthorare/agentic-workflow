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
using Aw.Sindri.Core.Workflow.Ids;
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
    private AgentRunning? _held;   // the launched run whose session is still live, so the interpreter loop can stop it when the instance ends or the run is replaced while it is parked or taken over (D11)
    private (string Token, AgentLaunched Launched)? _agent;   // the open run's launch, kept across a park or a takeover so the watch resumes without a second launch

    private static readonly BlockId _ciFix = BlockId.From("step.pr.ci-fix");   // launches in 'fact:workspace' on its own marker; every other agent Step does on the phase-workspace marker (LN-WF2)
    private static readonly FactKey _workspaceKey = FactKey.From("fact:workspace");
    private bool? _agentWorkspace;
    private bool AgentWorkspace => _agentWorkspace ??= Workflow.Patched("ci-fix-agent-workspace");   // read only for ci-fix, after every other marker
    private bool? _phaseWorkspace;
    private bool PhaseWorkspaceLaunch => _phaseWorkspace ??= Workflow.Patched("ln-wf2-phase-workspace-launch");   // LN-WF2: read for every other agent Step; a history without it launched them in their own worktrees

    private enum AgentEnd
    {
        Finished,
        Reported,
        Over,
    }

    /// <summary>The Steps this build runs off the desk, each with its runtime: the block map when its marker is set, then the agent map when its marker is set (one map, P8 D5).</summary>
    private static Dictionary<BlockId, StepRuntime> Runtimes(LoadedRecipe loaded, bool block, bool agent)
    {
        var runtimes = (block ? loaded.BlockRuntimes ?? [] : []).ToDictionary(static b => b, static _ => StepRuntime.Block);
        foreach (var step in agent ? loaded.AgentRuntimes ?? [] : [])
        {
            runtimes[step] = StepRuntime.Agent;
        }

        return runtimes;
    }

    private async Task<WorkflowInstance> RunAgentStep(WorkflowInstance instance, OpenRun open, Interpreting context)
    {
        if (_agent is not { } held || !string.Equals(held.Token, open.Token.Value, StringComparison.Ordinal))
        {
            var filling = await FillInputs(instance, open, context.Recipe.States[instance.Frame.State]).ConfigureAwait(true);   // #370: the Step's inputs, as the desk and block arms fill them, before the session starts
            if (filling.Parked is { } parked)
            {
                return parked;
            }

            if (filling.Failed is { } failed)
            {
                return await Decided(instance, open, failed, reportId: null, context).ConfigureAwait(true);
            }

            var (launched, done) = await LaunchPhase(instance, open, context).ConfigureAwait(true);
            if (done)
            {
                return launched;   // the run is over or its launch failed: the instance is already decided
            }

            instance = launched;
        }

        return await WatchPhase(instance, open, context).ConfigureAwait(true);
    }

    // Launches the session. Done is true when the run ended first or the launch failed; the instance is then the decided one, else it has its launch filled in.
    private async Task<(WorkflowInstance Instance, bool Done)> LaunchPhase(WorkflowInstance instance, OpenRun open, Interpreting context)
    {
        var (state, step) = StateOf(instance, context);
        using var cancel = new CancellationTokenSource();
        var run = AgentRunOf(instance, open, step);
        var item = instance.Facts.TryGetValue(FactKey.From("fact:item"), out var fact) ? fact : null;
        var workspace = (step.Use == _ciFix ? AgentWorkspace : PhaseWorkspaceLaunch) && instance.Facts.TryGetValue(_workspaceKey, out var held) ? held : null;   // LN-WF2: an instance that holds a workspace (the phase's worktree, or docs sync's own) runs its agent Steps there
        var launching = Workflow.ExecuteActivityAsync((AgentStepActivities a) => a.LaunchAsync(new AgentLaunchRun(run, item, workspace)), AgentOptions(_launchCall, open, state, cancel.Token));
        var (applied, end) = await AwaitAgent(instance, open, new Awaited(launching, cancel, Reports: false), context).ConfigureAwait(true);
        if (end == AgentEnd.Over)
        {
            return (await StopIfClosed(applied, new AgentRunning(open, state, run, Launched: null)).ConfigureAwait(true), true);
        }

        var launched = await Launching(launching).ConfigureAwait(true);
        if (launched.Failure is { } failure)
        {
            return (await Decided(applied, open, AgentFailed(open, step, StepFailed.LaunchReason, tokens: 0, $"{failure.Code}: {failure.Message}"), reportId: null, context).ConfigureAwait(true), true);
        }

        _agent = (open.Token.Value, launched);
        _held = new AgentRunning(open, state, run, launched);
        var filled = applied with { Frame = applied.Frame with { Run = open with { Launch = AgentLaunches.LaunchIdFor(run).Value } } };
        _instance = filled;   // why: the run's launch is part of the instance the Query and the validator read; no timer or state changed
        return (filled, false);
    }

    private static async Task<AgentLaunched> Launching(Task<AgentLaunched> launching)
    {
        try
        {
            return await launching.ConfigureAwait(true);
        }
        catch (ActivityFailureException e)
        {
            return new AgentLaunched(Worktree: null, StartHead: null, new AgentLaunchFailure(AgentLaunchFailureKind.StepFailed, "SND-LAUNCH-007", $"the launch activity failed: {e.InnerException?.Message ?? e.Message}"));
        }
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
        var reason = ended.EndedAs == AgentSessionEnd.Lost ? "lost" : "exit";
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
        Spawn(decision);   // a report that enters a child wait starts its child, as the desk, block and loop arms do
        Settle(reportId, new Reported(new CommandResult(GetInstance(), Happened(before, instance, decision)), Refusal: null));
        return instance;
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
        var writes = _recipe!.Catalog.Find(step.Use) is { } descriptor && descriptor.Capabilities.HasFlag(Capability.WriteScope); // why: as above
        return new AgentStepRun(WorkflowInstanceId.Of(address), address, open.Token, new StepRef(instance.Frame.State, step.Use.Value), writes);
    }

    private StepFailed AgentFailed(OpenRun open, StepUse step, string reason, int tokens, string? detail = null)
    {
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"failed\n{reason}\n{_runs}"))));
        return new StepFailed("service:agent:" + step.Use.Value, open.Token, reason, tokens, digest) { Detail = detail };
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
}
