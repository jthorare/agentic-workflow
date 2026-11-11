namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Which events moved a workflow run (<see cref="HealthViewModel"/>, continued; #2258 PR 17 part 2a): the Workflows page re-reads only after one of these.</summary>
internal sealed partial class HealthViewModel
{
    private long _workflowEvents;

    /// <summary>How many 'workflow.run.*' and 'workflow.node.*' events (a run started, finished, was cancelled; a node started, finished, failed) the stream has carried. A view compares it with what it last saw.</summary>
    internal long WorkflowEvents => Interlocked.Read(ref _workflowEvents);

    private void CountWorkflowEvent(string type)
    {
        if (type.StartsWith("workflow.run.", StringComparison.Ordinal) || type.StartsWith("workflow.node.", StringComparison.Ordinal))
        {
            _ = Interlocked.Increment(ref _workflowEvents);
        }
    }
}
