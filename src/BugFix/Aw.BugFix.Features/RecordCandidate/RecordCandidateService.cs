using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Core.Rules;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.RecordCandidate;

internal sealed class RecordCandidateService(IStateStore store, IGit git, WorkTree tree, TimeProvider clock) : IService<RecordCandidateRequest, RecordCandidateResponse>
{
    public ValueTask<Result<RecordCandidateResponse>> Handle(RecordCandidateRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Locked(request.StateDirectory, () => store.Load(request.StateDirectory).Bind(state => Record(request, state))));

    // Phase evaluate too: in mode B the first candidate may be evaluated before the second is recorded.
    private Result<RecordCandidateResponse> Record(RecordCandidateRequest request, BugFixState state)
    {
        var phase = state.Require(Phase.Fix, Phase.Evaluate);
        if (phase.IsError)
        {
            return phase.Error;
        }

        var (check, baseline) = state.Frozen;
        var commit = OnBranch(request)
            .Bind(_ => tree.Clean(state, request.WorkingDirectory, "commit the fix first"))
            .Map(static checkout => checkout.Head)
            .Bind(head => OnBaseline(request.WorkingDirectory, baseline.Commit, head))
            .Bind(head => CandidateRule.CanRecord(state, head, request.Hypothesis).Map(_ => head));
        var from = commit.Map(head => git.NetBase(request.WorkingDirectory, baseline.Commit, head));
        var added = from.Bind(net => git.AddedFiles(request.WorkingDirectory, net, commit.Value));
        var changed = added.Bind(_ => git.ChangedFiles(request.WorkingDirectory, from.Value, commit.Value))
            .Bind(paths => Untouched(paths, added.Value, check, request.AllowTestChanges));
        if (changed.IsError)
        {
            return changed.Error;
        }

        var approval = changed.Value.Count > 0 && request.AllowTestChanges && Touches(changed.Value, added.Value, check)
            ? $" --allow-test-changes ({(string.IsNullOrWhiteSpace(request.TestChangeReason) ? "no reason given" : request.TestChangeReason.Trim())})"
            : string.Empty;
        var candidate = new Candidate(
            CandidateId.Numbered(state.Candidates.Count + 1), state.Attempt, state.Mode ?? FixMode.A, request.Branch,
            request.WorkingDirectory, commit.Value, request.Hypothesis, changed.Value, Run: null, Verdict: null);
        store.Save(
            request.StateDirectory,
            (state with { Candidates = [.. state.Candidates, candidate] }).Step(state.Phase, $"record-candidate {candidate.Id.Value}{approval}", clock.GetUtcNow()));
        return new RecordCandidateResponse(candidate.Id, candidate.Commit, candidate.ChangedFiles);
    }

    private Result<Unit> OnBranch(RecordCandidateRequest request) =>
        git.Branch(request.WorkingDirectory).Bind<Unit>(branch => branch == request.Branch
            ? Unit.Value
            : BugFixErrors.CandidateRefused.With($"{request.WorkingDirectory} is on branch {branch}, not {request.Branch}"));

    private Result<string> OnBaseline(string directory, string baseline, string head)
    {
        if (head == baseline)
        {
            return BugFixErrors.CandidateRefused.With("no commits since the baseline; nothing to evaluate");
        }

        var builds = git.IsAncestor(directory, baseline, head);
        if (builds.IsError || !builds.Value)
        {
            return builds.IsError ? builds.Error : BugFixErrors.CandidateRefused.With($"{head} does not build on the baseline {baseline}; start the worktree from the baseline commit");
        }

        return git.SameTree(directory, head, baseline).Bind<string>(same => same
            ? BugFixErrors.CandidateRefused.With("the candidate's tree is identical to the baseline's; nothing was fixed")
            : head);
    }

    // Weakening what the check exercises (fixtures, helpers, its runner, config) is as bad as editing the check itself.
    // So is adding a file a loader picks ahead of existing code (a module that shadows another).
    // Build and lock files are ordinary changes. The flag lets the orchestrator through with a reason that goes into the history.
    private static Result<IReadOnlyList<string>> Untouched(IReadOnlyList<string> changed, IReadOnlyList<string> added, FrozenCheck check, bool approved)
    {
        var touched = Protected(changed, added, check);
        return touched.Count == 0 || approved
            ? Results.Success(changed)
            : BugFixErrors.CandidateRefused.With($"the fix touches test/fixture/runner-config files or adds a file a loader picks first ({string.Join(", ", touched)}); if the change is needed and does not weaken the check, record-candidate again with --allow-test-changes --reason <why> (the reason is recorded)");
    }

    private static bool Touches(IReadOnlyList<string> changed, IReadOnlyList<string> added, FrozenCheck check) => Protected(changed, added, check).Count > 0;

    private static List<string> Protected(IReadOnlyList<string> changed, IReadOnlyList<string> added, FrozenCheck check) =>
        [.. ProtectedPaths.Touched(changed, check.Path, check.CommandFiles).Union(ProtectedPaths.Shadowing(added, check.Directory, check.Path), StringComparer.Ordinal)];
}
