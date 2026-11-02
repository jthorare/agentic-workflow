namespace Aw.Context.Core.Vectors;

/// <summary>What one embed run did.</summary>
/// <param name="Embedded">How many chunks the model embedded.</param>
/// <param name="Stored">How many vectors the store kept; fewer than embedded means a chunk changed or went while its batch was embedding.</param>
/// <param name="Done">True when no chunk is left without a vector, false when the run stopped at its limit.</param>
public sealed record EmbedRecordsReport(int Embedded, int Stored, bool Done);
