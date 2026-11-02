using Aw.Context.Core.Engine;
using Aw.Context.Core.Vectors;
using Aw.Kernel;

namespace Aw.Context.Core.Facade;

/// <summary>The embed job's verb (owned-index spec §5.6): one run that gives the chunks without a vector for the pinned model their vectors. IContextEngine extends it.</summary>
public interface IContextEmbed
{
    /// <summary>Embeds the chunks that have no vector for the pinned model, newest-updated record first, at most <see cref="EmbedRecordsRequest.MaxChunks"/> of them. A model that is down stops the run with its error; what was stored stays, and the next run takes up from there.</summary>
    /// <param name="request">The limits of this run.</param>
    /// <param name="scope">The caller's scope.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>What the run embedded and stored and whether any chunk is left, SND-CONTEXT-006 for limits out of range, or SND-CONTEXT-231 when the model is down.</returns>
    public ValueTask<Result<EmbedRecordsReport>> Embed(EmbedRecordsRequest request, ContextScope scope, CancellationToken cancellationToken);
}
