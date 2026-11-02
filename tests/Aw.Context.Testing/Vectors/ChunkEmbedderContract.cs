using Aw.Context.Core;
using Aw.Context.Core.Vectors;
using Aw.Context.Testing.Records;
using Xunit;

namespace Aw.Context.Testing.Vectors;

/// <summary>The contract every <see cref="IChunkEmbedder"/> honours (spec §5.6): one unit-length vector per text, in order, repeatable, under the model it names; an empty batch is refused.</summary>
/// <typeparam name="TEmbedder">The implementation.</typeparam>
public abstract class ChunkEmbedderContract<TEmbedder>
    where TEmbedder : IChunkEmbedder
{
    private static readonly string _owner = typeof(TEmbedder).Name;

    /// <summary>A fresh embedder.</summary>
    /// <returns>The embedder.</returns>
    protected abstract TEmbedder Create();

    /// <summary>A batch answers one unit vector per text in order, and the same text gives the same vector.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_batch_answers_one_unit_vector_per_text_in_order()
    {
        var embedder = Create();
        var batch = ContractCheck.Ok(_owner, await embedder.Embed(["billing invoices", "login timeout", "billing invoices"], CancellationToken.None));
        ContractCheck.That(_owner, batch.Vectors.Count == 3, "one vector per text");
        ContractCheck.That(_owner, batch.Model == embedder.Model && batch.Model.Length > 0, "it names the model it was asked under");
        ContractCheck.That(_owner, batch.Vectors.All(v => MathF.Abs(MathF.Sqrt(v.Span.ToArray().Sum(c => c * c)) - 1f) < 1e-4f), "every vector is L2-normalised");
        ContractCheck.That(_owner, batch.Vectors[0].Span.SequenceEqual(batch.Vectors[2].Span) && !batch.Vectors[0].Span.SequenceEqual(batch.Vectors[1].Span), "the same text gives the same vector, another text another one, in order");
    }

    /// <summary>An empty batch is a query error.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_empty_batch_is_refused()
    {
        var embedder = Create();
        ContractCheck.Fails(_owner, await embedder.Embed([], CancellationToken.None), ContextErrors.QueryInvalid.Code);
    }
}
