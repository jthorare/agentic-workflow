using System.Globalization;
using System.Runtime.Versioning;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Records;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Sync;

/// <summary>The library feed across passes (PR review page spec 2026-11-04 §5.2): a root that comes back, a change to the synced repos, files that are touched but not edited, and a page limit smaller than the work.</summary>
[Trait("Lane", "integration")]
public sealed class LibraryFeedRecoveryTests
{
    private const UnixFileMode Open = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private static string Plan(int n) => string.Create(CultureInfo.InvariantCulture, $"plans/doc-{n}.md");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task A_root_that_recovers_counts_its_docs_and_near_misses_again()
    {
        Assert.SkipWhen(Environment.IsPrivilegedProcess, "root ignores file permissions");
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home);
        var feed = rig.Feed(rig.Vault());
        _ = await FeedRun.Once(feed, rig.Store, home.Scrubbers);

        File.SetUnixFileMode(rig.VaultPath, UnixFileMode.None);
        try
        {
            Assert.True((await feed.Pull(new((await rig.Store.Position(feed.Source, Ct)).Value.Cursor, 100, NotBefore: null), Ct)).IsError);
        }
        finally
        {
            File.SetUnixFileMode(rig.VaultPath, Open);
        }

        _ = await rig.PullNow(feed);

        var report = Assert.Single(rig.Status.Read());
        Assert.Null(report.Problem);
        Assert.Equal(3, report.Docs);
        Assert.Equal(4, report.NearMisses.Count);
    }

    [Fact]
    public async Task A_missing_status_file_does_not_make_an_unchanged_root_report_nothing()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home);
        var feed = rig.Feed(rig.Vault());
        _ = await FeedRun.Once(feed, rig.Store, home.Scrubbers);
        rig.Status.Write([]);

        _ = await rig.PullNow(feed);

        Assert.Equal(3, Assert.Single(rig.Status.Read()).Docs);
    }

    [Fact]
    public async Task Changing_the_synced_repos_moves_a_doc_between_the_owner_and_its_repo()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home);
        const string Notes = "doc:harbor/plans/harbor-web-notes.md";
        async Task<AudienceKey?> AudienceNow() => (await rig.Stored()).Single(static r => r.Key.Value == Notes).Audience;

        _ = await FeedRun.Once(rig.FeedSyncing([], rig.Vault()), rig.Store, home.Scrubbers);
        var before = await AudienceNow();
        _ = await FeedRun.Once(rig.FeedSyncing(["acme/harbor-web"], rig.Vault()), rig.Store, home.Scrubbers);
        var added = await AudienceNow();
        _ = await FeedRun.Once(rig.FeedSyncing([], rig.Vault()), rig.Store, home.Scrubbers);
        var removed = await AudienceNow();

        Assert.Equal(ProfileAudiencePolicy.LibraryDoc(["acme/harbor-web"], []), before);
        Assert.Equal(ProfileAudiencePolicy.LibraryDoc(["acme/harbor-web"], ["acme/harbor-web"]), added);
        Assert.Equal(before, removed);
    }

    [Fact]
    public async Task Files_touched_without_an_edit_are_not_emitted_again_and_later_edits_are_still_read()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home, copyFixture: false);
        var paths = Enumerable.Range(1, 6).Select(n => rig.Write(Plan(n), LibraryFeedRig.Doc(n))).ToList();
        var feed = rig.Feed(rig.Vault());
        _ = await FeedRun.Once(feed, rig.Store, home.Scrubbers);
        foreach (var path in paths)
        {
            File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(5));   // rsync, an Obsidian sync, a git checkout
        }

        var touched = await FeedRun.Once(feed, rig.Store, home.Scrubbers, limit: 2);
        foreach (var n in new[] { 4, 5, 6 })
        {
            _ = rig.Write(Plan(n), LibraryFeedRig.Doc(n, "second"));
        }

        var written = 0;
        for (var pass = 0; pass < 10; pass++)
        {
            var run = await FeedRun.Once(feed, rig.Store, home.Scrubbers, limit: 2);
            written += run.Report?.Written ?? 0;
        }

        Assert.Empty(touched.Page.Items);
        Assert.Equal(3, written);
        Assert.Empty((await rig.PullNow(feed, limit: 2)).Items);
    }

    [Fact]
    public async Task A_page_limit_smaller_than_the_new_files_takes_a_few_pulls_and_then_finishes()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home, copyFixture: false);
        for (var n = 1; n <= 5; n++)
        {
            _ = rig.Write(Plan(n), LibraryFeedRig.Doc(n));
        }

        var feed = rig.Feed(rig.Vault());
        var pages = new List<int>();
        for (var pass = 0; pass < 10; pass++)
        {
            var run = await FeedRun.Once(feed, rig.Store, home.Scrubbers, limit: 2);
            pages.Add(run.Page.Items.Count);
        }

        Assert.Equal([2, 2, 1, 0], pages.Take(4));
        Assert.Equal(5, (await rig.Stored()).Count);
        Assert.All(pages.Skip(3), static count => Assert.Equal(0, count));
    }
}
