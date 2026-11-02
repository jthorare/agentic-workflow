using System.Globalization;
using Aw.Embeddings.Testing;

namespace Aw.Embeddings.Tests.Ollama;

[Trait("Lane", "unit")]
public sealed class OllamaEmbedderTests
{
    private static readonly int[] _batchSizes = [32, 32, 6];
    private static readonly int[] _halvingPosts = [3, 1, 1, 1, 1];

    private static OllamaEmbedder Make(FakeOllamaHandler fake, string url = "http://127.0.0.1:11434", string? model = null)
        => new(A.Options(url) with { Model = model ?? A.Model }, fake);

    [Theory]
    [InlineData("http://localhost:11434")]
    [InlineData("https://127.0.0.1:11434")]
    [InlineData("http://192.168.1.5:11434")]
    [InlineData("http://user:pw@127.0.0.1:11434")]
    [InlineData("not a url")]
    public async Task A_non_loopback_target_is_refused_before_any_request(string url)
    {
        var fake = A.Ollama();
        using var embedder = Make(fake, url);

        var embedded = await embedder.Embed(["a"], TestContext.Current.CancellationToken);
        var probed = await embedder.Probe(TestContext.Current.CancellationToken);

        Assert.Equal(EmbeddingsErrors.TargetRefused.Code, embedded.Error.Code);
        Assert.Equal(EmbeddingsErrors.TargetRefused.Code, probed.Error.Code);
        Assert.Empty(fake.Calls);
    }

    [Theory]
    [InlineData("http://127.0.0.1:11434")]
    [InlineData("http://[::1]:11434")]
    public void Both_loopback_literals_are_allowed(string url) => Assert.True(OllamaEmbedder.IsLoopbackUrl(url));

    [Fact]
    public async Task A_cloud_model_is_refused()
    {
        var fake = A.Ollama();
        using var embedder = Make(fake, model: "gpt-oss:120b-cloud");

        var embedded = await embedder.Embed(["a"], TestContext.Current.CancellationToken);

        Assert.Equal(EmbeddingsErrors.TargetRefused.Code, embedded.Error.Code);
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task A_digest_other_than_the_pin_is_refused_and_nothing_is_embedded()
    {
        var fake = A.Ollama();
        fake.Models[A.Model] = "sha256:" + new string('0', 64);
        using var embedder = Make(fake);

        var embedded = await embedder.Embed(["a"], TestContext.Current.CancellationToken);

        Assert.Equal(EmbeddingsErrors.PinMismatch.Code, embedded.Error.Code);
        Assert.DoesNotContain(fake.Calls, c => c.Path == "/api/embed");
    }

    [Fact]
    public async Task A_missing_model_is_refused_and_never_pulled()
    {
        var fake = A.Ollama();
        fake.Models.Clear();
        using var embedder = Make(fake);

        var probed = await embedder.Probe(TestContext.Current.CancellationToken);

        Assert.Equal(EmbeddingsErrors.PinMismatch.Code, probed.Error.Code);
        Assert.All(fake.Calls, c => Assert.Equal("/api/tags", c.Path));
    }

    [Fact]
    public async Task The_digest_compares_with_or_without_the_sha256_prefix()
    {
        var fake = A.Ollama();
        fake.Models[A.Model] = A.Digest["sha256:".Length..];
        using var embedder = Make(fake);

        var probed = await embedder.Probe(TestContext.Current.CancellationToken);

        Assert.True(probed.IsSuccess);
    }

    [Fact]
    public async Task The_model_is_checked_once_not_per_call()
    {
        var fake = A.Ollama();
        using var embedder = Make(fake);

        _ = await embedder.Embed(["a"], TestContext.Current.CancellationToken);
        _ = await embedder.Embed(["b"], TestContext.Current.CancellationToken);

        _ = Assert.Single(fake.Calls, c => c.Path == "/api/tags");
    }

    [Fact]
    public async Task Texts_go_in_batches_of_32_with_truncate_off_and_come_back_in_order()
    {
        var fake = A.Ollama();
        using var embedder = Make(fake);
        var texts = Enumerable.Range(0, 70).Select(i => "text " + i.ToString(CultureInfo.InvariantCulture)).ToList();

        var embedded = await embedder.Embed(texts, TestContext.Current.CancellationToken);

        var posts = fake.Calls.Where(c => c.Path == "/api/embed").ToList();
        int[] sizes = [.. posts.Select(c => c.Inputs.Count)];
        Assert.Equal(_batchSizes, sizes);
        Assert.All(posts, c => Assert.False(c.Truncate));
        Assert.All(posts, c => Assert.Equal(A.Model, c.Model));
        float[][] expected = [.. texts.Select(FakeOllamaHandler.VectorOf)];
        float[][] actual = [.. embedded.Value.Select(v => v.ToArray())];
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task No_texts_means_no_request()
    {
        var fake = A.Ollama();
        using var embedder = Make(fake);

        var embedded = await embedder.Embed([], TestContext.Current.CancellationToken);

        Assert.Empty(embedded.Value);
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task An_input_over_the_context_is_halved_and_retried_once()
    {
        var fake = A.Ollama();
        fake.MaxInputChars = 60;
        using var embedder = Make(fake);
        var longText = new string('x', 100);

        var embedded = await embedder.Embed(["short", longText, "tiny"], TestContext.Current.CancellationToken);

        float[][] expected = [FakeOllamaHandler.VectorOf("short"), FakeOllamaHandler.VectorOf(new string('x', 50)), FakeOllamaHandler.VectorOf("tiny")];
        float[][] actual = [.. embedded.Value.Select(v => v.ToArray())];
        Assert.Equal(expected, actual);
        // the whole batch, then each text alone, then the long one's first half
        int[] posts = [.. fake.Calls.Where(c => c.Path == "/api/embed").Select(c => c.Inputs.Count)];
        Assert.Equal(_halvingPosts, posts);
    }

    [Fact]
    public async Task A_second_context_failure_is_an_error_result_not_a_third_try()
    {
        var fake = A.Ollama();
        fake.MaxInputChars = 10;
        using var embedder = Make(fake);

        var embedded = await embedder.Embed([new string('x', 100)], TestContext.Current.CancellationToken);

        Assert.Equal(EmbeddingsErrors.ServerFailed.Code, embedded.Error.Code);
        // the batch (of one), the text alone is the same call, so: batch, one, half
        Assert.Equal(3, fake.Calls.Count(c => c.Path == "/api/embed"));
    }

    [Fact]
    public async Task A_server_error_is_an_error_result_without_the_servers_words()
    {
        var fake = A.Ollama();
        fake.EmbedStatus = 500;
        using var embedder = Make(fake);

        var embedded = await embedder.Embed(["a"], TestContext.Current.CancellationToken);

        Assert.Equal(EmbeddingsErrors.ServerFailed.Code, embedded.Error.Code);
        Assert.DoesNotContain("scripted", embedded.Error.Message, StringComparison.Ordinal);
        Assert.Contains("500", embedded.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_cancelled_call_throws_and_sends_nothing()
    {
        var fake = A.Ollama();
        using var embedder = Make(fake);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await embedder.Embed(["a"], cancelled.Token));

        Assert.Empty(fake.Calls);
    }
}
