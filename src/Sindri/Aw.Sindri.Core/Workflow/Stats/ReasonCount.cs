namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>A reason and how often it happened.</summary>
/// <param name="Reason">The reason.</param>
/// <param name="N">How many times.</param>
public sealed record ReasonCount(string Reason, int N);
