using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.MarkWorkflowRunSeen;

/// <summary>
/// Marks a failed live run seen so its Home row clears (Home spec 2026-11-12 §3.2, E1). A person's write: a session is refused (SND-AUTH-002).
/// A run that did not fail or is a test run is left alone and answers ok; an unknown run is SND-GRAPH-101.
/// </summary>
/// <param name="runs">The run store.</param>
/// <param name="callers">The current caller.</param>
/// <param name="clock">The clock the mark's time comes from.</param>
internal sealed class MarkWorkflowRunSeenService(IWorkflowRunStore runs, ICallerContextAccessor callers, TimeProvider clock) : IService<MarkWorkflowRunSeenRequest, MarkWorkflowRunSeenResponse>
{
    public async ValueTask<Result<MarkWorkflowRunSeenResponse>> Handle(MarkWorkflowRunSeenRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current?.Class is not (CallerClass.Human or CallerClass.Service))
        {
            return SindriErrors.AuthRefused.With("Marking a failed run seen is the owner's to do; a session can't (fix: ask the owner to open the run)");
        }

        var run = await runs.Get(request.Run, cancellationToken).ConfigureAwait(false);
        if (run.IsError)
        {
            return run.Error;
        }

        // why: only a failed live run is on Home, so any other run (a succeeded one, a test run) has nothing to mark
        if (run.Value is not { Status: WorkflowRunStatus.Failed } failed || !string.Equals(failed.Mode, WorkflowRunMode.Live, StringComparison.Ordinal))
        {
            return new MarkWorkflowRunSeenResponse(request.Run, Marked: false);
        }

        var marked = await runs.MarkSeen(request.Run, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return marked.IsError ? marked.Error : new MarkWorkflowRunSeenResponse(request.Run, Marked: true);
    }
}
