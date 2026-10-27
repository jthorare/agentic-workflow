using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Features.StartInstance;

/// <summary>
/// 'workflow start' (engine spec rev 8 §5.1, §6.1, §6.3): resolves the item (<see cref="IItems"/>) and the approved
/// recipe (the latest, or 'id@hash') and its approval (RCP-CAP-003), checks that 'fact:item' names the same item (SND-WF-014) and that every input the
/// recipe declares is given, picks the attempt, stores the facts owner-only, and starts the workflow whose id is the
/// address; taken over when a launch is still requested for the item (UI spec E-19), so automation never runs beside a
/// session that's opening. Without '--attempt' the attempt is the latest one, or 1: a live one is returned as it is, an ended one is
/// shown with the hint to start the next. '--attempt next' is the one after the latest, refused while that one is
/// live, or while the item's claim ('workflow_claims', engine P6 unit (d)) is held by a live instance (SND-WF-012).
/// </summary>
/// <param name="items">Resolves the item.</param>
/// <param name="recipes">The approved recipes.</param>
/// <param name="directory">The attempts that exist.</param>
/// <param name="facts">Stores the input facts, and renders the generated ones.</param>
/// <param name="runtime">The instances on Temporal.</param>
/// <param name="launches">The item's launches (UI spec E-19): one still requested starts the instance taken over.</param>
/// <param name="claims">The item claims and their holders' liveness, for '--attempt next'.</param>
internal sealed class StartInstanceService(IItems items, IApprovedRecipes recipes, IInstanceDirectory directory, StartFacts facts, IInstanceRuntime runtime, ILaunchRequests launches, ClaimHolders claims)
    : IService<StartInstanceRequest, StartInstanceResponse>
{
    /// <summary>The tenant and ring every start runs under until the profile names them (S1-D11).</summary>
    private const string Tenant = "local";

    private static readonly FactKey _itemFact = FactKey.From("fact:item");

    public async ValueTask<Result<StartInstanceResponse>> Handle(StartInstanceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var prepared = await Prepare(request, cancellationToken).ConfigureAwait(false);
        if (prepared.IsError)
        {
            return prepared.Error;
        }

        var (item, recipe) = prepared.Value;
        var latest = await directory.Latest(item.Id, recipe.Version.Id, cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        var plan = Plan(request.NextAttempt, latest.Value);
        var refused = plan.IsError ? plan.Error : request.NextAttempt ? await HeldElsewhere(item.Id, cancellationToken).ConfigureAwait(false) : null;
        if (refused is not null)
        {
            return refused;
        }

        if (plan.Value.Ended is { } ended)
        {
            var view = await runtime.Get(ended, cancellationToken).ConfigureAwait(false);
            return view.IsError ? view.Error : new StartInstanceResponse(view.Value, Existing: true, Finished: true);
        }

        return await Begin(request, item, recipe, plan.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Starts the instance: the launches are read before any write, so a read error leaves no facts behind (UI spec E-19).</summary>
    private async ValueTask<Result<StartInstanceResponse>> Begin(StartInstanceRequest request, ResolvedItem item, ApprovedRecipe recipe, AttemptPlan plan, CancellationToken cancellationToken)
    {
        var launch = await launches.Requested(item.Id, cancellationToken).ConfigureAwait(false);
        if (launch.IsError)
        {
            return launch.Error;
        }

        var inputs = await facts.Store(request, recipe.Recipe, cancellationToken).ConfigureAwait(false);
        if (inputs.IsError)
        {
            return inputs.Error;
        }

        var started = await runtime.Start(
            new InstanceStart(recipe.Version, item.Id, plan.Attempt, inputs.Value.Inputs, Tenant, Ring: 0) { OpenLaunch = launch.Value?.Value, StartedAt = inputs.Value.StartedAt, StartedBy = inputs.Value.StartedBy, Again = request.Again },
            cancellationToken).ConfigureAwait(false);
        return started.IsError ? started.Error : new StartInstanceResponse(started.Value, plan.Existing, Finished: false);
    }

    /// <summary>
    /// The attempt a start runs (spec §5.1, §6.1): without '--attempt next' the latest, or 1 (a live one is returned by
    /// UseExisting, an ended one is shown); with it the one after the latest, refused while that one is live (SND-WF-012).
    /// </summary>
    private static Result<AttemptPlan> Plan(bool next, AttemptState? latest) => (next, latest) switch
    {
        (true, { Live: true } live) => WorkflowErrors.EarlierAttemptLive.With(
            $"{live.Address.Value} is still live (fix: finish it, or sindri workflow cancel {live.Address.Value}, then start --attempt next)"),
        (true, _) => new AttemptPlan((latest?.Attempt ?? 0) + 1, Ended: null, Existing: false),
        (false, { Live: false } ended) => new AttemptPlan(ended.Attempt, ended.Address, Existing: true),
        (false, { } running) => new AttemptPlan(running.Attempt, Ended: null, Existing: true),
        (false, null) => new AttemptPlan(1, Ended: null, Existing: false),
    };

    /// <summary>SND-WF-012 on the claim (spec §5.1): a live instance holds the item, so no next attempt starts; null when none does.</summary>
    private async ValueTask<Error?> HeldElsewhere(ItemKey item, CancellationToken cancellationToken)
    {
        var held = await claims.LiveHolder(item, cancellationToken).ConfigureAwait(false);
        return held.IsError ? held.Error
            : held.Value is { } holder ? WorkflowErrors.EarlierAttemptLive.With(
                $"{holder.Owner.Value} holds the claim on {item.Value} and is still live (fix: finish it, or sindri workflow cancel {holder.Owner.Value}, then start --attempt next)")
            : null;
    }

    /// <summary>What a start needs before it looks at the attempts: facts within 1 MiB, the item, 'fact:item' naming it, the approved recipe, and every input it declares.</summary>
    private async ValueTask<Result<(ResolvedItem Item, ApprovedRecipe Recipe)>> Prepare(StartInstanceRequest request, CancellationToken cancellationToken)
    {
        if (request.Facts.Where(static f => f.Value.Content.Length > IFactStore.MaxBytes).Select(static f => f.Key.Value).FirstOrDefault() is { } large)
        {
            return SindriErrors.InvalidInput.With($"{large} is larger than 1 MiB (fix: start it with a smaller file; facts travel as content, at most 1 MiB each)");
        }

        var item = await items.Resolve(request.Item, cancellationToken).ConfigureAwait(false);
        if (item.IsError)
        {
            return item.Error;
        }

        if (SameItem(request, item.Value) is { } mismatch)
        {
            return mismatch;
        }

        var recipe = await Recipe(request.Recipe, cancellationToken).ConfigureAwait(false);
        var approval = recipe.IsError ? recipe.Error : await recipes.Check(recipe.Value.Version, cancellationToken).ConfigureAwait(false);
        if (approval.IsError)
        {
            return approval.Error;
        }

        if (Refusal(recipe.Value, approval.Value) is { } refused)
        {
            return refused;
        }

        if (facts.NoRuntime(recipe.Value.Recipe) is { } noRuntime)
        {
            return noRuntime;
        }

        return InputRefusal(request, recipe.Value) is { } refusal ? refusal : (item.Value, recipe.Value);
    }

    /// <summary>A missing input, or, before any claim, attempt or instance, a seeded fact a registered check finds wrong.</summary>
    private Error? InputRefusal(StartInstanceRequest request, ApprovedRecipe recipe) =>
        recipe.Recipe.Inputs.FirstOrDefault(i => i.Generate is null && !request.Facts.ContainsKey(i.Key)) is { } missing
            ? SindriErrors.InvalidInput.With($"{recipe.Version.Id.Value} needs input {missing.Key.Value} (fix: --fact {missing.Key.Value}=<file>)")
            : facts.Validate(request, recipe.Recipe);

    private static Error? Refusal(ApprovedRecipe approved, ApprovalCheck check)
    {
        var recipe = approved.Version;
        return check switch
        {
            ApprovalCheck.Revoked => WorkflowErrors.RecipeRevoked.With($"{recipe.Id.Value}@{recipe.Hash.Display} was revoked; nothing was started (fix: start the approved version: sindri recipe list)"),
            _ when SubjectKinds.Legacy(approved.Recipe) is { } legacy => WorkflowErrors.RecipeInvalid.With($"{recipe.Id.Value}@{recipe.Hash.Display} was registered before input kinds ({legacy}); nothing was started (fix: {SubjectKinds.LegacyFix})"),
            ApprovalCheck.Drifted => WorkflowErrors.RecipePinDrift.With($"a block {recipe.Id.Value}@{recipe.Hash.Display} pins changed since approval; nothing was started (fix: re-approve it, see sindri recipe list --stale)"),
            ApprovalCheck.Holds or _ => null,
        };
    }

    /// <summary>'fact:item' is '{ id, repo, spec }' (spec §12); its 'id' must be the item '--item' named (SND-WF-014).</summary>
    private static Error? SameItem(StartInstanceRequest request, ResolvedItem item)
    {
        if (!request.Facts.TryGetValue(_itemFact, out var fact))
        {
            return null;
        }

        string? id;
        try
        {
            using var json = JsonDocument.Parse(fact.Content);
            id = json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("id", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException e) // boundary: a fact the human gave is input, and unreadable input is their error
        {
            return SindriErrors.InvalidInput.With($"fact:item ({fact.Name}) isn't JSON: {e.Message} (fix: give '{{ \"id\": \"{request.Item}\", \"repo\": … }}'; \"spec\" is optional)");
        }

        string[] names = [request.Item, item.Id.Value, .. item.Aliases.Select(static a => a.Value)];
        return id is not null && names.Contains(id, StringComparer.Ordinal)
            ? null
            : WorkflowErrors.ItemMismatch.With($"--item is {request.Item} but fact:item ({fact.Name}) names {id ?? "no id"} (fix: use the same id in both)");
    }

    /// <summary>The latest approved version of the id, or the approved 'id@hash'.</summary>
    private async ValueTask<Result<ApprovedRecipe>> Recipe(string reference, CancellationToken cancellationToken)
    {
        var parts = reference.Split('@', 2);
        if (!RecipeId.TryFrom(parts[0], out var id))
        {
            return SindriErrors.InvalidInput.With($"'{reference}' isn't a recipe id or id@hash (fix: sindri recipe list)");
        }

        if (parts is not [_, var hash])
        {
            return await recipes.Latest(id, cancellationToken).ConfigureAwait(false);
        }

        if (!RecipeHash.IsPrefix(hash))
        {
            return SindriErrors.InvalidInput.With($"'{hash}' isn't a recipe hash: 8 to 64 hex digits (fix: sindri recipe list)");
        }

        var latest = await recipes.Latest(id, cancellationToken).ConfigureAwait(false);
        if (latest.IsError || latest.Value.Version.Hash.MatchesPrefix(hash))
        {
            return latest;
        }

        return RecipeHash.TryFrom(hash.StartsWith("sha256:", StringComparison.Ordinal) ? hash : "sha256:" + hash, out var pinned)
            ? await recipes.Find(id, pinned, cancellationToken).ConfigureAwait(false)
            : WorkflowErrors.RecipeNotFound.With($"no approved {id.Value}@{hash} (fix: sindri recipe list)");
    }
}
