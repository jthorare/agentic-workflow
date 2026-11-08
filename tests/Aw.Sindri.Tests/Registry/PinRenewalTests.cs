using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.ApproveRecipe;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Registry;

/// <summary>
/// Pin drift (SND-RECIPE-006) renews itself when only a pinned block's version moved and its interface is as it was (owner q93, 2026-10-23;
/// ENG-064, RC-04), so a build that bumps a block does not park the running instances. A changed interface still drifts.
/// </summary>
public sealed class PinRenewalTests
{
    private static readonly BlockDescriptor _produce = TestBlocks.Step("step.produce");

    [Fact, Trait("Lane", "unit")]
    public void A_version_bump_keeps_the_interface_hash_and_a_contract_change_moves_it()
    {
        var shape = PinRules.Interface(_produce, SliceHarness.Json);

        Assert.Equal(shape, PinRules.Interface(_produce with { Version = _produce.Version + 1 }, SliceHarness.Json));
        Assert.NotEqual(PinRules.Hash(_produce, SliceHarness.Json), PinRules.Hash(_produce with { Version = _produce.Version + 1 }, SliceHarness.Json));
        Assert.NotEqual(shape, PinRules.Interface(_produce with { Capabilities = Capability.Network }, SliceHarness.Json));
        Assert.NotEqual(shape, PinRules.Interface(_produce with { Parameters = [.. _produce.Parameters, new ParameterSpec("extra", ParameterType.Text, Required: false, IsCapabilityField: false)] }, SliceHarness.Json));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_block_version_bump_with_the_same_interface_is_renewed_and_logged_instead_of_drifting()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = (await r.Approve(step)).Value;
        await Rewind(r, step, keepInterface: true);
        var parked = WorkflowInstanceId.For(new Guid(9, 0, 0, new byte[8]));
        r.Wake.Run(step.Hash, parked);

        var check = await r.Check(step);

        Assert.Equal(ApprovalCheck.Holds, check.Value);
        var renewal = r.Events(EventTypes.RecipeApproved)[^1];
        Assert.Equal("service:pin-drift", renewal.Envelope.Actor);
        Assert.True(r.H.Events.Payload<RecipeApproved>(renewal).Value.Drift);
        var standing = (await r.Approvals.Latest(step.Id, step.Version, step.Hash, TestContext.Current.CancellationToken)).Value!;
        Assert.Equal("service:pin-drift", standing.Approver);
        Assert.Equal(Build(r, step).Select(b => (b.Block, b.Hash, b.Interface)), standing.Pins.Blocks.Select(b => (b.Block, b.Hash, b.Interface)));
        Assert.Equal(RecipeWakeKind.Approved, r.Wake.Sent[^1].Kind);
        Assert.Equal([parked], await Running(r, step));
        Assert.Equal(ApprovalCheck.Holds, (await r.Check(step)).Value);   // why: the next check finds it holding and writes nothing
        Assert.Equal(2, r.Events(EventTypes.RecipeApproved).Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_changed_interface_still_drifts_and_writes_nothing()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = (await r.Approve(step)).Value;
        await Rewind(r, step, keepInterface: false);

        var check = await r.Check(step);

        Assert.Equal(ApprovalCheck.Drifted, check.Value);
        _ = Assert.Single(r.Events(EventTypes.RecipeApproved));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_approval_from_before_interfaces_were_pinned_drifts_until_it_is_approved_again()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = (await r.Approve(step)).Value;
        var standing = (await r.Approvals.Latest(step.Id, step.Version, step.Hash, TestContext.Current.CancellationToken)).Value!;
        var old = standing with { Pins = standing.Pins with { Blocks = [.. standing.Pins.Blocks.Select(b => new BlockPin(b.Block, RegistryData.Descriptor("old " + b.Block.Value)))] } };
        _ = await r.Approvals.Approve(old, TestContext.Current.CancellationToken);

        Assert.Equal(ApprovalCheck.Drifted, (await r.Check(step)).Value);
        _ = Assert.Single(r.Events(EventTypes.RecipeApproved));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_lease_the_renewal_cannot_be_logged_so_the_drift_stands()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = (await r.Approve(step)).Value;
        await Rewind(r, step, keepInterface: true);
        r.H.Held.Clear();

        Assert.Equal(ApprovalCheck.Drifted, (await r.Check(step)).Value);
    }

    private static IReadOnlyList<BlockPin> Build(RegistryHarness r, RegisteredRecipe step) =>
        PinRules.Blocks(step.Read(SliceHarness.Json).Value, r.Services.GetRequiredService<BlockCatalog>(), SliceHarness.Json);

    private static async Task<IReadOnlyList<WorkflowInstanceId>> Running(RegistryHarness r, RegisteredRecipe step) =>
        (await r.Wake.Running(step.Hash, TestContext.Current.CancellationToken)).Value;

    /// <summary>Stands the approval at the pins a previous build would have recorded: every descriptor hash other, the interface the same or not.</summary>
    private static async Task Rewind(RegistryHarness r, RegisteredRecipe step, bool keepInterface)
    {
        var standing = (await r.Approvals.Latest(step.Id, step.Version, step.Hash, TestContext.Current.CancellationToken)).Value!;
        var before = standing.Pins.Blocks.Select(b => new BlockPin(b.Block, RegistryData.Descriptor("old " + b.Block.Value), keepInterface ? b.Interface : RegistryData.Descriptor("old interface " + b.Block.Value)));
        _ = await r.Approvals.Approve(standing with { Pins = standing.Pins with { Blocks = [.. before] } }, TestContext.Current.CancellationToken);
    }
}
