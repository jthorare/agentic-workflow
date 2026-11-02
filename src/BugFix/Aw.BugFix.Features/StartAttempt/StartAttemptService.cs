using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Core.Rules;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.StartAttempt;

internal sealed class StartAttemptService(IStateStore store, TimeProvider clock) : IService<StartAttemptRequest, StartAttemptResponse>
{
    public ValueTask<Result<StartAttemptResponse>> Handle(StartAttemptRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Locked(request.StateDirectory, () => store.Load(request.StateDirectory).Bind(state => Start(request, state))));

    private Result<StartAttemptResponse> Start(StartAttemptRequest request, BugFixState state)
    {
        var allowed = state.Require(Phases(request)).Bind(_ => AttemptRule.CanStart(state, request.Mode, request.AbandonJudge, request.AbandonUnrun)).Bind(_ => HasUnrun(state, request));
        if (allowed.IsError)
        {
            return allowed.Error;
        }

        var attempt = state.Attempt + 1;
        store.Save(
            request.StateDirectory,
            (state with { Attempt = attempt, Mode = request.Mode, Status = BugFixStatus.Active })
                .Step(Phase.Fix, $"start-attempt {Names.Of(request.Mode)}{(request.AbandonJudge ? " --abandon-judge" : string.Empty)}{Left(state, request)}", clock.GetUtcNow()));
        return new StartAttemptResponse(Phase.Fix, attempt, request.Mode);
    }

    // A candidate without a run is left behind from phase fix too: record-run refused it there and the phase never moved on.
    private static Phase[] Phases(StartAttemptRequest request) =>
        request.AbandonUnrun ? [Phase.Reproduce, Phase.Evaluate, Phase.Fix] : [Phase.Reproduce, Phase.Evaluate];

    private static Result<Unit> HasUnrun(BugFixState state, StartAttemptRequest request) =>
        !request.AbandonUnrun || state.CurrentCandidates.Any(static c => c.Run is null)
            ? Results.Ok()
            : BugFixErrors.AttemptRefused.With("--abandon-unrun: no candidate of this attempt is without a run");

    private static string Left(BugFixState state, StartAttemptRequest request) =>
        request.AbandonUnrun
            ? $" --abandon-unrun ({string.Join(", ", state.CurrentCandidates.Where(static c => c.Run is null).Select(static c => c.Id.Value))})"
            : string.Empty;
}
