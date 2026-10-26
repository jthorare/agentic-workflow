using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ProposeFromScope;

/// <summary>
/// Writes a scoping run's new surfaces as 'propose' rows (spec §8.7): in the latest draft, which this opens as a copy of the
/// current map when there is none, so the current map is never written. A surface the map or an earlier call already holds, or
/// a person rejected, is no row; repeating a call is idempotent. The run's whole surface list is kept too (a second call replaces it), so its graph status can name every surface, mapped and unmapped ones included. A repo the caller can't address is SND-CONTEXT-045, the same
/// answer whether the profile lacks it or the session's scope excludes it.
/// </summary>
internal sealed class ProposeFromScopeService(IGraphStore store, IProposalRuns runs, TimeProvider clock) : IService<ProposeFromScopeRequest, ProposeResult>
{
    internal const string Who = MapOverride.EngineWho;

    public async ValueTask<Result<ProposeResult>> Handle(ProposeFromScopeRequest request, CancellationToken cancellationToken)
    {
        var proposal = request.Proposal;
        if (Addressable(request.Scope, proposal) is { } refused)
        {
            return refused;
        }

        if (proposal.Surfaces.Count == 0)
        {
            return await Withdraw(proposal, request.Scope, cancellationToken).ConfigureAwait(false);
        }

        var now = clock.GetUtcNow();
        var opened = await store.OpenDraft(now, cancellationToken).ConfigureAwait(false);
        var draft = opened.IsError ? opened.Error : await ReviewDrafts.Draft(store, cancellationToken).ConfigureAwait(false);
        if (opened.IsError || draft.IsError)
        {
            return opened.IsError ? opened.Error : draft.Error;
        }

        // Planned against what the caller may see, like every read: a node only a denied file evidences is not there to find existing.
        var seen = MapAccess.View(draft.Value.Graph, draft.Value.Overrides, request.Scope, pending: true, callerScope: false);
        if (seen.IsError)
        {
            return seen.Error;
        }

        var plan = ProposalPlanner.Plan(proposal, seen.Value, Who, now);
        var written = await runs.Propose(proposal, plan.Rows, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written.Error;
        }

        // A planned row another identical call wrote first is already there: it counts as existing, so the counts add up and none is negative.
        return new ProposeResult(opened.Value, written.Value, plan.Existing + Math.Max(0, plan.Rows.Count - written.Value), plan.Rejected, RowsOf(proposal, plan.Rows, draft.Value.Overrides));
    }

    // An empty surface list takes the run back (scoping spec C9): one undo row for each of its undecided proposals, in the draft there is, which this never opens. Accepted ones stay.
    private async ValueTask<Result<ProposeResult>> Withdraw(ScopeProposal proposal, ContextScope scope, CancellationToken cancellationToken)
    {
        var snapshot = await store.Snapshot(draft: true, cancellationToken).ConfigureAwait(false);
        if (snapshot.IsError || snapshot.Value is not { } draft)
        {
            return snapshot.IsError ? snapshot.Error : new ProposeResult(Draft: null, Added: 0, Existing: 0, Rejected: 0);
        }

        var seen = MapAccess.View(draft.Graph, draft.Overrides, scope, pending: true, callerScope: false);
        var standing = await runs.Run(proposal.Run, cancellationToken).ConfigureAwait(false);
        if (seen.IsError || standing.IsError)
        {
            return seen.IsError ? seen.Error : standing.Error;
        }

        if (IsAnotherReposRun(standing.Value, proposal))
        {
            return new ProposeResult(draft.Version, Added: 0, Existing: 0, Rejected: 0);   // the run id is another repo's run: not this caller's to withdraw, nor its list to replace
        }

        var now = clock.GetUtcNow();
        var (rows, kept) = ProposalPlanner.Withdraw(proposal.Run, proposal.Repo, seen.Value.Overrides, Who, now);
        var result = Outcome(proposal, draft.Version, rows, kept, seen.Value.Overrides);
        return await Record(proposal, rows, HasSurfaces(standing.Value), result, cancellationToken).ConfigureAwait(false);
    }

    // Saves the undo rows and the emptied surface list, unless there is nothing to take back and no list standing: a second withdrawal writes nothing.
    private async ValueTask<Result<ProposeResult>> Record(ScopeProposal proposal, IReadOnlyList<MapOverride> rows, bool listStanding, ProposeResult result, CancellationToken cancellationToken)
    {
        if (rows.Count == 0 && !listStanding)
        {
            return result;
        }

        var written = await runs.Propose(proposal, rows, cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : result;
    }

    private static bool IsAnotherReposRun(ScopeProposal? standing, ScopeProposal proposal) => standing is not null && standing.Repo != proposal.Repo;

    private static bool HasSurfaces(ScopeProposal? standing) => standing is { Surfaces.Count: > 0 };

    private static ProposeResult Outcome(ScopeProposal proposal, MapVersion version, IReadOnlyList<MapOverride> rows, int kept, IReadOnlyList<MapOverride> overrides) =>
        new(version, Added: 0, Existing: 0, Rejected: 0)
        {
            Withdrawn = rows.Count,
            Kept = kept,
            WithdrawnRows = rows.Count > 0 ? [.. rows.Select(o => o.ToId()).OrderBy(id => id.Value, StringComparer.Ordinal)] : LatestWithdrawal(proposal, overrides),
        };

    // The undo rows of the run's latest withdrawal (they share one timestamp): a repeat or a retry names them again, so a withdrawal whose event failed to append is recorded by the retry,
    // and the event covers that one withdrawal. Only the engine's own undo rows count; a person's undo of a proposal is not a withdrawal.
    private static List<OverrideId> LatestWithdrawal(ScopeProposal proposal, IReadOnlyList<MapOverride> overrides)
    {
        var proposed = overrides.Where(o => o.Action == OverrideAction.Propose && string.Equals(o.Proposal?.Run, proposal.Run, StringComparison.Ordinal) && o.Proposal?.Repo == proposal.Repo)
            .Select(o => o.ToId().Value).ToHashSet(StringComparer.Ordinal);
        var undos = overrides.Where(o => o.Action == OverrideAction.Undo && o.Undoes is { } undone && proposed.Contains(undone.Value) && string.Equals(o.Who, Who, StringComparison.Ordinal)).ToList();
        return undos.Count == 0 ? [] : [.. undos.Where(o => o.At == undos.Max(u => u.At)).Select(o => o.ToId()).OrderBy(id => id.Value, StringComparer.Ordinal)];
    }

    // The run's propose rows for this call's surfaces: the ones just planned and the ones the draft already held (a retry names them again).
    private static List<OverrideId> RowsOf(ScopeProposal proposal, IReadOnlyList<MapOverride> planned, IReadOnlyList<MapOverride> standing)
    {
        var keys = proposal.Surfaces.Select(ProposalNodes.KeyOf).OfType<NodeKey>().Select(k => k.Value).ToHashSet(StringComparer.Ordinal);
        return [.. planned.Concat(OverrideLog.Active(standing).Where(o => o.Action == OverrideAction.Propose && o.Proposal?.Run == proposal.Run && keys.Contains(o.Target.Value)))
            .Select(o => o.ToId()).DistinctBy(id => id.Value, StringComparer.Ordinal).OrderBy(id => id.Value, StringComparer.Ordinal)];
    }

    private static Error? Addressable(ContextScope scope, ScopeProposal proposal)
    {
        var addressed = SettingsReader.Read(scope.Settings).Bind(s => ScopeRules.Resolve(s, scope.Repos, scope.Caller));
        if (addressed.IsError && addressed.Error.Code != ContextErrors.UnknownAnchor.Code)
        {
            return addressed.Error;
        }

        return addressed.IsError || !addressed.Value.Any(r => r.Name == proposal.Repo)
            ? ContextErrors.RepoOutsideScope.With("that repo isn't one this caller can address (fix: name a repo of the approved profile; a session names its own X-Sindri-Repo)")
            : null;
    }
}
