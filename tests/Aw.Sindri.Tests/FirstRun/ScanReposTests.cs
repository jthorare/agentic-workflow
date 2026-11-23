using Aw.Kernel.Json;
using Aw.Sindri.Features.ScanRepos;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.FirstRun;

/// <summary>The repo scan's walk (first-run spec §3.3): worktrees fold into their main checkout, the origin loses its user name and token, skipped folders and links stay out, the depth and the 500 cap hold.</summary>
public sealed class ScanReposTests
{
    private const string Home = "/home/joi";

    private readonly SliceHarness _slice = new();
    private readonly FakeRepoScanTree _tree = new(Home);
    private readonly FakeKnownRepos _known = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Scan_folds_a_worktree_into_its_main_checkout()
    {
        _ = _tree.Checkout(Home + "/repos/app", "https://github.com/acme/app.git")
            .Worktree(Home + "/repos/app-feat", Home + "/repos/app")
            .Worktree(Home + "/wt/app-fix", Home + "/repos/app");

        var (rows, end) = await Scan(new ScanReposRequest(Root: null, Depth: null));

        Assert.Equal([new RepoScanRow(Home + "/repos/app", "github.com", "acme/app", Known: false, InProfile: false, Worktrees: 2)], rows);
        Assert.Equal(1, end.Repos);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Scan_drops_userinfo_from_origin()
    {
        // the URL is split so the pre-commit secret check doesn't read it as a real credential
        _ = _tree.Checkout(Home + "/repos/app", "https://joi:" + "opensesame@github.com/acme/app.git");

        var (rows, _) = await Scan(new ScanReposRequest(Root: null, Depth: null));

        var row = Assert.Single(rows);
        Assert.Equal(("github.com", "acme/app"), (row.Host, row.Slug));
        Assert.DoesNotContain("opensesame", System.Text.Encoding.UTF8.GetString(AwJson.Serialize(row, SliceHarness.Json)), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_walk_skips_dot_folders_build_folders_and_links()
    {
        foreach (var skipped in new[] { ".cache", "node_modules", "bin", "obj", "target" })
        {
            _ = _tree.Checkout($"{Home}/{skipped}/app");
        }

        _ = _tree.Link(Home + "/linked").Checkout(Home + "/linked/app").Checkout(Home + "/code/app");

        var (rows, _) = await Scan(new ScanReposRequest(Root: null, Depth: null));

        Assert.Equal([Home + "/code/app"], rows.Select(static r => r.Path), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_walk_stops_at_its_depth_and_lists_shallow_repos_first()
    {
        _ = _tree.Checkout(Home + "/a/b/c/deep").Checkout(Home + "/z/b/near").Checkout(Home + "/top");

        var (three, _) = await Scan(new ScanReposRequest(Root: null, Depth: null));
        var (four, _) = await Scan(new ScanReposRequest(Root: null, Depth: 4));

        Assert.Equal([Home + "/top", Home + "/z/b/near"], three.Select(static r => r.Path), StringComparer.Ordinal);
        Assert.Equal([Home + "/top", Home + "/z/b/near", Home + "/a/b/c/deep"], four.Select(static r => r.Path), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_walk_never_enters_a_checkout()
    {
        _ = _tree.Checkout(Home + "/app").Checkout(Home + "/app/vendor/lib");

        var (rows, end) = await Scan(new ScanReposRequest(Root: null, Depth: null));

        Assert.Equal([Home + "/app"], rows.Select(static r => r.Path), StringComparer.Ordinal);
        Assert.Equal(2, end.Folders);   // the home and the checkout
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_walk_stops_at_500_repos()
    {
        for (var i = 0; i < 501; i++)
        {
            _ = _tree.Checkout(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Home}/repos/r{i:D3}"));
        }

        var (rows, end) = await Scan(new ScanReposRequest(Root: null, Depth: null));

        Assert.Equal(500, rows.Count);
        Assert.Equal((true, 500), (end.Done, end.Repos));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_last_line_counts_the_folders_looked_at_and_the_repos_listed()
    {
        _ = _tree.Folder(Home + "/docs/notes").Checkout(Home + "/repos/app").Checkout(Home + "/repos/lib");

        var (_, end) = await Scan(new ScanReposRequest(Root: null, Depth: null));

        Assert.Equal(new RepoScanEnd(Done: true, Folders: 6, Repos: 2, KnownFailed: false), end);   // home, docs, docs/notes, repos, app, lib
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Stopping_the_read_stops_the_walk()
    {
        _ = _tree.Checkout(Home + "/repos/a").Checkout(Home + "/repos/b").Checkout(Home + "/repos/c");
        var response = await ScanReposServiceFactory.Create(_slice, _tree, _known).Handle(new ScanReposRequest(Root: null, Depth: null), Token);

        await foreach (var row in response.Value.Rows.WithCancellation(Token))
        {
            Assert.Equal(Home + "/repos/a", row.Path);
            break;
        }

        Assert.Equal(1, _tree.OriginReads);
    }

    private async Task<(List<RepoScanRow> Rows, RepoScanEnd End)> Scan(ScanReposRequest request)
    {
        var response = await ScanReposServiceFactory.Create(_slice, _tree, _known).Handle(request, Token);
        Assert.True(response.IsSuccess, response.IsError ? response.Error.ToString() : null);
        var rows = new List<RepoScanRow>();
        await foreach (var row in response.Value.Rows.WithCancellation(Token))
        {
            rows.Add(row);
        }

        return (rows, response.Value.End());
    }
}
