using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.OpenTargets;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.ListInstances;
using Aw.Sindri.Tests.Requests;
using Aw.Sindri.Tests.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.GetNeedsYou;

/// <summary>The read-time 'desk:' row of an instance that waits on the owner at a human-verb wait (#1858): a wait state with a 'command.&lt;verb&gt;' exit whose effect acts with a human's authority.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouOwnerStopsTests
{
    private const string Pull = """{"pr":42,"url":"https://github.com/o/r/pull/42"}""";

    private static readonly InstanceAddress _root = InstanceAddress.From("ITM-" + new string('1', 32) + "/line/1");
    private static readonly InstanceAddress _second = InstanceAddress.From("T-9/pr-babysitter/1");

    private readonly RequestQueryHarness _q = new();
    private readonly InMemoryRecipeStore _recipes = new();
    private readonly ItemId _item = RequestQueryHarness.Item('1');

    [Fact]
    public async Task An_instance_in_a_human_verb_wait_gets_a_desk_row_that_opens_the_workflow_with_its_pull_request()
    {
        var view = await Waiting("ready", Pull);

        var row = Assert.Single(await Read([view]));

        Expect.Equal(("desk:" + WorkflowInstanceId.Of(view.Address).Value, NeedsYouKind.Park, Urgency.Colour, "ready to merge"), (row.Id.Value, row.Kind, row.Urgency, row.Label));
        Expect.Equal("ApplyCommand:command", row.PrimaryAction!.Value.Value);
        Expect.Equal($"sindri workflow status {view.Address.Value}", row.Cli);
        Expect.Equal(new NeedsYouOpen(NeedsYouRoutes.Desk(view.Address), "Review and act", new NeedsYouOpenPr("https://github.com/o/r/pull/42", 42)), row.Open);
        Expect.Equal(ReadyNext(_item.Value, view.Address), row.Reason);
        Expect.Equal("An item", row.Title);
        Expect.Equal(view.Since, row.Since);
    }

    [Fact]
    public async Task A_wait_state_with_no_human_trigger_command_gets_no_row()
    {
        var view = await Waiting("merging", Pull);

        Assert.Empty(await Read([view]));
    }

    [Fact]
    public async Task Without_a_pr_status_fact_the_row_opens_with_no_pull_request()
    {
        var view = await Waiting("ready", prStatus: null);

        var row = Assert.Single(await Read([view]));

        Assert.Null(row.Open.Pr);
    }

    [Fact]
    public async Task An_instance_that_cannot_be_read_drops_only_its_own_row()
    {
        var readable = await Waiting("ready", Pull);
        var lost = readable with { Address = _second, Id = WorkflowInstanceId.Of(_second) };

        var row = Assert.Single(await Read([readable, lost], served: [readable]));

        Expect.Equal(WorkflowInstanceId.Of(readable.Address).Value, row.Subject.Id.Value);
    }

    [Fact]
    public async Task A_suspended_instance_gets_no_row()
    {
        var view = (await Waiting("ready", Pull)) with { Question = StatusCases.View("taken-over").Question };

        Assert.Empty(await Read([view]));
    }

    [Fact]
    public async Task An_instance_with_a_run_open_gets_no_row()
    {
        var view = (await Waiting("ready", Pull)) with { Run = StatusCases.View("desk-task").Run };

        Assert.Empty(await Read([view]));
    }

    [Fact]
    public async Task A_session_sees_the_row_whatever_its_repo()
    {
        var view = await Waiting("ready", Pull, repo: "agentic-workflow");
        using var entered = _q.Session("other-repo");

        _ = Assert.Single(await Read([view]));
    }

    [Fact]
    public async Task A_folded_row_of_the_same_id_wins()
    {
        var view = (await Waiting("ready", Pull)) with { Address = _root, Id = WorkflowInstanceId.Of(_root) };
        await _q.Views.Fold(EventTypes.WorkflowStarted, Transition(new FrameSummary("running", 1, Run: null, Child: null)), _item);
        await _q.Views.Fold(EventTypes.WorkflowTransition, Transition(new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk)), _item);
        using var entered = _q.Human();
        var service = new GetNeedsYouService(_q.Views.Views, _q.Views.Progress, _q.Views.Slice.Callers, LiveRows(Stops([view], served: null)), TimeProvider.System, Joins());

        var row = Assert.Single((await service.Handle(A.GetNeedsYouRequest().Build(), RequestQueryHarness.Token)).Value.Rows);

        Expect.Equal(("desk:" + WorkflowInstanceId.Of(_root).Value, "desk task"), (row.Id.Value, row.Label));
    }

    [Fact]
    public async Task The_owner_stop_row_matches_its_golden_file()
    {
        var view = await Waiting("ready", Pull);
        var rows = await Read([view]);

        ApiGoldens.ApiGolden.Match(
            Encoding.UTF8.GetString(AwJson.Serialize(new GetNeedsYouResponse(rows, Empty: null, new AsOf(7, 7, view.Since, Rebuilding: false, Stalled: false)), SliceHarness.Json)),
            Path.GetTempPath(), "needs-you", "owner-stop");
    }

    // The babysitter's 'ready' next sentence with {item} and {instance} filled, as the view serves it.
    private static string ReadyNext(string item, InstanceAddress address) =>
        $"Ready to merge {item}'s pull request: CI green on the head, the docs settled (see the view). Merge it, or send it back with rework notes, in the app or with `sindri workflow command {address.Value} merge`.";

    private static WorkflowEventPayload Transition(FrameSummary frame)
    {
        var delta = new WorkflowEventDelta(frame, Suspension: null, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, Outcome: null);
        var detail = new WorkflowEventDetail(TriggerKey.For(WorkflowInstanceId.From("WFI-" + new string('a', 32)), "t"), "human:cli", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null,
            Answer: null, Amount: null, Reason: null, [], [], Grant: null, Override: null, Refusal: null, [], delta);
        return new WorkflowEventPayload(_root.Value, "from", "to", "step.completed", Outcome: null, Run: null, detail);
    }

    private async Task<InstanceView> Waiting(string state, string? prStatus, string? repo = null)
    {
        var document = RecipeReaderTests.Reader.Read(ShippedRecipes.Read("sindri/workflow.pr-babysitter.yaml"), "workflow.pr-babysitter.yaml").Document!;   // why: the shipped recipes bind (ShippedRecipesTests)
        var text = Encoding.UTF8.GetString(AwJson.Serialize(document.Recipe with { Hash = null }, SliceHarness.Json));
        _ = await _recipes.Add(new RegisteredRecipe(document.Recipe.Id, document.Recipe.Version, document.Hash, text, Base: null, DateTimeOffset.UnixEpoch), TestContext.Current.CancellationToken);
        await _q.CreateItem(_item, repo);
        var basis = StatusCases.View("desk-task");
        return basis with
        {
            Recipe = new RecipeVersion(document.Recipe.Id, document.Recipe.Version, document.Hash),
            State = state,
            Task = null,
            Run = null,
            AgentRun = null,
            Question = null,
            ItemId = ItemKey.From(_item.Value),
            Next = ReadyNext(_item.Value, basis.Address),
            FactShas = prStatus is null ? null : new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:prStatus"] = new string('1', 64) },
        };
    }

    private async Task<IReadOnlyList<NeedsYouRow>> Read(InstanceView[] listed, InstanceView[]? served = null) =>
        await Stops(listed, served).Read(TestContext.Current.CancellationToken);

    private NeedsYouOwnerStops Stops(InstanceView[] listed, InstanceView[]? served)
    {
        var instances = new SomeInstances(served ?? listed);
        var reader = new OwnerStopReader(
            new RecordingInstances([.. listed.Select(static v => new InstanceRow(v.Address.Value, v.State, NeedsYou: false))]), instances, instances,
            new RecipeRegistryReader(_recipes, new InMemoryRecipeApprovals(), EngineFixture.Catalog, SliceHarness.Json, new NoProfileFacts()), EngineFixture.Catalog);
        return new NeedsYouOwnerStops(reader, _q.Views.Views, new Contents(), new PassThroughScrubber(), TimeProvider.System);
    }

    private NeedsYouLiveRows LiveRows(NeedsYouOwnerStops stops)
    {
        var jobs = new InMemoryJobViews();
        var mcp = new Core.Setup.McpAutoGate(_q.Views.Slice.Profiles, new Core.Setup.McpEvents(_q.Views.Slice.Held, _q.Views.Slice.Store, _q.Views.Slice.Events, _q.Views.Slice.Callers), new NoSwitch(), jobs);
        var notices = new SetupNotices(jobs, _q.Views.Slice.Profiles, new ApproveAllPlanReader(_q.Views.Slice.Profiles, new FakeShippedRecipes([])), new PassThroughScrubber(), TimeProvider.System, mcp, _q.Views.Slice.Profiles);
        return new NeedsYouLiveRows(notices, new PassThroughScrubber(), ownerStops: stops);
    }

    private NeedsYouJoins Joins() =>
        new(new SomeInstances(), new SomeInstances(), new RecipeRegistryReader(_recipes, new InMemoryRecipeApprovals(), EngineFixture.Catalog, SliceHarness.Json, new NoProfileFacts()), EngineFixture.Catalog);

    private sealed class Contents : IFactContents
    {
        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success((ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(Pull)));
    }

    private sealed class NoSwitch : Core.Setup.IMcpAutoSwitch
    {
        public bool Off => false;
    }

    private sealed class NoProfileFacts : IRecipeProfileFacts
    {
        public ValueTask<Result<RecipeProfileFacts>> Current(CancellationToken cancellationToken) => throw new NotSupportedException("reading a recipe back needs no profile");
    }
}
