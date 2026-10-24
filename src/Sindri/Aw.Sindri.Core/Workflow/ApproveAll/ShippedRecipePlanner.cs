using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// The shipped recipes' plan (setup-once spec §5.3): every shipped recipe that is not approved at its shipped hash with live pins, in the order approval happens. A recipe is waiting when it was never
/// approved (new), its hash or a pinned block moved (updated), or a recipe it uses moves (second look: its child will be approved at a new hash, which makes the parent child-stale, so the plan is empty
/// once it is applied). A hash the owner revoked stays out: a revoke is a decision, and a revoked hash can't be approved again. Reads only.
/// </summary>
/// <param name="shipped">The shipped recipes.</param>
/// <param name="approvals">The approvals and revocations.</param>
/// <param name="rows">Turns the waiting recipes into the plan's rows.</param>
/// <param name="catalog">This build's blocks, against which an approval's pins hold or drift.</param>
/// <param name="json">The package's AwJson options (they hash the descriptors).</param>
public sealed class ShippedRecipePlanner(ShippedRecipeSet shipped, IRecipeApprovals approvals, ShippedPlanRows rows, BlockCatalog catalog, JsonSerializerOptions json)
{
    /// <summary>The plan now.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The plan (empty when nothing waits).</returns>
    public async ValueTask<Result<ShippedRecipePlan>> Plan(CancellationToken cancellationToken)
    {
        var files = shipped.Files();
        var current = await approvals.Current(cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        List<WaitingRecipe> waiting = [];
        Dictionary<RecipeId, RecipeApproval> standing = [];
        foreach (var file in files)
        {
            var stands = await Stand(file, current.Value, cancellationToken).ConfigureAwait(false);
            if (stands.IsError)
            {
                return stands.Error;
            }

            if (stands.Value.Waiting is { } wait)
            {
                waiting.Add(wait);
            }
            else if (stands.Value.Approval is { } approval)
            {
                standing[file.Id] = approval;
            }
        }

        waiting.AddRange(SecondLooks(files, waiting, standing));
        return await rows.Build(waiting, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The approved recipes that use a recipe about to move, and so those that use them, each with the approval it stands on.</summary>
    private static List<WaitingRecipe> SecondLooks(IReadOnlyList<ShippedFile> files, List<WaitingRecipe> waiting, Dictionary<RecipeId, RecipeApproval> standing)
    {
        List<WaitingRecipe> added = [];
        var moving = waiting.Select(static w => w.Shipped.Id).ToHashSet();
        for (var grown = true; grown;)
        {
            grown = false;
            foreach (var file in files.Where(f => standing.ContainsKey(f.Id) && RecipeChildren.Of(f.Recipe).Any(moving.Contains)))
            {
                added.Add(new WaitingRecipe(file, PlanChange.SecondLook, standing[file.Id]));
                _ = standing.Remove(file.Id);
                _ = moving.Add(file.Id);
                grown = true;
                break;
            }
        }

        return added;
    }

    private async ValueTask<Result<ShippedStanding>> Stand(ShippedFile file, IReadOnlyList<RecipeApproval> current, CancellationToken cancellationToken)
    {
        var revoked = await approvals.Revocation(file.Hash, cancellationToken).ConfigureAwait(false);
        if (revoked.IsError || revoked.Value is not null)
        {
            return revoked.IsError ? revoked.Error : new ShippedStanding(Revoked: true, Waiting: null, Approval: null);
        }

        var latest = await approvals.Latest(file.Id, file.Version, file.Hash, cancellationToken).ConfigureAwait(false);
        return latest.IsError ? latest.Error : Judge(file, latest.Value, current);
    }

    /// <summary>Approved when the shipped hash has an approval whose block pins hold against this build and whose child pins are the latest approved children; else waiting.</summary>
    private ShippedStanding Judge(ShippedFile file, RecipeApproval? latest, IReadOnlyList<RecipeApproval> current)
    {
        var pinsHold = latest is not null && PinRules.Check(latest, revocation: null, PinRules.Blocks(file.Recipe, catalog, json)) == ApprovalCheck.Holds;
        if (latest is not null && pinsHold && latest.Pins.Children.All(pin => LatestOf(pin.Recipe, current)?.Hash == pin.Hash))
        {
            return new ShippedStanding(Revoked: false, Waiting: null, Approval: latest);
        }

        var previous = LatestOf(file.Id, current);
        var change = previous is null ? PlanChange.New : pinsHold ? PlanChange.SecondLook : PlanChange.Updated;
        return new ShippedStanding(Revoked: false, Waiting: new WaitingRecipe(file, change, previous), Approval: null);
    }

    private static RecipeApproval? LatestOf(RecipeId id, IReadOnlyList<RecipeApproval> current) => current.Where(a => a.Id == id).MaxBy(static a => a.Version);
}
