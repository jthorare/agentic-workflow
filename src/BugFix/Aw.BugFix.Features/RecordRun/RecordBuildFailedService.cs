using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Core.Rules;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.RecordRun;

internal sealed class RecordBuildFailedService(IStateStore store, IFiles files, TimeProvider clock) : IService<RecordBuildFailedRequest, RecordRunResponse>
{
    public ValueTask<Result<RecordRunResponse>> Handle(RecordBuildFailedRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Locked(request.StateDirectory, () => store.Load(request.StateDirectory).Bind(state => Record(request, state))));

    private Result<RecordRunResponse> Record(RecordBuildFailedRequest request, BugFixState state)
    {
        var candidate = state.Require(Phase.Fix, Phase.Evaluate).Bind(_ => state.Find(request.Candidate)).Bind(Unrun);
        if (candidate.IsError)
        {
            return candidate.Error;
        }

        var log = files.ReadText(request.Log);
        if (log.IsError)
        {
            return BugFixErrors.RunRefused.With($"the build log {request.Log} can't be read: {log.Error.Message}");
        }

        if (string.IsNullOrWhiteSpace(log.Value))
        {
            return BugFixErrors.RunRefused.With($"the build log {request.Log} is empty; a build that failed says why");
        }

        var recorded = candidate.Value with { Run = new CandidateRun(request.Log, Passed: false, clock.GetUtcNow()) };
        store.Save(request.StateDirectory, state.With(recorded).Step(Phase.Evaluate, $"record-run {recorded.Id.Value} --build-failed", clock.GetUtcNow(), request.Log));
        return new RecordRunResponse(recorded.Id, Passed: false, NextStep.For(state.With(recorded) with { Phase = Phase.Evaluate }));
    }

    private static Result<Candidate> Unrun(Candidate candidate) =>
        candidate.Run is null ? candidate : BugFixErrors.RunRefused.With($"a run is already recorded for {candidate.Id.Value}");
}
