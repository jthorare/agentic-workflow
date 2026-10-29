namespace Aw.Context.Core.ConceptSynthesis;

/// <summary>Builds record vectors from chunk vectors.</summary>
public static class RecordVectors
{
    /// <summary>Averages a record's chunk vectors into one L2-normalised vector.</summary>
    /// <param name="record">The record key.</param>
    /// <param name="version">The record version.</param>
    /// <param name="source">The record's source.</param>
    /// <param name="chunks">The record's chunk vectors, all of one dimension.</param>
    /// <returns>The record vector, or null when there are no chunks or they cancel out.</returns>
    public static RecordVector? Mean(string record, string version, string source, IEnumerable<float[]> chunks) =>
        throw new NotSupportedException();
}
