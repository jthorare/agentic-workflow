using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Testing;
using Aw.CodeIndex.Tests.Build;
using Aw.Kernel;

namespace Aw.CodeIndex.Tests.Embedding;

[Trait("Lane", "unit")]
public sealed class NeighborsTests
{
    private const string TextOfSecond = "M0001\nvoid M0001()\n{ Work(0001); }";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<BuildHarness> Built(int methods = 3, FakeEmbedder? embedder = null, CodeIndexSettings? settings = null)
    {
        var harness = new BuildHarness { Embedder = embedder ?? new FakeEmbedder(), Settings = settings ?? CodeIndexSettings.Default };
        var symbols = Enumerable.Range(0, methods).Select(i => Parsed.Method("A.M" + i.ToString("D4", CultureInfo.InvariantCulture), 2 + (i * 3)) with { Body = "{ Work(" + i.ToString("D4", CultureInfo.InvariantCulture) + "); }" }).ToList();
        _ = harness.Parser.With("src/A.cs", new ParseOutcome(symbols));
        _ = harness.Snapshot.WithFile(BuildHarness.First, "src/A.cs", "class A");
        _ = await harness.BuildAndEmbed(BuildHarness.First, cancellationToken: Token);
        return harness;
    }

    [Fact]
    public async Task The_symbol_whose_text_is_the_query_is_the_nearest()
    {
        var harness = await Built();

        var answer = (await harness.Facade.Neighbors(harness.Scope, BuildHarness.Name, TextOfSecond, 2, Token)).Value;

        Assert.Equal(2, answer.Neighbors.Count);
        Assert.Equal("M0001", answer.Neighbors[0].Symbol.Name);
        Assert.Equal(1.0, answer.Neighbors[0].Score, 4);
        Assert.True(answer.Neighbors[0].Score >= answer.Neighbors[1].Score);
        Assert.Equal([TextOfSecond], harness.Embedder.Batches[^1], StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_answer_is_stamped_with_the_embeddings_layer()
    {
        var harness = await Built();

        var stamp = (await harness.Facade.Neighbors(harness.Scope, BuildHarness.Name, TextOfSecond, 1, Token)).Value.Stamp;

        Assert.Equal(BuildHarness.Name, stamp.Repo);
        Assert.Equal(BuildHarness.First, stamp.Commit);
        Assert.Equal(LayerName.Embeddings, stamp.Layer.Layer);
        Assert.Equal(LayerState.Ok, stamp.Layer.State);
    }

    [Fact]
    public async Task A_pending_layer_answers_from_the_vectors_it_has()
    {
        var harness = await Built();
        _ = harness.Parser.With("src/B.cs", new ParseOutcome([Parsed.Method("B.N", 2)]));
        _ = harness.Snapshot.WithFile(BuildHarness.Second, "src/A.cs", "class A").WithFile(BuildHarness.Second, "src/B.cs", "class B");
        _ = await harness.Build(BuildHarness.Second, cancellationToken: Token);

        var answer = (await harness.Facade.Neighbors(harness.Scope, BuildHarness.Name, "M0001\nvoid M0001()\n{ Work(0001); }", 3, Token)).Value;

        Assert.Equal(3, answer.Neighbors.Count);
        Assert.Equal(LayerState.Pending, answer.Stamp.Layer.State);
    }

    [Fact]
    public async Task An_unavailable_layer_answers_with_its_own_code()
    {
        var harness = await Built(embedder: new FakeEmbedder { FailProbe = CodeIndexErrors.PinMismatch.With("digest differs") });

        var answer = await harness.Facade.Neighbors(harness.Scope, BuildHarness.Name, TextOfSecond, 2, Token);

        Assert.Equal(CodeIndexErrors.PinMismatch.Code, answer.Error.Code);
        Assert.Equal(ErrorKind.Unavailable, answer.Error.Kind);
    }

    [Fact]
    public async Task A_disabled_layer_answers_index_not_built()
    {
        var off = CodeIndexSettings.Default with { Embeddings = new EmbeddingsSettings(Enabled: false, CodeIndexSettings.DefaultEmbeddingsUrl, Model: null) };
        var harness = await Built(settings: off);

        var answer = await harness.Facade.Neighbors(harness.Scope, BuildHarness.Name, TextOfSecond, 2, Token);

        Assert.Equal(CodeIndexErrors.IndexNotBuilt.Code, answer.Error.Code);
    }

    [Fact]
    public async Task A_repo_with_no_index_or_outside_the_scope_answers_index_not_built()
    {
        var harness = new BuildHarness();
        var elsewhere = new CodeIndexScope([new CodeIndexRepo(RepoName("web"), "/work/web", CodeIndexSettings.Default)], new CodeIndexCaller(CallerKind.Session, RepoName("web")));

        var none = await harness.Facade.Neighbors(harness.Scope, BuildHarness.Name, "x", 1, Token);
        var outside = await (await Built()).Facade.Neighbors(elsewhere, BuildHarness.Name, "x", 1, Token);

        Assert.Equal(CodeIndexErrors.IndexNotBuilt.Code, none.Error.Code);
        Assert.Equal(CodeIndexErrors.IndexNotBuilt.Code, outside.Error.Code);
    }

    [Theory]
    [InlineData("", 3)]
    [InlineData("   ", 3)]
    [InlineData("text", 0)]
    [InlineData("text", -1)]
    public async Task A_blank_text_or_a_k_below_1_is_a_bad_query(string text, int k)
    {
        var harness = await Built();

        var answer = await harness.Facade.Neighbors(harness.Scope, BuildHarness.Name, text, k, Token);

        Assert.Equal(CodeIndexErrors.QueryInvalid.Code, answer.Error.Code);
        Assert.DoesNotContain(harness.Embedder.Batches.Skip(1), b => b.Count > 0);
    }

    [Fact]
    public async Task A_text_over_the_embed_limit_is_cut_not_refused()
    {
        var harness = await Built();

        var answer = await harness.Facade.Neighbors(harness.Scope, BuildHarness.Name, new string('x', CodeIndexRules.EmbedTextMaxCharacters + 1_000), 1, Token);

        Assert.False(answer.IsError);
        Assert.Equal(CodeIndexRules.EmbedTextMaxCharacters, harness.Embedder.Batches[^1].Single().Length);
    }

    [Fact]
    public async Task A_model_changed_since_the_build_answers_pin_mismatch()
    {
        var harness = await Built();
        var changed = CodeIndexSettings.Default with { Embeddings = new EmbeddingsSettings(Enabled: true, CodeIndexSettings.DefaultEmbeddingsUrl, "another-model:1") };
        var scope = new CodeIndexScope([new CodeIndexRepo(BuildHarness.Name, "/work/api", changed)], new CodeIndexCaller(CallerKind.Service, Repo: null));

        var answer = await harness.Facade.Neighbors(scope, BuildHarness.Name, TextOfSecond, 2, Token);

        Assert.Equal(CodeIndexErrors.PinMismatch.Code, answer.Error.Code);
    }

    [Fact]
    public async Task An_embed_failure_at_query_time_is_the_answer()
    {
        var embedder = new FakeEmbedder();
        var harness = await Built(embedder: embedder);
        embedder.FailEmbedAfter = (embedder.Batches.Count, CodeIndexErrors.EmbeddingServerFailed.With("went away"));

        var answer = await harness.Facade.Neighbors(harness.Scope, BuildHarness.Name, TextOfSecond, 2, Token);

        Assert.Equal(CodeIndexErrors.EmbeddingServerFailed.Code, answer.Error.Code);
    }

    private static Core.Values.RepoName RepoName(string name) => Core.Values.RepoName.From(name);
}
