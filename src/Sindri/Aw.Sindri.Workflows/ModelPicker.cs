using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Picks the model an engine launch runs on (model-pick spec 4, 5): the candidates after the step's floor and the profile's pin and cap, then Jev among them, then the rules fallback.
/// The pick is stored first ('model.picking', 'model.picked' under the launch's keys), so an activity retry reads it back and never asks Jev twice.
/// </summary>
/// <param name="settings">The profile's pick settings.</param>
/// <param name="catalog">The models each agent offers.</param>
/// <param name="history">How each model fared on the step.</param>
/// <param name="judge">The judge's model-pick question.</param>
/// <param name="store">Writes and reads back the pick's events.</param>
/// <param name="clock">Times the judge call.</param>
internal sealed class ModelPicker(IModelPickSettings settings, IModelCatalog catalog, IModelPickHistory history, IModelPickJudge judge, ModelPickStore store, TimeProvider clock)
{
    /// <summary>Picks the model for <paramref name="work"/>.</summary>
    /// <param name="work">The run and its launch request.</param>
    /// <param name="cancellationToken">Cancels the pick.</param>
    /// <returns>The model, or the refusal when no model is allowed.</returns>
    public async Task<ModelPickOutcome> Pick(ModelPickWork work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        var outcome = await PickModel(work, cancellationToken).ConfigureAwait(false);
        return outcome with { CanEscalate = await StrongerAllowed(work, outcome, cancellationToken).ConfigureAwait(false) };
    }

    private async Task<ModelPickOutcome> PickModel(ModelPickWork work, CancellationToken cancellationToken)
    {
        var run = work.Run;
        var agent = work.Target.Agent;
        var state = run.Run.Step.State;
        if (run.Model is { } named)
        {
            return await Record(work, Picked(work, named, ModelPickedBy.Constraint, [named], "step"), cancellationToken).ConfigureAwait(false);   // the recipe names the model: nothing to choose
        }

        var enabled = await settings.PickEnabled(cancellationToken).ConfigureAwait(false);
        var config = enabled ? await settings.For(agent, state, cancellationToken).ConfigureAwait(false) : ModelPickSettings.Off;
        if (!config.PickOn)
        {
            return await Record(work, await PickOff(work, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        }

        var models = config.Models ?? await catalog.ModelsOf(agent, cancellationToken).ConfigureAwait(false);
        var ladder = ModelBands.Rank(models);
        var selection = ModelCandidates.For(ladder, new ModelConstraints(Floor(run.Floor), config.Cap, config.Pin, work.Drafter, run.Previous?.Model));
        if (ladder.Count == 0)
        {
            return await Record(work, Picked(work, model: null, ModelPickedBy.Default, []), cancellationToken).ConfigureAwait(false);   // the agent reports no models: its own default
        }

        var left = selection.Candidates;
        if (left.Count == 0)
        {
            return new ModelPickOutcome(Model: null, Refusal(agent, state, selection));
        }

        if (left.Count == 1)
        {
            return await Record(work, Picked(work, left[0].Model.Slug, ModelPickedBy.Constraint, [left[0].Model.Slug], RuleOf(selection)), cancellationToken).ConfigureAwait(false);
        }

        return await Choose(work, config, left, cancellationToken).ConfigureAwait(false);
    }

    // With the pick off the agent runs its own default, except that a step's floor still holds (model-pick M-9): a step with a floor runs on the cheapest model at or above it, so an install that never
    // turned the pick on does not draft below strong. The agent's own default is unknown here (the profile's list is read only when the pick is on), so the floor model is passed, not compared.
    private async Task<ModelPicked> PickOff(ModelPickWork work, CancellationToken cancellationToken)
    {
        var floor = Floor(work.Run.Floor);
        if (floor != ModelTier.None && await catalog.ModelsOf(work.Target.Agent, cancellationToken).ConfigureAwait(false) is { Count: > 0 } models
            && ModelCandidates.For(ModelBands.Rank(models), new ModelConstraints(floor)).Candidates is [{ } cheapest, ..])
        {
            return Picked(work, cheapest.Model.Slug, ModelPickedBy.Constraint, [cheapest.Model.Slug], "floor");
        }

        return Picked(work, model: null, ModelPickedBy.Default, [], "pick-off");
    }

    // Whether the one retry may follow a failure of the picked model (spec 7.1): a model above it is still allowed inside the pin, the cap and the floor. A Step that names its model, one that reads
    // a workspace (the retry would start in the failed run's dirty worktree) and the retry itself never escalate.
    private async Task<bool> StrongerAllowed(ModelPickWork work, ModelPickOutcome outcome, CancellationToken cancellationToken)
    {
        var run = work.Run;
        if (outcome is not { Refusal: null, Model: { } model } || run.Model is not null || run.Run.Escalation > 0 || run.Workspace is not null || !await settings.PickEnabled(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var config = await settings.For(work.Target.Agent, run.Run.Step.State, cancellationToken).ConfigureAwait(false);
        if (!config.PickOn)
        {
            return false;
        }

        var models = config.Models ?? await catalog.ModelsOf(work.Target.Agent, cancellationToken).ConfigureAwait(false);
        return ModelCandidates.For(ModelBands.Rank(models), new ModelConstraints(Floor(run.Floor), config.Cap, config.Pin, work.Drafter, model)).Candidates.Count > 0;
    }

    private async Task<ModelPickOutcome> Choose(ModelPickWork work, ModelPickSettings config, IReadOnlyList<RankedModel> left, CancellationToken cancellationToken)
    {
        var launch = work.Request.Launch;
        var started = clock.GetUtcNow();
        var picking = new ModelPicking(launch, work.Request.Item, work.Run.Run.Address, work.Run.Run.Step.State, work.Request.Run?.Number ?? 0, started);
        var appended = await store.Picking(picking, cancellationToken).ConfigureAwait(false);
        if (appended.AlreadyRecorded && await store.Find(appended.Sequence, launch, cancellationToken).ConfigureAwait(false) is { } prior)
        {
            return new ModelPickOutcome(prior.Model);   // an earlier attempt of this activity already chose
        }

        var candidates = left.Select(static (c, i) => new PickCandidate("m" + (i + 1).ToString(CultureInfo.InvariantCulture), c.Model.Slug, Tier(c.Tiers))).ToList();
        var slugs = candidates.Select(static c => c.Model).ToList();
        var rows = work.Run.Recipe is { } recipe
            ? await history.For(recipe, work.Run.RecipeVersion, work.Run.Run.Step.State, slugs, cancellationToken).ConfigureAwait(false)
            : [];
        var ask = new ModelPickAsk(work.Run.Run.Step.Block + " " + work.Run.Run.Step.State, Kind(work.Run.Run.Step.Block), Brief(work.Request), work.Files, candidates, config.DenyPaths ?? [], rows);
        var clockStart = clock.GetTimestamp();
        var reply = await judge.Pick(ask, cancellationToken).ConfigureAwait(false);
        var elapsed = (int)Math.Min(int.MaxValue, clock.GetElapsedTime(clockStart).TotalMilliseconds);
        var (chosen, by, reason) = Decide(reply, candidates);
        var picked = Picked(work, chosen.Model, by, [.. candidates.Select(static c => c.Key + "=" + c.Model)]) with
        {
            Reason = reason,
            Decision = reply.DecisionRef,
            JevMs = elapsed,
            JevCalls = 1,
            ContentSent = string.Equals(by, ModelPickedBy.Jev, StringComparison.Ordinal),
            Unusual = rows.Any(r => string.Equals(r.Model, chosen.Model, StringComparison.Ordinal) && r.Unusual),
            Files = [.. work.Files.Select(static f => new PickedFile(f.Path, f.Text.Length))],
        };
        return await Record(work, picked, cancellationToken).ConfigureAwait(false);
    }

    // Jev's key when it is one of the candidates; the cheapest candidate otherwise, with the reason Jev did not decide.
    private static (PickCandidate Chosen, string By, string? Reason) Decide(ChooseAnswer reply, List<PickCandidate> candidates)
    {
        if (reply.Status == ChooseStatus.Decided && reply.Choice is { } key)
        {
            var found = candidates.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal));
            if (found is not null)
            {
                return string.Equals(reply.Provider, "rules", StringComparison.Ordinal) ? (found, ModelPickedBy.Rules, reply.Reason) : (found, ModelPickedBy.Jev, null);
            }

            return (candidates[0], ModelPickedBy.Rules, "unknown-key");
        }

        return (candidates[0], ModelPickedBy.Rules, reply.Status == ChooseStatus.Failed ? "judge-unavailable" : reply.Reason);
    }

    // Stores the pick; when the key was written already (a retry, a race) the stored pick wins and is what runs.
    private async Task<ModelPickOutcome> Record(ModelPickWork work, ModelPicked picked, CancellationToken cancellationToken)
    {
        var appended = await store.Picked(picked, cancellationToken).ConfigureAwait(false);
        if (!appended.AlreadyRecorded)
        {
            return new ModelPickOutcome(picked.Model);
        }

        var stored = await store.Find(appended.Sequence - 1, work.Request.Launch, cancellationToken).ConfigureAwait(false);
        return new ModelPickOutcome(stored is null ? picked.Model : stored.Model);
    }

    private static ModelPicked Picked(ModelPickWork work, string? model, string by, IReadOnlyList<string> candidates, string? reason = null) => new(
        work.Request.Launch, work.Request.Item, work.Run.Run.Address, work.Run.Run.Step.State, work.Request.Run?.Number ?? 0, work.Target.Agent, model, by, reason, Decision: null,
        candidates, BriefHash(Brief(work.Request)), Files: [], Omitted: [], JevMs: 0, JevCalls: 0, ContentSent: false, Unusual: false, work.Run.Previous?.Model, work.Run.Previous?.Kind);

    // The rule that left one model, for the Why sentence: the pin, else the cap, else the floor, else the challenger rule; 'only-model' when the agent offers one model and nothing removed it.
    private static string? RuleOf(ModelSelection selection) =>
        selection.Removed.Any(static r => r.Constraint == ModelConstraint.Pin) ? "pin"
        : selection.Removed.Any(static r => r.Constraint == ModelConstraint.Cap) ? "cap"
        : selection.Removed.Any(static r => r.Constraint == ModelConstraint.Floor) ? "floor"
        : selection.Removed.Any(static r => r.Constraint is ModelConstraint.Drafter or ModelConstraint.DrafterUnknown) ? "challenger"
        : "only-model";

    private static string Brief(LaunchRequest request) => request.Step?.Brief ?? string.Empty;

    private static string BriefHash(string brief) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(brief)));

    private static ModelTier Floor(string? floor) => floor switch
    {
        "cheap" => ModelTier.Cheap,
        "standard" => ModelTier.Standard,
        "strong" => ModelTier.Strong,
        _ => ModelTier.None,
    };

    // The highest band the model sits in, as the question's tier word.
    private static string Tier(ModelTier tiers) => tiers.HasFlag(ModelTier.Strong) ? "strong" : tiers.HasFlag(ModelTier.Standard) ? "standard" : "cheap";

    private static string Kind(string block) =>
        block.Contains("draft", StringComparison.Ordinal) ? "draft"
        : block.Contains("challenge", StringComparison.Ordinal) ? "challenge"
        : block.Contains("questions", StringComparison.Ordinal) ? "questions"
        : block.Contains("apply", StringComparison.Ordinal) ? "apply"
        : "other";

    // SND-MODEL-003 with what the rules removed and the fix (model-pick spec 12).
    private static Error Refusal(AgentId agent, string state, ModelSelection selection)
    {
        var removed = string.Join(", ", selection.Removed.Select(static r => $"{r.Model.Model.Slug} ({r.Constraint.ToString().ToLowerInvariant()})"));
        return SindriErrors.ModelNoneAllowed.With(
            $"No model is allowed for step '{state}' on {agent.Value}. The rules removed every model: {removed}. Fix: raise workflow.agent.cap, lower the step's floor, or give the agent more models in workflow.agent.models; then approve the profile and run the step again with 'sindri workflow answer <address> --resume'.");
    }
}
