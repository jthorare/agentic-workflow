using System.Globalization;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.ModelHistory;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.GetLaunchPicks;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.StreamEvents;

namespace Aw.Sindri.Tests.PickReads;

/// <summary>GET /sindri/launch/picks (model-pick spec §6.1): the picks of the ledger with each run's tokens, priced cost and outcome word.</summary>
[Lane(TestLane.Unit)]
public sealed class GetLaunchPicksTests
{
    private static readonly InstanceAddress _one = InstanceAddress.From("T-1/step/1");
    private static readonly InstanceAddress _two = InstanceAddress.From("T-2/step/1");
    private static readonly LaunchId _first = A.FirstLaunchId;
    private static readonly LaunchId _second = LaunchId.From("LCH-00000000000000000000000000000002");
    private static readonly LaunchId _third = LaunchId.From("LCH-00000000000000000000000000000003");

    private readonly SliceHarness _h = new();
    private LeaseGrant _grant = null!;
    private int _rows;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<LaunchPicks> Read(GetLaunchPicksRequest? request = null, params ModelHistoryRow[] history)
    {
        var service = new GetLaunchPicksService(_h.Store, _h.Events, new NoItems(), StreamEventsServices.Repos(_h), _h.Callers, new FakeModelHistory(history), new OneInstance(StatusCases.View("done") with { Address = _one, Id = WorkflowInstanceId.Of(_one) }));
        using var entered = _h.Callers.Enter(new CallerContextBuilder().Human().Build());
        return (await service.Handle(request ?? new GetLaunchPicksRequest(), Ct)).Value;
    }

    private async Task Add<TPayload>(string type, TPayload payload, string key, string? item = null)
    {
        _grant ??= await _h.HoldLease();
        var entry = _h.Events.Entry(type, payload, key) with { Item = item ?? A.FirstItemId.Value };
        Assert.False((await _h.Store.Append(entry, _grant, Ct)).IsError);
    }

    private Task Picked(LaunchId launch, InstanceAddress instance, string state = "questions", string model = "claude-haiku-4-5", string by = "jev", string? from = null, string? kind = null, ItemId? item = null) =>
        Add(EventTypes.ModelPicked, PickRecords.Picked(launch, instance, model, by, state: state, from: from, kind: kind, item: item), "model.pick:" + launch.Value, (item ?? A.FirstItemId).Value);

    private Task Picking(LaunchId launch, InstanceAddress instance) =>
        Add(EventTypes.ModelPicking, PickRecords.Picking(launch, instance), "model.picking:" + launch.Value);

    private Task Usage(LaunchId launch, int sample, long input, long output, string? model = "claude-haiku-4-5") =>
        Add(EventTypes.SessionUsage, PickRecords.Usage(launch, sample, input, output, model), "session.usage:" + launch.Value + ":" + sample.ToString(CultureInfo.InvariantCulture));

    private static WorkflowEventDetail Detail(FrameSummary? frame, string? failureKind = null) =>
        new(
            TriggerId: TriggerKey.For(WorkflowInstanceId.Of(_one), "run:r1:abc"), TriggerActor: "agent", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null, Answer: null, Amount: null,
            Reason: null, Gates: [], Evidence: [], Grant: null, Override: null, Refusal: null, Effects: [],
            Delta: new WorkflowEventDelta(Frame: frame, Suspension: null, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, Outcome: null))
        {
            FailureKind = failureKind,
        };

    private string NextKey(InstanceAddress instance, string kind)
    {
        _rows++;
        return "workflow:" + instance.Value + ":" + kind + "-" + _rows.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The row that opens a run: its frame names the run's token and the launch id as the actor.</summary>
    private Task Opened(LaunchId launch, InstanceAddress instance, string run = "r1") =>
        Add(EventTypes.WorkflowTransition,
            new WorkflowEventPayload(instance.Value, "start", "questions", "step.completed", "done", Run: null, Detail(new FrameSummary(State: "questions", Entry: 1, Run: run, Child: null, Runtime: StepRuntime.Agent) { Actor = launch.Value })),
            NextKey(instance, "open"));

    /// <summary>What a real run mirrors when the instance starts into an agent Step (#1466): a 'workflow.started' whose frame names the first launch, written BEFORE that launch's model.picking.</summary>
    private Task Started(LaunchId launch, InstanceAddress instance, string run) =>
        Add(EventTypes.WorkflowStarted,
            new WorkflowEventPayload(instance.Value, "start", "questions", "start", "questions", Run: null, Detail(new FrameSummary(State: "questions", Entry: 1, Run: run, Child: null, Runtime: StepRuntime.Agent) { Actor = launch.Value })),
            NextKey(instance, "started"));

    /// <summary>The row that ends a run: step.completed, or step.failed with the failure kind.</summary>
    private Task Closed(InstanceAddress instance, string trigger, string? kind = null, string run = "r1") =>
        Add(EventTypes.WorkflowTransition,
            new WorkflowEventPayload(instance.Value, "questions", "done", trigger, "done", run, Detail(frame: null, kind)),
            NextKey(instance, "close"));

    [Fact]
    public async Task An_empty_ledger_has_no_picks() => Assert.Empty((await Read()).Picks);

    [Fact]
    public async Task A_pick_with_usage_has_the_last_samples_tokens_and_its_priced_cost()
    {
        await Picked(_first, _one);
        await Usage(_first, 1, input: 1_000, output: 200);
        await Usage(_first, 2, input: 3_000, output: 500);

        var pick = Assert.Single((await Read()).Picks);

        Assert.Equal((_first, 3_000L, 500L, 0.0055m, false), (pick.Launch, pick.InputTokens, pick.OutputTokens, pick.CostUsd, pick.Picking));   // (3000 x $1 + 500 x $5) per million
        Assert.Equal("claude-haiku-4-5", pick.Pick?.Model);
        Assert.Equal("Picked by Jev: the cheapest model it expects to pass.", pick.Why);
    }

    [Fact]
    public async Task A_model_with_no_price_has_its_tokens_and_a_null_cost()
    {
        await Picked(_first, _one, model: "model-with-no-price");
        await Usage(_first, 1, input: 1_000, output: 200, model: "model-with-no-price");

        var pick = Assert.Single((await Read()).Picks);

        Assert.Equal((1_000L, 200L), (pick.InputTokens, pick.OutputTokens));
        Assert.Null(pick.CostUsd);
    }

    [Fact]
    public async Task A_pick_with_no_usage_yet_has_zero_tokens_and_no_cost()
    {
        await Picked(_first, _one);

        var pick = Assert.Single((await Read()).Picks);

        Assert.Equal((0L, 0L), (pick.InputTokens, pick.OutputTokens));
        Assert.Null(pick.CostUsd);
    }

    [Fact]
    public async Task Model_picking_with_no_model_picked_is_picking_with_no_pick_and_the_asking_sentence()
    {
        await Picking(_first, _one);

        var pick = Assert.Single((await Read()).Picks);

        Assert.Equal((true, null, PickWhy.Asking, LaunchPickOutcomes.Running), (pick.Picking, pick.Pick, pick.Why, pick.Outcome));
    }

    [Fact]
    public async Task Model_picked_after_model_picking_ends_the_picking_state_and_makes_one_row()
    {
        await Picking(_first, _one);
        await Picked(_first, _one);

        var pick = Assert.Single((await Read()).Picks);

        Assert.False(pick.Picking);
        Assert.NotNull(pick.Pick);
    }

    [Fact]
    public async Task A_run_with_no_end_is_running_and_launch_failed_is_failed()
    {
        await Picked(_first, _one);
        await Picked(_second, _two);
        await Add(EventTypes.LaunchFailed, new LaunchFailed(_second, A.FirstItemId, "SND-LAUNCH-009", "timed out"), "launch.outcome:" + _second.Value);

        var picks = (await Read()).Picks;

        Assert.Equal([LaunchPickOutcomes.Running, LaunchPickOutcomes.Failed], picks.Select(static p => p.Outcome), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("step.completed", null, "passed")]
    [InlineData("step.failed", "refused", "refused")]
    [InlineData("step.failed", "check_failed", "failed")]
    [InlineData("step.failed", "crashed", "failed")]
    [InlineData("step.failed", null, "failed")]
    public async Task The_closing_row_of_the_run_gives_the_outcome_word(string trigger, string? kind, string word)
    {
        await Picked(_first, _one);
        await Opened(_first, _one);
        await Closed(_one, trigger, kind);

        Assert.Equal(word, Assert.Single((await Read()).Picks).Outcome);
    }

    [Fact]
    public async Task A_retry_is_its_own_row_with_the_escalation_number_and_the_retry_sentence()
    {
        await Picked(_first, _one, model: "claude-haiku-4-5");
        await Opened(_first, _one);
        await Closed(_one, "step.failed", "refused");
        await Picked(_second, _one, model: "claude-sonnet-4-5", from: "claude-haiku-4-5", kind: "refused");
        await Opened(_second, _one);
        await Closed(_one, "step.completed");

        var picks = (await Read()).Picks;

        Assert.Equal([0, 1], picks.Select(static p => p.Escalation));
        Assert.Equal(["refused", "passed"], picks.Select(static p => p.Outcome), StringComparer.Ordinal);
        Assert.Equal("Retried on claude-sonnet-4-5 after claude-haiku-4-5 was refused.", picks[1].Why);
    }

    /// <summary>#1466, the rows of a real escalated run: the start row (naming only the first launch) comes before the picks, the refusal mirrors nothing, the retry keeps the run token, one step.completed ends it.</summary>
    [Fact]
    public async Task An_escalated_real_run_shows_refused_then_passed_and_the_escalation_footer()
    {
        await Started(_first, _one, run: "1.1");
        await Picked(_first, _one, model: "claude-haiku-4-5");
        await Picked(_second, _one, model: "claude-sonnet-4-5", by: "constraint", from: "claude-haiku-4-5", kind: "refused");
        await Closed(_one, "step.completed", run: "1.1");
        var haiku = new ModelHistoryRow(FakeModelHistory.Recipe, 1, "questions", "claude-haiku-4-5", Runs: 1, Refused: 1, CheckFailed: 0, Crashed: 0, RedraftCaused: 0, LastRunAt: null, [ModelOutcome.Refused]);
        var sonnet = new ModelHistoryRow(FakeModelHistory.Recipe, 1, "questions", "claude-sonnet-4-5", Runs: 1, Refused: 0, CheckFailed: 0, Crashed: 0, RedraftCaused: 0, LastRunAt: null, [ModelOutcome.Ok]);

        var read = await Read(history: [haiku, sonnet]);

        Assert.Equal([LaunchPickOutcomes.Refused, LaunchPickOutcomes.Passed], read.Picks.Select(static p => p.Outcome), StringComparer.Ordinal);
        Assert.Equal(["claude-haiku-4-5", "claude-sonnet-4-5"], read.Escalation!.Select(static e => e.Model), StringComparer.Ordinal);
    }

    /// <summary>The rows M-6 writes when a refusal is mirrored: the start row before the picks, step.failed refused, the retry's own opening row, step.completed.</summary>
    [Fact]
    public async Task A_mirrored_refusal_and_a_mirrored_retry_pair_by_run_token_even_when_the_start_row_precedes_the_picks()
    {
        await Started(_first, _one, run: "1.1");
        await Picked(_first, _one, model: "claude-haiku-4-5");
        await Closed(_one, "step.failed", "refused", run: "1.1");
        await Picked(_second, _one, model: "claude-sonnet-4-5", by: "constraint", from: "claude-haiku-4-5", kind: "refused");
        await Opened(_second, _one, run: "1.1");
        await Closed(_one, "step.completed", run: "1.1");

        Assert.Equal([LaunchPickOutcomes.Refused, LaunchPickOutcomes.Passed], (await Read()).Picks.Select(static p => p.Outcome), StringComparer.Ordinal);
    }

    [Fact]
    public async Task With_no_history_for_the_picked_model_there_is_no_escalation_footer()
    {
        await Picked(_first, _one);

        Assert.Empty((await Read()).Escalation ?? []);
    }

    [Fact]
    public async Task A_picked_models_recent_runs_give_its_escalation_rate_for_the_state()
    {
        await Picked(_first, _one);
        ModelOutcome[] recent = [ModelOutcome.Ok, ModelOutcome.Ok, ModelOutcome.Refused, ModelOutcome.CheckFailed, ModelOutcome.Crashed];
        var row = new ModelHistoryRow(FakeModelHistory.Recipe, 1, "questions", "claude-haiku-4-5", Runs: 5, Refused: 1, CheckFailed: 1, Crashed: 1, RedraftCaused: 0, LastRunAt: null, recent);

        var rate = Assert.Single((await Read(history: row)).Escalation!);   // why: the model has history, so the list is there

        Assert.Equal(("questions", "claude-haiku-4-5", 4, 2, 0.5), (rate.State, rate.Model, rate.Runs, rate.Retried, rate.EscalationRate));
        Assert.Equal("retried on a stronger model in 2 of 4 runs", rate.Sentence);
    }

    [Fact]
    public async Task Picks_come_oldest_first()
    {
        await Picked(_second, _two);
        await Picked(_first, _one);
        await Picked(_third, _one, state: "draft");

        Assert.Equal([_second, _first, _third], (await Read()).Picks.Select(static p => p.Launch));
    }

    [Fact]
    public async Task The_instance_filter_keeps_only_that_instances_picks()
    {
        await Picked(_first, _one);
        await Picked(_second, _two);

        var picks = (await Read(new GetLaunchPicksRequest(Instance: _two))).Picks;

        Assert.Equal([_second], picks.Select(static p => p.Launch));
    }

    [Fact]
    public async Task The_item_filter_keeps_only_that_items_picks()
    {
        var other = ItemId.From("ITM-00000000000000000000000000000002");
        foreach (var item in new[] { A.FirstItemId, other })
        {
            var created = new ItemCreated(_h.Ids.New<CommandId>(), item, "t", Repo: null, Link: null, Body: null, Owner: null);
            await Add(EventTypes.ItemCreated, created, "item.created:" + item.Value, item.Value);
        }

        await Picked(_first, _one);
        await Picked(_second, _two, item: other);

        var picks = (await Read(new GetLaunchPicksRequest(Item: ItemRef.To(other)))).Picks;

        Assert.Equal([_second], picks.Select(static p => p.Launch));
    }
}
