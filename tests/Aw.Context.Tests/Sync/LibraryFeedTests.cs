using Aw.Context.Core.Audience;
using Aw.Context.Core.Sources;
using Aw.Context.Tests.Infrastructure;
using Aw.Kernel.Testing;

namespace Aw.Context.Tests.Sync;

/// <summary>The library feed over the made-up vault in tests/fixtures/library-vault: what a tick stores, what it leaves, and what a removal or an unreadable root does (PR review page spec 2026-11-04 §4.1, §5.2).</summary>
[Trait("Lane", "integration")]
public sealed class LibraryFeedTests
{
    private static readonly string[] _admitted = ["doc:harbor/plans/ferry-booking.md", "doc:harbor/plans/future-convention.md", "doc:harbor/plans/harbor-web-notes.md"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_tick_stores_exactly_the_admitted_docs_keyed_by_root_and_path_versioned_by_the_sha256_of_the_bytes()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home);

        var run = await FeedRun.Once(rig.Feed(rig.Vault()), rig.Store, home.Scrubbers);

        var stored = await rig.Stored();
        Assert.Equal(_admitted, stored.Select(static r => r.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(3, run.Report!.Written);
        var ferry = stored.Single(static r => r.Key.Value.EndsWith("ferry-booking.md", StringComparison.Ordinal));
        Assert.Equal(LibraryFeedRig.Sha256(Path.Combine(rig.VaultPath, "plans", "ferry-booking.md")), ferry.Version.Value);
        Assert.Contains("Harbor ferry booking", ferry.Title, StringComparison.Ordinal);
        Assert.All(stored, static r => Assert.Equal(RecordKind.Doc, r.Kind));
    }

    [Fact]
    public async Task A_doc_is_audienced_through_its_repos_front_matter_and_the_rest_are_the_owners()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home);

        _ = await FeedRun.Once(rig.Feed(rig.Vault()), rig.Store, home.Scrubbers);

        var stored = (await rig.Stored()).ToDictionary(static r => r.Key.Value, StringComparer.Ordinal);
        Assert.Equal(ProfileAudiencePolicy.LibraryDoc(["acme/harbor-web"], ["acme/harbor-web"]), stored["doc:harbor/plans/harbor-web-notes.md"].Audience);
        Assert.Equal(ProfileAudiencePolicy.LibraryDoc([], ["acme/harbor-web"]), stored["doc:harbor/plans/ferry-booking.md"].Audience);
    }

    [Fact]
    public async Task Near_misses_are_reported_with_their_paths_lines_and_fixes_and_nothing_of_them_is_stored()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home);

        _ = await FeedRun.Once(rig.Feed(rig.Vault()), rig.Store, home.Scrubbers);

        var report = Assert.Single(rig.Status.Read());
        Assert.Equal(LibraryFeedRig.Harbor, report.Root);
        Assert.Null(report.Problem);
        Assert.Equal(3, report.Docs);
        Assert.Equal(["plans/blank-first.md", "plans/float-one.md", "plans/quoted-one.md", "plans/yes-one.md"], report.NearMisses.Select(static m => m.Path).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(report.NearMisses, static m => Assert.Equal("SND-LIB-019", m.Error.Code));
        var quoted = report.NearMisses.Single(static m => m.Path == "plans/quoted-one.md");
        Assert.Equal(2, quoted.Error.Line);
        Assert.StartsWith("plans/quoted-one.md:2: aw-doc is \"1\"", quoted.Error.Message, StringComparison.Ordinal);
        Assert.NotEmpty(quoted.Error.Fix);
        Assert.DoesNotContain(await rig.Stored(), static r => r.Key.Value.Contains("quoted-one", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Deny_globs_dot_folders_and_secret_files_are_never_read()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home);

        _ = await FeedRun.Once(rig.Feed(rig.Vault()), rig.Store, home.Scrubbers);

        var keys = (await rig.Stored()).Select(static r => r.Key.Value).ToList();
        Assert.DoesNotContain(keys, static k => k.Contains(".obsidian", StringComparison.Ordinal) || k.Contains("secrets", StringComparison.Ordinal)
            || k.Contains(".env", StringComparison.Ordinal) || k.Contains("drafts", StringComparison.Ordinal) || k.Contains("plain-note", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_replay_over_an_unchanged_root_is_an_empty_page_and_writes_nothing()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home);
        var feed = rig.Feed(rig.Vault());
        var first = await FeedRun.Once(feed, rig.Store, home.Scrubbers);

        var replay = await FeedRun.Once(feed, rig.Store, home.Scrubbers);

        Assert.Null(replay.Report);
        Assert.Empty(replay.Page.Items);
        Assert.Empty(replay.Page.Removed);
        Assert.Equal(first.Page.Next, replay.Page.Next);
    }

    [Fact]
    public async Task A_changed_file_is_the_only_one_written_and_an_edit_that_keeps_the_bytes_writes_nothing()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home);
        var feed = rig.Feed(rig.Vault());
        _ = await FeedRun.Once(feed, rig.Store, home.Scrubbers);

        var path = Path.Combine(rig.VaultPath, "plans", "ferry-booking.md");
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(5));
        var touched = await FeedRun.Once(feed, rig.Store, home.Scrubbers);
        _ = rig.Write("plans/ferry-booking.md", (await File.ReadAllTextAsync(path, Ct)).Replace("next ferry", "next sailing", StringComparison.Ordinal));
        var edited = await FeedRun.Once(feed, rig.Store, home.Scrubbers);

        Assert.Equal(0, touched.Report?.Written ?? 0);
        Assert.Equal(1, edited.Report!.Written);
    }

    [Fact]
    public async Task A_deleted_file_and_a_file_that_lost_its_front_matter_are_removed()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home);
        var feed = rig.Feed(rig.Vault());
        _ = await FeedRun.Once(feed, rig.Store, home.Scrubbers);

        File.Delete(Path.Combine(rig.VaultPath, "plans", "ferry-booking.md"));
        _ = rig.Write("plans/harbor-web-notes.md", "# Harbor web notes\n\nThe front matter is gone.\n");
        var run = await FeedRun.Once(feed, rig.Store, home.Scrubbers);

        Assert.Equal(["doc:harbor/plans/ferry-booking.md", "doc:harbor/plans/harbor-web-notes.md"], run.Page.Removed.Select(static k => k.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["doc:harbor/plans/future-convention.md"], (await rig.Stored()).Select(static r => r.Key.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_root_that_cannot_be_listed_fails_its_tick_with_the_reason_and_removes_nothing()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home);
        var feed = rig.Feed(rig.Vault());
        _ = await FeedRun.Once(feed, rig.Store, home.Scrubbers);

        Directory.Delete(rig.VaultPath, recursive: true);
        var position = (await rig.Store.Position(feed.Source, Ct)).Value;
        var pulled = await feed.Pull(new(position.Cursor, 100, NotBefore: null), Ct);

        Assert.True(pulled.IsError);
        Assert.Equal(_admitted.Length, (await rig.Stored()).Count);
        var report = Assert.Single(rig.Status.Read());
        Assert.False(string.IsNullOrEmpty(report.Problem));
        Assert.Equal(rig.VaultPath, report.Path);
    }

    [Fact]
    public async Task One_unreadable_root_does_not_stop_the_other_and_keeps_its_own_docs()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home);
        var gone = new Context.Core.Engine.LibraryRoot { Name = "mounted", Path = Path.Combine(Path.GetTempPath(), "aw-library-not-here-" + RealTime.NewGuid().ToString("N")) };

        var run = await FeedRun.Once(rig.Feed(rig.Vault(), gone), rig.Store, home.Scrubbers);

        Assert.Equal(3, run.Report!.Written);
        Assert.Null(rig.Status.Read().Single(static r => r.Root == LibraryFeedRig.Harbor).Problem);
        Assert.NotNull(rig.Status.Read().Single(static r => r.Root == "mounted").Problem);
    }

    [Fact]
    public async Task A_symlinked_file_or_folder_is_not_followed()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home);
        _ = File.CreateSymbolicLink(Path.Combine(rig.VaultPath, "plans", "linked-plan.md"), Path.Combine(rig.VaultPath, "plans", "ferry-booking.md"));
        _ = Directory.CreateSymbolicLink(Path.Combine(rig.VaultPath, "linked-folder"), Path.Combine(rig.VaultPath, "plans"));

        _ = await FeedRun.Once(rig.Feed(rig.Vault()), rig.Store, home.Scrubbers);

        Assert.Equal(_admitted, (await rig.Stored()).Select(static r => r.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_front_matter_file_over_one_mebibyte_is_stored_and_not_failed()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home, copyFixture: false);
        _ = rig.Write("big.md", "---\naw-doc: 1\nid: big-plan\nkind: plan\ntitle: \"Big plan\"\nstatus: draft\n---\n\n" + string.Concat(Enumerable.Repeat("harbor ferry sailing schedule line\n", 31000)));

        var run = await FeedRun.Once(rig.Feed(rig.Vault()), rig.Store, home.Scrubbers);

        Assert.Equal(1, run.Report!.Written);
        Assert.Equal(["doc:harbor/big.md"], (await rig.Stored()).Select(static r => r.Key.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_aw_root_reads_only_plans_proof_and_system_design_folders_of_each_slug_and_never_the_tool_state()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home, copyFixture: false);
        const string Doc = "---\naw-doc: 1\nid: harbor-doc\nkind: plan\ntitle: \"Harbor doc\"\nstatus: draft\n---\n\n# Harbor doc\n";
        foreach (var path in new[]
        {
            "acme-harbor/plans/20261001-100000-harbor-booking/plan.md", "acme-harbor/plans/epic.md", "acme-harbor/proof/stage-booking/verification-plan.md",
            "acme-harbor/system-design/harbor-ferry/doc.md", "acme-harbor/system-design/harbor-ferry/prs/pr-01.md", "acme-harbor/notes/loose.md", "acme-harbor/proof/loose.md",
            "bin-r1b/acme/plans/x.md", "cache/acme/plans/x.md", "skill-evals/acme/plans/x.md", "digests/acme/plans/x.md", "meta/acme/plans/x.md", ".hidden/acme/plans/x.md", "top.md",
        })
        {
            _ = rig.Write(path, Doc);
        }

        var aw = new Context.Core.Engine.LibraryRoot { Name = "aw", Path = rig.VaultPath, IsAw = true };
        _ = await FeedRun.Once(rig.Feed(aw), rig.Store, home.Scrubbers);

        Assert.Equal(
            [
                "doc:aw/acme-harbor/plans/20261001-100000-harbor-booking/plan.md", "doc:aw/acme-harbor/plans/epic.md",
                "doc:aw/acme-harbor/proof/stage-booking/verification-plan.md", "doc:aw/acme-harbor/system-design/harbor-ferry/doc.md",
            ],
            (await rig.Stored()).Select(static r => r.Key.Value).Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }
}
