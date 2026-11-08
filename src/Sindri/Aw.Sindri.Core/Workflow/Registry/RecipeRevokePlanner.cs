using Aw.Kernel;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>
/// Plans a revoke (engine spec §4.1, §4.2, W11): the cascade (the generated recipes approved from the hash, and theirs in
/// turn), the parents that pin it as a child, and the running instances that will park. IssueChallenge and RevokeRecipe
/// share it, so the preview a human reads is the one the revoke is checked against. A domain service; it writes nothing.
/// </summary>
/// <param name="store">The registered recipes.</param>
/// <param name="approvals">The approvals and revocations.</param>
/// <param name="wake">The running instances.</param>
public sealed class RecipeRevokePlanner(IRecipeStore store, IRecipeApprovals approvals, IRecipeWake wake)
{
    /// <summary>The plan for 'revoke &lt;id&gt;@&lt;hash&gt;', the hash given in full or by a prefix of 8 or more hex digits.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="hash">The hash or its prefix.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The plan; SND-RECIPE-002 when no registered version of the id matches; SND-RECIPE-003 when it was never approved; SND-RECIPE-005 when it is already revoked and nothing approved from it is left to cascade to.</returns>
    public async ValueTask<Result<RevokePlan>> Revoke(RecipeId id, string hash, CancellationToken cancellationToken)
    {
        var versions = await store.Versions(id, cancellationToken).ConfigureAwait(false);
        if (versions.IsError)
        {
            return versions.Error;
        }

        var matching = versions.Value.Where(r => string.Equals(r.Hash.Value, hash, StringComparison.Ordinal) || r.Hash.MatchesPrefix(hash)).ToList();
        return matching.Count == 1
            ? await Revoke(matching[0], cancellationToken).ConfigureAwait(false)
            : WorkflowErrors.RecipeNotFound.With(matching.Count == 0
                ? $"no registered {id.Value} has hash {hash}; sindri recipe list shows what is"
                : $"{hash} matches more than one {id.Value}; give more of the hash");
    }

    /// <summary>The plan for revoking <paramref name="recipe"/>.</summary>
    /// <param name="recipe">The registered recipe.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The plan, or the refusal.</returns>
    public async ValueTask<Result<RevokePlan>> Revoke(RegisteredRecipe recipe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var refusal = await Refusal(recipe, cancellationToken).ConfigureAwait(false);
        if (refusal is not null)
        {
            return refusal;
        }

        var cascade = await Cascade(recipe.Hash, cancellationToken).ConfigureAwait(false);
        if (cascade.IsError)
        {
            return cascade.Error;
        }

        var parents = await approvals.Pinning(recipe.Hash, cancellationToken).ConfigureAwait(false);
        if (parents.IsError)
        {
            return parents.Error;
        }

        var parks = await Parks([recipe.Hash, .. cascade.Value.Select(static a => a.Hash)], cancellationToken).ConfigureAwait(false);
        if (parks.IsError)
        {
            return parks.Error;
        }

        var preview = new RevokePreview(recipe.Id, recipe.Version, recipe.Hash, parks.Value, TakeoversEnded: [],
            [.. cascade.Value.Select(static a => new RecipeRef(a.Id, a.Hash))], [.. parents.Value.Select(static a => new RecipeRef(a.Id, a.Hash))]);
        return new RevokePlan(recipe, preview, cascade.Value, ChallengeAction.Revoke(recipe.Id, recipe.Hash),
            ChallengeSubject.ForRecipe(recipe.Hash, recipe.Version, pinsDigest: null));
    }

    private async ValueTask<Error?> Refusal(RegisteredRecipe recipe, CancellationToken cancellationToken)
    {
        var name = $"{recipe.Id.Value}@{recipe.Hash.Display}";
        var revoked = await approvals.Revocation(recipe.Hash, cancellationToken).ConfigureAwait(false);
        if (revoked.IsError)
        {
            return revoked.Error;
        }

        if (revoked.Value is not null)
        {
            // A revoke whose cascade stopped partway (the hash stamped, some generated recipes not) is finished by running
            // it again: every step is keyed by its hash, so the stamped ones record nothing new.
            var rest = await Cascade(recipe.Hash, cancellationToken).ConfigureAwait(false);
            return rest.IsError ? rest.Error
                : rest.Value.Count == 0 ? WorkflowErrors.RecipeRevoked.With($"{name} is already revoked; nothing to do")
                : null;
        }

        var latest = await approvals.Latest(recipe.Id, recipe.Version, recipe.Hash, cancellationToken).ConfigureAwait(false);
        return latest.IsError ? latest.Error
            : latest.Value is null ? WorkflowErrors.RecipeNotApproved.With($"{name} was never approved, so there is nothing to revoke")
            : null;
    }

    /// <summary>The live approvals made from <paramref name="root"/>, and from those in turn (through revoked ones too), nearest first; each once.</summary>
    private async ValueTask<Result<IReadOnlyList<RecipeApproval>>> Cascade(RecipeHash root, CancellationToken cancellationToken)
    {
        var found = new List<RecipeApproval>();
        var queue = new Queue<RecipeHash>([root]);
        var seen = new HashSet<RecipeHash> { root };
        while (queue.TryDequeue(out var hash))
        {
            var made = await approvals.GeneratedFrom(hash, cancellationToken).ConfigureAwait(false);
            if (made.IsError)
            {
                return made.Error;
            }

            foreach (var approval in made.Value.Where(a => seen.Add(a.Hash)))
            {
                queue.Enqueue(approval.Hash);   // walk through a recipe already stamped, so a resumed revoke reaches what lies below it
                var revoked = await approvals.Revocation(approval.Hash, cancellationToken).ConfigureAwait(false);
                if (revoked.IsError)
                {
                    return revoked.Error;
                }

                if (revoked.Value is null)
                {
                    found.Add(approval);
                }
            }
        }

        return found;
    }

    private async ValueTask<Result<IReadOnlyList<WorkflowInstanceId>>> Parks(IReadOnlyList<RecipeHash> hashes, CancellationToken cancellationToken)
    {
        var instances = new List<WorkflowInstanceId>();
        foreach (var hash in hashes)
        {
            var running = await wake.Running(hash, cancellationToken).ConfigureAwait(false);
            if (running.IsError)
            {
                return running.Error;
            }

            instances.AddRange(running.Value);
        }

        return instances;
    }
}
