using Aw.Context.Core;
using Aw.Embeddings;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Host.ContextVectors;

namespace Aw.Sindri.Tests.ContextVectors;

/// <summary>The adapter behind Context's query embedder (owned-index spec §4.4): one text, the model's vector, L2-normalised.</summary>
[Lane(TestLane.Unit)]
public sealed class EmbeddingsQueryEmbedderTests
{
    private static readonly float[] _normalised = [0.6f, 0.8f];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_vector_is_normalised_and_names_the_model()
    {
        var embedder = new EmbeddingsQueryEmbedder(new StubEmbedder([3, 4]), "m@sha256:ab");

        var result = await embedder.Embed("why is checkout slow", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("m@sha256:ab", result.Value.Model);
        Assert.Equal(_normalised, result.Value.Vec.ToArray());
    }

    [Fact]
    public async Task One_text_is_sent_as_is()
    {
        var stub = new StubEmbedder([1, 0]);

        _ = await new EmbeddingsQueryEmbedder(stub, "m").Embed("login timeout", Ct);

        Assert.Equal(["login timeout"], stub.Seen);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_text_is_a_query_error_and_nothing_is_embedded(string text)
    {
        var stub = new StubEmbedder([1, 0]);

        var result = await new EmbeddingsQueryEmbedder(stub, "m").Embed(text, Ct);

        Assert.Equal(ContextErrors.QueryInvalid.Code, result.Error.Code);
        Assert.Empty(stub.Seen);
    }

    [Theory]
    [InlineData("AW-EMBEDDINGS-003")]
    [InlineData("AW-EMBEDDINGS-004")]
    [InlineData("AW-EMBEDDINGS-005")]
    public async Task A_model_that_cannot_answer_is_the_embedder_unavailable_error(string code)
    {
        var failing = new StubEmbedder(new Error(code, ErrorKind.Unavailable, "down"));

        var result = await new EmbeddingsQueryEmbedder(failing, "m").Embed("x", Ct);

        Assert.Equal(ContextErrors.EmbedderUnavailable.Code, result.Error.Code);
        Assert.Contains(code, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_zero_vector_is_the_embedder_unavailable_error()
    {
        var result = await new EmbeddingsQueryEmbedder(new StubEmbedder([0, 0]), "m").Embed("x", Ct);

        Assert.Equal(ContextErrors.EmbedderUnavailable.Code, result.Error.Code);
    }

    private sealed class StubEmbedder : IEmbedder
    {
        private readonly Result<IReadOnlyList<ReadOnlyMemory<float>>> _answer;

        public StubEmbedder(float[] vector)
        {
            _answer = Results.Success<IReadOnlyList<ReadOnlyMemory<float>>>([vector]);
        }

        public StubEmbedder(Error error)
        {
            _answer = error;
        }

        public List<string> Seen { get; } = [];

        public ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Ok());

        public ValueTask<Result<IReadOnlyList<ReadOnlyMemory<float>>>> Embed(IReadOnlyList<string> texts, CancellationToken cancellationToken)
        {
            Seen.AddRange(texts);
            return ValueTask.FromResult(_answer);
        }
    }
}
