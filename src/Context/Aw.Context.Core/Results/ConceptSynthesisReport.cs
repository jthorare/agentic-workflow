namespace Aw.Context.Core.Results;

/// <summary>What one concept synthesis run did over the whole record store (#1597).</summary>
/// <param name="Groups">Groups the grouper found.</param>
/// <param name="Unchanged">Groups whose membership matched a labeled concept: no model call.</param>
/// <param name="Labeled">Groups the model named this run.</param>
/// <param name="Queued">Groups still waiting for a name: the budget ran out, the model was down or its answer could not be read.</param>
/// <param name="Pending">Live records with no vector yet.</param>
/// <param name="Concepts">Concepts stored after the run.</param>
/// <param name="MultiSource">Stored concepts that cite records from two or more sources.</param>
/// <param name="Covered">Records that sit in a labeled group.</param>
/// <param name="Embedded">Live records that have a vector.</param>
/// <param name="Cosines">The nearest-neighbour cosines in buckets, lowest first, to choose the threshold by eye.</param>
/// <param name="Tokens">Model tokens this run spent.</param>
/// <param name="Code">The error code the run ended with, or null.</param>
public sealed record ConceptSynthesisReport(
    int Groups,
    int Unchanged,
    int Labeled,
    int Queued,
    int Pending,
    int Concepts,
    int MultiSource,
    int Covered,
    int Embedded,
    IReadOnlyList<CosineBucket> Cosines,
    int Tokens,
    string? Code)
{
    /// <summary>The share of embedded records that sit in a labeled group, 0 to 1 (0 when none are embedded).</summary>
    public double Coverage => Embedded == 0 ? 0 : (double)Covered / Embedded;

    /// <summary>A run that stopped before it read anything.</summary>
    /// <param name="code">The error code.</param>
    /// <returns>The report.</returns>
    public static ConceptSynthesisReport Failed(string code) => new(0, 0, 0, 0, 0, 0, 0, 0, 0, [], 0, code);
}
