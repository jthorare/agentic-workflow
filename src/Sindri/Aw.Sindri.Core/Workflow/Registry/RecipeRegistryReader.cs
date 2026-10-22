using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>
/// Reads where the registered recipes stand (engine spec §4.1, §6.3 'recipe list [--stale]', §9.1): each recipe's line
/// (unapproved, approved or revoked, how it was approved, why it is stale, its inputs), the registry view the validator
/// checks a new recipe against, the registered child recipes and a base. A read-only domain service over the registry
/// ports, so ListRecipes, ShowRecipe and RegisterRecipe share one reading of standing, and the queries never take a
/// writing port (ARCH-CQRS-002).
/// </summary>
/// <param name="store">The registered recipes.</param>
/// <param name="approvals">The approvals and revocations.</param>
/// <param name="catalog">This build's blocks, against which an approval's pins drift.</param>
/// <param name="json">The package's AwJson options (they read the stored text and hash the descriptors).</param>
/// <param name="profile">The approved profile's facts, whose 'workflow.effects' RCP-EFFECT-001 checks against (S1-D11).</param>
/// <param name="names">The names the app shows instead of the ids (setup-once spec §7.7); null shows the ids.</param>
public sealed class RecipeRegistryReader(IRecipeStore store, IRecipeApprovals approvals, BlockCatalog catalog, JsonSerializerOptions json, IRecipeProfileFacts profile, IRecipeDisplayNames? names = null)
{
    /// <summary>Every registered recipe's line, by id and then version.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The lines (empty when nothing is registered).</returns>
    public async ValueTask<Result<IReadOnlyList<RecipeSummary>>> Summaries(CancellationToken cancellationToken)
    {
        var all = await store.All(cancellationToken).ConfigureAwait(false);
        var current = all.IsError ? all.Error : await approvals.Current(cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        var lines = new List<RecipeSummary>(all.Value.Count);
        foreach (var recipe in all.Value)
        {
            var line = await Summary(recipe, current.Value, cancellationToken).ConfigureAwait(false);
            if (line.IsError)
            {
                return line.Error;
            }

            lines.Add(line.Value);
        }

        return lines;
    }

    /// <summary>The ids of the registered recipes some registered recipe starts as a child (a 'waitFor: { child }').</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The ids; empty when no recipe has a child.</returns>
    public async ValueTask<Result<IReadOnlySet<RecipeId>>> ChildRecipeIds(CancellationToken cancellationToken)
    {
        var all = await store.All(cancellationToken).ConfigureAwait(false);
        if (all.IsError)
        {
            return all.Error;
        }

        var ids = new HashSet<RecipeId>();
        foreach (var registered in all.Value)
        {
            var parsed = Parse(registered);
            if (parsed.IsSuccess)
            {
                ids.UnionWith(parsed.Value.States.Values.Select(static s => s.WaitFor?.Child?.Recipe).OfType<RecipeId>());
            }
        }

        return Results.Success<IReadOnlySet<RecipeId>>(ids);
    }

    /// <summary>The line of <paramref name="recipe"/>.</summary>
    /// <param name="recipe">A registered recipe.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The line.</returns>
    public async ValueTask<Result<RecipeSummary>> Summary(RegisteredRecipe recipe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var current = await approvals.Current(cancellationToken).ConfigureAwait(false);
        return current.IsError ? current.Error : await Summary(recipe, current.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// What the validator sees of the registry (RCP-VERSION-001, -002, RCP-CAP-003): every registered key, and each
    /// approved hash's standing against this build (a child-stale approval still holds; only drift and revoke don't).
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The view.</returns>
    public async ValueTask<Result<RecipeRegistryView>> View(CancellationToken cancellationToken)
    {
        var lines = await Summaries(cancellationToken).ConfigureAwait(false);
        if (lines.IsError)
        {
            return lines.Error;
        }

        var standing = lines.Value
            .Where(static l => l.Standing != RecipeStanding.Unapproved)
            .ToDictionary(static l => l.Hash, static l => l switch
            {
                { Standing: RecipeStanding.Revoked } => RegistryApproval.Revoked,
                _ when l.Stale.Contains(StaleReason.PinDrift) => RegistryApproval.Drifted,
                _ => RegistryApproval.Holds,
            });
        return new RecipeRegistryView([.. lines.Value.Select(static l => new RegisteredVersion(l.Id, l.Version, l.Hash))], standing);
    }

    /// <summary>
    /// The child recipes a served validation resolves (spec §9.1): for each registered id, its latest approved version
    /// that isn't revoked (the one a parent pins), else its highest registered version.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The resolver.</returns>
    public async ValueTask<Result<IChildRecipeResolver>> Children(CancellationToken cancellationToken)
    {
        var all = await store.All(cancellationToken).ConfigureAwait(false);
        var current = all.IsError ? all.Error : await approvals.Current(cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        var found = new Dictionary<RecipeId, ChildRecipe>();
        foreach (var versions in all.Value.GroupBy(static r => r.Id))
        {
            var approved = current.Value.Where(a => a.Id == versions.Key).MaxBy(static a => a.Version);
            var pick = versions.FirstOrDefault(r => r.Hash == approved?.Hash) ?? versions.MaxBy(static r => r.Version)!;   // why: a group is never empty
            var parsed = Parse(pick);
            if (parsed.IsSuccess)
            {
                found[versions.Key] = new ChildRecipe(parsed.Value, pick.Hash);
            }
        }

        return new RegisteredChildren(found);
    }

    /// <summary>
    /// What a served validation of <paramref name="recipe"/> checks against (spec §9.1): this build's catalog, the approved
    /// profile's effect allowlist (RCP-EFFECT-001; skipped when none is approved or it has no 'workflow.effects'), the registered children, the base when
    /// the recipe names a registered one (a recipe naming an unregistered base is hand-written, RCP-CAP-004), and the
    /// registry view (RCP-VERSION-001, -002, RCP-CAP-003).
    /// </summary>
    /// <param name="recipe">The recipe about to be registered.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The context.</returns>
    public async ValueTask<Result<ValidationContext>> Validation(Recipe recipe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var facts = await profile.Current(cancellationToken).ConfigureAwait(false);
        var view = facts.IsError ? facts.Error : await View(cancellationToken).ConfigureAwait(false);
        var children = view.IsError ? view.Error : await Children(cancellationToken).ConfigureAwait(false);
        var baseRecipe = children.IsError || recipe.Base is not { } named
            ? children.IsError ? children.Error : Results.Success<Recipe?>(value: null)
            : await Recipe(named.Hash, cancellationToken).ConfigureAwait(false);
        return baseRecipe.IsError
            ? baseRecipe.Error
            : new ValidationContext(catalog, facts.Value, children.Value, baseRecipe.Value) { Registry = view.Value };
    }

    /// <summary>The recipe registered under <paramref name="hash"/>, read back (a generated recipe's base).</summary>
    /// <param name="hash">The hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The recipe, or null when that hash isn't registered.</returns>
    public async ValueTask<Result<Recipe?>> Recipe(RecipeHash hash, CancellationToken cancellationToken)
    {
        var found = await store.Find(hash, cancellationToken).ConfigureAwait(false);
        if (found.IsError || found.Value is not { } recipe)
        {
            return found.IsError ? found.Error : Results.Success<Recipe?>(value: null);
        }

        var parsed = Parse(recipe);
        return parsed.IsError ? parsed.Error : parsed.Value;
    }

    private async ValueTask<Result<RecipeSummary>> Summary(RegisteredRecipe recipe, IReadOnlyList<RecipeApproval> current, CancellationToken cancellationToken)
    {
        var parsed = Parse(recipe);
        var latest = parsed.IsError ? parsed.Error : await approvals.Latest(recipe.Id, recipe.Version, recipe.Hash, cancellationToken).ConfigureAwait(false);
        var revoked = latest.IsError ? latest.Error : await approvals.Revocation(recipe.Hash, cancellationToken).ConfigureAwait(false);
        if (revoked.IsError)
        {
            return revoked.Error;
        }

        var (standing, stale) = Stand(parsed.Value, latest.Value, revoked.Value, current);
        var order = StatePathOrder.Of(parsed.Value);
        return new RecipeSummary(recipe.Id, recipe.Version, recipe.Hash, standing, latest.Value?.Via, stale, parsed.Value.Inputs, parsed.Value.Urgency, parsed.Value.Outputs, order.OrderedBy, order.States, order.Loops, parsed.Value.Name, names?.Of(recipe.Id));
    }

    /// <summary>
    /// Where a recipe stands: revoked, unapproved, or approved and stale when its pinned blocks drifted from this build
    /// (PinRules.Check) or a pinned child is no longer its id's latest approved hash (revoked, or a newer version approved).
    /// </summary>
    private (RecipeStanding Standing, IReadOnlyList<StaleReason> Stale) Stand(Recipe parsed, RecipeApproval? latest, RecipeRevocation? revoked, IReadOnlyList<RecipeApproval> current)
    {
        if (revoked is not null || latest is null)
        {
            // An unapproved legacy-shape row is marked too: it can't be approved as it stands, and the fix is the same.
            return (revoked is null ? RecipeStanding.Unapproved : RecipeStanding.Revoked, revoked is null && SubjectKinds.Legacy(parsed) is not null ? [StaleReason.LegacyShape] : []);
        }

        var stale = new List<StaleReason>();
        if (PinRules.Check(latest, revocation: null, PinRules.Blocks(parsed, catalog, json)) == ApprovalCheck.Drifted)
        {
            stale.Add(StaleReason.PinDrift);
        }

        if (latest.Pins.Children.Any(pin => current.Where(a => a.Id == pin.Recipe).MaxBy(static a => a.Version)?.Hash != pin.Hash))
        {
            stale.Add(StaleReason.ChildStale);
        }

        if (SubjectKinds.Legacy(parsed) is not null)
        {
            stale.Add(StaleReason.LegacyShape);
        }

        return (RecipeStanding.Approved, stale);
    }

    private Result<Recipe> Parse(RegisteredRecipe recipe) => recipe.Read(json);
}
