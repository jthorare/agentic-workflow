namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>What an evidence provider found (spec §2, A §17.11).</summary>
/// <param name="Passed">Whether the evidence passes.</param>
/// <param name="Reasons">Why, in the words the human reads.</param>
public sealed record EvidenceResult(bool Passed, IReadOnlyList<string> Reasons);
