using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>
/// Plans an approval (engine spec §4.1, §4.2, W8, W11): reads the registered recipe and its approvals, works out the pins
/// (each named block's descriptor hash, each child's latest approved hash) and the preview, and names the challenge's
/// action and subject. IssueChallenge, ApproveRecipe and ShowRecipe share it, so the preview a human reads is the one the
/// approval is checked against. A domain service over the registry ports; it writes nothing.
/// </summary>
/// <param name="store">The registered recipes.</param>
/// <param name="approvals">The approvals and revocations.</param>
/// <param name="wake">The running instances (a drift approval resumes its parked ones).</param>
/// <param name="catalog">This build's blocks.</param>
/// <param name="json">The package's AwJson options (they read the stored text and hash the pins).</param>
/// <param name="profile">The approved profile's facts (its effect allowlist, which the preview marks each effect against).</param>
/// <param name="inputRuntimes">The Step inputs this build runs (an approval refuses one with no runtime, SND-WF-016).</param>
public sealed class RecipeApprovalPlanner(IRecipeStore store, IRecipeApprovals approvals, IRecipeWake wake, BlockCatalog catalog, JsonSerializerOptions json, IRecipeProfileFacts profile, StepInputRuntimes inputRuntimes)
{
    /// <summary>The plan for 'approve &lt;id&gt; &lt;version&gt;': the version registered under that id.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="version">Its version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The plan, or SND-RECIPE-002 when that version isn't registered, or the plan's refusal.</returns>
    public async ValueTask<Result<ApprovalPlan>> Approve(RecipeId id, int version, CancellationToken cancellationToken)
    {
        var versions = await store.Versions(id, cancellationToken).ConfigureAwait(false);
        if (versions.IsError)
        {
            return versions.Error;
        }

        return versions.Value.FirstOrDefault(r => r.Version == version) is { } recipe
            ? await Approve(recipe, cancellationToken).ConfigureAwait(false)
            : WorkflowErrors.RecipeNotFound.With(string.Create(CultureInfo.InvariantCulture,
                $"{id.Value} version {version} isn't registered; register it with sindri recipe register <file> (sindri recipe list shows what is)"));
    }

    /// <summary>The plan for 'approve &lt;id&gt;@&lt;hash&gt;', the hash given in full or by a prefix of 8 or more hex digits.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="hash">The hash or its prefix.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The plan, or SND-RECIPE-002 when no single registered version of the id matches, or the plan's refusal.</returns>
    public async ValueTask<Result<ApprovalPlan>> Approve(RecipeId id, string hash, CancellationToken cancellationToken)
    {
        var found = await Find(id, hash, cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error : await Approve(found.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The registered version of <paramref name="id"/> named by <paramref name="hash"/>: the full hash, its 64 digits, or a prefix of 8 or more.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="hash">The hash or its prefix.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The recipe, or SND-RECIPE-002 when no single registered version of the id matches.</returns>
    public async ValueTask<Result<RegisteredRecipe>> Find(RecipeId id, string hash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hash);
        var versions = await store.Versions(id, cancellationToken).ConfigureAwait(false);
        if (versions.IsError)
        {
            return versions.Error;
        }

        var matching = versions.Value.Where(r => string.Equals(r.Hash.Value, hash, StringComparison.Ordinal) || r.Hash.MatchesPrefix(hash)).ToList();
        return matching.Count == 1
            ? matching[0]
            : WorkflowErrors.RecipeNotFound.With(matching.Count == 0
                ? $"no registered {id.Value} has hash {hash}; sindri recipe list shows what is"
                : $"{hash} matches more than one {id.Value}; give more of the hash");
    }

    /// <summary>
    /// What approving <paramref name="recipe"/> now would show ('recipe show'), whatever it stands at: approved with the
    /// same pins, revoked, without evals, or with a child not yet approved (that child gets no pin). It refuses nothing.
    /// </summary>
    /// <param name="recipe">The registered recipe.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The preview.</returns>
    public async ValueTask<Result<ApprovalPreview>> Preview(RegisteredRecipe recipe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var parsed = recipe.Read(json);
        var current = parsed.IsError ? parsed.Error : await approvals.Current(cancellationToken).ConfigureAwait(false);
        var latest = current.IsError ? current.Error : await approvals.Latest(recipe.Id, recipe.Version, recipe.Hash, cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        var pins = PinRules.Pins(parsed.Value, catalog, current.Value, json);
        var drift = latest.Value is { } before && before.Pins != pins ? before : null;
        var plan = await Plan(recipe, (parsed.Value, current.Value), pins, drift, cancellationToken).ConfigureAwait(false);
        return plan.IsError ? plan.Error : plan.Value.Preview;
    }

    /// <summary>The plan for approving <paramref name="hash"/>, which must be registered under <paramref name="id"/>.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="hash">The hash.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The plan, or SND-RECIPE-002, or the plan's refusal.</returns>
    public async ValueTask<Result<ApprovalPlan>> Approve(RecipeId id, RecipeHash hash, CancellationToken cancellationToken)
    {
        var found = await store.Find(hash, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        return found.Value is { } recipe && recipe.Id == id
            ? await Approve(recipe, cancellationToken).ConfigureAwait(false)
            : WorkflowErrors.RecipeNotFound.With($"no recipe {id.Value}@{hash.Display} is registered; sindri recipe list shows what is");
    }

    /// <summary>
    /// The plan for approving <paramref name="recipe"/>. Refused when it is legacy-shape, registered before input kinds, and not yet approved (SND-RECIPE-001; a first approval only), its hash or its base is revoked (SND-RECIPE-005), it declares no
    /// evals (SND-RECIPE-008) or a child it starts has no approved hash to pin (SND-RECIPE-003). A recipe already approved
    /// with the same pins plans as <see cref="ApprovalPlan.AlreadyApproved"/>: approving it again records nothing and sends the
    /// wake again, so an instance a first wake missed can still resume.
    /// </summary>
    /// <param name="recipe">The registered recipe.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The plan, or the refusal.</returns>
    public async ValueTask<Result<ApprovalPlan>> Approve(RegisteredRecipe recipe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var standing = await Standing(recipe, cancellationToken).ConfigureAwait(false);
        if (standing.IsError)
        {
            return standing.Error;
        }

        var (parsed, current) = standing.Value;
        var pins = Pins(recipe, parsed, current);
        if (pins.IsError)
        {
            return pins.Error;
        }

        var latest = await approvals.Latest(recipe.Id, recipe.Version, recipe.Hash, cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        return await Plan(recipe, standing.Value, pins.Value, latest.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<ApprovalPlan>> Plan(RegisteredRecipe recipe, (Recipe Parsed, IReadOnlyList<RecipeApproval> Current) standing, RecipePins pins,
        RecipeApproval? latest, CancellationToken cancellationToken)
    {
        var diff = await Diff(recipe, standing.Current, cancellationToken).ConfigureAwait(false);
        var resumes = latest is null ? Results.Success<IReadOnlyList<WorkflowInstanceId>>([]) : await wake.Running(recipe.Hash, cancellationToken).ConfigureAwait(false);
        var facts = await profile.Current(cancellationToken).ConfigureAwait(false);
        if (diff.IsError || resumes.IsError || facts.IsError)
        {
            return diff.IsError ? diff.Error : resumes.IsError ? resumes.Error : facts.Error;
        }

        var preview = ApprovalPreviews.Approve(standing.Parsed, recipe.Hash, pins, catalog, latest) with { Diff = diff.Value, Resumes = resumes.Value, Effects = ApprovalPreviews.Effects(standing.Parsed, facts.Value.EffectAllowlist) };
        // The pins are bound on every approval, not only a drift one: a child approved while the human reads the preview
        // would otherwise be pinned without being seen.
        var subject = ChallengeSubject.ForRecipe(recipe.Hash, recipe.Version, PinRules.Digest(pins, json));
        return new ApprovalPlan(recipe, preview, latest, ChallengeAction.Approve(recipe.Id, recipe.Version), subject);
    }

    /// <summary>The recipe read back and the current approvals, unless the hash is revoked, the recipe declares no evals, or a first approval meets a legacy-shape row (SND-RECIPE-001).</summary>
    private async ValueTask<Result<(Recipe Parsed, IReadOnlyList<RecipeApproval> Current)>> Standing(RegisteredRecipe recipe, CancellationToken cancellationToken)
    {
        var revoked = await approvals.Revocation(recipe.Hash, cancellationToken).ConfigureAwait(false);
        if (revoked.IsError || revoked.Value is not null)
        {
            return revoked.IsError ? revoked.Error : WorkflowErrors.RecipeRevoked.With($"{Name(recipe)} was revoked and can't be approved again; raise its version and approve that");
        }

        if (await RevokedBase(recipe, cancellationToken).ConfigureAwait(false) is { } revokedBase)
        {
            return revokedBase;
        }

        var parsed = recipe.Read(json);
        if ((parsed.IsError ? parsed.Error : await Shape(recipe, parsed.Value, cancellationToken).ConfigureAwait(false)) is { } refused)
        {
            return refused;
        }

        if (StepInputRefusals.Check(parsed.Value, inputRuntimes) is { } noRuntime)
        {
            return noRuntime;
        }

        var current = await approvals.Current(cancellationToken).ConfigureAwait(false);
        return current.IsError ? current.Error : (parsed.Value, current.Value);
    }

    /// <summary>SND-RECIPE-001 for a legacy-shape row not yet approved, SND-RECIPE-008 for a recipe without evals, else null.</summary>
    private async ValueTask<Error?> Shape(RegisteredRecipe recipe, Recipe parsed, CancellationToken cancellationToken)
    {
        // The legacy check comes before the evals one, whose fix (write evals, register again) can't help a legacy row. Only a first approval is
        // refused: a row already approved can still be re-approved (drift) or woken, so an instance parked on it isn't left with only cancel.
        if (SubjectKinds.Legacy(parsed) is { } legacy)
        {
            var approved = await approvals.Latest(recipe.Id, recipe.Version, recipe.Hash, cancellationToken).ConfigureAwait(false);
            return approved.IsError ? approved.Error
                : approved.Value is null ? WorkflowErrors.RecipeInvalid.With($"{Name(recipe)} was registered before input kinds ({legacy}) and can't be approved (fix: {SubjectKinds.LegacyFix})")
                : null;
        }

        return parsed.Evals.Count == 0
            ? WorkflowErrors.RecipeEvalsFailing.With($"{Name(recipe)} declares no evals; approving needs them (fix: write one with sindri recipe eval --scaffold <file>, list it under evals:, register again)")
            : null;
    }

    /// <summary>SND-RECIPE-005 when <paramref name="recipe"/> was generated from a base that has been revoked since it was registered.</summary>
    private async ValueTask<Error?> RevokedBase(RegisteredRecipe recipe, CancellationToken cancellationToken)
    {
        if (recipe.Base is not { } from)
        {
            return null;
        }

        var revoked = await approvals.Revocation(from.Hash, cancellationToken).ConfigureAwait(false);
        return revoked.IsError ? revoked.Error
            : revoked.Value is null ? null
            : WorkflowErrors.RecipeRevoked.With(
                $"{Name(recipe)} was generated from base {from.Id.Value}@{from.Hash.Display}, which was revoked since; generate the recipe from an approved base");
    }

    /// <summary>
    /// The block pins from this build, and each child's latest approved hash (PinRules.Pins; spec §4.1: a child is created
    /// from the hash its parent pinned). A child with no approved version refuses the approval.
    /// </summary>
    private Result<RecipePins> Pins(RegisteredRecipe recipe, Recipe parsed, IReadOnlyList<RecipeApproval> current)
    {
        var unpinned = RecipeChildren.Of(parsed)
            .Where(child => !current.Any(a => a.Id == child))
            .OrderBy(static c => c.Value, StringComparer.Ordinal)
            .Cast<RecipeId?>()
            .FirstOrDefault();
        return unpinned is { } child
            ? WorkflowErrors.RecipeNotApproved.With($"{Name(recipe)} starts child {child.Value}, which has no approved version to pin; approve {child.Value} first")
            : PinRules.Pins(parsed, catalog, current, json);
    }

    /// <summary>The diff against the last approved version of the same id (another hash), or null when there is none.</summary>
    private async ValueTask<Result<string?>> Diff(RegisteredRecipe recipe, IReadOnlyList<RecipeApproval> current, CancellationToken cancellationToken)
    {
        if (current.Where(a => a.Id == recipe.Id && a.Hash != recipe.Hash).MaxBy(static a => a.Version) is not { } previous)
        {
            return Results.Success<string?>(value: null);
        }

        var before = await store.Find(previous.Hash, cancellationToken).ConfigureAwait(false);
        if (before.IsError || before.Value is not { } old)
        {
            return before.IsError ? before.Error : Results.Success<string?>(value: null);
        }

        var normalized = old.Read(json);   // why: an old row lacks the fields later versions default, so compare its normalized form, not its stored text
        if (normalized.IsError)
        {
            return normalized.Error;
        }

        return LineDiff.Unified(ApprovalPreviews.Pretty(Encoding.UTF8.GetString(AwJson.Serialize(normalized.Value with { Hash = null }, json))), ApprovalPreviews.Pretty(recipe.Text),
            string.Create(CultureInfo.InvariantCulture, $"{Name(old)} (approved, version {old.Version})"),
            string.Create(CultureInfo.InvariantCulture, $"{Name(recipe)} (version {recipe.Version})"));
    }

    private static string Name(RegisteredRecipe recipe) => $"{recipe.Id.Value}@{recipe.Hash.Display}";
}
