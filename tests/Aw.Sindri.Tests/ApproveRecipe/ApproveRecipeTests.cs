using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.ApproveRecipe;

/// <summary>Approving a recipe (engine spec §4.1, §4.2, Phase 2's signal): a human at a terminal answers the challenge and gets the approval and its preview.</summary>
public sealed class ApproveRecipeTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task Approving_workflow_step_as_a_human_at_a_terminal_yields_an_approval_with_its_preview()
    {
        await using var registry = new RegistryHarness();
        _ = await registry.H.HoldLease();
        var document = RecipeReaderTests.Reader.Read(ShippedRecipes.Read("sindri/workflow.step.yaml"), "workflow.step.yaml").Document!;
        var step = await registry.Register(document);
        var issued = (await registry.Issue("approve workflow.step 5")).Value;   // the server binds the subject, pins included
        var human = A.Caller().AtTerminal().Client(ClientLabel.Cli).Build();

        var approved = await registry.Run<ApproveRecipeRequest, ApproveRecipeResponse>(
            new ApproveRecipeRequest(step.Id, step.Hash, new ChallengeAnswer(issued.Challenge, "approve workflow.step 5")), human);

        Assert.True(approved.IsSuccess, approved.IsError ? approved.Error.ToString() : null);
        var (approval, preview) = (approved.Value.Approval, approved.Value.Preview);
        Assert.Equal((step.Id, 5, step.Hash, ApprovalVia.Desk, human.Actor), (approval.Id, approval.Version, approval.Hash, approval.Via, approval.Approver));
        Assert.Equal((step.Id, 5, step.Hash), (preview.Id, preview.Version, preview.Hash));
        Assert.NotEmpty(preview.Pins.Blocks);
        Assert.Equal(approval.Pins, preview.Pins);
        Assert.Equal(approval, (await registry.Approvals.Latest(step.Id, 5, step.Hash, TestContext.Current.CancellationToken)).Value);
    }
    [Fact, Trait("Lane", "unit")]
    public async Task Approval_appends_recipe_approved_with_its_pins_through_the_ledger()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");

        var approved = (await r.Approve(step)).Value;

        var recorded = Assert.Single(r.Events(EventTypes.RecipeApproved));
        var payload = r.H.Events.Payload<RecipeApproved>(recorded).Value;
        Assert.Equal((step.Id, step.Hash, ApprovalVia.Desk, false, "human:cli"), (payload.Id, payload.Hash, payload.Via, payload.Drift, recorded.Envelope.Actor));
        Assert.Equal(approved.Approval.Pins.Blocks, payload.Pins.Blocks);
        Assert.Equal(recorded.Sequence, approved.Accepted.Sequence);
        Assert.Equal([BlockId.From("step.checks"), BlockId.From("step.produce"), BlockId.From("step.verify")], approved.Preview.Pins.Blocks.Select(b => b.Block));
        Assert.Null(approved.Preview.Diff);
        Assert.Contains("3 declared", approved.Preview.Evals, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Approving_the_same_pins_again_records_nothing_and_wakes_the_running_instances_again()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        var first = (await r.Approve(step)).Value;
        var parked = WorkflowInstanceId.For(new Guid(7, 0, 0, new byte[8]));
        r.Wake.Run(step.Hash, parked);

        var again = await r.Approve(step);   // why: a drift park whose Approved wake was missed has no other way out

        Assert.True(again.IsSuccess, again.IsError ? again.Error.ToString() : null);
        Assert.True(again.Value.AlreadyApproved);
        Assert.False(first.AlreadyApproved);
        Assert.Equal([parked], again.Value.Resumed);
        Assert.Equal(first.Approval, again.Value.Approval);
        _ = Assert.Single(r.Events(EventTypes.RecipeApproved));
        var sent = Assert.Single(r.Wake.Sent);   // why: the first approval had nothing to resume, so it sent none
        Assert.Equal((RecipeWakeKind.Approved, first.Accepted.Sequence), (sent.Kind, sent.Sequence));
        _ = Assert.Single((await r.Approvals.History(step.Id, 5, step.Hash, Token)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Approving_the_same_pins_again_after_another_recipe_was_approved_wakes_under_this_hashs_own_approval()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        var item = await r.Shipped("sindri/workflow.item.yaml");
        var first = (await r.Approve(step)).Value;
        var other = (await r.Approve(item)).Value;
        r.Wake.Run(step.Hash, WorkflowInstanceId.For(new Guid(8, 0, 0, new byte[8])));
        Assert.NotEqual(first.Accepted.Sequence, other.Accepted.Sequence);

        var again = await r.Approve(step);

        Assert.True(again.IsSuccess, again.IsError ? again.Error.ToString() : null);
        Assert.Equal(first.Accepted.Sequence, again.Value.Accepted.Sequence);   // why: the other recipe's approval is later, and is not this hash's
        Assert.Equal(first.Accepted.Sequence, Assert.Single(r.Wake.Sent).Sequence);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Approving_the_same_pins_again_with_a_wake_that_fails_is_refused_and_records_nothing()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = await r.Approve(step);
        r.Wake.Fail = SindriErrors.NotRunning.With("Temporal didn't take the wake");

        var again = await r.Approve(step);

        Assert.True(again.IsError);
        Assert.Equal(SindriErrors.NotRunning.Code, again.Error.Code);
        _ = Assert.Single(r.Events(EventTypes.RecipeApproved));
        _ = Assert.Single((await r.Approvals.History(step.Id, 5, step.Hash, Token)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Drift_reapproval_of_the_same_hash_appends_a_row_and_event_with_the_new_pins_and_wakes_its_instances()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = await r.Approvals.Approve(RegistryData.Approval(step, Stale()), Token);
        var parked = WorkflowInstanceId.For(new Guid(7, 0, 0, new byte[8]));
        r.Wake.Run(step.Hash, parked);

        var issued = (await r.Issue("approve workflow.step 5")).Value;
        var approved = await r.Run<ApproveRecipeRequest, ApproveRecipeResponse>(
            new ApproveRecipeRequest(step.Id, step.Hash, new ChallengeAnswer(issued.Challenge, issued.Action)), RegistryHarness.Human);

        Assert.True(approved.IsSuccess, approved.IsError ? approved.Error.ToString() : null);
        Assert.True(issued.Approve!.IsDrift);   // why: an approval's challenge carries its preview
        Assert.Equal(_blocks, issued.Approve.Changed.Select(c => c.Block.Value), StringComparer.Ordinal);
        Assert.Equal([parked], approved.Value.Resumed);
        var history = (await r.Approvals.History(step.Id, 5, step.Hash, Token)).Value;
        Assert.Equal(2, history.Count);
        Assert.Equal(approved.Value.Preview.Pins.Blocks, history[1].Pins.Blocks);
        Assert.True(r.H.Events.Payload<RecipeApproved>(Assert.Single(r.Events(EventTypes.RecipeApproved))).Value.Drift);
        Assert.Equal(RecipeWakeKind.Approved, Assert.Single(r.Wake.Sent).Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Parent_pins_its_childs_approved_hash_and_waits_for_the_child_to_be_approved()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        var item = await r.Shipped("sindri/workflow.item.yaml");

        var early = await r.Issue("approve workflow.item 3");
        _ = await r.Approve(step);
        var approved = (await r.Approve(item)).Value;

        Assert.Equal(WorkflowErrors.RecipeNotApproved.Code, early.Error.Code);
        Assert.Contains("approve workflow.step first", early.Error.Message, StringComparison.Ordinal);
        Assert.Equal([new ChildPin(step.Id, step.Hash)], approved.Approval.Pins.Children);
        Assert.Equal(["effect.claim.ref"], approved.Preview.Effects.Select(e => e.Kind.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Preview_of_a_new_version_diffs_against_the_last_approved_one()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = await r.Approve(step);
        _ = await r.Register(RegistryHarness.Parse(step) with { Version = 6 });

        var preview = (await r.Issue("approve workflow.step 6")).Value.Approve!;   // why: an approval's challenge carries its preview

        Assert.StartsWith($"--- workflow.step@{step.Hash.Display} (approved, version 5)\n+++ workflow.step@", preview.Diff, StringComparison.Ordinal);
        Assert.Contains("\n-  \"version\": 5,\n+  \"version\": 6,\n", preview.Diff, StringComparison.Ordinal);
    }

    /// <summary>Pins as an older build saw them: the same blocks with other descriptor hashes.</summary>
    private static RecipePins Stale() =>
        new([.. _blocks.Select(b => new BlockPin(BlockId.From(b), RegistryData.Descriptor("old " + b)))], []);

    private static readonly string[] _blocks = ["step.checks", "step.produce", "step.verify"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
