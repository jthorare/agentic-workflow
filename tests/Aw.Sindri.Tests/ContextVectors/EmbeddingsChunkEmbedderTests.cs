using Aw.Context.Core;
using Aw.Embeddings;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Host.ContextVectors;

namespace Aw.Sindri.Tests.ContextVectors;

/// <summary>The adapter behind Context's chunk embedder (owned-index spec §5.6): a batch of texts, the model's vectors in order, the model key named.</summary>
[Lane(TestLane.Unit)]
public sealed class EmbeddingsChunkEmbedderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_batch_comes_back_in_order_under_the_model_key()
    {
        var stub = new StubEmbedder();
        var embedder = new EmbeddingsChunkEmbedder(stub, "m@sha256:ab");

        var result = await embedder.Embed(["a", "bb"], Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("m@sha256:ab", embedder.Model);
        Assert.Equal("m@sha256:ab", result.Value.Model);
        Assert.Equal([1f, 0f], result.Value.Vectors[0].ToArray());
        Assert.Equal([2f, 0f], result.Value.Vectors[1].ToArray());
        Assert.Equal(["a", "bb"], stub.Seen);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(33)]
    public async Task A_batch_outside_one_to_32_texts_is_refused(int count)
    {
        var result = await new EmbeddingsChunkEmbedder(new StubEmbedder(), "m").Embed([.. Enumerable.Repeat("t", count)], Ct);

        Assert.Equal(ContextErrors.QueryInvalid.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_model_that_is_down_is_the_embedder_unavailable_error_with_its_cause()
    {
        var result = await new EmbeddingsChunkEmbedder(new StubEmbedder { Failure = EmbeddingsErrors.ServerFailed.With("connection refused") }, "m").Embed(["a"], Ct);

        Assert.Equal(ContextErrors.EmbedderUnavailable.Code, result.Error.Code);
        Assert.Contains(EmbeddingsErrors.ServerFailed.Code, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_different_number_of_vectors_than_texts_is_unavailable()
    {
        var result = await new EmbeddingsChunkEmbedder(new StubEmbedder { Drop = true }, "m").Embed(["a", "b"], Ct);

        Assert.Equal(ContextErrors.EmbedderUnavailable.Code, result.Error.Code);
    }

    private sealed class StubEmbedder : IEmbedder
    {
        public List<string> Seen { get; } = [];

        public Error? Failure { get; init; }

        public bool Drop { get; init; }

        public ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Ok());

        public ValueTask<Result<IReadOnlyList<ReadOnlyMemory<float>>>> Embed(IReadOnlyList<string> texts, CancellationToken cancellationToken)
        {
            if (Failure is { } failure)
            {
                return ValueTask.FromResult<Result<IReadOnlyList<ReadOnlyMemory<float>>>>(failure);
            }

            Seen.AddRange(texts);
            IReadOnlyList<ReadOnlyMemory<float>> vectors = [.. texts.Skip(Drop ? 1 : 0).Select(static t => new ReadOnlyMemory<float>([t.Length, 0f]))];
            return ValueTask.FromResult(Results.Success(vectors));
        }
    }
}
