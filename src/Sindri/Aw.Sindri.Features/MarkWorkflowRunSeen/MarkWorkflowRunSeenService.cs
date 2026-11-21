using Aw.Kernel;
using Aw.Kernel.Services;
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
    public ValueTask<Result<MarkWorkflowRunSeenResponse>> Handle(MarkWorkflowRunSeenRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(callers);
        ArgumentNullException.ThrowIfNull(clock);
        return ValueTask.FromResult<Result<MarkWorkflowRunSeenResponse>>(new MarkWorkflowRunSeenResponse(request.Run, Marked: false));   // red: E1 marks the run here
    }
}
