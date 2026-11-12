using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.AdvanceReport;

internal sealed class AdvanceReportService(IStateStore store, IFiles files, TimeProvider clock) : IService<AdvanceReportRequest, AdvanceReportResponse>
{
    public ValueTask<Result<AdvanceReportResponse>> Handle(AdvanceReportRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult((request.Candidate is null) == request.Unresolved
            ? store.Locked(request.StateDirectory, () => store.Load(request.StateDirectory).Bind(state => Report(request, state)))
            : Results.Failure<AdvanceReportResponse>(BugFixErrors.Invalid.With("pass exactly one of --candidate <id> or --unresolved")));

    // "Resolved" is recorded only with a passing run and a resolved judge decision.
    private Result<AdvanceReportResponse> Report(AdvanceReportRequest request, BugFixState state)
    {
        if (state.Phase == Phase.Report)
        {
            return BugFixErrors.ReportRefused.With("already reported");
        }

        if (request.Candidate is not { } id)
        {
            store.Save(request.StateDirectory, (state with { Status = BugFixStatus.Unresolved }).Step(Phase.Report, "advance report --unresolved", clock.GetUtcNow()));
            return new AdvanceReportResponse(Phase.Report, BugFixStatus.Unresolved, Candidate: null);
        }

        var candidate = state.Find(id).Bind(static c => c.ResolvesTicket());
        if (candidate.IsError)
        {
            return candidate.Error;
        }

        var sweep = request.SweepFile is { } file
            ? files.ReadText(file).Bind(SweepRule.Check)
            : BugFixErrors.SweepRefused.With("a resolved report needs the sweep: pass --sweep <file> (the \"Where else we checked\" table and the Prevention decision)");
        if (sweep.IsError)
        {
            return sweep.Error;
        }

        store.Save(
            request.StateDirectory,
            (state with { Status = BugFixStatus.Resolved, ResolvedBy = id }).Step(Phase.Report, $"advance report {id.Value}", clock.GetUtcNow(), request.SweepFile));
        return new AdvanceReportResponse(Phase.Report, BugFixStatus.Resolved, id);
    }
}
