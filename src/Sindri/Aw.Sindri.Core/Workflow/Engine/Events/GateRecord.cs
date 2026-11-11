using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Events;

/// <summary>One gate's answer as an event records it (spec §5.6).</summary>
/// <param name="Gate">The gate.</param>
/// <param name="Verdict">Its verdict; null when no runtime answered.</param>
/// <param name="Score">Its score, for a gate with a threshold.</param>
/// <param name="Threshold">The recipe's threshold.</param>
/// <param name="Reasons">Its reasons.</param>
/// <param name="Outcome">What it meant for the transition.</param>
public sealed record GateRecord(
    BlockId Gate,
    GateVerdict? Verdict,
    double? Score,
    double? Threshold,
    IReadOnlyList<string> Reasons,
    GateOutcome Outcome);
