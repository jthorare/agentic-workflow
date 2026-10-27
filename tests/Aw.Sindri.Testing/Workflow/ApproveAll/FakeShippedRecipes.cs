using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Testing.Workflow.ApproveAll;

/// <summary>
/// The shipped recipes with a seeded set (the stand-in for the workflow engine's implementation of <see cref="IShippedRecipes"/>): its plan lists the recipes not approved
/// yet, children first, and applying it approves them in order, refuses a seeded refusal and skips what waits on it. It writes nothing but its own record of approvals.
/// </summary>
public sealed class FakeShippedRecipes : IShippedRecipes
{
    private readonly List<ShippedSeed> _seeds;
    private readonly List<ApproveAllApproved> _approved = [];

    /// <summary>A shipped set.</summary>
    /// <param name="seeds">The recipes, in any order.</param>
    public FakeShippedRecipes(IEnumerable<ShippedSeed> seeds)
    {
        ArgumentNullException.ThrowIfNull(seeds);
        _seeds = [.. seeds];
    }

    /// <summary>Every recipe approved so far, in the order it was approved.</summary>
    public IReadOnlyList<ApproveAllApproved> Approved => _approved;

    /// <summary>When set, an apply waits for it before it approves anything (a job that is still running).</summary>
    public TaskCompletionSource? Hold { get; set; }

    /// <summary>How many plans were applied.</summary>
    public int Applied { get; private set; }

    /// <summary>The previously approved recipes the plan hands to <see cref="ShippedRecipePlan.OwnApproval"/>.</summary>
    public ICollection<OwnApprovalRow> OwnApproval { get; } = [];

    /// <summary>Arranges the seeded recipe <paramref name="id"/> to change hash (the plan moved between the preview and the apply).</summary>
    /// <param name="id">The recipe.</param>
    /// <param name="hash">Its new hash.</param>
    public void Move(RecipeId id, RecipeHash hash) => _seeds[_seeds.FindIndex(s => s.Id == id)] = _seeds.First(s => s.Id == id) with { Hash = hash };

    /// <inheritdoc/>
    public ValueTask<Result<ShippedRecipePlan>> Plan(CancellationToken cancellationToken)
    {
        var rows = Ordered().Where(s => !_approved.Exists(a => a.Id == s.Id && a.Hash == s.Hash)).Select(Row).ToList();
        return ValueTask.FromResult<Result<ShippedRecipePlan>>(new ShippedRecipePlan(rows, [.. OwnApproval]));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<ApproveAllResult>> Approve(RedeemedPlan plan, IProgress<string> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(progress);
        Applied++;
        if (Hold is { } held)
        {
            await held.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        List<ApproveAllApproved> approved = [];
        List<ApproveAllRefused> refused = [];
        List<ApproveAllSkipped> skipped = [];
        foreach (var row in plan.Plan.Recipes)
        {
            progress.Report(row.DisplayName);
            var blocked = row.Children.Cast<RecipeId?>().FirstOrDefault(c => refused.Exists(r => r.Id == c) || skipped.Exists(s => s.Id == c));
            var seed = _seeds.Find(s => s.Id == row.Id);
            if (blocked is { } because)
            {
                skipped.Add(new ApproveAllSkipped(row.Id, row.DisplayName, because));
            }
            else if (Refusal(row, seed) is { } refusal)
            {
                refused.Add(refusal);
            }
            else
            {
                var done = new ApproveAllApproved(row.Id, row.DisplayName, row.Hash);
                approved.Add(done);
                _approved.Add(done);
            }
        }

        return new ApproveAllResult(approved, refused, skipped);
    }

    private static ApproveAllRefused? Refusal(ApproveAllRow row, ShippedSeed? seed)
    {
        var cli = $"sindri recipe approve {row.Id.Value}";
        return seed is null ? new ApproveAllRefused(row.Id, row.Hash, WorkflowErrors.RecipeNotShipped.Code, $"{row.DisplayName} is not one this build ships.", cli)
            : seed.Hash != row.Hash ? new ApproveAllRefused(row.Id, row.Hash, WorkflowErrors.RecipeChallengeStale.Code, $"{row.DisplayName} changed while Sindri was approving. Read the new plan.", cli)
            : seed.Refusal is { } r ? new ApproveAllRefused(row.Id, row.Hash, r.Code, r.Sentence, r.Cli)
            : null;
    }

    private ApproveAllRow Row(ShippedSeed seed)
    {
        var change = seed.PreviousApprovedHash is null ? PlanChange.New : PlanChange.Updated;
        return new ApproveAllRow(seed.Id, seed.DisplayName, seed.Version, seed.Hash, seed.PreviousApprovedHash, change, PlanChangeWords.For(change), seed.Capabilities, CapabilityDelta.None, [], seed.Children);
    }

    /// <summary>The seeds with every recipe after the recipes it uses; ties keep the seeded order.</summary>
    private List<ShippedSeed> Ordered()
    {
        List<ShippedSeed> ordered = [];
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        void Visit(ShippedSeed seed)
        {
            if (ordered.Contains(seed) || !visiting.Add(seed.Id.Value))
            {
                return;
            }

            foreach (var child in seed.Children.Select(c => _seeds.Find(s => s.Id == c)).OfType<ShippedSeed>())
            {
                Visit(child);
            }

            ordered.Add(seed);
        }

        _seeds.ForEach(Visit);
        return ordered;
    }
}
