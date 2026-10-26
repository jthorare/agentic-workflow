using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Core.Rules;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.RecordRun;

internal sealed class RecordRunService(IStateStore store, IRunLedger ledger, IFiles files, WorkTree tree, TimeProvider clock)
    : IService<RecordRunRequest, RecordRunResponse>
{
    public ValueTask<Result<RecordRunResponse>> Handle(RecordRunRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Locked(request.StateDirectory, () => store.Load(request.StateDirectory).Bind(state => Record(request, state))));

    private Result<RecordRunResponse> Record(RecordRunRequest request, BugFixState state)
    {
        var candidate = state.Require(Phase.Fix, Phase.Evaluate).Bind(_ => state.Find(request.Candidate)).Bind(Unrun);
        if (candidate.IsError)
        {
            return candidate.Error;
        }

        var check = state.Frozen.Check;
        var run = ledger.Read(request.Evidence)
            .Bind(stored => EvidenceRule.Accept(state, stored))
            .Bind(record => Against(state, candidate.Value, record))
            .Bind(record => Frozen(check, candidate.Value, record))
            .Bind(check.Accepts);   // last: a rewritten check or a moved branch is a stronger finding than a run with no verdict
        if (run.IsError)
        {
            return run.Error;
        }

        var passed = run.Value.Outcome == RunOutcome.Passed;
        var recorded = candidate.Value with { Run = new CandidateRun(request.Evidence, passed, clock.GetUtcNow()) };
        store.Save(request.StateDirectory, state.With(recorded).Step(Phase.Evaluate, $"record-run {recorded.Id.Value}", clock.GetUtcNow(), request.Evidence));
        return new RecordRunResponse(recorded.Id, passed, passed ? $"judge {recorded.Id.Value}" : NextStep.For(state.With(recorded) with { Phase = Phase.Evaluate }));
    }

    private static Result<Candidate> Unrun(Candidate candidate) =>
        candidate.Run is null ? candidate : BugFixErrors.RunRefused.With($"a run is already recorded for {candidate.Id.Value}");

    private Result<RunRecord> Against(BugFixState state, Candidate candidate, RunRecord record)
    {
        if (record.Commit != candidate.Commit)
        {
            return BugFixErrors.RunRefused.With($"the evidence ran against {record.Commit}, not {candidate.Id.Value}'s commit {candidate.Commit}");
        }

        var worktree = tree.Clean(state, candidate.Worktree, "the candidate's worktree must stay as recorded");
        if (worktree.IsSuccess && worktree.Value.Head != candidate.Commit)
        {
            return BugFixErrors.RunRefused.With($"{candidate.Id.Value}'s branch moved to {worktree.Value.Head} after record-candidate; record a new candidate");
        }

        return worktree.Map(_ => record);
    }

    // The frozen-check rule: a fix must not weaken the check, neither the file nor what the run executed.
    private Result<RunRecord> Frozen(FrozenCheck check, Candidate candidate, RunRecord record)
    {
        var current = tree.Snapshot(candidate.Worktree).Bind(worktree => files.Sha256(files.FullPath(worktree.Root, check.Path)));
        if (current.IsError && current.Error.Code != BugFixErrors.FileUnreadable.Code)
        {
            return current.Error;
        }

        return current.IsSuccess && current.Value == check.Sha256 && record.CheckSha256 == check.Sha256
            ? record
            : BugFixErrors.CheckNotFrozen.With($"the check file {check.Path} changed since the baseline");
    }
}
