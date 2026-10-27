using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The agent runtime in the interpreter (engine P8 U3; spec §5.3, §6.5): a Step whose descriptor says 'agent' is launched once, watched, and
/// closed by its session's own 'complete' report, or ended as 'exit' or 'lost'. FakeLauncher, FakeAgentSessions and FakeRunDiff stand in for the
/// real adapters; the polls are 100 ms.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(nameof(WallClockAlone))]
public sealed class AgentRuntimeInterpreterTests : AgentRuntimeTestBase
{
    [Fact(Timeout = 120_000)]
    public async Task An_agent_step_launches_once_and_its_report_moves_the_instance()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            var view = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            var result = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(Report(view.Run!.Value)), new WorkflowUpdateOptions(new string('a', 32)));
            await WaitFor(() => _launcher.Stops.Count == 1, ct);

            var request = Assert.Single(_launcher.Requests);
            Assert.Equal(("done", false, LaunchMode.Open, rig.Worktree), (result.Instance.State, request.Attended, request.Mode, request.Worktree));
            Assert.Equal(rig.Launch, request.Launch);
            Assert.Equal((StepPermissionMode.Default, null), (request.Step?.Policy?.Mode, request.Step?.Policy?.WritablePath));   // not plan mode: the session must be able to report
            Assert.Equal(["Bash(sindri workflow complete:*)", "Bash(sindri workflow status:*)", "Bash(sindri workflow events:*)"], request.Step?.Policy?.AllowedTools, StringComparer.Ordinal);
            Assert.Equal(rig.Launch, Assert.Single(_launcher.Stops));   // the session closes with its run
            Assert.Equal(0, _launcher.SessionCount);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_session_that_ends_without_a_report_is_step_failed_exit_and_the_Step_runs_again()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            var first = rig.Launch;
            _sessions.End(first, SessionEndReason.Exit, exitCode: 1);

            Assert.Equal("exit", await rig.FailureReason(ct));
            await WaitFor(() => _launcher.Requests.Count == 2, ct);   // the engine runs the Step again, and asks no one
            await WaitFor(() => _launcher.Stops.Contains(first), ct);   // the run closed, so its session is stopped
            Assert.Null((await handle.QueryAsync(static wf => wf.GetInstance())).Question);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task Three_sessions_that_end_without_a_report_park_the_Step_as_unavailable_not_as_a_question()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await EndEveryRun(SessionEndReason.Exit, exitCode: 1, ct);

            var parked = await WorkflowsHarness.Until(handle, static v => v.Question is not null);

            Assert.Equal(ParkReason.Unavailable, parked.Question?.Reason);
            Assert.Contains("agent session is gone (exit), relaunched 2 times", parked.Question?.Question, StringComparison.Ordinal);
            Assert.Equal(3, _launcher.Requests.Count);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_session_ended_for_a_refused_report_fails_the_Step_with_the_refusal_in_the_question()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            var refusals = rig.Sindri.GetRequiredService<ReportRefusals>();
            await EndEveryRun(SessionEndReason.Exit, exitCode: 0, ct, launch => refusals.Record(launch, "fact:design is a payload, so it must be a JSON object or array, and it isn't JSON at all"));

            var parked = await WorkflowsHarness.Until(handle, static v => v.Question is not null);

            Assert.Equal("exit", await rig.FailureReason(ct));
            Assert.Contains("the last error: exit: fact:design is a payload", parked.Question?.Question, StringComparison.Ordinal);
            Assert.Contains("isn't JSON at all", parked.Question?.Question, StringComparison.Ordinal);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_lost_session_is_step_failed_lost()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await EndEveryRun(SessionEndReason.Lost, exitCode: null, ct);

            _ = await WorkflowsHarness.Until(handle, static v => v.Question is not null);

            Assert.Equal("lost", await rig.FailureReason(ct));
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_worker_restart_reattaches_and_starts_no_second_session()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        var handle = await rig.Start();
        using (var first = rig.Worker())
        {
            await first.ExecuteAsync(async () =>
            {
                _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
                await Launched(ct);
            }, ct);   // the worker stops here, with the session live
        }

        using var second = rig.Worker();
        await second.ExecuteAsync(async () =>
        {
            var first = rig.Launch;
            _sessions.End(first, SessionEndReason.Exit, exitCode: 0);

            Assert.Equal("exit", await rig.FailureReason(ct));   // the engine then runs the Step again, a new run with its own launch
            _ = Assert.Single(_launcher.Requests, r => r.Launch == first);   // the replay launched nothing for the first run
            await AgentRig.Replayed(handle);
        }, ct);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_relaunch_of_the_live_session_with_no_chosen_id_keeps_the_recorded_session_and_does_not_park()
    {
        var ct = TestContext.Current.CancellationToken;
        var chosen = SessionId.From("sess-i28");
        _launcher.PreChosenSession = chosen;
        _launcherSeen = new SkippedOnRelaunch(_launcher);   // #295 I28: a launcher that did not start the session answers it with a Skipped viewer and no chosen id
        _diffSeen = new FailsOnce(_diff);   // the first launch attempt dies after the outcome is recorded, so the activity retries
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            var view = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await WaitFor(() => _launcher.Requests.Count == 2, ct);
            await Launched(ct);
            var parked = (await handle.QueryAsync(static wf => wf.GetInstance())).Question;

            Assert.Null(parked);
            Assert.Equal([chosen.Value], await rig.StartedSessions(ct));   // one outcome, and it keeps the id of the first
            _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(Report(view.Run!.Value)), new WorkflowUpdateOptions(new string('b', 32)));
            await WaitFor(() => _launcher.Stops.Count == 1, ct);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task Takeover_keeps_the_session_and_handback_resumes_the_watch()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await WaitFor(() => _launcher.Requests.Count > 0, ct);   // the takeover may land in the launch phase or in the watch: the invariant below holds for both
            var takeover = new TakeoverRequested("human:cli", new CommandStamp(CommandNonce.From(new string('1', 32)), new string('0', 64)));
            _ = await handle.ExecuteUpdateAsync(wf => wf.TakeoverAsync(takeover), new WorkflowUpdateOptions(new string('1', 32)));
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "taken-over");
            Assert.Empty(_launcher.Stops);   // the takeover keeps the session

            var handback = new HandbackRequested("human:cli", new CommandStamp(CommandNonce.From(new string('2', 32)), new string('0', 64)));
            _ = await handle.ExecuteUpdateAsync(wf => wf.HandbackAsync(handback), new WorkflowUpdateOptions(new string('2', 32)));
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "write" && v.Run is not null);
            await WaitFor(() => _launcher.SessionCount == 1, ct);
            Assert.Equal(1, _launcher.SessionCount);   // one live session, however often the launch was requested
            Assert.Empty(_launcher.Stops);   // and the handback stopped nothing
            var first = rig.Launch;
            _sessions.End(first, SessionEndReason.Exit, exitCode: 0);   // the resumed watch sees it

            var reason = await rig.FailureReason(ct);   // waits for the mirror row of the failure

            Assert.Equal("exit", reason);
            Assert.Equal(first, _launcher.Requests.Select(static r => r.Launch).First());   // a relaunch after the handback is the same launch id, which the launcher keeps idempotent
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_cancel_of_a_taken_over_run_stops_the_session_the_takeover_kept()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            var takeover = new TakeoverRequested("human:cli", new CommandStamp(CommandNonce.From(new string('1', 32)), new string('0', 64)));
            _ = await handle.ExecuteUpdateAsync(wf => wf.TakeoverAsync(takeover), new WorkflowUpdateOptions(new string('1', 32)));
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "taken-over");
            Assert.Empty(_launcher.Stops);

            _ = await Cancel(handle, 'e');

            await WaitFor(() => _launcher.Stops.Contains(rig.Launch), ct);
            Assert.Equal(0, _launcher.SessionCount);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_failed_launch_is_step_failed_launch_and_the_launchers_words_reach_the_question()
    {
        var ct = TestContext.Current.CancellationToken;
        _launcherSeen = new FailsEveryLaunch(_launcher, SindriErrors.LaunchAgentMissing.With("the agent is gone"));   // the engine tries three times, then parks unavailable
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            var parked = await WorkflowsHarness.Until(handle, static v => v.Question is not null);

            Assert.Contains("the agent is gone", parked.Question?.Question, StringComparison.Ordinal);
            Assert.Equal(("launch", "agent"), await rig.Failure(ct));
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_target_the_profile_cannot_resolve_is_step_failed_launch()
    {
        var ct = TestContext.Current.CancellationToken;
        _factRepo = "elsewhere";   // fact:item names a repo the approved profile doesn't list
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            var parked = await WorkflowsHarness.Until(handle, static v => v.Question is not null);

            Assert.Contains("SND-LAUNCH-004", parked.Question?.Question, StringComparison.Ordinal);
            Assert.Equal(("launch", "agent"), await rig.Failure(ct));
            Assert.Empty(_launcher.Requests);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_write_scope_step_launches_with_its_worktree_writable_and_the_commit_rules()
    {
        var ct = TestContext.Current.CancellationToken;
        _root = "workflow.dev-agent-write";
        _also = [_writeRecipe];
        _more = new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:change"] = "the change", ["fact:review"] = "the review" };
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "fix");
            await Launched(ct);

            var request = Assert.Single(_launcher.Requests);

            Assert.Equal((StepPermissionMode.Default, request.Worktree), (request.Step?.Policy?.Mode, request.Step?.Policy?.WritablePath));
            var allowed = request.Step?.Policy?.AllowedTools ?? [];
            Assert.Contains("Bash(git commit:*)", allowed, StringComparer.Ordinal);
            Assert.Contains("Bash(sindri workflow complete:*)", allowed, StringComparer.Ordinal);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task Cancel_stops_the_launch()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            var stamp = new CommandStamp(CommandNonce.From(new string('c', 32)), new string('1', 64));

            var cancelled = await handle.ExecuteUpdateAsync(wf => wf.CancelAsync(new CancelRequested("human:cli", CancelOrigin.Command, stamp, Sequence: null, Parent: null)), new WorkflowUpdateOptions(new string('c', 32)));

            Assert.Equal("cancelled", cancelled.Instance.State);
            await WaitFor(() => _launcher.Stops.Contains(rig.Launch), ct);
            Assert.Equal(0, _launcher.SessionCount);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task An_accepted_report_over_budget_keeps_its_transition_and_is_not_rerun()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            var view = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            _sessions.Spend(rig.Launch, sample: 1, tokens: 5_000);   // the state's budget is 1000

            var result = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(Report(view.Run!.Value)), new WorkflowUpdateOptions(new string('b', 32)));

            Assert.Equal(("done", null), (result.Instance.State, result.Instance.Question?.Reason));   // the report wins; the overage is recorded
            _ = Assert.Single(_launcher.Requests);   // and no second session ran
        });
    }

    /// <summary>A history recorded after the block runtime and before this one has no 'p8-agent-runtime' marker: it replays on the desk path, as recorded.</summary>
    [Fact(Timeout = 120_000)]
    public async Task A_history_before_P8_replays_on_the_desk()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var stream = typeof(RecipeFixtures).Assembly.GetManifestResourceStream("histories/dev-timeout-before-p8.json")!;
        using var reader = new StreamReader(stream);
        var history = Temporalio.Common.WorkflowHistory.FromJson("T-1/workflow.dev-timeout/1", await reader.ReadToEndAsync(ct));

        var replayed = await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>()).ReplayWorkflowAsync(history, throwOnReplayFailure: false, ct);

        Assert.Null(replayed.ReplayFailure);
    }

    // The launcher has been asked and the launch activity has had time to finish; a command that lands before it does is applied in the launch phase, which the tests that need the watch avoid.
    [Fact(Timeout = 120_000)]
    public async Task An_agent_report_that_enters_a_child_wait_starts_the_child()
    {
        var ct = TestContext.Current.CancellationToken;
        _root = "workflow.dev-agent-parent";
        _also = [ChildRecipes.Child(), _parentRecipe];
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            var view = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(Report(view.Run!.Value)), new WorkflowUpdateOptions(new string('a', 32)));

            var parent = await WorkflowsHarness.Until(handle, static v => v.State == "run" && v.Children.Count == 1);

            Assert.Equal("t-child", Assert.Single(parent.Children).Address.Value.Split('/')[^1].Split('.')[0]);
        });
    }
}
