using Aw.Sindri.Core.Workflow.Engine.Events;

namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>One gate's answer on a workflow ledger row, as the stats count it.</summary>
/// <param name="Gate">The gate's block id.</param>
/// <param name="Outcome">What the answer meant for the transition.</param>
/// <param name="Threshold">The recipe's threshold, when the gate has one.</param>
public sealed record StatGate(string Gate, GateOutcome Outcome, double? Threshold);
