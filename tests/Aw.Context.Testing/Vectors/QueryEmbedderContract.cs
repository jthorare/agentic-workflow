using Aw.Context.Core;
using Aw.Context.Core.Vectors;
using Aw.Context.Testing.Records;
using Xunit;

namespace Aw.Context.Testing.Vectors;

/// <summary>The contract every <see cref="IQueryEmbedder"/> honours (spec §4.4): a unit-length vector, the same one for the same text, a model name, and an empty text refused.</summary>
/// <typeparam name="TEmbedder">The implementation.</typeparam>
public abstract class QueryEmbedderContract<TEmbedder>
    where TEmbedder : IQueryEmbedder
{
    private static readonly string _owner = typeof(TEmbedder).Name;

    /// <summary>A fresh embedder.</summary>
    /// <returns>The embedder.</returns>
    protected abstract TEmbedder Create();

    /// <summary>A vector is L2-normalised, repeatable, and names its model.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_vector_has_unit_length_repeats_and_names_its_model()
    {
        var embedder = Create();
        var first = ContractCheck.Ok(_owner, await embedder.Embed("why is checkout slow", CancellationToken.None));
        var again = ContractCheck.Ok(_owner, await embedder.Embed("why is checkout slow", CancellationToken.None));
        var length = MathF.Sqrt(first.Vec.Span.ToArray().Sum(c => c * c));
        ContractCheck.That(_owner, MathF.Abs(length - 1f) < 1e-4f, "the vector is L2-normalised");
        ContractCheck.That(_owner, first.Vec.Span.SequenceEqual(again.Vec.Span) && first.Model == again.Model, "the same text gives the same vector");
        ContractCheck.That(_owner, first.Model.Length > 0, "it names the model");
    }

    /// <summary>Different texts give different vectors, and every vector has the same dimension.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Different_texts_differ_and_share_one_dimension()
    {
        var embedder = Create();
        var a = ContractCheck.Ok(_owner, await embedder.Embed("billing invoices", CancellationToken.None));
        var b = ContractCheck.Ok(_owner, await embedder.Embed("login timeout", CancellationToken.None));
        ContractCheck.That(_owner, a.Vec.Length == b.Vec.Length && a.Vec.Length > 0, "one dimension");
        ContractCheck.That(_owner, !a.Vec.Span.SequenceEqual(b.Vec.Span), "two texts that share no word differ");
    }

    /// <summary>An empty text is a query error, not a vector.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_empty_text_is_refused()
    {
        var embedder = Create();
        ContractCheck.Fails(_owner, await embedder.Embed("  ", CancellationToken.None), ContextErrors.QueryInvalid.Code);
    }
}
