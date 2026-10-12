namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>A gate's answer (spec §2): pass, fail or uncertain, with a score for a rung-2 or rung-3 gate.</summary>
/// <param name="Verdict">Pass, fail or uncertain.</param>
/// <param name="Score">The score, for a gate with a threshold.</param>
/// <param name="Reasons">Why, in the words the human reads.</param>
public sealed record GateResult(GateVerdict Verdict, double? Score, IReadOnlyList<string> Reasons);
