using Aw.Kernel;

namespace Aw.Context.Core.Vectors;

/// <summary>Embeds a batch of chunk texts with the pinned model (spec §5.6); the Host binds it to 'Aw.Embeddings'. A model that is down is 'SND-CONTEXT-231': the embed job stops and what it stored stays.</summary>
public interface IChunkEmbedder
{
    /// <summary>The name and digest of the pinned model: the key a chunk's missing vector is looked up under.</summary>
    public string Model { get; }

    /// <summary>Embeds texts, in order.</summary>
    /// <param name="texts">The chunk texts, 1 to 32.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One vector per text, in order; a failure is returned, nothing is partly answered.</returns>
    public ValueTask<Result<ChunkEmbeddings>> Embed(IReadOnlyList<string> texts, CancellationToken cancellationToken);
}
