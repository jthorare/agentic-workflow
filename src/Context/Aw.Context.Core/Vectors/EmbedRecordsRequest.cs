namespace Aw.Context.Core.Vectors;

/// <summary>Asks to embed the chunks that have no vector for the current model (spec §5.6).</summary>
/// <param name="MaxChunks">At most this many chunks in this run, 1 to 100,000; the default is one chunk of work, 4,096 chunks. The job is restartable, so a caller loops until the report says nothing is left.</param>
/// <param name="BatchSize">How many texts go to the model in one call, 1 to 32.</param>
public sealed record EmbedRecordsRequest(int MaxChunks = 4096, int BatchSize = 32);
