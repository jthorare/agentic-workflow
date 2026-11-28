namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>
/// Which of a workflow's live runs a page read keeps (#4334), applied in the store's query before the page cap, so a filter reaches runs older than the newest page.
/// Runs come newest first (by start time, then by occurrence number), as <see cref="IWorkflowRunStore.Recent"/> orders them.
/// </summary>
/// <param name="Statuses">The statuses kept; none keeps every status.</param>
/// <param name="Since">Keeps runs that started at or after this instant, or null for no lower bound.</param>
/// <param name="Until">Keeps runs that started before this instant, or null for no upper bound.</param>
/// <param name="Before">Keeps runs that come after this run in the order (older ones), or null to start at the newest; the cursor of the next page.</param>
public sealed record WorkflowRunQuery(IReadOnlyList<WorkflowRunStatus> Statuses, DateTimeOffset? Since, DateTimeOffset? Until, WorkflowRun? Before)
{
    /// <summary>Gets the query that keeps every live run.</summary>
    public static WorkflowRunQuery All { get; } = new([], Since: null, Until: null, Before: null);
}
