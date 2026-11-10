using Aw.Context.Core.Library;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Sync;

/// <summary>A file the host names in <see cref="ILibrarySkips"/> (the Obsidian todo note) is never read as a library doc, and a doc stored before it was named is removed.</summary>
[Trait("Lane", "integration")]
public sealed class LibraryFeedSkipTests
{
    private const string Note = "Todo.md";

    private sealed class Skips(string root, params string[] relative) : ILibrarySkips
    {
        public ValueTask<IReadOnlyList<string>> In(string name, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<string>>(name == root ? relative : []);
    }

    private static LibraryFeed Feed(LibraryFeedRig rig, TempHome home, ILibrarySkips skips) =>
        new(new LibrarySetup([rig.Vault()], []), rig.Store, home.Scrubbers, rig.Status, home.Clock, skips);

    [Fact]
    public async Task A_skipped_design_doc_is_not_stored()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home, copyFixture: false);
        _ = rig.Write(Note, LibraryFeedRig.Doc(1));
        _ = rig.Write("plans/other.md", LibraryFeedRig.Doc(2));

        _ = await FeedRun.Once(Feed(rig, home, new Skips(LibraryFeedRig.Harbor, Note)), rig.Store, home.Scrubbers);

        Assert.Equal(["doc:harbor/plans/other.md"], (await rig.Stored()).Select(static r => r.Key.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Naming_a_stored_doc_later_removes_it_on_the_next_pass()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home, copyFixture: false);
        _ = rig.Write(Note, LibraryFeedRig.Doc(1));
        _ = await FeedRun.Once(rig.Feed(rig.Vault()), rig.Store, home.Scrubbers);
        _ = Assert.Single(await rig.Stored());

        var feed = Feed(rig, home, new Skips(LibraryFeedRig.Harbor, Note));
        var page = await rig.PullNow(feed);

        Assert.Equal(["doc:harbor/Todo.md"], page.Removed.Select(static k => k.Value), StringComparer.Ordinal);
    }
}
