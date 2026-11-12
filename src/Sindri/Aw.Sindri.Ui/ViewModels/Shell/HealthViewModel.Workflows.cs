namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Which events moved a workflow or one of its runs (<see cref="HealthViewModel"/>, continued; #2258 PR 17 part 2a, #2823): the Workflows page re-reads only after one of these.</summary>
internal sealed partial class HealthViewModel
{
    private static readonly HashSet<string> _definitionEvents = new(StringComparer.Ordinal) { "workflow.created", "workflow.revised", "workflow.activated", "workflow.paused", "workflow.archived" };

    private long _workflowEvents;

    /// <summary>How many 'workflow.run.*' and 'workflow.node.*' events (a run started, finished, was cancelled; a node started, finished, failed) and workflow definition events (workflow.created, revised, activated, paused, archived) the stream has carried. Other workflow.* types (notify, taken-over) are not counted. A view compares it with what it last saw.</summary>
    internal long WorkflowEvents => Interlocked.Read(ref _workflowEvents);

    private void CountWorkflowEvent(string type)
    {
        if (type.StartsWith("workflow.run.", StringComparison.Ordinal) || type.StartsWith("workflow.node.", StringComparison.Ordinal) || _definitionEvents.Contains(type))
        {
            _ = Interlocked.Increment(ref _workflowEvents);
        }
    }
}
