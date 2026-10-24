using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>A new instance (spec §5.1, §5.2), and the engine triggers the decision hands to the frame and child rules (§5.7).</summary>
public sealed class StartAndEngineTriggerTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Start_counts_budgets_per_round_unless_the_history_predates_the_marker(bool pooledBudget, bool perRound)
    {
        var context = EngineFixture.Context() with { PooledBudget = pooledBudget };

        var applied = Assert.IsType<Applied>(TransitionDecision.Start(Request(), Step, context, new FakeEngineRules()));

        Assert.Equal(perRound, applied.Changes[0].After.Spent.IsPerRound);
    }

    [Fact, Trait("Lane", "unit")]
    public void Start_enters_the_initial_state_as_entry_one()
    {
        var applied = Assert.IsType<Applied>(TransitionDecision.Start(Request(), Step, EngineFixture.Context(), new FakeEngineRules()));

        var (row, started) = (applied.Changes[0].After, applied.Changes[0].Event);
        Assert.Equal(("produce", 1, RunToken.For(1, 1)), (row.State, row.Frame.Entry, row.Frame.Run!.Token));
        Assert.Equal(new LoopState(1, 5, Last: null), row.Loops["revisions"]);
        Assert.Equal(Fact("Add a --version flag."), row.Facts[FactKey.From("fact:spec")]);
        Assert.Equal(WorkflowEventType.Started, started.Type);
        Assert.Same(row, started.Started);
        var start = Assert.Single(applied.FollowUps);
        Assert.Equal((FollowUpKind.StartRun, BlockId.From("step.produce")), (start.Kind, start.Step!.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void Start_keeps_the_tenant_and_ring_of_the_workflow_input()
    {
        var applied = Assert.IsType<Applied>(TransitionDecision.Start(Request() with { Tenant = "acme", Ring = 2 }, Step, EngineFixture.Context(), new FakeEngineRules()));

        var (row, started) = (applied.Changes[0].After, applied.Changes[0].Event);
        Assert.Equal(("acme", 2), (row.Tenant, row.Ring));
        Assert.Equal(("acme", 2), (started.Started!.Tenant, started.Started.Ring));
    }

    [Fact, Trait("Lane", "unit")]
    public void Start_on_a_revoked_recipe_is_rejected()
    {
        var decision = TransitionDecision.Start(Request(), Step, EngineFixture.Context() with { Approval = ApprovalCheck.Revoked }, new FakeEngineRules());

        Assert.Equal("SND-RECIPE-005", Assert.IsType<Rejected>(decision).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Start_requested_taken_over_starts_taken_over()
    {
        var applied = Assert.IsType<Applied>(TransitionDecision.Start(Request() with { TakenOver = true }, Step, EngineFixture.Context(), new FakeEngineRules()));

        var row = applied.Changes[0].After;
        Assert.Equal((EngineStates.TakenOver, "produce"), (row.State, row.Frame.State));
    }

    [Fact, Trait("Lane", "unit")]
    public void Start_taken_over_records_the_callers_actor_and_only_the_takeover_is_the_engines()
    {
        var applied = Assert.IsType<Applied>(TransitionDecision.Start(Request() with { TakenOver = true }, Step, EngineFixture.Context(), new FakeEngineRules()));

        var (row, started) = (applied.Changes[0].After, applied.Changes[0].Event);
        Assert.Equal(("human:thor", "service:engine"), (started.Trigger.Actor, row.Suspension?.Actor));
    }

    [Fact, Trait("Lane", "unit")]
    public void Child_parked_mirrors_the_park_and_resumed_releases_it()
    {
        var running = At(Item, "running", entry: 4) with { Frame = At(Item, "running", entry: 4).Frame with { Child = ChildId } };
        var parked = new ChildChanged("service:workflow.inbox", ChildId, ChildChange.Parked, ParkReason.NoProgress, "same findings twice", TriggerKey.For(ChildId, "run:3.1:abc"));

        var mirrored = Assert.IsType<Applied>(Decide(running, Item, parked)).Changes[0].After;
        var resumed = new ChildChanged("service:workflow.inbox", ChildId, ChildChange.Resumed, Reason: null, Question: null, TriggerKey.For(ChildId, "cmd:x"));
        var released = Assert.IsType<Applied>(Decide(mirrored, Item, resumed)).Changes[0].After;

        Assert.Equal((EngineStates.Parked, ParkReason.NoProgress), (mirrored.State, mirrored.Park));
        Assert.Equal(("running", 4, ChildId), (released.State, released.Frame.Entry, released.Frame.Child));
    }

    [Fact, Trait("Lane", "unit")]
    public void Cancel_is_exempt_from_the_approval_check()
    {
        var cancel = new CancelRequested("human:thor", CancelOrigin.Command, new CommandStamp(CommandNonce.From(new string('b', 32)), Sha("cancel")), Sequence: null, Parent: null);

        var applied = Assert.IsType<Applied>(Decide(At(Step, "produce"), Step, cancel, context: EngineFixture.Context() with { Approval = ApprovalCheck.Revoked }));

        Assert.Equal((EngineStates.Cancelled, "cancelled"), (applied.Changes[0].After.State, applied.Changes[0].After.Outcome?.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_grandchild_wait_creates_a_grandchild_with_an_id_of_its_own()
    {
        var middle = Nested("workflow.middle", "workflow.step");
        var outer = Nested("workflow.outer", "workflow.middle");
        var context = EngineFixture.Context() with { Children = new InMemoryChildRecipes().With(middle).With(new ChildRecipe(Step, Step.Hash!.Value)) };

        var applied = Assert.IsType<Applied>(TransitionDecision.Start(NestedRequest(outer, middle), outer.Recipe, context));

        Assert.Equal(3, applied.Changes.Count);
        var (root, child, grandchild) = (applied.Changes[0].After, applied.Changes[1].After, applied.Changes[2].After);
        Assert.Equal(3, new[] { root.Id, child.Id, grandchild.Id }.Distinct().Count());
        // §5.1: each child's workflow id is its address, and its opaque id is derived from that address.
        Assert.Equal(root.Address.Child(middle.Recipe.Id, 1), child.Address);
        Assert.Equal(child.Address.Child(Step.Id, 1), grandchild.Address);
        Assert.Equal((IdFor(child.Address), IdFor(grandchild.Address)), (child.Id, grandchild.Id));
        Assert.Equal(TriggerKey.For(grandchild.Id, "start"), applied.Changes[2].Event.Trigger.Key);
        Assert.Equal((child.Id, root.Id), (root.Frame.Child, child.Parent));
        Assert.Equal((grandchild.Id, child.Id), (child.Frame.Child, grandchild.Parent));
        Assert.Equal(Step.Hash, child.Recipe.Children[Step.Id]);
        Assert.Equal(("produce", RunToken.For(1, 1)), (grandchild.State, grandchild.Frame.Run?.Token));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_child_wait_the_pins_lack_is_rejected_not_thrown()
    {
        var middle = Nested("workflow.middle", "workflow.step");
        var outer = Nested("workflow.outer", "workflow.middle");
        var context = EngineFixture.Context() with { Children = new InMemoryChildRecipes().With(middle).With(new ChildRecipe(Step, Step.Hash!.Value)) };
        var unpinned = NestedRequest(outer, middle) with { Recipe = NestedRequest(outer, middle).Recipe with { Children = new Dictionary<RecipeId, RecipeHash>() } };

        var rejected = Assert.IsType<Rejected>(TransitionDecision.Start(unpinned, outer.Recipe, context));

        Assert.Equal("SND-RECIPE-002", rejected.Error.Code);
        Assert.Contains("workflow.middle", rejected.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_child_that_resolves_to_another_hash_than_the_pin_is_rejected()
    {
        var middle = Nested("workflow.middle", "workflow.step");
        var outer = Nested("workflow.outer", "workflow.middle");
        var drifted = new ChildRecipe(middle.Recipe, RecipeHash.From("sha256:" + new string('6', 64)));
        var context = EngineFixture.Context() with { Children = new InMemoryChildRecipes().With(drifted).With(new ChildRecipe(Step, Step.Hash!.Value)) };

        var rejected = Assert.IsType<Rejected>(TransitionDecision.Start(NestedRequest(outer, middle), outer.Recipe, context));

        Assert.Equal("SND-RECIPE-002", rejected.Error.Code);
        Assert.Contains(middle.Hash.Value, rejected.Error.Message, StringComparison.Ordinal);
    }

    /// <summary>A child's id as spec §5.4 derives it: a UUIDv8 over SHA-256 of the creating trigger's key.</summary>
    private static WorkflowInstanceId IdFor(InstanceAddress address)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(address.Value))[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return WorkflowInstanceId.For(new Guid(bytes, bigEndian: true));
    }

    /// <summary>A recipe whose initial state waits on a <paramref name="child"/> child.</summary>
    private static ChildRecipe Nested(string id, string child)
    {
        string[] lines =
        [
            "schema: 1",
            $"id: {id}",
            "version: 1",
            "inputs: [{ key: fact:item, type: item, kind: item }]",
            "initial: running",
            "states:",
            "  running: { kind: wait, timeout: { after: 14d, then: park },",
            $"             waitFor: {{ child: {{ recipe: {child}, inputs: {{ \"fact:spec\": {{ fact: fact:stepSpec }} }}, onParked: propagate }} }},",
            "             next: \"Waiting for {child}.\" }",
            "  done:    { kind: terminal, outcome: resolved }",
            "  dropped: { kind: terminal, outcome: unresolved }",
            "transitions:",
            "  - { from: running, to: done,    on: wait.satisfied, when: { outcome: resolved } }",
            "  - { from: running, to: dropped, on: wait.satisfied, when: { outcome: cancelled } }",
        ];
        var yaml = string.Join('\n', lines) + "\n";
        var document = RecipeComposition.ReadClean(yaml, id + ".yaml");
        return new ChildRecipe(document.Recipe with { Hash = document.Hash }, document.Hash);
    }

    private static StartRequest NestedRequest(ChildRecipe outer, ChildRecipe middle) => Request() with
    {
        Address = InstanceAddress.Root(ItemKey.From("T-3"), outer.Recipe.Id, 1),
        Recipe = new PinnedRecipe(outer.Recipe.Id, 1, outer.Hash, new Dictionary<RecipeId, RecipeHash> { [middle.Recipe.Id] = middle.Hash }),
    };

    private static StartRequest Request() => new(
        TriggerKey.For(Id, "cmd:" + new string('c', 32) + ":start:" + Sha("start")),
        Id,
        InstanceAddress.Root(ItemKey.From("T-3"), Step.Id, 1),
        new PinnedRecipe(Step.Id, Step.Version, Step.Hash!.Value, new Dictionary<RecipeId, RecipeHash>()),
        ItemKey.From("T-3"),
        Attempt: 1,
        Parent: null,
        new Dictionary<FactKey, FactValue> { [FactKey.From("fact:spec")] = Fact("Add a --version flag.") },
        "human:thor",
        TakenOver: false,
        Tenant: "local",
        Ring: 0);
}
