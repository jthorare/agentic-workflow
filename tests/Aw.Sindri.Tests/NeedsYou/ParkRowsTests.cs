using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.Items;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>The 'park' rows of the needs-you list: engine parks and gates, desk tasks and idle takeovers (API spec section 3.6, API P11 U3).</summary>
[Lane(TestLane.Unit)]
public sealed class ParkRowsTests
{
    private static readonly PassThroughScrubber _plain = new();
    private static readonly InstanceAddress _address = InstanceAddress.From("ITM-11111111111111111111111111111111/step/1");
    private static readonly DateTimeOffset _since = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Park_row_carries_the_engines_action_id_and_answer_kind()
    {
        var row = NeedsYouRows.ForPark(Item(), new ParkRowInput(_address, Park(ParkReason.Rounds, ParkKind.Loop)), _since, _plain);

        Assert.Equal((NeedsYouKind.Park, SubjectKind.Instance, WorkflowInstanceId.Of(_address).Value), (row.Kind, row.Subject.Kind, row.Subject.Id.Value));
        Assert.Equal(("park:" + WorkflowInstanceId.Of(_address).Value, AnswerKind.Grant, ActionId.From("ApplyCommand:answer-grant"), Urgency.Pulse), (row.Id.Value, row.AnswerKind, row.PrimaryAction, row.Urgency));
        Assert.Equal(("the item", ItemQueryHarness.Id('1'), "sindri workflow answer " + _address.Value + " --grant 1"), (row.Title, row.Item, row.Cli));
        Assert.Equal(_since, row.Since);
        Assert.Null(row.Via);   // a park the engine can answer itself is no pointer
    }

    [Theory]
    [InlineData("parked-no-progress-child")]
    [InlineData("escalated-child")]
    [InlineData("parked-mirrored-needs-answer")]
    [InlineData("parked-mirrored-rounds")]
    [InlineData("parked-mirrored-timeout")]
    [InlineData("parked-mirrored-no-runtime")]
    [InlineData("parked-mirrored-budget")]
    [InlineData("parked-mirrored-spec-conflict")]
    [InlineData("parked-mirrored-needs-answer-wait")]
    public void A_mirrored_park_row_carries_the_same_action_and_cli_as_the_status_answer_line(string name)
    {
        var instance = StatusCases.Instance(name);
        var suspension = instance.Suspension!;
        var summary = new SuspensionSummary(suspension.Kind, suspension.Reason, suspension.ParkKind, suspension.Escalation, suspension.Mirrors?.Value, suspension.Mirrors is null ? null : "T-3/step/1/step.4", suspension.MirroredParkKind, suspension.MirroredEscalation);   // the address the engine's event carries for a mirrored child

        var row = NeedsYouRows.ForPark(Item(), new ParkRowInput(instance.Address, summary), _since, _plain);

        var line = StatusCases.View(name).AnswerLine!;
        Assert.Equal((line.ActionId, line.Cli, line.Kind, line.Via), (row.PrimaryAction, row.Cli, row.AnswerKind, row.Via));
    }

    [Fact]
    public void A_mirrored_park_row_names_the_child_it_points_at()
    {
        var child = "ITM-11111111111111111111111111111111/step/1/workflow.step.1";
        var summary = new SuspensionSummary(SuspensionKind.Park, ParkReason.NeedsAnswer, ParkKind.Step, Escalation: null, Mirrors: "WFI-22222222222222222222222222222222", MirrorsAddress: child);

        var row = NeedsYouRows.ForPark(Item(), new ParkRowInput(_address, summary), _since, _plain);

        Assert.Equal(("child", child), (row.Via, row.Child));
    }

    [Fact]
    public void A_park_the_engine_answers_itself_names_no_child()
    {
        var row = NeedsYouRows.ForPark(Item(), new ParkRowInput(_address, Park(ParkReason.Rounds, ParkKind.Loop)), _since, _plain);

        Assert.Null(row.Child);
    }

    [Theory]
    [InlineData("parked-mirrored-needs-answer")]
    [InlineData("parked-mirrored-needs-answer-wait")]
    [InlineData("parked-mirrored-budget")]
    [InlineData("parked-mirrored-spec-conflict")]
    public void A_mirrored_park_the_child_decides_points_its_row_at_the_child(string name)
    {
        var suspension = StatusCases.Instance(name).Suspension!;
        var summary = new SuspensionSummary(suspension.Kind, suspension.Reason, suspension.ParkKind, suspension.Escalation, suspension.Mirrors?.Value);

        var row = NeedsYouRows.ForPark(Item(), new ParkRowInput(_address, summary), _since, _plain);

        Assert.EndsWith("<the child's answer>", row.Cli, StringComparison.Ordinal);
        Assert.Equal(ActionId.From("ApplyCommand:answer-text"), row.PrimaryAction);
        Assert.Equal("child", row.Via);
    }

    [Fact]
    public void Gate_escalation_row_is_answer_accept_and_cli_only()
    {
        var suspension = new SuspensionSummary(SuspensionKind.Escalation, Reason: null, ParkKind: null, EscalationKind.Gate, Mirrors: null);

        var row = NeedsYouRows.ForPark(Item(), new ParkRowInput(_address, suspension), _since, _plain);

        Assert.Equal((AnswerKind.Accept, ActionId.From("ApplyCommand:answer-accept"), "sindri workflow answer " + _address.Value + " --accept"), (row.AnswerKind, row.PrimaryAction, row.Cli));
    }

    [Fact]
    public void Desk_task_row_offers_CompleteStep()
    {
        var row = NeedsYouRows.ForDeskTask(Item(), _address, _since, _plain);

        Assert.Equal(("desk:" + WorkflowInstanceId.Of(_address).Value, ActionId.From("CompleteStep"), Urgency.Colour, null), (row.Id.Value, row.PrimaryAction, row.Urgency, row.AnswerKind));
        Assert.Equal(NeedsYouKind.Park, row.Kind);
    }

    [Fact]
    public void Idle_takeover_row_offers_handback()
    {
        var row = NeedsYouRows.ForIdleTakeover(Item(), _address, _since, _plain);

        Assert.Equal(("takeover:" + WorkflowInstanceId.Of(_address).Value, ActionId.From("ApplyCommand:handback"), Urgency.Calm), (row.Id.Value, row.PrimaryAction, row.Urgency));
        Assert.Equal("sindri handback " + _address.Value, row.Cli);
    }

    [Fact]
    public void A_stalled_agent_row_takes_the_park_row_id_so_the_run_s_own_park_replaces_it()
    {
        var row = NeedsYouRows.ForStalledAgent(Item(), _address, _since, _plain);

        Assert.Equal(("park:" + WorkflowInstanceId.Of(_address).Value, NeedsYouKind.Park, "agent · no progress", Urgency.Pulse), (row.Id.Value, row.Kind, row.Label, row.Urgency));
        Assert.Equal(("sindri workflow status " + _address.Value, ActionId.From("ApplyCommand:cancel"), _since), (row.Cli, row.PrimaryAction, row.Since));
        Assert.Contains("the agent ended its turn without reporting", row.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Drift_park_row_has_no_answer_kind()
    {
        var row = NeedsYouRows.ForPark(Item(), new ParkRowInput(_address, Park(ParkReason.SpecConflict, ParkKind.Drift)), _since, _plain);

        Assert.Null(row.AnswerKind);
        Assert.Equal(Urgency.Pulse, row.Urgency);
    }

    [Fact]
    public void A_dispatch_park_row_says_the_refusal_not_drift()
    {
        var row = NeedsYouRows.ForPark(Item(), new ParkRowInput(_address, Park(ParkReason.SpecConflict, ParkKind.Dispatch), Refusal: "SND-WF-010: fact:nextPhase must be a phase"), _since, _plain);

        Assert.Equal("park · dispatch refused", row.Label);
        Assert.Contains("SND-WF-010: fact:nextPhase must be a phase", row.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("drifted", row.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_dispatch_park_row_with_no_recorded_refusal_says_an_effect_was_refused()
    {
        var row = NeedsYouRows.ForPark(Item(), new ParkRowInput(_address, Park(ParkReason.SpecConflict, ParkKind.Dispatch)), _since, _plain);

        Assert.Contains("an effect was refused", row.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ParkKind.Drift, "its recipe drifted since approval")]
    [InlineData(ParkKind.Revoked, "its recipe was revoked")]
    [InlineData(ParkKind.ChildRevoked, "a child's recipe was revoked")]
    public void The_other_spec_conflict_parks_keep_their_words_even_with_a_refusal(ParkKind kind, string words)
    {
        var row = NeedsYouRows.ForPark(Item(), new ParkRowInput(_address, Park(ParkReason.SpecConflict, kind), Refusal: "SND-WF-010: x"), _since, _plain);

        Assert.Contains(words, row.Reason, StringComparison.Ordinal);
    }

    public static TheoryData<ParkReason, ParkKind> Parks()
    {
        var data = new TheoryData<ParkReason, ParkKind>();
        foreach (var reason in Enum.GetValues<ParkReason>())
        {
            foreach (var kind in Enum.GetValues<ParkKind>())
            {
                data.Add(reason, kind);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Parks))]
    public void Every_park_shape_row_names_actions_the_catalog_has_and_a_primary_among_them(ParkReason reason, ParkKind kind) =>
        AssertCatalogued(NeedsYouRows.ForPark(Item(), new ParkRowInput(_address, Park(reason, kind)), _since, _plain));

    [Theory]
    [InlineData(EscalationKind.Gate)]
    [InlineData(EscalationKind.Timeout)]
    [InlineData(EscalationKind.Child)]
    public void Every_escalation_row_names_actions_the_catalog_has_and_a_primary_among_them(EscalationKind escalation) =>
        AssertCatalogued(NeedsYouRows.ForPark(Item(), new ParkRowInput(_address, new SuspensionSummary(SuspensionKind.Escalation, Reason: null, ParkKind: null, escalation, Mirrors: null)), _since, _plain));

    [Fact]
    public void Desk_task_and_idle_takeover_rows_name_actions_the_catalog_has()
    {
        AssertCatalogued(NeedsYouRows.ForDeskTask(Item(), _address, _since, _plain));
        AssertCatalogued(NeedsYouRows.ForIdleTakeover(Item(), _address, _since, _plain));
    }

    [Theory]
    [InlineData(ParkKind.Drift)]
    [InlineData(ParkKind.ChildRevoked)]
    public void A_drift_or_revoked_child_park_row_offers_approve_first_and_then_cancel(ParkKind kind)
    {
        var row = NeedsYouRows.ForPark(Item(), new ParkRowInput(_address, Park(ParkReason.SpecConflict, kind)), _since, _plain);

        Assert.Equal(ActionId.From("ApproveRecipe"), row.PrimaryAction);
        Assert.Equal([ActionId.From("ApproveRecipe"), ActionId.From("ApplyCommand:cancel")], row.Actions);
    }

    private static void AssertCatalogued(NeedsYouRow row)
    {
        var catalog = Host.Catalog.EngineRows.All.Select(static r => r.Action).ToHashSet();
        Assert.Contains(row.PrimaryAction!.Value, row.Actions);
        Assert.All(row.Actions, action => Assert.Contains(action, catalog));
    }

    private static SuspensionSummary Park(ParkReason reason, ParkKind kind) => new(SuspensionKind.Park, reason, kind, Escalation: null, Mirrors: null);

    private static ItemRow Item() => new(
        ItemQueryHarness.Id('1'), "the item", RepoName.From("agentic-workflow"), ItemState.Parked, Step: "produce", Version: 2, "human:thor", _since, _since, _since,
        Link: null, Alias: null, Body: null, WaitingOn: null, Instance: _address.Value, Owner: null, Sequence: 3);
}
