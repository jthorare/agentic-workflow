using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Testing.Fixtures;

namespace Aw.Context.Tests.Infrastructure;

// FetchMany's per-repo failures (#3243 review): kept out of RepoDocsTests.cs for the 300-line limit.
public sealed partial class RepoDocsTests
{
    [Fact, Trait("Lane", "integration")]
    public async Task FetchMany_keeps_a_healthy_repos_docs_when_another_repo_cant_be_read()
    {
        using var home = new TempHome();
        using var fixture = FixtureRepo.Build();
        var healthy = new ContextRepo(ContextRepoName.From("split-panes"), fixture.Path);
        var broken = new ContextRepo(ContextRepoName.From("gone"), Path.Combine(Path.GetTempPath(), "aw-ctx-no-such-repo"));
        var blob = Git(fixture.Path, "rev-parse", "HEAD:docs/architecture.md");
        var source = new RepoDocsSource([healthy, broken], home.Database, home.Git, home.Scrubbers, home.Clock);
        static SourceRef Ref(string id, string version, string key) => new(SourceId.From("repo-docs"), ExternalId.From(id), SourceVersion.From(version), Permalink: null, RecordKey.From(key));
        var good = Ref("split-panes/docs/architecture.md#L1-L2", blob, "repo:split-panes/docs/architecture.md");
        var lost = Ref("gone/docs/a.md#L1-L2", blob, "repo:gone/docs/a.md");

        var many = await source.FetchMany([lost, good], TestContext.Current.CancellationToken);

        Assert.True(many.IsSuccess);   // why: one checkout gone made every repo's docs vanish
        Assert.True(many.Value[0].IsError);
        Assert.True(many.Value[1].IsSuccess);
    }
}
