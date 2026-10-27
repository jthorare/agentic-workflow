using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.PromoteMap;

/// <summary>
/// Makes the latest draft the current map (spec §8.7), when nothing in it is left to decide (SND-CONTEXT-046: no draft, or
/// undecided review items; a skipped item isn't decided). The proposals a person accepted become nodes of the version first,
/// because a 'propose' row only adds its node to a draft and a pending one is never part of the current map. Sindri checks
/// the caller and the typed confirmation first (protected).
/// </summary>
internal sealed class PromoteMapService(IGraphStore store, TimeProvider clock) : IService<PromoteMapRequest, MapPromoted>
{
    public async ValueTask<Result<MapPromoted>> Handle(PromoteMapRequest request, CancellationToken cancellationToken)
    {
        if (MapAccess.Gate(request.Scope) is { } refused)
        {
            return refused;
        }

        var found = await store.Snapshot(draft: true, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        if (found.Value is not { } draft)
        {
            return request.ExpectedVersion is { } wanted ? Changed(wanted, latest: null) : PromoteReadiness.Check(queue: null).Error;
        }

        if (request.ExpectedVersion is { } expected && expected != draft.Version.Number)
        {
            return Changed(expected, draft.Version.Number);
        }

        // The queue the review shows: an item a deny glob hides is neither decidable nor blocking, and the stored map loses what the globs hide.
        var seen = ReviewDrafts.Visible(draft, request.Scope);
        if (seen.IsError)
        {
            return seen.Error;
        }

        var ready = PromoteReadiness.Check(seen.Value.Queue);
        return ready.IsError ? ready.Error : await Promote(draft, seen.Value.View, cancellationToken).ConfigureAwait(false);
    }

    // The caller decided about one draft; a rebuild replaced it or it was promoted (retrieval spec §8.7a).
    private static Error Changed(int expected, int? latest) => ContextErrors.DraftChanged.With(string.Create(CultureInfo.InvariantCulture,
        $"draft {expected} was expected, {(latest is { } n ? $"the latest draft is {n}" : "there is no draft")} (a rebuild replaced it, or it was promoted); nothing was promoted"));

    private async ValueTask<Result<MapPromoted>> Promote(MapSnapshot draft, MapView view, CancellationToken cancellationToken)
    {
        var previous = await store.Current(cancellationToken).ConfigureAwait(false);
        var number = previous.IsError ? previous.Error : await WithAcceptedProposals(draft, view, cancellationToken).ConfigureAwait(false);
        var promoted = number.IsError ? Reworded(number.Error) : await store.Promote(number.Value, cancellationToken).ConfigureAwait(false);
        return promoted.IsError
            ? Reworded(promoted.Error)
            : new MapPromoted(promoted.Value, previous.Value is { } old ? old with { Status = MapVersionStatus.Retained } : null);
    }

    // The store says only that the draft is gone (046); here the cause is a promote, and the way on is the one the docs name.
    private static Error Reworded(Error error) => error.Code == ContextErrors.PromoteRefused.Code
        ? ContextErrors.PromoteRefused.With("the draft changed while it was being promoted (a rebuild replaced it, or it was promoted); nothing was promoted (fix: review the new draft with 'sindri context map review', then promote again)")
        : error;

    // The draft's own number, or the number of a copy of it that holds the accepted proposals' nodes and none of what the deny globs hide. The copy replaces only the draft that was read:
    // if a rebuild replaced it meanwhile the promote is refused (046), and the rebuild's draft and any build still being written stay. The copy carries the undo rows for the person's
    // accepts, renames, moves and merges about what it cuts, so they commit with the replacement or not at all (a cut leaves no orphan, and a committed promote is never reported as a failure).
    private async ValueTask<Result<int>> WithAcceptedProposals(MapSnapshot draft, MapView view, CancellationToken cancellationToken)
    {
        var proposed = OverrideLog.Active(view.Overrides).Where(o => o.Action == OverrideAction.Propose).Select(o => o.Target.Value).ToHashSet(StringComparer.Ordinal);
        // Indexed once (per node a Resolve, per edge a scan of every edge, made a promote proposals × graph): a key resolves when it, or the
        // target its alias names, is a node of the graph; an edge is new when no edge of the graph states the same fact.
        var aliases = view.Graph.Aliases.DistinctBy(a => a.Alias.Value, StringComparer.Ordinal).ToDictionary(a => a.Alias.Value, a => a.Target.Value, StringComparer.Ordinal);
        var keys = view.Graph.Nodes.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        var facts = view.Graph.Edges.Select(e => (e.From.Value, e.To.Value, e.Type)).ToHashSet();
        var nodes = view.Applied.Nodes.Where(n => proposed.Contains(n.Key.Value) && !keys.Contains(aliases.GetValueOrDefault(n.Key.Value) ?? n.Key.Value)).ToList();
        if (nodes.Count == 0 && !view.Filtered)
        {
            return draft.Version.Number;
        }

        var edges = view.Applied.Edges.Where(e => proposed.Contains(e.From.Value) && !facts.Contains((e.From.Value, e.To.Value, e.Type)));
        var joined = view.Graph with { Nodes = [.. view.Graph.Nodes, .. nodes], Edges = [.. view.Graph.Edges, .. edges] };
        var undo = view.UndoCutDecisions(draft.Overrides, clock.GetUtcNow());
        var saved = await store.ReplaceDraft(draft.Version.Number, new MapImport(draft.Version.Repo, draft.Version.Commit, joined, undo) { Ruleset = draft.Version.Ruleset, Dropped = draft.Version.Dropped }, draft.Version.Created, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : saved.Value.Number;
    }
}
