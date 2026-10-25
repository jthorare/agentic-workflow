using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Embedding;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.CodeIndex.Features.EmbedCodeIndex;

/// <summary>
/// The embed slice (spec §5.6, §5.7): embeds a repo's symbols that have no vector, without the heavy slot, and carries the work on
/// until the layer is ok (or the model fails, or the call is cancelled). It works in chunks of <see cref="CodeIndexRules.MaxEmbeddedPerBuild"/>
/// symbols and each chunk is one copy-embed-publish of the live index, the same way a build is written: the store holds the repo's
/// index lock from the copy to the publish, so a build of the same repo waits for the chunk (and the chunk for the build) and neither
/// discards the other's work, and every published chunk survives a cancel or a crash. A move needs no special handling: the build
/// already dropped the vectors of changed and removed files, so only their symbols (and new ones) are pending.
/// </summary>
/// <param name="store">The index files.</param>
/// <param name="layer">The embeddings layer, which probes the model and writes the vectors.</param>
/// <param name="clock">The time source.</param>
public sealed class EmbedCodeIndexService(IIndexStore store, EmbeddingsLayer layer, TimeProvider clock) : IService<EmbedCodeIndexRequest, EmbedCodeIndexResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<EmbedCodeIndexResponse>> Handle(EmbedCodeIndexRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var last = -1;
        int? carried = null;
        var made = 0;
        while (true)
        {
            var chunk = await Chunk(request, cancellationToken).ConfigureAwait(false);
            if (chunk.IsError)
            {
                return chunk;
            }

            // the first chunk's carried count is what the run began with; later chunks count their predecessors' vectors as carried
            carried ??= chunk.Value.Carried;
            made += chunk.Value.Made;
            var total = chunk.Value with { Carried = carried.Value, Made = made };
            if (total.Embeddings != LayerState.Pending || total.Embedded <= last)
            {
                return total;
            }

            last = total.Embedded;
        }
    }

    private async ValueTask<Result<EmbedCodeIndexResponse>> Chunk(EmbedCodeIndexRequest request, CancellationToken cancellationToken)
    {
        var repo = request.Repo;
        var begun = await store.BeginBuild(repo.Name, incremental: true, cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            if (begun.Value.Previous is not { } previous)
            {
                return CodeIndexErrors.IndexNotBuilt.With($"There is no index for '{repo.Name.Value}' to embed yet.");
            }

            var ran = await layer.Run(begun.Value, repo.Settings.Embeddings, request.Progress, CodeIndexRules.MaxEmbeddedPerBuild, cancellationToken).ConfigureAwait(false);
            if (ran.IsError)
            {
                return ran.Error;
            }

            var published = await begun.Value.Publish(previous.Meta with { BuiltAt = clock.GetUtcNow() }, cancellationToken).ConfigureAwait(false);
            return published.IsError
                ? published.Error
                : new EmbedCodeIndexResponse(repo.Name, ran.Value.Embedded, ran.Value.Embeddable, ran.Value.Carried, ran.Value.Made, published.Value.Layers.First(l => l.Layer == LayerName.Embeddings).State);
        }
    }
}
