using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Features.CheckSandbox;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.Items;
using Aw.Sindri.Tests.Workflow.Engine;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.CheckSandbox;

/// <summary>
/// SandboxParkedInstances finds a -014/-017 park by the code in the question text, which the engine's decision code writes (TransitionDecision.ParkFor). These tests build the parks
/// through that code, not by hand, so a change to that text breaks here and not silently in Check again.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class SandboxParkedInstancesTests
{
    private const string ProduceStep = "step.produce";
    private static readonly RunToken _run = RunToken.For(1, 1);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static WorkflowInstance Running()
    {
        var at = At(Step, "produce");
        var loops = new Dictionary<string, LoopState>(StringComparer.Ordinal) { ["revisions"] = new LoopState(3, 3, Last: null) };
        return at with { Frame = at.Frame with { Run = new OpenRun(_run, StepRuntime.Agent, "LCH-" + new string('a', 32), Task: null) }, Loops = loops };
    }

    private static DecisionContext AgentOnly()
    {
        var blocks = EngineFixture.Catalog.Blocks.Select(static b => b.Id.Value == ProduceStep ? b with { DeskCapable = false } : b);
        return EngineFixture.Context(ProduceStep) with { Catalog = new BlockCatalog([.. blocks], EngineFixture.Catalog.Events) };
    }

    private static WorkflowInstance Park(Trigger trigger) =>
        Assert.IsType<Applied>(Decide(Running(), Step, trigger, context: AgentOnly())).Changes[0].After;

    private static Task<IReadOnlyList<ParkedOnSandbox>> Listed(WorkflowInstance parked, IViewReader? views = null)
    {
        var view = InstanceViews.Of(parked, StatusCases.Recipe with { Recipe = Step, Catalog = EngineFixture.Catalog }, DateTimeOffset.UnixEpoch, timeoutAt: null, new InstanceViewContext());
        var row = new InstanceRow(parked.Address.Value, EngineStates.Parked, NeedsYou: true);
        return new SandboxParkedInstances(new Rows(row), new Reader(view), views).Parked(Token).AsTask();
    }

    [Fact]
    public async Task A_no_runtime_park_the_decision_made_for_a_sandbox_refusal_is_listed()
    {
        var parked = Park(new RuntimeUnavailable("service:agent:" + ProduceStep, _run, "SND-LAUNCH-014", "no-bwrap", Source: null, "no sandbox tool"));

        var found = Assert.Single(await Listed(parked));

        Assert.Equal(WorkflowInstanceId.Of(parked.Address), found.Instance);
    }

    [Fact]
    public async Task A_launch_park_the_decision_made_is_listed()
    {
        var parked = Park(new LaunchRefused("service:agent:" + ProduceStep, _run, "SND-LAUNCH-016", "fs-deny", Source: null, "the canary failed"));

        _ = Assert.Single(await Listed(parked));
    }

    [Fact]
    public async Task A_listed_park_names_its_item_and_the_items_title()
    {
        var parked = Park(new RuntimeUnavailable("service:agent:" + ProduceStep, _run, "SND-LAUNCH-014", "no-bwrap", Source: null, "no sandbox tool"));

        var found = Assert.Single(await Listed(parked, new Titles("Add the tax field")));

        Assert.Equal(("T-3", "Add the tax field"), (found.Item, found.Title));
    }

    [Fact]
    public async Task A_park_whose_item_cant_be_read_is_still_listed_without_a_title()
    {
        var parked = Park(new RuntimeUnavailable("service:agent:" + ProduceStep, _run, "SND-LAUNCH-014", "no-bwrap", Source: null, "no sandbox tool"));

        var found = Assert.Single(await Listed(parked, new Titles(title: null)));

        Assert.Equal(("T-3", null), (found.Item, found.Title));
    }

    private sealed class Titles(string? title) : IViewReader
    {
        public ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
            where TRow : class
        {
            var row = title is null ? null : new ItemRow(
                ItemQueryHarness.Id('1'), title, Repo: null, ItemState.Parked, Step: null, Version: 1, "human:thor", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
                Link: null, Alias: null, Body: null, WaitingOn: null, Instance: null, Owner: null, Sequence: 1) as TRow;
            return ValueTask.FromResult<Result<TRow?>>(row);
        }

        public ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
            where TRow : class => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<TRow>>> Range<TRow>(string view, string afterKey, string? beforeKey, int max, CancellationToken cancellationToken)
            where TRow : class => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<TRow>>> Where<TRow>(string view, ViewColumn column, string value, CancellationToken cancellationToken)
            where TRow : class => throw new NotSupportedException();

        public ValueTask<Result<ProjectorState?>> State(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Rows(InstanceRow row) : IWorkflowInstances
    {
        public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(InstanceFilter filter, bool ended, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<IReadOnlyList<InstanceRow>>>(new[] { row });

        public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(string? query, bool ended, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<IReadOnlyList<InstanceRow>>>(new[] { row });
    }

    private sealed class Reader(InstanceView view) : IInstanceReader
    {
        public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) => ValueTask.FromResult<Result<InstanceView>>(view);
    }
}
