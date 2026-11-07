using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>The library page's cap (PR review page spec §6.1): at most 200 docs with errors and 200 near misses are listed, the counts stay whole, and the order is kept.</summary>
[Lane(TestLane.Unit)]
public sealed class LibraryPageTests
{
    private static string Draft(int n) => "draft-" + n.ToString("000", CultureInfo.InvariantCulture);

    private static string MissName(int n) => "miss-" + n.ToString("000", CultureInfo.InvariantCulture);

    private static LibraryBrokenDoc Broken(int n) => new(Draft(n), $"~/notes/{Draft(n)}.md", [new LibraryDocError("SND-LIB-010", "x")]);

    private static LibraryNearMiss Miss(int n) => new($"~/notes/{MissName(n)}.md", "SND-LIB-019", "y");

    private static LibraryPage Page(int broken, int misses) =>
        LibraryPage.From(new LibraryPageFacts(Docs: 500, [.. Enumerable.Range(1, broken).Select(Broken)], [.. Enumerable.Range(1, misses).Select(Miss)], [], []));

    [Theory]
    [InlineData(199, 199, false)]
    [InlineData(200, 200, false)]
    [InlineData(201, 200, true)]
    public void The_docs_with_errors_are_cut_at_200(int broken, int listed, bool capped)
    {
        var page = Page(broken, misses: 0);

        Assert.Equal((listed, broken, capped), (page.Broken.Count, page.WithErrors, page.CappedText is not null));
    }

    [Fact]
    public void A_capped_page_says_how_many_it_lists_of_how_many()
    {
        var page = Page(broken: 250, misses: 0);

        Assert.Equal("Showing the first 200 of 250 docs with errors; run `sindri library validate` for the rest", page.CappedText);
    }

    [Theory]
    [InlineData(200, false)]
    [InlineData(201, true)]
    public void The_near_misses_are_cut_at_200_too(int misses, bool capped)
    {
        var page = Page(broken: 0, misses);

        Assert.Equal((Math.Min(misses, 200), capped), (page.NearMisses.Count, page.CappedText is not null));
    }

    [Fact]
    public void Both_lists_capped_say_so_in_one_text()
    {
        var page = Page(broken: 201, misses: 300);

        Assert.Equal("Showing the first 200 of 201 docs with errors; run `sindri library validate` for the rest. Showing the first 200 of 300 near misses; run `sindri library validate` for the rest", page.CappedText);
    }

    [Fact]
    public void The_listed_rows_keep_their_order()
    {
        var page = Page(broken: 205, misses: 205);

        Assert.Equal(Enumerable.Range(1, 200).Select(Draft), page.Broken.Select(b => b.Doc), StringComparer.Ordinal);
        Assert.Equal(Enumerable.Range(1, 200).Select(n => $"~/notes/{MissName(n)}.md"), page.NearMisses.Select(m => m.Path), StringComparer.Ordinal);
    }

    [Fact]
    public void An_empty_library_is_all_zeros_with_no_text()
    {
        var page = LibraryPage.From(new LibraryPageFacts(Docs: 0, [], [], [], []));

        Assert.Equal((0, 0, 0, 0, null), (page.Docs, page.WithErrors, page.NearMisses.Count, page.UnreadableRoots.Count, page.CappedText));
    }

    [Fact]
    public void The_unreadable_roots_pass_through()
    {
        var root = new ReviewUnreadableRoot("~/notes/vault", "permission denied");

        var page = LibraryPage.From(new LibraryPageFacts(Docs: 33, [], [], [root], []));

        Assert.Equal([root], page.UnreadableRoots);
    }

    [Fact]
    public void The_roots_are_ordered_for_the_page()
    {
        var vault = new LibraryRootFacts("work", LibraryRootKind.Vault, "~/work", Repo: null, Deny: [], Off: false, ScannedAt: null, Docs: 1, Errors: 0, Unreadable: null);
        var aw = new LibraryRootFacts("aw", LibraryRootKind.Aw, "~/.agentic-workflow", Repo: null, Deny: [], Off: false, ScannedAt: null, Docs: 1, Errors: 0, Unreadable: null);

        var page = LibraryPage.From(new LibraryPageFacts(Docs: 2, [], [], [], [vault, aw]));

        Assert.Equal(["aw", "work"], page.Roots.Select(static r => r.Name), StringComparer.Ordinal);
    }
}
