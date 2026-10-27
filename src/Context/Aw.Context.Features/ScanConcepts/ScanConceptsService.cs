using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.ConceptScan;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Models;
using Aw.Context.Core.Review;
using Aw.Context.Core.Search;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ScanConcepts;

/// <summary>
/// The concept scan (owner, q110): reads a compact summary of the latest draft's features (the current version when no draft is open: a refresh promotes its draft within moments), asks a model for candidate product
/// concepts and the searches that test them, runs those searches over that map with the engine's map search, asks the model again with the hits and keeps the concept hints that pass <see cref="ConceptScanReplies.Hints"/>.
/// Hints are stored beside that version and nothing else is touched:
/// not the graph, the overrides or the pins, and a feature no hint names stays unplaced. Both calls are scrubbed and held to <see cref="ConceptScanLimits"/>. A model that does not answer, or answers with no
/// usable concept, ends the scan with an outcome, not an error: the map is as it was.
/// </summary>
internal sealed class ScanConceptsService(IGraphStore store, IScrubberFactory scrubbers, IModelPort model, IConceptScanSearch search, IConceptHintStore hintStore, TimeProvider clock)
    : IService<ScanConceptsRequest, ConceptScanReport>
{
    public async ValueTask<Result<ConceptScanReport>> Handle(ScanConceptsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (MapAccess.Gate(request.Scope) is { } refused)
        {
            return refused;
        }

        var settings = SettingsReader.Read(request.Scope.Settings);
        var built = settings.Bind(s => scrubbers.For(s.ExtraPatterns));
        var target = built.IsError ? built.Error : await Target(request, cancellationToken).ConfigureAwait(false);
        if (built.IsError || target.IsError)
        {
            return built.IsError ? built.Error : target.Error;
        }

        var map = target.Value;
        var keys = ConceptScanSummary.FeatureKeys(map.View.Applied);
        if (keys.Count == 0)
        {
            return new ConceptScanReport(map.Number, map.OnDraft, ConceptScanOutcome.NothingToScan, Hints: 0, Searches: 0);
        }

        var summary = built.Value.Egress.Scrub(ConceptScanSummary.Build(map.View.Applied, ConceptGlossaryBuilder.Glossary(map.View.Applied), ConceptScanPrompts.SummaryRoom)).Text;   // the terms the build stored on the concepts join the summary
        return await Scan(request.Scope, (map, summary, keys, settings.Value.ConceptScan), built.Value.Egress, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<ConceptScanReport>> Scan(ContextScope scope, (ScanTarget Map, string Summary, IReadOnlyList<string> Keys, ConceptScanSettings Config) read, IEgressScrubber egress, CancellationToken cancellationToken)
    {
        var (map, summary, keys, config) = read;
        var propose = ConceptScanPrompts.Propose(summary);
        var tier = config.Tiers.Pick(keys.Count, ConceptScanTiers.EstimateTokens(propose));   // the size is known before the first call; both calls ask for this tier
        var first = await Ask(propose, (tier, config.Models.For(tier)), cancellationToken).ConfigureAwait(false);
        var candidates = first is null ? [] : ConceptScanReplies.Candidates(first.Text);
        if (candidates.Count == 0)
        {
            return new ConceptScanReport(map.Number, map.OnDraft, first is null ? ConceptScanOutcome.ModelUnavailable : ConceptScanOutcome.NoConcepts, Hints: 0, Searches: 0, tier, first?.Model);
        }

        var found = await Searches(candidates, (scope, new MapRead(map.OnDraft, Version: null)), egress, cancellationToken).ConfigureAwait(false);
        var second = await Ask(ConceptScanPrompts.Conclude(summary, candidates, found), (tier, config.Models.For(tier)), cancellationToken).ConfigureAwait(false);
        if (second is null)
        {
            return new ConceptScanReport(map.Number, map.OnDraft, ConceptScanOutcome.ModelUnavailable, Hints: 0, found.Count, tier, first?.Model);
        }

        var hints = ConceptScanReplies.Hints(second.Text, new HashSet<string>(keys, StringComparer.Ordinal));
        var cutOff = CutOff(first, second);
        var saved = await hintStore.Save(map.Number, hints, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return saved.IsError
            ? saved.Error
            : new ConceptScanReport(map.Number, map.OnDraft, hints.Count == 0 ? ConceptScanOutcome.NoConcepts : ConceptScanOutcome.Scanned, hints.Count, found.Count, tier, second.Model, cutOff);
    }

    // True when either reply broke off mid-JSON: the scan kept what was whole.
    private static bool CutOff(ModelAnswer? first, ModelAnswer second) => (first is not null && ConceptScanReplies.IsCutOff(first.Text)) || ConceptScanReplies.IsCutOff(second.Text);

    // The model's answer, or null when no agent answered.
    private async ValueTask<ModelAnswer?> Ask(string prompt, (ModelTier Tier, IReadOnlyDictionary<string, string> Models) pick, CancellationToken cancellationToken)
    {
        var answer = await model.Ask(new ModelRequest(ModelPurpose.Concepts, prompt, ConceptScanLimits.MaxOutputTokens, ConceptScanLimits.CallTimeout, pick.Tier, pick.Models), cancellationToken).ConfigureAwait(false);
        return answer.IsError ? null : answer.Value;
    }

    // Each query once, over the draft; a search that fails or finds nothing is a search with no lines, never a failed scan.
    private async ValueTask<List<ConceptSearchResult>> Searches(IReadOnlyList<ConceptCandidate> candidates, (ContextScope Scope, MapRead Read) at, IEgressScrubber egress, CancellationToken cancellationToken)
    {
        var found = new List<ConceptSearchResult>();
        foreach (var query in candidates.SelectMany(static c => c.Queries).Distinct(StringComparer.OrdinalIgnoreCase).Take(ConceptScanLimits.MaxSearches))
        {
            var view = await search.Search(new MapSearchQuery(query, ConceptScanLimits.HitsPerSearch, Area: null, Repo: null, at.Read), at.Scope, cancellationToken).ConfigureAwait(false);
            var lines = view.IsError ? [] : view.Value.Hits.Select(h => egress.Scrub(h.Key.Value + " | " + h.Label).Text).ToList();
            found.Add(new ConceptSearchResult(query, lines));
        }

        return found;
    }

    // The latest draft; when there is none, the current version (a refresh promotes its draft within moments, so the scan usually meets the version that just went current).
    private async ValueTask<Result<ScanTarget>> Target(ScanConceptsRequest request, CancellationToken cancellationToken)
    {
        var draft = await ReviewDrafts.Draft(store, cancellationToken).ConfigureAwait(false);
        if (draft.IsSuccess)
        {
            return request.ExpectedVersion is { } named && named != draft.Value.Version.Number
                ? Changed(named, draft.Value.Version.Number)
                : Viewed(draft.Value, request.Scope, onDraft: true);
        }

        if (draft.Error.Code != ContextErrors.NoDraftMap.Code)
        {
            return draft.Error;
        }

        if (request.ExpectedVersion is { } wanted)
        {
            return Changed(wanted, latest: null);
        }

        var current = await store.Snapshot(draft: false, cancellationToken).ConfigureAwait(false);
        return current.IsError ? current.Error : current.Value is { } map ? Viewed(map, request.Scope, onDraft: false) : draft.Error;
    }

    private static Result<ScanTarget> Viewed(MapSnapshot snapshot, ContextScope scope, bool onDraft)
    {
        var view = MapAccess.View(snapshot.Graph, snapshot.Overrides, scope, pending: onDraft);
        return view.IsError ? view.Error : new ScanTarget(snapshot.Version.Number, onDraft, view.Value);
    }

    private sealed record ScanTarget(int Number, bool OnDraft, MapView View);

    // The caller named one draft and it is not the latest: a rebuild replaced it, or it was promoted. The scan stores nothing for it.
    private static Error Changed(int expected, int? latest) => ContextErrors.DraftChanged.With(string.Create(CultureInfo.InvariantCulture,
        $"draft {expected} was expected, {(latest is { } n ? $"the latest draft is {n}" : "there is no draft")} (a rebuild replaced it, or it was promoted)"));
}
