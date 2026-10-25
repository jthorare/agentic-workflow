using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Workflow.ApproveAll;

namespace Aw.Sindri.Tests.ApproveAll;

/// <summary>The shipped sets the approve-all tests and goldens share: the spec's examples (setup-once spec §8), with the hashes padded to 64 digits.</summary>
internal static class ApproveAllFixtures
{
    public static readonly RecipeId Step = RecipeId.From("workflow.step");
    public static readonly RecipeId Item = RecipeId.From("workflow.item");
    public static readonly RecipeId Note = RecipeId.From("workflow.note");
    public static readonly RecipeId Review = RecipeId.From("workflow.review");
    public static readonly RecipeId Ship = RecipeId.From("workflow.ship");

    private static readonly CapabilityLine _produce = new(CapabilityId.From("step.produce"), "Starts an AI agent session in a repo you added");
    private static readonly CapabilityLine _notify = new(CapabilityId.From("effect.notify.desk"), "Puts a question or notice in your Needs you list");
    private static readonly CapabilityLine _shipOpen = new(CapabilityId.From("step.ship.open"), "Opens or updates a pull request and reads its checks");

    public static RecipeHash Hash(string prefix) => RecipeHash.From("sha256:" + prefix.PadRight(64, '0'));

    /// <summary>'One step' (updated since its last approval) and 'Work item' (never approved, uses the step).</summary>
    public static ShippedSeed[] StepAndItem() =>
    [
        new(Step, "One step", Hash("1a2b3c4d5e6f"), [], [_produce, _notify], Hash("0f1e2d3c4b5a"), Version: 2),
        new(Item, "Work item", Hash("9f8e7d6c5b4a"), [Step], [], Version: 3),
    ];

    /// <summary>The fresh-box plan: 'Ship a change', never approved, with a pull-request capability.</summary>
    public static ShippedSeed[] FirstShip() => [new(Ship, "Ship a change", Hash("5d4c3b2a1f0e"), [], [_shipOpen, _notify], Version: 2)];

    /// <summary>The step, a note, an item the checks refuse (it changed while Sindri was approving) and a review loop that waits on the item.</summary>
    public static ShippedSeed[] OneRefused() =>
    [
        new(Step, "One step", Hash("1a2b3c4d5e6f"), [], [_produce], Hash("0f1e2d3c4b5a"), Version: 2),
        new(Note, "Note", Hash("2c3d4e5f6a7b"), [], [], Version: 1),
        new(Item, "Work item", Hash("9f8e7d6c5b4a"), [Step], [], Version: 3,
            Refusal: new SeededRefusal("SND-RECIPE-009", "Work item changed while Sindri was approving. Read the new plan.", "sindri recipe approve <id>")),
        new(Review, "Review loop", Hash("4e5f6a7b8c9d"), [Item], [], Version: 1),
    ];

    /// <summary>'Ship a change' was approved before and now adds a pull-request capability: it needs its own approval and is not in the plan.</summary>
    public static OwnApprovalRow ShipNeedsItsOwn() =>
        new(Ship, "Ship a change", OwnApprovalReason.GainsCapability, "Ship a change can now open a pull request. Approve it on its own after reading it.", [_shipOpen]);
}
