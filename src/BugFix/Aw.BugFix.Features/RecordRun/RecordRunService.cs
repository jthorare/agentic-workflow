using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Core.Rules;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.RecordRun;

internal sealed class RecordRunService(IStateStore store, IRunLedger ledger, CheckPin pin, WorkTree tree, TimeProvider clock)
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
            .Bind(record => pin.Verify(check, state.Frozen.Baseline.Commit, candidate.Value, record).Map(note => (Record: record, Note: note)))
            .Bind(pinned => check.Accepts(pinned.Record).Map(record => (Record: record, pinned.Note)));   // last: a rewritten check or a moved branch is a stronger finding than a run with no verdict
        if (run.IsError)
        {
            return run.Error;
        }

        var passed = run.Value.Record.Outcome == RunOutcome.Passed;
        var recorded = candidate.Value with { Run = new CandidateRun(request.Evidence, passed, clock.GetUtcNow()) };
        store.Save(request.StateDirectory, state.With(recorded).Step(Phase.Evaluate, $"record-run {recorded.Id.Value}{run.Value.Note}", clock.GetUtcNow(), request.Evidence));
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
}
