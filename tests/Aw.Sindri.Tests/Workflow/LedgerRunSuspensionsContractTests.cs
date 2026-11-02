using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The ledger adapter of the run-suspension port, over the in-memory ledger and real 'launch.*' and 'workflow.*' rows: the contract the fake honours, plus the cases only the fold can get wrong.</summary>
[Lane(TestLane.Unit)]
public sealed class LedgerRunSuspensionsContractTests : RunSuspensionsContract
{
    private static readonly InstanceAddress _mine = InstanceAddress.From("T-1/step/1");
    private static readonly LaunchId _other = LaunchId.From("LCH-00000000000000000000000000000002");

    private readonly SliceHarness _slice = new();
    private LeaseGrant? _grant;
    private int _rows;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    protected override async ValueTask<IRunSuspensions> Create()
    {
        _grant = await _slice.HoldLease();
        return new LedgerRunSuspensions(_slice.Store, _slice.Events);
    }

    protected override async ValueTask Run(IRunSuspensions subject, LaunchId launch)
    {
        await Started(launch, _mine);
        await Workflow(_mine, EventTypes.WorkflowStarted, Frame(launch), suspension: null);
    }

    protected override async ValueTask Suspend(IRunSuspensions subject, LaunchId launch) => await Workflow(_mine, EventTypes.WorkflowTransition, frame: null, Park(SuspensionKind.Park));

    protected override async ValueTask Resume(IRunSuspensions subject, LaunchId launch) => await Workflow(_mine, EventTypes.WorkflowTransition, Frame(launch), suspension: null);

    [Fact]
    public async Task A_failed_launch_is_not_suspended()
    {
        var subject = await Create();
        await Run(subject, A.FirstLaunchId);
        await Suspend(subject, A.FirstLaunchId);
        await Append(EventTypes.LaunchFailed, new LaunchFailed(_other, A.FirstItemId, "SND-LAUNCH-007", "interrupted"), $"launch.outcome:{_other.Value}");
        await Workflow(_mine, EventTypes.WorkflowTransition, Frame(_other), Park(SuspensionKind.Park));

        var answer = await subject.IsSuspended(_other, Token);

        Expect.True(answer.IsSuccess && !answer.Value);
    }

    [Fact]
    public async Task An_attended_launch_is_not_suspended()
    {
        var subject = await Create();
        await Started(A.FirstLaunchId, instance: null);
        await Workflow(_mine, EventTypes.WorkflowStarted, Frame(A.FirstLaunchId), Park(SuspensionKind.Park));

        var answer = await subject.IsSuspended(A.FirstLaunchId, Token);

        Expect.True(answer.IsSuccess && !answer.Value);
    }

    [Theory]
    [InlineData(SuspensionKind.Escalation)]
    [InlineData(SuspensionKind.Takeover)]
    public async Task Takeover_and_escalation_count_as_suspended(SuspensionKind kind)
    {
        var subject = await Create();
        await Run(subject, A.FirstLaunchId);
        await Workflow(_mine, EventTypes.WorkflowTransition, frame: null, Park(kind));

        var answer = await subject.IsSuspended(A.FirstLaunchId, Token);

        Expect.True(answer.IsSuccess && answer.Value);
    }

    [Fact]
    public async Task A_launch_whose_run_was_replaced_is_not_suspended()
    {
        var subject = await Create();
        await Run(subject, A.FirstLaunchId);
        await Workflow(_mine, EventTypes.WorkflowTransition, Frame(_other), suspension: null);
        await Workflow(_mine, EventTypes.WorkflowTransition, frame: null, Park(SuspensionKind.Park));

        var first = await subject.IsSuspended(A.FirstLaunchId, Token);

        Expect.True(first.IsSuccess && !first.Value);
    }

    [Fact]
    public async Task An_ended_instance_is_not_suspended()
    {
        var subject = await Create();
        await Run(subject, A.FirstLaunchId);
        await Suspend(subject, A.FirstLaunchId);
        await Workflow(_mine, EventTypes.WorkflowTransition, new FrameSummary("done", Entry: 9, Run: null, Child: null), suspension: null, outcome: "done");

        var answer = await subject.IsSuspended(A.FirstLaunchId, Token);

        Expect.True(answer.IsSuccess && !answer.Value);
    }

    [Fact]
    public async Task A_child_instances_park_is_not_the_parents()
    {
        var subject = await Create();
        await Run(subject, A.FirstLaunchId);
        await Workflow(InstanceAddress.From("T-1/step/1/child.2"), EventTypes.WorkflowTransition, frame: null, Park(SuspensionKind.Park));

        var answer = await subject.IsSuspended(A.FirstLaunchId, Token);

        Expect.True(answer.IsSuccess && !answer.Value);
    }

    [Fact]
    public async Task A_row_without_detail_keeps_the_suspension()
    {
        var subject = await Create();
        await Run(subject, A.FirstLaunchId);
        await Suspend(subject, A.FirstLaunchId);
        await Append(EventTypes.WorkflowNotified, new WorkflowEventPayload(_mine.Value, "parked", "parked", "notify", Outcome: null, Run: null), Key(_mine));

        var answer = await subject.IsSuspended(A.FirstLaunchId, Token);

        Expect.True(answer.IsSuccess && answer.Value);
    }

    [Fact]
    public async Task An_unreadable_ledger_is_an_error()
    {
        _ = await Create();
        var subject = new LedgerRunSuspensions(new ReadFailingLedger(_slice.Store), _slice.Events);

        var answer = await subject.IsSuspended(A.FirstLaunchId, Token);

        Expect.True(answer.IsError);
    }

    private static FrameSummary Frame(LaunchId actor) => new("work", Entry: 1, Run: "1.1", Child: null, StepRuntime.Agent) { Actor = actor.Value };

    private static SuspensionSummary Park(SuspensionKind kind) => new(kind, Reason: null, ParkKind: null, Escalation: null, Mirrors: null);

    private ValueTask Started(LaunchId launch, InstanceAddress? instance) =>
        Append(EventTypes.LaunchStarted,
            new LaunchStarted(launch, A.FirstItemId, LaunchMode.Open, AgentId.From("claude-code"), SessionId: null, "sindri", new LaunchViewer("warp", "Warp", ViewerOutcomeKind.Opened, Code: null, Detail: null),
                ["tmux"], CatalogSource.Builtin, "sha256:abc", instance),
            $"launch.outcome:{launch.Value}");

    private ValueTask Workflow(InstanceAddress instance, string type, FrameSummary? frame, SuspensionSummary? suspension, string? outcome = null) =>
        Append(type,
            new WorkflowEventPayload(instance.Value, "work", "work", "t", outcome, Run: null,
                new WorkflowEventDetail(TriggerKey.For(WorkflowInstanceId.From("WFI-00000000000000000000000000000001"), "t"), "service:workflow", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null,
                    Answer: null, Amount: null, Reason: null, Gates: [], Evidence: [], Grant: null, Override: null, Refusal: null, Effects: [],
                    new WorkflowEventDelta(frame, suspension, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, outcome))),
            Key(instance));

    private string Key(InstanceAddress instance) => string.Create(CultureInfo.InvariantCulture, $"{LedgerFilter.InstanceKeyPrefix(instance)}t{_rows++}");

    private async ValueTask Append<T>(string type, T payload, string key)
    {
        var appended = await _slice.Store.Append(_slice.Events.Entry(type, payload, key, A.FirstItemId.Value), _grant!, Token);   // why: Create ran first
        Expect.True(appended.IsSuccess);
    }
}
