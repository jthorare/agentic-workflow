using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Features.EmbedCodeIndex;
using Aw.CodeIndex.Testing;
using Aw.CodeIndex.Tests.Build;

namespace Aw.CodeIndex.Tests.Embedding;

[Trait("Lane", "unit")]
public sealed class EmbeddingsLayerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static BuildHarness Arranged(int methods, FakeEmbedder? embedder = null, CodeIndexSettings? settings = null)
    {
        var harness = new BuildHarness { Embedder = embedder ?? new FakeEmbedder(), Settings = settings ?? CodeIndexSettings.Default };
        var symbols = new List<ParsedSymbol> { Parsed.Class("A", 1) };
        symbols.AddRange(Enumerable.Range(0, methods).Select(i => Parsed.Method("A.M" + Number(i), 2 + (i * 3)) with { Body = "{ Work(" + Number(i) + "); }" }));
        _ = harness.Parser.With("src/A.cs", new ParseOutcome(symbols));
        _ = harness.Snapshot.WithFile(BuildHarness.First, "src/A.cs", "class A").WithFile(BuildHarness.Second, "src/A.cs", "class A");
        return harness;
    }

    private static string Number(int i) => i.ToString("D4", CultureInfo.InvariantCulture);

    private static async Task<LayerRecord> Layer(BuildHarness harness)
        => (await harness.Header())!.Layers.Single(l => l.Layer == LayerName.Embeddings);

    [Fact]
    public async Task A_build_embeds_each_method_as_its_name_signature_and_body_and_records_the_layer_ok()
    {
        var embedder = new FakeEmbedder();
        var harness = Arranged(2, embedder);

        var report = await harness.BuildAndEmbed(BuildHarness.First, cancellationToken: Token);

        Assert.Equal(["A\nclass A\n", "M0000\nvoid M0000()\n{ Work(0000); }", "M0001\nvoid M0001()\n{ Work(0001); }"], Assert.Single(embedder.Batches), StringComparer.Ordinal);
        Assert.Equal((3, 3), (report.Embedded, report.Embeddable));   // the two methods and the class, embedded by name and signature
        var layer = await Layer(harness);
        Assert.Equal(LayerState.Ok, layer.State);
        Assert.Null(layer.Code);
        Assert.Equal("3 of 3 embedded", layer.Detail);
        Assert.Equal(FakeEmbedder.DefaultModel, layer.Stamp);
        Assert.Equal(LayerState.Ok, report.Embeddings);
    }

    [Fact]
    public async Task A_build_only_records_the_layer_pending_from_the_counts_and_never_calls_the_embedder()
    {
        var embedder = new FakeEmbedder();
        var harness = Arranged(2, embedder);

        var report = await harness.Build(BuildHarness.First, cancellationToken: Token);

        Assert.Equal((0, 3), (report.Embedded, report.Embeddable));
        var layer = await Layer(harness);
        Assert.Equal(LayerState.Pending, layer.State);
        Assert.Equal("0 of 3 embedded", layer.Detail);
        Assert.Equal(0, embedder.Probes);
        Assert.Empty(embedder.Batches);
    }

    [Fact]
    public async Task Texts_go_to_the_embedder_in_batches_of_32()
    {
        var embedder = new FakeEmbedder();

        _ = await Arranged(70, embedder).BuildAndEmbed(BuildHarness.First, cancellationToken: Token);

        Assert.Equal([32, 32, 7], embedder.Batches.Select(b => b.Count));
    }

    [Fact]
    public async Task A_repo_over_one_chunk_is_embedded_in_chunks_of_4096_until_the_layer_is_ok()
    {
        var harness = Arranged(4_100);

        var done = await harness.BuildAndEmbed(BuildHarness.First, cancellationToken: Token);
        var layer = await Layer(harness);

        Assert.Equal((4_101, 4_101, LayerState.Ok), (done.Embedded, done.Embeddable, done.Embeddings));
        Assert.Equal("4101 of 4101 embedded", layer.Detail);
        Assert.Equal([128, 1], [harness.Embedder.Batches.Take(128).Count(), harness.Embedder.Batches.Skip(128).Count()]);
        Assert.Equal([5], harness.Embedder.Batches.Skip(128).Select(b => b.Count));
    }

    [Fact]
    public async Task A_server_that_is_down_makes_the_layer_unavailable_with_its_code_and_the_build_still_publishes()
    {
        var embedder = new FakeEmbedder { FailProbe = CodeIndexErrors.EmbeddingServerFailed.With("down") };
        var harness = Arranged(3, embedder);

        var report = await harness.Build(BuildHarness.First, cancellationToken: Token);
        var embed = await harness.Embed.Handle(new EmbedCodeIndexRequest(harness.Repo, new Progress<string>()), Token);

        Assert.Equal(BuildOutcome.Published, report.Outcome);
        var layer = await Layer(harness);
        Assert.Equal(LayerState.Unavailable, layer.State);
        Assert.Equal(CodeIndexErrors.EmbeddingServerFailed.Code, layer.Code);
        Assert.Equal((0, 4, LayerState.Unavailable), (embed.Value.Embedded, embed.Value.Embeddable, embed.Value.Embeddings));
        Assert.Empty(embedder.Batches);
        Assert.Equal(LayerState.Ok, (await harness.Header())!.Layers.Single(l => l.Layer == LayerName.Structure).State);
    }

    [Fact]
    public async Task A_model_that_is_not_the_pinned_one_makes_the_layer_unavailable_with_the_pin_code()
    {
        var embedder = new FakeEmbedder { FailProbe = CodeIndexErrors.PinMismatch.With("digest differs") };

        var harness = Arranged(1, embedder);

        _ = await harness.BuildAndEmbed(BuildHarness.First, cancellationToken: Token);

        Assert.Equal(CodeIndexErrors.PinMismatch.Code, (await Layer(harness)).Code);
    }

    [Fact]
    public async Task A_refused_url_makes_the_layer_unavailable_with_the_target_code()
    {
        var settings = CodeIndexSettings.Default with { Embeddings = new EmbeddingsSettings(Enabled: true, "http://localhost:11434", Model: null) };
        var harness = Arranged(1, settings: settings);

        _ = await harness.BuildAndEmbed(BuildHarness.First, cancellationToken: Token);
        var layer = await Layer(harness);

        Assert.Equal(LayerState.Unavailable, layer.State);
        Assert.Equal(CodeIndexErrors.EmbeddingTargetRefused.Code, layer.Code);
    }

    [Fact]
    public async Task A_server_that_fails_part_way_keeps_the_vectors_made_and_marks_the_layer_unavailable()
    {
        var embedder = new FakeEmbedder { FailEmbedAfter = (1, CodeIndexErrors.EmbeddingServerFailed.With("went away")) };
        var harness = Arranged(70, embedder);

        var report = await harness.BuildAndEmbed(BuildHarness.First, cancellationToken: Token);

        Assert.Equal((32, 71, LayerState.Unavailable), (report.Embedded, report.Embeddable, report.Embeddings));
        var layer = await Layer(harness);
        Assert.Equal(LayerState.Unavailable, layer.State);
        Assert.Equal(CodeIndexErrors.EmbeddingServerFailed.Code, layer.Code);
        Assert.Equal("32 of 71 embedded", layer.Detail);
    }

    [Fact]
    public async Task A_layer_the_profile_turned_off_is_disabled_and_nothing_is_sent_or_kept()
    {
        var embedder = new FakeEmbedder();
        var settings = CodeIndexSettings.Default with { Embeddings = new EmbeddingsSettings(Enabled: false, CodeIndexSettings.DefaultEmbeddingsUrl, Model: null) };
        var harness = Arranged(2, embedder, settings);

        var report = await harness.Build(BuildHarness.First, cancellationToken: Token);

        Assert.Equal(LayerState.Disabled, (await Layer(harness)).State);
        Assert.Equal(0, embedder.Probes);
        Assert.Empty(embedder.Batches);
        Assert.Equal(0, report.Embedded);
        await using var build = (await harness.Store.BeginBuild(BuildHarness.Name, incremental: true, Token)).Value;
        Assert.Empty((await build.EmbedCandidates(10, Token)).Value);
    }

    [Fact]
    public async Task The_settings_model_is_the_layers_stamp_and_the_vectors_model()
    {
        var settings = CodeIndexSettings.Default with { Embeddings = new EmbeddingsSettings(Enabled: true, CodeIndexSettings.DefaultEmbeddingsUrl, "my-model:3") };
        var harness = Arranged(1, settings: settings);

        _ = await harness.Build(BuildHarness.First, cancellationToken: Token);

        Assert.Equal("my-model:3", (await Layer(harness)).Stamp);
    }

    [Fact]
    public async Task Progress_lines_carry_counts_only()
    {
        var lines = new Lines();

        _ = await Arranged(40).BuildAndEmbed(BuildHarness.First, lines, Token);

        Assert.Contains("embeddings 32 of 41", lines.All, StringComparer.Ordinal);
        Assert.Contains("embeddings 41 of 41", lines.All, StringComparer.Ordinal);
        Assert.DoesNotContain(lines.All, l => l.Contains("Work(", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_cancel_between_batches_leaves_the_live_index_as_the_build_published_it()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var embedder = new FakeEmbedder();
        var harness = Arranged(70, embedder);
        var lines = new CancelAfterEmbedding(cancel);

        _ = await harness.Build(BuildHarness.First, cancellationToken: Token);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await harness.Embed.Handle(new EmbedCodeIndexRequest(harness.Repo, lines), cancel.Token));

        Assert.Equal("0 of 71 embedded", (await Layer(harness)).Detail);
    }

    [Fact]
    public async Task A_secret_in_a_body_is_scrubbed_and_cut_before_it_is_stored_and_embedded()
    {
        var embedder = new FakeEmbedder();
        var harness = new BuildHarness { Embedder = embedder, Scrubber = new FakeBodyScrubber("hunter2") };
        var leaky = Parsed.Method("A.Run", 2) with { Body = "{ Login(\"hunter2\"); " + new string('x', 3_000) + " }" };
        _ = harness.Parser.With("src/A.cs", new ParseOutcome([leaky]));
        _ = harness.Snapshot.WithFile(BuildHarness.First, "src/A.cs", "class A");

        _ = await harness.BuildAndEmbed(BuildHarness.First, cancellationToken: Token);

        var text = Assert.Single(Assert.Single(embedder.Batches));
        Assert.DoesNotContain("hunter2", text, StringComparison.Ordinal);
        Assert.Contains("[redacted]", text, StringComparison.Ordinal);
        Assert.True(text.Length <= CodeIndexRules.EmbedTextMaxCharacters);
    }

    private sealed class CancelAfterEmbedding(CancellationTokenSource cancel) : IProgress<string>
    {
        public void Report(string value)
        {
            if (value.StartsWith("embeddings", StringComparison.Ordinal))
            {
                cancel.Cancel();
            }
        }
    }
}
