using Aw.Embeddings;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.SearchMemory;
using Aw.Sindri.Host.Memory;
using Aw.Sindri.Testing.Memory;

namespace Aw.Sindri.Tests.Memory;

/// <summary>The Host adapter: the profile switch and a missing model both turn into Unavailable, and recall then answers by keywords.</summary>
[Lane(TestLane.Unit)]
public sealed class EmbeddingsMemoryEmbedderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FakeEmbedder(Result<IReadOnlyList<ReadOnlyMemory<float>>> answer) : IEmbedder
    {
        public int Calls { get; private set; }

        public ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken) => ValueTask.FromResult<Result<Unit>>(Unit.Value);

        public ValueTask<Result<IReadOnlyList<ReadOnlyMemory<float>>>> Embed(IReadOnlyList<string> texts, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(answer);
        }
    }

    private static Result<IReadOnlyList<ReadOnlyMemory<float>>> Vectors(int count) =>
        Results.Success<IReadOnlyList<ReadOnlyMemory<float>>>([.. Enumerable.Repeat(new ReadOnlyMemory<float>([1f, 0f]), count)]);

    [Fact]
    public async Task A_profile_with_embeddings_off_is_unavailable_and_never_calls_the_model()
    {
        var model = new FakeEmbedder(Vectors(1));
        var embedder = new EmbeddingsMemoryEmbedder(model, static _ => ValueTask.FromResult(false));

        var answer = await embedder.Embed(["text"], Token);

        Assert.False(answer.IsAvailable);
        Assert.Equal(0, model.Calls);
    }

    [Fact]
    public async Task A_model_error_is_unavailable_with_its_code_and_not_an_exception()
    {
        var model = new FakeEmbedder(EmbeddingsErrors.PinMismatch.With("not pulled"));
        var embedder = new EmbeddingsMemoryEmbedder(model, static _ => ValueTask.FromResult(true));

        var answer = await embedder.Embed(["text"], Token);

        Assert.False(answer.IsAvailable);
        Assert.Contains(EmbeddingsErrors.PinMismatch.Code, answer.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_working_model_gives_one_vector_per_text()
    {
        var embedder = new EmbeddingsMemoryEmbedder(new FakeEmbedder(Vectors(2)), static _ => ValueTask.FromResult(true));

        var answer = await embedder.Embed(["a", "b"], Token);

        Assert.True(answer.IsAvailable);
        Assert.Equal(2, answer.Vectors!.Count);
    }

    [Fact]
    public async Task Recall_over_an_unavailable_model_falls_back_to_keywords_and_says_so()
    {
        var h = new MemoryHarness();
        await h.Memory();
        _ = await h.Store.Add(MemorySamples.Fact("a", "deploys run from main"), Token);
        var off = new EmbeddingsMemoryEmbedder(new FakeEmbedder(Vectors(1)), static _ => ValueTask.FromResult(false));
        var search = new SearchMemoryService(h.Store, off, h.Clock);

        var result = await search.Handle(new SearchMemoryRequest("team-notes", "deploys main", Level: null, Limit: null), Token);

        Assert.Equal("keyword", result.Value.Mode);
        Assert.Equal(["a"], result.Value.Facts.Select(static f => f.Key), StringComparer.Ordinal);
        Assert.Contains("switched off", result.Value.Note, StringComparison.Ordinal);
    }
}
