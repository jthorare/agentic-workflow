using System.Globalization;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.Launching;
using Aw.Sindri.Features.ListInstances;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.Launch;
using Aw.Sindri.Tests.ListInstances;
using Aw.Sindri.Tests.Views;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>
/// The frozen check for the read side of a stalled agent (#1265, ask 2). An agent whose session sat idle and was ended with no report reads as a working
/// one in 'workflow list', as healthy in status and doctor, and as "a dependency that is down" once it parks. Each read says it went idle with no report.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class StalledAgentReadSideCheckTests
{
    private const string ProduceStep = "step.produce";
    private const string DraftStep = "step.draft";

    private static readonly InstanceAddress _address = InstanceAddress.From($"{A.FirstItemId.Value}/step/1");

    private readonly ViewsHarness _v = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The ledger as the idle sweep leaves it: a Step's launch started, then its session ended as idle.</summary>
    private async Task LaunchEndedIdle()
    {
        _ = await _v.Slice.HoldLease();
        await _v.Item(repo: null);
        await _v.Request(A.FirstLaunchId);
        await _v.Start(A.FirstLaunchId, instance: _address, state: "draft", attempt: 1);
        await _v.End(SessionEndReason.Idle, A.FirstLaunchId);
    }

    private static StepFailed IdleFailure(int entry, int run) =>
        new("service:agent:" + ProduceStep, RunToken.For(entry, run), "idle", Tokens: 0, Sha(string.Create(CultureInfo.InvariantCulture, $"idle\n{entry}.{run}")))
        {
            Detail = "agent-stalled: the agent sat idle with no report",
        };

    /// <summary>The instance after its agent went idle three times: the third failure has used the retries, so it parks.</summary>
    private static WorkflowInstance ParkedAfterIdle()
    {
        var instance = At(Step, "produce");
        var run = instance.Frame.Run!;
        var third = instance with { Frame = instance.Frame with { Run = run with { Token = RunToken.For(instance.Frame.Entry, 3), Retries = 2 } } };
        return Assert.IsType<Applied>(Decide(third, Step, IdleFailure(1, 3))).Changes[0].After;
    }

    [Fact]
    public void The_park_question_for_an_idle_agent_says_it_went_idle_with_no_report()
    {
        var question = ParkedAfterIdle().Suspension!.Question;

        Assert.StartsWith($"{ProduceStep}'s agent session went idle with no report (agent-stalled), relaunched 2 times; the last error: ", question, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_running_row_whose_agent_launch_ended_idle_says_stalled_not_drafting()
    {
        await LaunchEndedIdle();
        var instance = At(Step, "produce");
        var running = InstanceViews.Of(instance with { Frame = instance.Frame with { Run = instance.Frame.Run! with { Runtime = StepRuntime.Agent } } }, StatusCases.Recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext());
        var view = running with
        {
            Address = _address,
            ItemId = ItemKey.From(A.FirstItemId.Value),
            Subject = new SubjectRef("item", A.FirstItemId.Value, A.FirstItemId.Value, Href: null),
            Step = new StepDeclaration("active", DraftStep, DraftStep),
            AgentRun = new OpenRun(RunToken.For(1, 1), StepRuntime.Agent, A.FirstLaunchId.Value, Task: null),
        };
        var service = new ListInstancesService(
            new NoDirectory(),
            new RecordingInstances([new InstanceRow(view.Address.Value, view.State, NeedsYou: false)]),
            new EndedInstanceViews(new FixedReader([view])),
            _v.Views,
            new FixedCaller(current: null),
            rowBuilder: new InstanceRows(new FakeTimeProvider()));

        var now = (await service.Handle(new ListInstancesRequest(), Token)).Value.Instances[0].Now;

        Assert.Contains("stalled", now, StringComparison.Ordinal);
        Assert.Contains("no report", now, StringComparison.Ordinal);
        Assert.DoesNotContain("drafting", now, StringComparison.Ordinal);
    }

    [Fact]
    public void A_row_parked_after_idle_stops_says_the_agent_stalled_not_a_dependency_is_down()
    {
        var view = InstanceViews.Of(ParkedAfterIdle(), StatusCases.Recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext());

        var now = InstanceSummaries.Of(view, new InstanceSummaryContext(StatusCases.Now)).Now;

        Assert.Contains("stalled", now, StringComparison.Ordinal);
        Assert.DoesNotContain("dependency that is down", now, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Status_health_is_not_healthy_while_a_stalled_agent_session_exists()
    {
        var h = _v.Slice;
        var approved = SliceHarness.Profile("5s");
        h.Profiles.Live = approved;
        _ = await h.HoldLease();
        _ = await new ApproveProfileService(h.Profiles, h.Profiles, h.Held, h.Store, h.Events, new ProfileChecks(FakeContextEngine.Instance, new FixedAgentCatalog())).Handle(new ApproveProfileRequest(approved.Hash, h.Confirm(approved.Hash)), Token);
        await _v.Item(repo: null);
        await _v.Request(A.FirstLaunchId);
        await _v.Start(A.FirstLaunchId, instance: _address, state: "draft", attempt: 1);
        await _v.End(SessionEndReason.Idle, A.FirstLaunchId);
        _v.Publish(stalledAt: null);   // no per-launch marker files, and the launch is no longer live: only the launches view remembers it
        var service = new GetStatusService(h.Held, new ProfileReads(h.Profiles, h.Profiles), h.Store, h.Events, _v.Progress, new HealthServiceRows(_v.Views, []), new InMemorySessionSnapshot());

        var health = (await service.Handle(new GetStatusRequest(), Token)).Value.Health!;

        Assert.NotEqual(HealthWord.Healthy, health.Word);
        Assert.Contains(health.Problems, static p => p.Text.Contains("idle", StringComparison.Ordinal) && p.Text.Contains("no report", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Doctor_warns_about_a_stalled_agent_session()
    {
        await LaunchEndedIdle();
        var report = new LaunchShowReport("Print", "detected", "/home/demo/launcher.json", [], []);
        var setup = new ScriptedLaunchSetup(Kernel.Results.Success(new LaunchInitReport("print", "detected", Path: null, Json: "{}")), Kernel.Results.Success(report));
        var reader = new LaunchTargetsReader(_v.Slice.Store, _v.Slice.Events, _v.Slice.Profiles, new FixedLaunchSpend(), _v.Progress);
        var checks = new LauncherDoctorChecks(setup, new LaunchViews(_v.Views), reader, _v.Slice.Clock);

        var rows = await checks.Run(Token);

        Assert.Contains(rows, static r => r.State != CheckState.Ok && r.Detail.Contains("idle", StringComparison.Ordinal) && r.Detail.Contains("no report", StringComparison.Ordinal));
    }
}
