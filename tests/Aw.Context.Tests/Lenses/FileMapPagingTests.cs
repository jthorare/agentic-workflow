using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Graph;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;
using Aw.Kernel;

namespace Aw.Context.Tests.Lenses;

public sealed class FileMapPagingTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From(FixtureMap.Repo);
    private static readonly FileMapBasis _basis = new("v13", "7c3e1a2");

    // 'dirs' directories of 'perDir' files each, plus 'loose' files at the root: dir00/f0000.ts ... and loose000.md.
    private static string[] Files(int dirs, int perDir, int loose = 0) =>
    [
        .. Enumerable.Range(0, dirs).SelectMany(d => Enumerable.Range(0, perDir).Select(f => string.Create(CultureInfo.InvariantCulture, $"dir{d:D2}/f{f:D4}.ts"))),
        .. Enumerable.Range(0, loose).Select(l => string.Create(CultureInfo.InvariantCulture, $"loose{l:D3}.md")),
    ];

    private static GraphSnapshot Graph() => new GraphBuilder().Node("feature:a")
        .Edge("code:dir00/f0000.ts", EdgeType.Implements, "feature:a", provenance: Provenance.Deterministic).Build();

    private static Result<FileMapView> Page(string[] files, string? cursor = null, int? limit = null, bool unmapped = false, string path = "") =>
        FileMapBuilder.Build(_basis, new FileMapQuery(_repo, path, unmapped, Draft: false, cursor, limit), files, Graph());

    [Fact, Trait("Lane", "unit")]
    public void With_no_cursor_and_no_limit_every_child_comes_back_and_there_is_no_next_page()
    {
        var view = Page(Files(0, 0, loose: 1500)).Value;

        Assert.Equal((1500, 1500), (view.Entries.Count, view.Total));
        Assert.Null(view.NextCursor);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cursor_without_a_limit_pages_by_200_and_names_the_next()
    {
        var files = Files(0, 0, loose: 450);

        var view = Page(files, cursor: "loose009.md").Value;

        Assert.Equal((200, 450), (view.Entries.Count, view.Total));
        Assert.Equal("loose209.md", view.NextCursor);
    }

    [Fact, Trait("Lane", "unit")]
    public void Following_the_cursors_visits_every_entry_once_and_ends_with_a_null_cursor()
    {
        var files = Files(7, 3, loose: 5);
        var seen = new List<string>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var view = Page(files, cursor, limit: 4).Value;
            seen.AddRange(view.Entries.Select(e => e.Path));
            cursor = view.NextCursor;
            pages++;
        }
        while (cursor is not null);

        Assert.Equal(3, pages);
        Assert.Equal(12, seen.Count);
        Assert.Equal(seen.Count, seen.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal([.. Enumerable.Range(0, 7).Select(d => string.Create(CultureInfo.InvariantCulture, $"dir{d:D2}/")), .. Enumerable.Range(0, 5).Select(l => string.Create(CultureInfo.InvariantCulture, $"loose{l:D3}.md"))], seen);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_limit_is_held_between_1_and_1000()
    {
        var files = Files(0, 0, loose: 1200);

        Assert.Equal(1000, Page(files, limit: 5000).Value.Entries.Count);
        _ = Assert.Single(Page(files, limit: 0).Value.Entries);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_unmapped_list_follows_the_page_while_totals_count_every_file_under_the_path()
    {
        var view = Page(Files(3, 4), limit: 2).Value;

        Assert.Equal(new FileMapTotals(Files: 12, Mapped: 1, Suggested: 0, Unmapped: 11), view.Totals);
        Assert.Equal(["dir00/", "dir01/"], view.Entries.Select(e => e.Path), StringComparer.Ordinal);
        Assert.Equal(["dir00/", "dir01/"], view.Unmapped.Select(u => u.Path), StringComparer.Ordinal);
        Assert.Equal("dir01/", view.NextCursor);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_unmapped_filter_pages_over_what_is_left_and_counts_only_that()
    {
        var files = Files(5, 2);

        var view = Page(files, limit: 3, unmapped: true).Value;

        Assert.Equal(["dir00/", "dir01/", "dir02/"], view.Entries.Select(e => e.Path), StringComparer.Ordinal);
        Assert.Equal(5, view.Total);
        Assert.Equal("dir02/", view.NextCursor);
        Assert.Equal(["dir03/", "dir04/"], Page(files, view.NextCursor, 3, unmapped: true).Value.Entries.Select(e => e.Path), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cursor_that_is_not_a_child_of_the_path_is_refused()
    {
        var files = Files(2, 2);

        Assert.Equal(ContextErrors.QueryInvalid.Code, Page(files, cursor: "dir00/f0000.ts").Error.Code);
        // At the root any one-segment name is a child (one that has gone resumes after where it sorted); under a path, a cursor outside it is refused.
        Assert.Equal(ContextErrors.QueryInvalid.Code, Page(files, cursor: "elsewhere/", path: "dir00").Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cursor_whose_entry_has_gone_resumes_after_where_it_sorted()
    {
        var files = Files(0, 0, loose: 4);

        var view = Page(files, cursor: "loose001a.md").Value;

        Assert.Equal(["loose002.md", "loose003.md"], view.Entries.Select(e => e.Path), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_big_directory_pages_without_reading_the_files_under_the_entries_it_does_not_show()
    {
        // 30,000 files in 30 directories: one page of 10 holds 10 directory entries and their files are counted, never listed.
        var view = Page(Files(30, 1000), limit: 10).Value;

        Assert.Equal((10, 30, 30000), (view.Entries.Count, view.Total, view.Totals.Files));
        Assert.All(view.Entries, e => Assert.Equal(1000, e.Files));
        Assert.Equal("dir09/", view.NextCursor);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_service_lists_the_repo_once_per_commit_however_many_pages_are_read()
    {
        var tree = Files(30, 1000).ToDictionary(f => f, _ => "x", StringComparer.Ordinal);
        var reader = new CountingReader(new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, tree));
        using var world = new MapWorld(reader);
        _ = await world.ImportYaml(MapWorld.TabsMap);

        FileMapView? last = null;
        foreach (var cursor in new string?[] { null, "dir09/", "dir19/" })
        {
            last = (await world.Engine.FileMap(new FileMapQuery(_repo, string.Empty, UnmappedOnly: false, Draft: false, cursor, 10), MapWorld.Human, MapWorld.None)).Value;
            Assert.Equal(10, last.Entries.Count);
        }

        Assert.Equal((1, 3), (reader.Lists, reader.Heads));
        Assert.Null((await world.Engine.FileMap(new FileMapQuery(_repo, string.Empty, UnmappedOnly: false, Draft: false, "dir29/", 10), MapWorld.Human, MapWorld.None)).Value.NextCursor);
        Assert.Equal(1, reader.Lists);
    }

    private sealed class CountingReader(IRepoReader inner) : IRepoReader
    {
        public int Lists { get; private set; }

        public int Heads { get; private set; }

        public ValueTask<Result<string>> Head(ContextRepo repo, CancellationToken cancellationToken)
        {
            Heads++;
            return inner.Head(repo, cancellationToken);
        }

        public ValueTask<Result<IReadOnlyList<string>>> ListFiles(ContextRepo repo, string commit, CancellationToken cancellationToken)
        {
            Lists++;
            return inner.ListFiles(repo, commit, cancellationToken);
        }

        public ValueTask<Result<string>> Read(ContextRepo repo, string commit, string path, CancellationToken cancellationToken) => inner.Read(repo, commit, path, cancellationToken);

        public ValueTask<Result<IReadOnlyDictionary<string, string>>> BlobIds(ContextRepo repo, string commit, IReadOnlyList<string> paths, CancellationToken cancellationToken) => inner.BlobIds(repo, commit, paths, cancellationToken);

        public ValueTask<Result<RepoChanges>> Changes(ContextRepo repo, DiffRange range, CancellationToken cancellationToken) => inner.Changes(repo, range, cancellationToken);
    }
}
