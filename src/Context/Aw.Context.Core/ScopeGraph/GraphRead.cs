namespace Aw.Context.Core.ScopeGraph;

/// <summary>One context_node, context_neighbors or context_query call a scoping Step's session made against its graph (issue 1005).</summary>
/// <param name="Node">The node id asked about.</param>
/// <param name="Tool">The tool that was called.</param>
/// <param name="Outside">True when the call stepped outside the graph.</param>
/// <param name="Reason">Why, required when <paramref name="Outside"/> is true.</param>
/// <param name="At">When.</param>
public sealed record GraphRead(string Node, string Tool, bool Outside, string? Reason, DateTimeOffset At);
