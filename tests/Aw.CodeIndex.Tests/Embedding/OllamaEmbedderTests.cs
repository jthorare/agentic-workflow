using System.Net;
using System.Text.Json;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Infrastructure.Embedding;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Embedding;

[Trait("Lane", "unit")]
public sealed class OllamaEmbedderTests
{
    private static readonly ModelPin _pin = new(FakeOllamaServer.Model, "sha256:" + FakeOllamaServer.Digest);
    private static readonly EmbeddingsSettings _settings = new(Enabled: true, CodeIndexSettings.DefaultEmbeddingsUrl, Model: null);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static OllamaEmbedder Over(FakeOllamaServer server) => new(_pin, server);

    [Fact]
    public async Task Probe_reads_the_tags_once_and_sends_nothing_to_embed()
    {
        var server = new FakeOllamaServer();

        var probe = await Over(server).Probe(_settings, Token);

        Assert.False(probe.IsError);
        var request = Assert.Single(server.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://127.0.0.1:11434/api/tags", request.Uri.ToString());
    }

    [Fact]
    public async Task Probe_says_pin_mismatch_when_the_model_is_not_there()
    {
        var server = new FakeOllamaServer();
        _ = server.Models.Remove(FakeOllamaServer.Model);

        var probe = await Over(server).Probe(_settings, Token);

        Assert.Equal(CodeIndexErrors.PinMismatch.Code, probe.Error.Code);
    }

    [Fact]
    public async Task Probe_says_pin_mismatch_when_the_digest_is_not_the_pinned_one()
    {
        var server = new FakeOllamaServer();
        server.Models[FakeOllamaServer.Model] = new string('a', 64);

        var probe = await Over(server).Probe(_settings, Token);

        Assert.Equal(CodeIndexErrors.PinMismatch.Code, probe.Error.Code);
    }

    [Fact]
    public async Task Probe_accepts_the_digest_with_or_without_its_algorithm_prefix()
    {
        var server = new FakeOllamaServer();
        server.Models[FakeOllamaServer.Model] = "sha256:" + FakeOllamaServer.Digest.ToUpperInvariant();

        Assert.False((await Over(server).Probe(_settings, Token)).IsError);
    }

    [Fact]
    public async Task A_model_other_than_the_pin_must_exist_but_has_no_digest_to_check()
    {
        var server = new FakeOllamaServer();
        server.Models["mxbai-embed-large:latest"] = new string('b', 64);

        var present = await Over(server).Probe(_settings with { Model = "mxbai-embed-large:latest" }, Token);
        var absent = await Over(server).Probe(_settings with { Model = "other:latest" }, Token);

        Assert.False(present.IsError);
        Assert.Equal(CodeIndexErrors.PinMismatch.Code, absent.Error.Code);
    }

    [Fact]
    public async Task A_down_server_is_embedding_server_failed_for_probe_and_embed()
    {
        var server = new FakeOllamaServer { Down = true };

        var probe = await Over(server).Probe(_settings, Token);
        var embed = await Over(server).Embed(_settings, ["x"], Token);

        Assert.Equal(CodeIndexErrors.EmbeddingServerFailed.Code, probe.Error.Code);
        Assert.Equal(CodeIndexErrors.EmbeddingServerFailed.Code, embed.Error.Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task A_redirect_or_an_error_status_is_embedding_server_failed(HttpStatusCode status)
    {
        var server = new FakeOllamaServer { Status = status };

        var probe = await Over(server).Probe(_settings, Token);
        var embed = await Over(server).Embed(_settings, ["x"], Token);

        Assert.Equal(CodeIndexErrors.EmbeddingServerFailed.Code, probe.Error.Code);
        Assert.Equal(CodeIndexErrors.EmbeddingServerFailed.Code, embed.Error.Code);
        Assert.DoesNotContain(server.Requests, r => r.Uri.Host != "127.0.0.1");
    }

    [Fact]
    public async Task Embed_posts_the_model_and_the_batch_to_api_embed()
    {
        var server = new FakeOllamaServer();

        var embedded = await Over(server).Embed(_settings, ["one", "two"], Token);

        Assert.False(embedded.IsError);
        var request = Assert.Single(server.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://127.0.0.1:11434/api/embed", request.Uri.ToString());
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(FakeOllamaServer.Model, body.RootElement.GetProperty("model").GetString());
        Assert.Equal(["one", "two"], body.RootElement.GetProperty("input").EnumerateArray().Select(i => i.GetString()), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Embed_on_an_ipv6_loopback_url_reaches_that_address()
    {
        var server = new FakeOllamaServer();

        var embedded = await Over(server).Embed(_settings with { Url = "http://[::1]:11434" }, ["x"], Token);

        Assert.False(embedded.IsError);
        Assert.Equal("http://[::1]:11434/api/embed", server.Requests.Single().Uri.ToString());
    }

    [Fact]
    public async Task A_missing_model_on_embed_is_embedding_server_failed()
    {
        var server = new FakeOllamaServer();
        _ = server.Models.Remove(FakeOllamaServer.Model);

        var embed = await Over(server).Embed(_settings, ["x"], Token);

        Assert.Equal(CodeIndexErrors.EmbeddingServerFailed.Code, embed.Error.Code);
    }

    [Theory]
    [InlineData("""{"embeddings":[]}""")]
    [InlineData("""{"embeddings":[[1,2],[1]]}""")]
    [InlineData("""{"embeddings":[[],[]]}""")]
    [InlineData("""{"embeddings":"nope"}""")]
    [InlineData("""not json""")]
    [InlineData("""{}""")]
    public async Task An_answer_of_an_unexpected_shape_is_embedding_server_failed(string body)
    {
        var server = new FakeOllamaServer { EmbedBody = body };

        var embed = await Over(server).Embed(_settings, ["a", "b"], Token);

        Assert.Equal(CodeIndexErrors.EmbeddingServerFailed.Code, embed.Error.Code);
    }

    [Fact]
    public async Task A_vector_count_that_differs_from_the_batch_is_embedding_server_failed()
    {
        var server = new FakeOllamaServer { DropLastVector = true };

        var embed = await Over(server).Embed(_settings, ["a", "b"], Token);

        Assert.Equal(CodeIndexErrors.EmbeddingServerFailed.Code, embed.Error.Code);
    }

    [Fact]
    public async Task A_body_past_the_models_context_is_cut_so_the_server_does_not_answer_400_issue_3128()
    {
        var server = new FakeOllamaServer { MaxInputCharacters = 3_000 };

        var embedded = await Over(server).Embed(_settings, ["short", new string('x', 6_000)], Token);

        Assert.False(embedded.IsError);
        Assert.Equal(2, embedded.Value.Count);
    }

    [Fact]
    public async Task A_cut_never_leaves_half_a_surrogate_pair_issue_3128()
    {
        var server = new FakeOllamaServer();
        var text = new string('x', OllamaEmbedder.InputMaxCharacters - 1) + "\U0001F600tail";

        _ = await Over(server).Embed(_settings, [text], Token);

        using var body = JsonDocument.Parse(server.Requests.Last().Body);
        var sent = body.RootElement.GetProperty("input")[0].GetString()!;
        Assert.Equal(OllamaEmbedder.InputMaxCharacters - 1, sent.Length);
        Assert.DoesNotContain('\uFFFD', sent);
    }

    [Fact]
    public async Task One_input_the_server_refuses_is_retried_alone_then_skipped_and_counted_issue_3128()
    {
        var server = new FakeOllamaServer { RejectInputContaining = "POISON" };
        using var embedder = Over(server);

        var embedded = await embedder.Embed(_settings, ["one", "POISON", "three"], Token);

        Assert.False(embedded.IsError);
        Assert.Equal(3, embedded.Value.Count);
        Assert.Equal(FakeEmbedder.VectorOf("one"), embedded.Value[0].ToArray());
        Assert.Equal(0, embedded.Value[1].Length);
        Assert.Equal(FakeEmbedder.VectorOf("three"), embedded.Value[2].ToArray());
        Assert.Contains(server.Requests, r => r.Body.Contains("POISON", StringComparison.Ordinal) && !r.Body.Contains("three", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_batch_of_good_inputs_is_one_request_and_skips_none_issue_3128()
    {
        var server = new FakeOllamaServer { RejectInputContaining = "POISON" };
        using var embedder = Over(server);

        var embedded = await embedder.Embed(_settings, ["one", "two"], Token);

        Assert.False(embedded.IsError);
        _ = Assert.Single(server.Requests);
    }

    [Fact]
    public async Task An_empty_batch_sends_nothing()
    {
        var server = new FakeOllamaServer();

        var embedded = await Over(server).Embed(_settings, [], Token);

        Assert.Empty(embedded.Value);
        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task A_refused_target_sends_nothing()
    {
        var server = new FakeOllamaServer();

        _ = await Over(server).Probe(_settings with { Url = "http://localhost:11434" }, Token);
        _ = await Over(server).Embed(_settings with { Model = "x-cloud" }, ["x"], Token);

        Assert.Empty(server.Requests);
    }

    [Fact]
    public void ModelFor_is_the_pinned_name_when_the_settings_name_none()
        => Assert.Equal(FakeOllamaServer.Model, Over(new FakeOllamaServer()).ModelFor(_settings));

    [Fact]
    public void The_default_pin_is_the_one_in_the_code_index_lock()
    {
        var row = File.ReadLines(Path.Combine(RepoRoot(), "context", "code-index.lock"))
            .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Single(f => f.Length == 3 && f[0] == "ollama-model");

        Assert.Equal(new ModelPin(row[1], row[2]), OllamaEmbedder.DefaultPin);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("the repo root was not found above the test binaries");
    }
}
