namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>A gate's refusals.</summary>
/// <param name="Refused">Answers that refused.</param>
/// <param name="Evaluated">Answers in all.</param>
public sealed record GateRefusals(int Refused, int Evaluated);
