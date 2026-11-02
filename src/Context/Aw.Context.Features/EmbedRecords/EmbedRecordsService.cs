using Aw.Context.Core;
using Aw.Context.Core.Records;
using Aw.Context.Core.Vectors;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.EmbedRecords;

/// <summary>
/// Embeds the chunks that have no vector for the pinned model, newest-updated record first, a batch at a time (spec §5.6).
/// Each batch is stored on its own with the guarded insert, so a stop (a limit, a model that went down) loses at most the
/// batch in flight and the next run takes up where this one left off.
/// </summary>
internal sealed class EmbedRecordsService(IRecordStore store, IChunkEmbedder embedder) : IService<EmbedRecordsRequest, EmbedRecordsReport>
{
    public async ValueTask<Result<EmbedRecordsReport>> Handle(EmbedRecordsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!InRange(request))
        {
            return ContextErrors.QueryInvalid.With("an embed run takes 1 to 100,000 chunks in batches of 1 to 32");
        }

        var embedded = 0;
        var stored = 0;
        string? stuckOn = null;
        while (embedded < request.MaxChunks)
        {
            var batch = await store.ChunksToEmbed(embedder.Model, Math.Min(request.BatchSize, request.MaxChunks - embedded), cancellationToken).ConfigureAwait(false);
            if (batch.IsError || batch.Value.Count == 0)
            {
                return batch.IsError ? batch.Error : new EmbedRecordsReport(embedded, stored, Done: true);
            }

            var worklist = Worklist(batch.Value);
            if (worklist == stuckOn)
            {
                return new EmbedRecordsReport(embedded, stored, Done: false);   // the same chunks and texts as the batch that stored nothing: asking again would loop
            }

            var kept = await EmbedAndStore(batch.Value, cancellationToken).ConfigureAwait(false);
            if (kept.IsError)
            {
                return kept.Error;
            }

            embedded += batch.Value.Count;
            stored += kept.Value;
            stuckOn = kept.Value == 0 ? worklist : null;   // nothing stuck: a changed chunk gets one retry with its new text
        }

        var left = await store.ChunksToEmbed(embedder.Model, 1, cancellationToken).ConfigureAwait(false);
        return left.IsError ? left.Error : new EmbedRecordsReport(embedded, stored, left.Value.Count == 0);
    }

    private static bool InRange(EmbedRecordsRequest request) => request.MaxChunks is >= 1 and <= 100_000 && request.BatchSize is >= 1 and <= 32;

    private static string Worklist(IReadOnlyList<ChunkToEmbed> chunks) => string.Join('\n', chunks.Select(static c => c.Key + "\t" + c.TextHash));

    private async ValueTask<Result<int>> EmbedAndStore(IReadOnlyList<ChunkToEmbed> chunks, CancellationToken cancellationToken)
    {
        var vectors = await embedder.Embed([.. chunks.Select(c => c.Text)], cancellationToken).ConfigureAwait(false);
        if (vectors.IsError)
        {
            return vectors.Error;
        }

        if (vectors.Value.Vectors.Count != chunks.Count)
        {
            return ContextErrors.EmbedderUnavailable.With($"the model answered {vectors.Value.Vectors.Count} vectors for {chunks.Count} texts");
        }

        var writes = chunks.Select((c, i) => new VectorWrite(VectorSpace.Chunk, c.Key, vectors.Value.Model, c.TextHash, vectors.Value.Vectors[i])).ToList();
        return await store.PutVectors(writes, cancellationToken).ConfigureAwait(false);
    }
}
