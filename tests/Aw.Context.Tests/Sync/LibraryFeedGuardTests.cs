using System.Runtime.Versioning;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Sync;

/// <summary>What the library feed refuses to do to stored docs, and what it reports instead (PR review page spec 2026-11-04 §5.1, §5.2): an emptied root, a dropped root, case tricks in a path, a deny glob that never finishes, a cursor it doesn't know.</summary>
[Trait("Lane", "integration")]
public sealed class LibraryFeedGuardTests
{
    private const string Front = "---\naw-doc: 1\nid: tricky\nkind: note\ntitle: \"Tricky\"\nstatus: draft\n---\n\n# Tricky\n";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_root_that_lists_no_file_while_docs_are_stored_keeps_them_and_says_so_by_name()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home);
        var feed = rig.Feed(rig.Vault());
        _ = await FeedRun.Once(feed, rig.Store, home.Scrubbers);
        foreach (var entry in new DirectoryInfo(rig.VaultPath).EnumerateFileSystemInfos())   // an unmounted drive leaves its empty mount point
        {
            if (entry is DirectoryInfo folder)
            {
                folder.Delete(recursive: true);
            }
            else
            {
                entry.Delete();
            }
        }

        var pulled = await feed.Pull(new RecordPull((await rig.Store.Position(feed.Source, Ct)).Value.Cursor, 100, NotBefore: null), Ct);

        Assert.True(pulled.IsError);
        Assert.Equal(3, (await rig.Stored()).Count);
        Assert.Contains("is empty, 3 docs kept", Assert.Single(rig.Status.Read()).Problem, StringComparison.Ordinal);
        Assert.DoesNotContain(rig.VaultPath, pulled.Error.Message, StringComparison.Ordinal);   // a session can read a job's error: roots are named, not located
        Assert.Contains(LibraryFeedRig.Harbor, pulled.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task A_root_the_owner_cannot_read_fails_its_tick_removes_nothing_and_names_no_path_in_the_error()
    {
        Assert.SkipWhen(Environment.IsPrivilegedProcess, "root ignores file permissions");
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home);
        var feed = rig.Feed(rig.Vault());
        _ = await FeedRun.Once(feed, rig.Store, home.Scrubbers);

        File.SetUnixFileMode(rig.VaultPath, UnixFileMode.None);
        try
        {
            var pulled = await feed.Pull(new RecordPull((await rig.Store.Position(feed.Source, Ct)).Value.Cursor, 100, NotBefore: null), Ct);

            Assert.True(pulled.IsError);
            Assert.DoesNotContain(rig.VaultPath, pulled.Error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.SetUnixFileMode(rig.VaultPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Assert.Equal(3, (await rig.Stored()).Count);
        Assert.False(string.IsNullOrEmpty(Assert.Single(rig.Status.Read()).Problem));
    }

    [Fact]
    public async Task A_root_dropped_from_the_profile_has_its_docs_removed()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home);
        _ = await FeedRun.Once(rig.Feed(rig.Vault()), rig.Store, home.Scrubbers);

        var run = await FeedRun.Once(rig.Feed(), rig.Store, home.Scrubbers);   // no root left: the profile dropped the vault, or set stateDir: off

        Assert.Equal(3, run.Page.Removed.Count);
        Assert.Empty(await rig.Stored());
    }

    [Fact]
    public async Task The_built_in_deny_list_ignores_case()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home, copyFixture: false);
        _ = rig.Write("Secrets/ferry-keys.md", Front);
        _ = rig.Write(".ENV.md", Front);
        _ = rig.Write("id_ferry.md", Front);
        _ = rig.Write("ID_ferry-two.md", Front);
        _ = rig.Write("plans/ok.md", Front);

        _ = await FeedRun.Once(rig.Feed(rig.Vault()), rig.Store, home.Scrubbers);

        Assert.Equal(["doc:harbor/plans/ok.md"], (await rig.Stored()).Select(static r => r.Key.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_deny_glob_that_never_finishes_is_a_root_problem_not_an_exception()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home, copyFixture: false);
        _ = rig.Write(string.Join('/', Enumerable.Repeat("a", 40)) + "/b.md", Front);
        var root = new Aw.Context.Core.Engine.LibraryRoot { Name = LibraryFeedRig.Harbor, Path = rig.VaultPath, Deny = [string.Join('/', Enumerable.Repeat("**", 10)) + "/zzz"] };

        var pulled = await rig.Feed(root).Pull(new RecordPull(After: null, 100, NotBefore: null), Ct);

        Assert.True(pulled.IsError);
        Assert.False(string.IsNullOrEmpty(Assert.Single(rig.Status.Read()).Problem));
    }

    [Fact]
    public async Task A_cursor_that_does_not_start_with_the_feeds_format_is_no_cursor()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home);
        var feed = rig.Feed(rig.Vault());
        var first = await FeedRun.Once(feed, rig.Store, home.Scrubbers);
        var report = Assert.Single(rig.Status.Read());
        rig.Status.Write([report with { Docs = 99 }]);

        _ = await feed.Pull(new RecordPull(first.Page.Next, 100, NotBefore: null), Ct);
        var shortcut = Assert.Single(rig.Status.Read()).Docs;
        _ = await feed.Pull(new RecordPull(SourceCursor.From("v9" + first.Page.Next.Value[4..]), 100, NotBefore: null), Ct);

        Assert.Equal(99, shortcut);   // the known cursor: nothing changed, so the last report stands
        Assert.Equal(3, Assert.Single(rig.Status.Read()).Docs);   // an unknown format: a full read
    }
}
