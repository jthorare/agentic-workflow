using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Features.BuildCodeIndex;
using Aw.CodeIndex.Features.EmbedCodeIndex;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Build;

// The slice over git, Roslyn and the SQLite index files: what the fakes promise holds for the real adapters (spec §11).
public sealed class BuildCodeIndexRealStoreTests
{
    private const string Orders = "namespace Shop;\n\npublic class Orders\n{\n    public void Place()\n    {\n        Validate();\n    }\n\n    private void Validate()\n    {\n    }\n}\n";
    private const string Api = "namespace Shop;\n\npublic class Api\n{\n    public void Post(Orders orders)\n    {\n        orders.Place();\n    }\n}\n";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<BuildReport> Build(RealIndex index, CodeIndexRepo repo, Core.Values.CommitSha commit)
        => (await index.Service.Handle(new BuildCodeIndexRequest(repo, commit, new Progress<string>()), Token)).Value;

    [Fact, Trait("Lane", "integration")]
    public async Task A_git_repo_builds_to_files_symbols_and_a_caller_edge_that_a_reader_can_follow()
    {
        using var git = A.GitRepo().WithFile("src/Orders.cs", Orders).WithFile("src/Api.cs", Api).WithFile("README.md", "# shop").Build();
        using var index = new RealIndex();
        var repo = git.AsRepo("shop");

        var report = await Build(index, repo, git.Head);
        var place = (await index.Facade.FindSymbols(RealIndex.ScopeOf(repo), new SymbolQuery("Place", SymbolMatch.Exact, Repo: null), Token)).Value.Hits.Single();
        var callers = (await index.Facade.Callers(RealIndex.ScopeOf(repo), place.Ref, CallDepth.One, includeImplementations: false, Token)).Value;

        Assert.Equal(BuildOutcome.Published, report.Outcome);
        Assert.Equal(2, report.Counts.Files);
        Assert.Equal(5, report.Counts.Symbols);
        Assert.Equal("Post", callers.Edges.Single().Other!.Name);
        Assert.Equal(Coverage.Full, callers.Coverage);
        Assert.Equal(0, index.Heavy.Held);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_second_commit_builds_incrementally_over_the_real_index_and_drops_a_deleted_file()
    {
        using var git = A.GitRepo().WithFile("src/Orders.cs", Orders).WithFile("src/Api.cs", Api).Build();
        using var index = new RealIndex();
        var repo = git.AsRepo("shop");
        _ = await Build(index, repo, git.Head);
        git.WriteFile("src/Api.cs", Api.Replace("orders.Place();", "orders.Place();\n        orders.Place();", StringComparison.Ordinal));
        var second = git.Commit("edit api");

        var report = await Build(index, repo, second);

        Assert.True(report.Incremental);
        Assert.Equal(second, report.Commit);
        Assert.Equal(5, report.Counts.Symbols);
        Assert.Equal(second, (await index.Facade.Heads(RealIndex.ScopeOf(repo), Token)).Value.Single().Commit);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_commit_the_repo_does_not_have_is_002_and_leaves_no_index()
    {
        using var git = A.GitRepo().WithFile("src/Orders.cs", Orders).Build();
        using var index = new RealIndex();
        var repo = git.AsRepo("shop");

        var result = await index.Service.Handle(new BuildCodeIndexRequest(repo, Core.Values.CommitSha.From(new string('a', 40)), new Progress<string>()), Token);

        Assert.Equal("SND-CODEIDX-002", result.Error.Code);
        Assert.Equal(IndexState.Missing, (await index.Facade.Status(RealIndex.ScopeOf(repo), Token)).Value.Repos.Single().State);
        Assert.Equal(0, index.Heavy.Held);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_embeddings_layer_and_Neighbors_work_over_the_real_store_and_the_stored_bodies_go_once_embedded()
    {
        using var git = A.GitRepo().WithFile("src/Orders.cs", Orders).WithFile("src/Api.cs", Api).Build();
        var embedder = new FakeEmbedder();
        using var index = new RealIndex(embedder: embedder);
        var repo = git.AsRepo("shop");

        _ = await Build(index, repo, git.Head);
        var report = (await index.Embed.Handle(new EmbedCodeIndexRequest(repo, new Progress<string>()), Token)).Value;
        var texts = embedder.Batches.Single();
        var answer = (await index.Facade.Neighbors(RealIndex.ScopeOf(repo), repo.Name, texts[0], 3, Token)).Value;
        await using var next = (await index.Store.BeginBuild(repo.Name, incremental: true, Token)).Value;

        Assert.Equal((5, 5), (report.Embedded, report.Embeddable));   // the three methods and the two classes (CIX-L60: types are embedded by name and signature)
        Assert.Equal(LayerState.Ok, report.Embeddings);
        Assert.Equal(3, answer.Neighbors.Count);
        Assert.Equal(1.0, answer.Neighbors[0].Score, 4);
        Assert.All(texts, t => Assert.Contains("\n", t, StringComparison.Ordinal));
        Assert.Empty((await next.EmbedCandidates(10, Token)).Value);
    }
}
