namespace Aw.Context.Core.Results;

/// <summary>What one decision pass over the stored records did, one row per source it read (#1597).</summary>
/// <param name="Sources">The sources, in source order.</param>
public sealed record DecisionReport(IReadOnlyList<SourceDecisions> Sources);
