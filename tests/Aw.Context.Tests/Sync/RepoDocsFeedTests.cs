using Aw.Context.Core.Docs;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Sync;

[Trait("Lane", "integration")]
public sealed class RepoDocsFeedTests : IDisposable
{
    private static readonly string[] _globs = ["docs/**/*.md"];

    private readonly FakeRecordStore _store = new();
    private readonly Scrubbers _scrubbers = new FakeScrubberFactory().For([]).Value;
    private readonly TempGitRepo _git = new(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["docs/a.md"] = "# Alpha\n\nThe alpha doc.\n",
        ["docs/b.md"] = "# Beta\n\nThe beta doc.\n",
        ["notes.txt"] = "not a doc",
    });

    public void Dispose() => _git.Dispose();

    [Fact]
    public async Task A_doc_is_a_record_keyed_by_repo_and_path_versioned_by_its_blob_with_chunks_and_line_spans()
    {
        var run = await FeedRun.Once(Feed(), _store, _scrubbers);

        var key = RepoDocs.Key(_git.Repo.Name, "docs/a.md");
        var record = (await _store.Get(key, TestContext.Current.CancellationToken)).Value!;
        var chunks = (await _store.Chunks(key, TestContext.Current.CancellationToken)).Value;
        Assert.Equal(2, run.Report!.Written);
        Assert.Equal("repo:aw-split-panes/docs/a.md", key.Value);
        Assert.Equal(RecordKind.Doc, record.Kind);
        Assert.Equal(_git.Run("rev-parse", "HEAD:docs/a.md"), record.Version.Value);
        Assert.Equal("Alpha", record.Title);
        Assert.Equal(_git.Repo.Name, record.Repo);
        Assert.False(record.Blocked);
        Assert.Equal(_scrubbers.Ruleset.Value, record.Ruleset);
        var chunk = Assert.Single(chunks);
        Assert.Equal(0, chunk.Seq);
        Assert.Equal((1, 3), (chunk.LineStart, chunk.LineEnd));
        Assert.Contains("The alpha doc.", chunk.Text, StringComparison.Ordinal);
        Assert.Null((await _store.Get(RepoDocs.Key(_git.Repo.Name, "notes.txt"), TestContext.Current.CancellationToken)).Value);
    }

    [Fact]
    public async Task A_replay_at_the_same_head_is_an_empty_page_and_a_replayed_batch_changes_nothing()
    {
        var feed = Feed();
        var first = await FeedRun.Once(feed, _store, _scrubbers);

        var replay = await FeedRun.Once(feed, _store, _scrubbers);
        var again = RecordBatches.From(feed.Source, first.Page, SyncPhase.Incremental, _scrubbers, RecordSamples.Now);
        var report = (await _store.Commit(again, TestContext.Current.CancellationToken)).Value;

        Assert.Empty(replay.Page.Items);
        Assert.Null(replay.Report);
        Assert.Equal(first.Page.Next, replay.Page.Next);
        Assert.Equal((0, 2, 0), (report.Written, report.Unchanged, report.Tombstoned));
    }

    [Fact]
    public async Task An_edited_doc_is_written_again_and_only_it()
    {
        _ = await FeedRun.Once(Feed(), _store, _scrubbers);
        await File.AppendAllTextAsync(Path.Combine(_git.Path, "docs", "a.md"), "\nMore alpha.\n", TestContext.Current.CancellationToken);
        _ = _git.Run("commit", "-q", "-am", "edit");

        var run = await FeedRun.Once(Feed(), _store, _scrubbers);

        Assert.Equal([RepoDocs.Key(_git.Repo.Name, "docs/a.md")], run.Page.Items.Select(i => i.Key));
        Assert.Equal((1, 0), (run.Report!.Written, run.Report.Unchanged));
    }

    [Fact]
    public async Task A_doc_gone_from_the_tree_is_a_tombstone_in_the_page()
    {
        _ = await FeedRun.Once(Feed(), _store, _scrubbers);
        _ = _git.Run("rm", "-q", "docs/b.md");
        _ = _git.Run("commit", "-q", "-m", "drop b");

        var run = await FeedRun.Once(Feed(), _store, _scrubbers);

        var key = RepoDocs.Key(_git.Repo.Name, "docs/b.md");
        Assert.Equal([key], run.Page.Removed);
        Assert.Equal(1, run.Report!.Tombstoned);
        var tombstone = (await _store.Get(key, TestContext.Current.CancellationToken)).Value!;
        _ = Assert.NotNull(tombstone.RemovedAt);
        Assert.Empty((await _store.Chunks(key, TestContext.Current.CancellationToken)).Value);
        Assert.Empty(await FeedRun.UntilQuiet(Feed(), _store, _scrubbers));
    }

    [Fact]
    public async Task A_doc_the_scrubber_blocks_keeps_its_record_without_chunks_and_a_denied_path_is_no_record()
    {
        await File.WriteAllTextAsync(Path.Combine(_git.Path, "docs", "blocked.md"), "# Hidden\n\n" + FakeScrubber.BlockMe + "\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_git.Path, "docs", "denied.md"), "# Denied\n\nbody\n", TestContext.Current.CancellationToken);
        _ = _git.Run("add", "-A");
        _ = _git.Run("commit", "-q", "-m", "more");
        var repo = _git.Repo with { DenyPaths = ["docs/denied.md"] };

        _ = await FeedRun.Once(new RepoDocsFeed([repo], _globs, [], _store, new GitCli(), _scrubbers), _store, _scrubbers);

        var key = RepoDocs.Key(repo.Name, "docs/blocked.md");
        var record = (await _store.Get(key, TestContext.Current.CancellationToken)).Value!;
        Assert.True(record.Blocked);
        Assert.Equal(key.Value, record.Title);
        Assert.Empty((await _store.Chunks(key, TestContext.Current.CancellationToken)).Value);
        Assert.Null((await _store.Get(RepoDocs.Key(repo.Name, "docs/denied.md"), TestContext.Current.CancellationToken)).Value);
    }

    [Fact]
    public async Task A_page_cut_by_the_limit_answers_a_cursor_that_is_not_final_and_the_next_pulls_finish_without_repeating()
    {
        var feed = Feed();

        var runs = await FeedRun.UntilQuiet(feed, _store, _scrubbers, limit: 1);

        Assert.Equal([1, 1], runs.Select(r => r.Page.Items.Count));
        Assert.StartsWith("more:", runs[0].Page.Next.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("more:", runs[1].Page.Next.Value, StringComparison.Ordinal);
        Assert.Equal(2, (await _store.List(new RecordFilter(feed.Source), after: null, 10, TestContext.Current.CancellationToken)).Value.Count);
    }

    [Fact]
    public async Task A_changed_scrub_ruleset_hands_every_doc_over_again()
    {
        _ = await FeedRun.Once(Feed(), _store, _scrubbers);
        var other = new FakeScrubberFactory().For([new("acme", "ACME-[0-9]+")]).Value;

        var run = await FeedRun.Once(new RepoDocsFeed([_git.Repo], _globs, [], _store, new GitCli(), other), _store, other);

        Assert.Equal(2, run.Page.Items.Count);
        Assert.Equal(other.Ruleset.Value, (await _store.Get(RepoDocs.Key(_git.Repo.Name, "docs/a.md"), TestContext.Current.CancellationToken)).Value!.Ruleset);
    }

    private RepoDocsFeed Feed() => new([_git.Repo], _globs, [], _store, new GitCli(), _scrubbers);
}
