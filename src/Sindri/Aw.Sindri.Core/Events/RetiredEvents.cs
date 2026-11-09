namespace Aw.Sindri.Core.Events;

/// <summary>The event kinds the recipe, pipeline and scoping engines wrote before they were removed. Old ledgers still hold them; projections show them as history only (the item timeline lists every event).</summary>
public static class RetiredEvents
{
    private static readonly string[] _prefixes = ["workflow.", "line.", "recipe.", "scope."];

    // why: the graph engine writes live kinds under the same 'workflow.' prefix (EventTypes.GraphRuns); they feed view_workflow_runs and needs-you.
    private static readonly string[] _live = ["workflow.run.", "workflow.node.", EventTypes.WorkflowNotify];

    /// <summary>Whether <paramref name="eventType"/> is a retired kind ('workflow.*', 'line.*', 'recipe.*' or 'scope.*'). The graph engine's 'workflow.run.*', 'workflow.node.*' and 'workflow.notify' stay live, and so does 'profile.approved' until profile approval is dropped.</summary>
    /// <param name="eventType">The event type.</param>
    /// <returns>True for a retired kind.</returns>
    public static bool Is(string eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        return !_live.Any(live => eventType.StartsWith(live, StringComparison.Ordinal))
            && _prefixes.Any(prefix => eventType.StartsWith(prefix, StringComparison.Ordinal));
    }
}
