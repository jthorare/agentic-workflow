using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Features.BuildCodeIndex;
using Aw.CodeIndex.Features.EmbedCodeIndex;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Build;

// Regression (#990): every main move rewrote every graph file, and a rewritten file loses its symbols' vectors, so every build re-embedded the whole repo.
// A graph file whose blob and planned symbols match the stored ones keeps its rows, and with them its vectors.
public sealed class BuildCodeIndexGraphIncrementalTests
{
    private const string FileA = "export function placeOrder() {\n  return computeTotal();\n}\n";
    private const string FileB = "export function computeTotal() {\n  return 42;\n}\n";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly GraphSnapshot _graph = new(
        [new GraphNode("src_a_place", "src/a.ts", "placeOrder", 1, 0), new GraphNode("src_b_total", "src/b.ts", "computeTotal", 1, 0)],
        [new GraphEdge("src_a_place", "src_b_total", "calls", "EXTRACTED", 2)]);

    [Fact, Trait("Lane", "integration")]
    public async Task A_build_after_one_graph_file_changed_embeds_only_that_file_again()
    {
        using var git = A.GitRepo().WithFile("src/a.ts", FileA).WithFile("src/b.ts", FileB).Build();
        var embedder = new FakeEmbedder();
        using var index = new RealIndex(embedder: embedder, graph: new FakeGraphRunner { Graph = _graph });
        var repo = git.AsRepo("web");
        _ = (await index.Service.Handle(new BuildCodeIndexRequest(repo, git.Head, new Progress<string>()), Token)).Value;
        _ = (await index.Embed.Handle(new EmbedCodeIndexRequest(repo, new Progress<string>()), Token)).Value;
        var before = embedder.Batches.Count;
        git.WriteFile("src/b.ts", FileB.Replace("42", "43", StringComparison.Ordinal));
        var second = git.Commit("edit b");

        _ = (await index.Service.Handle(new BuildCodeIndexRequest(repo, second, new Progress<string>()), Token)).Value;
        var embedded = (await index.Embed.Handle(new EmbedCodeIndexRequest(repo, new Progress<string>()), Token)).Value;

        var texts = embedder.Batches.Skip(before).SelectMany(b => b).ToList();
        var again = Assert.Single(texts);
        Assert.Contains("return 43;", again, StringComparison.Ordinal);
        Assert.Equal((2, 2), (embedded.Embedded, embedded.Embeddable));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_graph_file_with_the_same_blob_but_new_planned_symbols_is_written_again()
    {
        const string withExtra = FileA + "export function extraStep() {\n  return 7;\n}\n";
        using var git = A.GitRepo().WithFile("src/a.ts", withExtra).WithFile("src/b.ts", FileB).Build();
        var embedder = new FakeEmbedder();
        var runner = new FakeGraphRunner { Graph = _graph };
        using var index = new RealIndex(embedder: embedder, graph: runner);
        var repo = git.AsRepo("web");
        _ = (await index.Service.Handle(new BuildCodeIndexRequest(repo, git.Head, new Progress<string>()), Token)).Value;
        _ = (await index.Embed.Handle(new EmbedCodeIndexRequest(repo, new Progress<string>()), Token)).Value;
        var before = embedder.Batches.Count;
        runner.Graph = new GraphSnapshot([.. _graph.Nodes, new GraphNode("src_a_extra", "src/a.ts", "extraStep", 5, 0)], _graph.Edges);
        var second = git.Commit("no file change");

        _ = (await index.Service.Handle(new BuildCodeIndexRequest(repo, second, new Progress<string>()), Token)).Value;
        var embedded = (await index.Embed.Handle(new EmbedCodeIndexRequest(repo, new Progress<string>()), Token)).Value;

        var texts = embedder.Batches.Skip(before).SelectMany(b => b).ToList();
        Assert.Equal(2, texts.Count);   // a.ts is rewritten with both of its symbols; b.ts keeps its vector
        Assert.DoesNotContain(texts, t => t.Contains("return 42;", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("return 7;", StringComparison.Ordinal));
        Assert.Equal((3, 3), (embedded.Embedded, embedded.Embeddable));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Turning_embeddings_on_gives_unchanged_graph_files_their_bodies_and_then_their_vectors()
    {
        using var git = A.GitRepo().WithFile("src/a.ts", FileA).WithFile("src/b.ts", FileB).Build();
        var embedder = new FakeEmbedder();
        using var index = new RealIndex(embedder: embedder, graph: new FakeGraphRunner { Graph = _graph });
        var off = git.AsRepo("web");
        off = off with { Settings = off.Settings with { Embeddings = off.Settings.Embeddings with { Enabled = false } } };
        _ = (await index.Service.Handle(new BuildCodeIndexRequest(off, git.Head, new Progress<string>()), Token)).Value;
        var on = git.AsRepo("web");
        var second = git.Commit("same files");

        _ = (await index.Service.Handle(new BuildCodeIndexRequest(on, second, new Progress<string>()), Token)).Value;
        var embedded = (await index.Embed.Handle(new EmbedCodeIndexRequest(on, new Progress<string>()), Token)).Value;

        Assert.Equal((2, 2), (embedded.Embedded, embedded.Embeddable));
        Assert.Contains(embedder.Batches.SelectMany(b => b), t => t.Contains("return 42;", StringComparison.Ordinal));
    }
}
