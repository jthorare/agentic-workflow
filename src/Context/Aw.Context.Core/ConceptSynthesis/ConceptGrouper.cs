using Aw.Context.Core.Vectors;

namespace Aw.Context.Core.ConceptSynthesis;

/// <summary>Groups records whose vectors are near each other across sources.</summary>
public static class ConceptGrouper
{
    /// <summary>Joins each record to its near neighbours and keeps the connected groups.</summary>
    /// <param name="records">The record vectors, all of one dimension.</param>
    /// <param name="settings">The knobs.</param>
    /// <param name="scan">The vector scan.</param>
    /// <returns>The groups and every neighbour cosine seen.</returns>
    public static GroupingResult Group(IReadOnlyList<RecordVector> records, GroupingSettings settings, IVectorScan scan) =>
        throw new NotSupportedException();
}
