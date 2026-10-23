using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Embedding;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Features.BuildCodeIndex;
using Aw.CodeIndex.Features.EmbedCodeIndex;
using Aw.CodeIndex.Testing;
using Aw.Kernel;

namespace Aw.CodeIndex.Tests.Build;

// A graph symbol's text is read from the snapshot after the graph is merged. A blob that cannot be read or opened costs that file its text, never the build.
public sealed class BuildCodeIndexGraphBodiesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly GraphSnapshot _graph = new(
        [new GraphNode("src_a_f", "src/a.ts", "f", 1, 0), new GraphNode("src_b_g", "src/b.ts", "g", 1, 0), new GraphNode("src_c_h", "src/c.ts", "h", 1, 0)], []);

    private static async Task<(BuildReport Report, FakeEmbedder Embedder)> Build(Func<IRepoSnapshot, IRepoSnapshot> wrap)
    {
        var harness = new BuildHarness { Graph = new FakeGraphRunner { Graph = _graph } };
        _ = harness.Snapshot.WithFile(BuildHarness.First, "src/a.ts", "function f() { ok(); }").WithFile(BuildHarness.First, "src/b.ts", "function g() { UNREADABLE(); }").WithFile(BuildHarness.First, "src/c.ts", "function h() { ABSENT(); }");
        var service = new BuildCodeIndexService(harness.Heads, harness.Snapshot, new StructureWriter(harness.Snapshot, [harness.Parser], harness.Quarantine, harness.Scrubber), new BuildLayers(new EmbeddingsLayer(harness.Embedder, harness.Clock), new GraphLayer(wrap(harness.Snapshot), harness.Clock, harness.Scrubber, harness.Graph)), harness.Store, harness.Heavy, harness.Clock);
        var result = await service.Handle(new BuildCodeIndexRequest(harness.Repo, BuildHarness.First, new Progress<string>()), Token);
        _ = await harness.Embed.Handle(new EmbedCodeIndexRequest(harness.Repo, new Progress<string>()), Token);
        return (result.Value, harness.Embedder);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_whose_blob_cannot_be_read_or_is_missing_keeps_its_symbols_without_text_and_is_counted()
    {
        var (report, embedder) = await Build(inner => new FlakySnapshot(inner, failOpen: false));

        var layer = report.Layers.Single(l => l.Layer == LayerName.Graph);
        Assert.Equal((LayerState.Ok, "3 symbols, 0 calls, 0 dropped, 2 files without text"), (layer.State, layer.Detail));
        var texts = embedder.Batches.SelectMany(b => b).ToList();
        Assert.Contains(texts, t => t.Contains("ok();", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, t => t.Contains("UNREADABLE", StringComparison.Ordinal) || t.Contains("ABSENT", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Blobs_that_cannot_be_opened_leave_every_graph_file_without_text_and_the_layer_ok()
    {
        var (report, _) = await Build(inner => new FlakySnapshot(inner, failOpen: true));

        var layer = report.Layers.Single(l => l.Layer == LayerName.Graph);
        Assert.Equal((BuildOutcome.Published, LayerState.Ok, "3 symbols, 0 calls, 0 dropped, 3 files without text"), (report.Outcome, layer.State, layer.Detail));
    }

    // Reads of a blob holding UNREADABLE fail and of one holding ABSENT answer null; failOpen makes OpenBlobs fail after the graph tool has run.
    private sealed class FlakySnapshot(IRepoSnapshot inner, bool failOpen) : IRepoSnapshot
    {
        private int _opens;

        public ValueTask<Result<SnapshotInventory>> Inventory(CodeIndexRepo repo, CommitSha commit, SnapshotLimits limits, CancellationToken cancellationToken)
            => inner.Inventory(repo, commit, limits, cancellationToken);

        public async ValueTask<Result<IBlobReader>> OpenBlobs(CodeIndexRepo repo, CancellationToken cancellationToken)
        {
            _opens++;
            if (failOpen && _opens > 1)
            {
                return CodeIndexErrors.IndexDamaged.With("cannot open");
            }

            var opened = await inner.OpenBlobs(repo, cancellationToken);
            return opened.IsError ? opened.Error : new Reader(opened.Value);
        }
    }

    private sealed class Reader(IBlobReader inner) : IBlobReader
    {
        public async ValueTask<Result<byte[]?>> Read(string blob, CancellationToken cancellationToken)
        {
            var read = await inner.Read(blob, cancellationToken);
            var text = read is { IsError: false, Value: { } bytes } ? System.Text.Encoding.UTF8.GetString(bytes) : string.Empty;
            return text.Contains("UNREADABLE", StringComparison.Ordinal) ? CodeIndexErrors.IndexDamaged.With("read failed")
                : text.Contains("ABSENT", StringComparison.Ordinal) ? (byte[]?)null
                : read;
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
