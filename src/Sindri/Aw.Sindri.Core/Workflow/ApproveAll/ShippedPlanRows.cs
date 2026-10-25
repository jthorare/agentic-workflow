using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// Turns the waiting shipped recipes into the plan (setup-once spec §5.3): their capabilities in plain words, what each gained since its last approved hash, and the order approval happens in
/// (children first). A previously approved recipe that gains a capability is never in the hold: it goes to the recipes approved one at a time, and so does a recipe waiting on a child that does.
/// A never-approved recipe is new in the hold, with its full set shown. Reads only.
/// </summary>
/// <param name="store">The registered recipes, whose stored text gives a recipe's last approved capabilities.</param>
/// <param name="catalog">This build's blocks, whose sentences name the capabilities.</param>
/// <param name="json">The package's AwJson options (they read the stored text and hash the descriptors).</param>
public sealed class ShippedPlanRows(IRecipeStore store, BlockCatalog catalog, JsonSerializerOptions json)
{
    internal async ValueTask<Result<ShippedRecipePlan>> Build(IReadOnlyList<WaitingRecipe> waiting, CancellationToken cancellationToken)
    {
        List<ApproveAllRow> rows = [];
        Dictionary<RecipeId, OwnApprovalRow> own = [];
        foreach (var item in Ordered(waiting))
        {
            var now = RecipeCapabilitySet.Of(item.Shipped.Recipe, catalog);
            var before = await Before(item.Previous, cancellationToken).ConfigureAwait(false);
            if (before.IsError)
            {
                return before.Error;
            }

            var delta = item.Previous is null ? CapabilityDelta.None : RecipeCapabilitySet.Delta(before.Value, now);
            if (OwnApproval(item, delta, own) is { } alone)
            {
                own[item.Shipped.Id] = alone;
            }
            else
            {
                rows.Add(Row(item, now, delta));
            }
        }

        return new ShippedRecipePlan(rows, [.. own.Values]);
    }

    private ApproveAllRow Row(WaitingRecipe item, IReadOnlyList<CapabilityLine> now, CapabilityDelta delta)
    {
        var shipped = item.Shipped;
        IReadOnlyList<string> changed = item.Previous is { } previous
            ? [.. PinRules.Blocks(shipped.Recipe, catalog, json).Where(pin => !previous.Pins.Blocks.Contains(pin)).Select(static pin => pin.Block.Value)]
            : [];
        return new ApproveAllRow(shipped.Id, shipped.Name, shipped.Version, shipped.Hash, item.Previous?.Hash, item.Change, PlanChangeWords.For(item.Change), now, delta, changed, [.. RecipeChildren.Of(shipped.Recipe)]);
    }

    private static OwnApprovalRow? OwnApproval(WaitingRecipe item, CapabilityDelta delta, Dictionary<RecipeId, OwnApprovalRow> own)
    {
        var name = item.Shipped.Name;
        if (item.Previous is null)
        {
            return null;
        }

        if (item.Change != PlanChange.SecondLook && delta.Added.Count > 0)
        {
            return new OwnApprovalRow(item.Shipped.Id, name, OwnApprovalReason.GainsCapability,
                $"{name} can now do more: {string.Join("; ", delta.Added.Select(static c => c.Sentence))}.", delta.Added)
            {
                Hash = item.Shipped.Hash,
                Version = item.Shipped.Version,
            };
        }

        var child = RecipeChildren.Of(item.Shipped.Recipe).Where(own.ContainsKey).Select(id => own[id]).FirstOrDefault();
        return child is null ? null : new OwnApprovalRow(item.Shipped.Id, name, OwnApprovalReason.WaitsForChild, $"{name} waits for {child.DisplayName}, which gained a capability.", [])
        {
            Hash = item.Shipped.Hash,
            Version = item.Shipped.Version,
        };
    }

    /// <summary>The capabilities of the last approved hash, from its stored recipe; empty when that text is gone (every capability then looks new, the safe direction).</summary>
    private async ValueTask<Result<IReadOnlyList<CapabilityLine>>> Before(RecipeApproval? previous, CancellationToken cancellationToken)
    {
        if (previous is null)
        {
            return Results.Success<IReadOnlyList<CapabilityLine>>([]);
        }

        var found = await store.Find(previous.Hash, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var read = found.Value?.Read(json);
        return read is { IsSuccess: true } ? Results.Success(RecipeCapabilitySet.Of(read.Value, catalog)) : Results.Success<IReadOnlyList<CapabilityLine>>([]);
    }

    /// <summary>The waiting recipes with every recipe after the recipes it uses; ties keep the embedded order.</summary>
    private static List<WaitingRecipe> Ordered(IReadOnlyList<WaitingRecipe> waiting)
    {
        List<WaitingRecipe> ordered = [];
        HashSet<RecipeId> visited = [];
        void Visit(WaitingRecipe item)
        {
            if (!visited.Add(item.Shipped.Id))
            {
                return;
            }

            foreach (var child in RecipeChildren.Of(item.Shipped.Recipe).Select(id => waiting.FirstOrDefault(w => w.Shipped.Id == id)).OfType<WaitingRecipe>())
            {
                Visit(child);
            }

            ordered.Add(item);
        }

        foreach (var item in waiting)
        {
            Visit(item);
        }

        return ordered;
    }
}
