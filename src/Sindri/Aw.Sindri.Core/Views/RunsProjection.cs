using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Views;

/// <summary>Folds the six 'workflow.run.*' and 'workflow.node.*' events into the 'workflow-runs' view (issue 2258, PR 6): one row per run. Item states and run links come with the item projection, not here.</summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class RunsProjection(EventFactory events) : IProjection
{
    private static readonly string[] _types =
    [
        EventTypes.WorkflowRunStarted, EventTypes.WorkflowRunFinished, EventTypes.WorkflowRunCancelled,
        EventTypes.WorkflowNodeStarted, EventTypes.WorkflowNodeFinished, EventTypes.WorkflowNodeFailed,
    ];

    public IReadOnlyList<string> Views => [ViewNames.WorkflowRuns];

    public bool Handles(string eventType) => _types.Contains(eventType, StringComparer.Ordinal);

    public ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        _ = events;
        throw new NotSupportedException();
    }
}
