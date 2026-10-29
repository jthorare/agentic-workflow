namespace Aw.Context.Core.Results;

/// <summary>What one decision pass over the stored records did, one row per source it read, and the concept synthesis that follows it (#1597).</summary>
/// <param name="Sources">The sources, in source order.</param>
/// <param name="Synthesis">The concept synthesis run, or null when it did not run (off, no labeling, or nothing configured).</param>
public sealed record DecisionReport(IReadOnlyList<SourceDecisions> Sources, ConceptSynthesisReport? Synthesis = null);
