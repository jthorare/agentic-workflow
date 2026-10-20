namespace Aw.Sindri.Core.Line;

/// <summary>The profile values graph.json is derived with.</summary>
/// <param name="Parallel">workflow.line.parallel: how many phases a wave runs at once.</param>
/// <param name="MaxStack">workflow.line.maxStack: recorded in graph.json (PLN-STACK-001 checks it).</param>
/// <param name="DefaultBranch">The branch a phase with no dependency starts from.</param>
public sealed record PlanGraphOptions(int Parallel = 2, int MaxStack = 4, string DefaultBranch = "main");
