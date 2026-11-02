using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Sync;

[Trait("Lane", "integration")]
public sealed class RepoDocsFeedStoreTests
{
    [Fact]
    public async Task The_fixture_repos_docs_land_in_the_sqlite_store_with_chunks_and_a_replay_writes_nothing()
    {
        using var home = new TempHome();
        using var fixture = FixtureRepo.Build();
        var repo = new ContextRepo(ContextRepoName.From("split-panes"), fixture.Path);
        var store = new SqliteRecordStore(home.Database);
        var feed = new RepoDocsFeed([repo], ["docs/**/*.md"], [], store, home.Git, home.Scrubbers);

        var first = await FeedRun.Once(feed, store, home.Scrubbers);
        var replay = await FeedRun.Once(feed, store, home.Scrubbers);

        var records = (await store.List(new RecordFilter(RepoDocs.Id), after: null, 100, TestContext.Current.CancellationToken)).Value;
        Assert.Equal(2, first.Report!.Written);
        Assert.Equal(2, records.Count);
        Assert.All(records, r => Assert.StartsWith("repo:split-panes/docs/", r.Key.Value, StringComparison.Ordinal));
        var architecture = records.Single(r => r.Key.Value.EndsWith("architecture.md", StringComparison.Ordinal));
        Assert.Equal(await BlobOf(fixture, "docs/architecture.md"), architecture.Version.Value);
        var chunks = (await store.Chunks(architecture.Key, TestContext.Current.CancellationToken)).Value;
        Assert.NotEmpty(chunks);
        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(c => c.Seq));
        Assert.Null(replay.Report);
        Assert.Equal(first.Page.Next, (await store.Position(RepoDocs.Id, TestContext.Current.CancellationToken)).Value.Cursor);
    }

    private static async Task<string> BlobOf(FixtureRepo fixture, string path) =>
        (await new GitCli().Tree(fixture.Path, fixture.Head, TestContext.Current.CancellationToken)).Value[path];
}
