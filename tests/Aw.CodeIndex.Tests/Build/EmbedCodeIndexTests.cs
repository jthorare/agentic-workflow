using System.Text;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Features.BuildCodeIndex;
using Aw.CodeIndex.Features.EmbedCodeIndex;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Build;

// Embedding is its own slice: it does not take the heavy slot, it carries on until the layer is ok without a HEAD move, and a move embeds only what changed.
public sealed class EmbedCodeIndexTests
{
    private const int Loops = 20;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Class(string name, string body) => $"namespace Shop;\n\npublic class {name}\n{{\n    public int {name}One()\n    {{\n        return {body}1;\n    }}\n\n    public int {name}Two()\n    {{\n        return {body}2;\n    }}\n}}\n";

    private static async Task<BuildReport> Build(RealIndex index, CodeIndexRepo repo, Core.Values.CommitSha commit)
        => (await index.Service.Handle(new BuildCodeIndexRequest(repo, commit, new Progress<string>()), Token)).Value;

    // The slice may do one chunk per call: ask again until it says ok, within a bound.
    private static async Task<EmbedCodeIndexResponse> EmbedToOk(RealIndex index, CodeIndexRepo repo)
    {
        var service = index.Embed;
        var response = (await service.Handle(new EmbedCodeIndexRequest(repo, new Progress<string>()), Token)).Value;
        for (var i = 0; i < Loops && response.Embeddings != LayerState.Ok; i++)
        {
            response = (await service.Handle(new EmbedCodeIndexRequest(repo, new Progress<string>()), Token)).Value;
        }

        return response;
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_class_is_embedded_by_its_name_and_signature_so_similar_code_finds_types()
    {
        using var git = A.GitRepo().WithFile("src/Alpha.cs", Class("Alpha", "")).Build();
        var embedder = new FakeEmbedder();
        using var index = new RealIndex(embedder: embedder);
        var repo = git.AsRepo("shop");
        _ = await Build(index, repo, git.Head);

        var response = await EmbedToOk(index, repo);

        Assert.Equal(LayerState.Ok, response.Embeddings);
        Assert.Equal(3, response.Embeddable);
        Assert.Contains(embedder.Batches.SelectMany(b => b), t => t.StartsWith("Alpha\n", StringComparison.Ordinal) && !t.Contains("return", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_repo_larger_than_one_batch_is_fully_embedded_without_a_head_move_or_the_heavy_slot()
    {
        var source = new StringBuilder("namespace Shop;\n\npublic class Big\n{\n");
        for (var i = 0; i < CodeIndexRules.MaxEmbeddedPerBuild + 100; i++)
        {
            _ = source.Append(System.Globalization.CultureInfo.InvariantCulture, $"    public int M{i}() {{ return {i}; }}\n");
        }

        _ = source.Append('}');
        using var git = A.GitRepo().WithFile("src/Big.cs", source.ToString()).Build();
        var embedder = new FakeEmbedder();
        using var index = new RealIndex(embedder: embedder);
        var repo = git.AsRepo("shop");
        var commit = git.Head;
        _ = await Build(index, repo, commit);
        await using var otherJob = (await index.Heavy.Take("someone else", TimeSpan.Zero, waiting: null, Token)).Value;

        var response = await EmbedToOk(index, repo);

        Assert.Equal(LayerState.Ok, response.Embeddings);
        Assert.True(response.Embeddable > CodeIndexRules.MaxEmbeddedPerBuild);
        Assert.Equal(response.Embeddable, response.Embedded);
        Assert.Equal(response.Embeddable, embedder.Batches.Sum(b => b.Count));
        Assert.Equal(commit, git.Head);
        Assert.Equal(1, index.Heavy.Held);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_move_embeds_only_changed_files_and_prunes_removed_ones()
    {
        using var git = A.GitRepo().WithFile("src/A.cs", Class("A", "")).WithFile("src/B.cs", Class("B", "")).WithFile("src/C.cs", Class("C", "")).Build();
        var embedder = new FakeEmbedder();
        using var index = new RealIndex(embedder: embedder);
        var repo = git.AsRepo("shop");
        _ = await Build(index, repo, git.Head);
        var first = await EmbedToOk(index, repo);
        var seen = embedder.Batches.Sum(b => b.Count);
        git.WriteFile("src/B.cs", Class("B", "10"));
        _ = git.Git("rm", "-q", "src/C.cs");
        var second = git.Commit("change b, remove c");

        _ = await Build(index, repo, second);
        var moved = await EmbedToOk(index, repo);
        var resent = embedder.Batches.SelectMany(b => b).Skip(seen).ToList();
        var scope = RealIndex.ScopeOf(repo);
        var removed = (await index.Facade.FindSymbols(scope, new SymbolQuery("COne", SymbolMatch.Exact, Repo: null), Token)).Value.Hits;

        Assert.Equal(LayerState.Ok, moved.Embeddings);
        Assert.Equal(first.Embeddable / 3 * 2, moved.Embeddable);
        Assert.Equal(moved.Embeddable, moved.Embedded);
        Assert.NotEmpty(resent);
        Assert.All(resent, t => Assert.StartsWith("B", t, StringComparison.Ordinal));
        Assert.Equal(first.Embeddable / 3, resent.Count);
        Assert.Empty(removed);
    }
}
