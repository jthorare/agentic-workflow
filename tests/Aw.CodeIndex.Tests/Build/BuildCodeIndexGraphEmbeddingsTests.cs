using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Features.BuildCodeIndex;
using Aw.CodeIndex.Features.EmbedCodeIndex;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Build;

// Regression: symbols that only the graph layer produces (TypeScript, kind graph-symbol) were never embedded, because they carried no body and the
// embed candidates are the symbols with a stored body. A build embeds them with the source text read at their file and line span.
public sealed class BuildCodeIndexGraphEmbeddingsTests
{
    private const string FileA = "export function placeOrder() {\n  return computeTotal();\n}\n";
    private const string FileB = "export function computeTotal() {\n  return 42;\n}\n";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly GraphSnapshot _graph = new(
        [new GraphNode("src_a_place", "src/a.ts", "placeOrder", 1, 0), new GraphNode("src_b_total", "src/b.ts", "computeTotal", 1, 0)],
        [new GraphEdge("src_a_place", "src_b_total", "calls", "EXTRACTED", 2)]);

    [Fact, Trait("Lane", "integration")]
    public async Task A_build_embeds_graph_symbols_with_their_source_text()
    {
        using var git = A.GitRepo().WithFile("src/a.ts", FileA).WithFile("src/b.ts", FileB).Build();
        var embedder = new FakeEmbedder();
        using var index = new RealIndex(embedder: embedder, graph: new FakeGraphRunner { Graph = _graph });
        var repo = git.AsRepo("web");

        _ = (await index.Service.Handle(new BuildCodeIndexRequest(repo, git.Head, new Progress<string>()), Token)).Value;
        var embedded = (await index.Embed.Handle(new EmbedCodeIndexRequest(repo, new Progress<string>()), Token)).Value;

        Assert.Equal((2, 2, LayerState.Ok), (embedded.Embedded, embedded.Embeddable, embedded.Embeddings));
        var texts = embedder.Batches.SelectMany(b => b).ToList();
        Assert.Contains(texts, t => t.Contains("export function placeOrder", StringComparison.Ordinal) && t.Contains("return computeTotal();", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("return 42;", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_graph_class_is_stored_class_like_and_embedded_by_its_name_alone()
    {
        const string classFile = "export class Cart {\n  private marker = 'class-body-line';\n}\n";
        using var git = A.GitRepo().WithFile("src/a.ts", classFile).WithFile("src/b.ts", FileB).Build();
        var embedder = new FakeEmbedder();
        var graph = new GraphSnapshot([new GraphNode("src_a_cart", "src/a.ts", "Cart", 1, 0, IsClass: true), new GraphNode("src_b_total", "src/b.ts", "computeTotal", 1, 0)], []);
        using var index = new RealIndex(embedder: embedder, graph: new FakeGraphRunner { Graph = graph });
        var repo = git.AsRepo("web");

        _ = (await index.Service.Handle(new BuildCodeIndexRequest(repo, git.Head, new Progress<string>()), Token)).Value;
        var embedded = (await index.Embed.Handle(new EmbedCodeIndexRequest(repo, new Progress<string>()), Token)).Value;

        Assert.Equal((2, 2, LayerState.Ok), (embedded.Embedded, embedded.Embeddable, embedded.Embeddings));
        var texts = embedder.Batches.SelectMany(b => b).ToList();
        Assert.Contains(texts, t => t.Contains("return 42;", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, t => t.Contains("class-body-line", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.StartsWith("Cart\n", StringComparison.Ordinal));   // the class is embedded by its name, never its body (CIX-L60)
        var found = (await index.Facade.FindSymbols(RealIndex.ScopeOf(repo), new SymbolQuery("Cart", SymbolMatch.Exact, Repo: null), Token)).Value;
        Assert.Equal(SymbolKind.Class, found.Hits.Single().Kind);
    }
}
