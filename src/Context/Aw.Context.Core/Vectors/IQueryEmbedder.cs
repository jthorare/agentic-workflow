using Aw.Kernel;

namespace Aw.Context.Core.Vectors;

/// <summary>Embeds a query with the pinned model (spec §4.4, §6.1); the Host binds it to 'Aw.Embeddings'. A model that is down is 'SND-CONTEXT-231': the dense leg drops out and the answer says so.</summary>
public interface IQueryEmbedder
{
    /// <summary>Embeds one text.</summary>
    /// <param name="text">The query or anchor name; 1 to 8,000 characters.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The same text always gives the same vector; an empty text is 'SND-CONTEXT-006'.</returns>
    public ValueTask<Result<QueryEmbedding>> Embed(string text, CancellationToken cancellationToken);
}
