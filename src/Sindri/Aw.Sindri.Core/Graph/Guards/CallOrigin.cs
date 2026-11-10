namespace Aw.Sindri.Core.Graph.Guards;

/// <summary>The run that starts another workflow, as its own trigger describes it.</summary>
/// <param name="Run">The run's id ('&lt;slug&gt;@&lt;revision&gt;:&lt;occurrence&gt;').</param>
/// <param name="Depth">The run's own depth: 'trigger.callDepth', 0 for a run no workflow called.</param>
/// <param name="ParentRun">The run that called it ('trigger.parentRun'), or null.</param>
public sealed record CallOrigin(string Run, int Depth, string? ParentRun)
{
    /// <summary>The slugs of the workflows up the call chain, oldest first ('trigger.ancestors'); null for a top-level run or a run started before the field existed.</summary>
    public IReadOnlyList<string>? Ancestors { get; init; }
}
