using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Infrastructure.Reviews;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>The page cache on disk (PR review page spec §6.2 "Cache"): a snapshot per head, owner-only, the newest three heads kept, and path segments checked before they build a path.</summary>
[Lane(TestLane.Integration)]
public sealed class FileReviewPagesTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FileReviewPages _cache;

    public FileReviewPagesTests()
    {
        _cache = new FileReviewPages(new FakeSindriHome(_home.Path), _clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ReviewPullRef Pr => new("acme", "api", 7);

    public void Dispose() => _home.Dispose();

    private static string Sha(char c) => new(c, 40);

    private static ReviewPageModel Page(string sha, string title = "Title") =>
        new(
            new AsOf(1, 1, KnownInstants.Epoch, Rebuilding: false, Stalled: false),
            new ReviewPagePr("acme/api", 7, title, "sam-dev", Yours: false, Draft: false, "open", new ReviewPageHead(sha, sha[..7], "feat/x"), "main", "https://github.com/acme/api/pull/7", [], PostsFixThreads: false),
            new ReviewPageSettings("default", "default", Pick: null), Moving: null, Offline: null, [new ReviewPageStrip("ci", "1 failing")],
            [
                new ReviewPagePanel("ci", "ready", Text: null, new CiData([new ReviewPageRun("ui", "actions", "completed", "failure", sha[..7], "https://github.com/acme/api/pull/7/checks")], Babysitter: null)),
                new ReviewPagePanel("risk", "empty", "No risky paths touched", new RiskData([])),
            ],
            new ReviewPageDiff("empty", "This PR changes no files", 0, Capped: false, []));

    [Fact]
    public async Task A_written_page_reads_back_as_the_newest_with_its_panels_and_the_time_it_was_cached()
    {
        _ = (await _cache.Write(Pr, Page(Sha('a')), Token)).Value;

        var cached = (await _cache.Newest(Pr, Token)).Value;

        Assert.Equal(KnownInstants.Epoch, cached?.CachedAt);
        Assert.Equal(["ci", "risk"], [.. cached!.Page.Panels.Select(static p => p.Key)]);
        Assert.Equal("failure", Assert.Single(Assert.IsType<CiData>(cached.Page.Panels[0].Data).Runs).Conclusion);
        Assert.True(File.Exists(Path.Combine(_home.Path, "reviews", "cache", "acme", "api", "7", Sha('a'), "page.json")));
        Assert.Null((await _cache.Newest(new ReviewPullRef("acme", "api", 8), Token)).Value);
    }

    [Fact]
    public async Task The_newest_page_is_the_latest_written_whatever_its_head()
    {
        _ = await _cache.Write(Pr, Page(Sha('a'), "old"), Token);
        _clock.Advance(TimeSpan.FromMinutes(5));
        _ = await _cache.Write(Pr, Page(Sha('b'), "new"), Token);

        Assert.Equal("new", (await _cache.Newest(Pr, Token)).Value?.Page.Pr.Title);
    }

    [Fact]
    public async Task Only_the_newest_three_heads_of_a_pr_are_kept()
    {
        foreach (var c in "abcd")
        {
            _ = await _cache.Write(Pr, Page(Sha(c)), Token);
            _clock.Advance(TimeSpan.FromMinutes(1));
        }

        var kept = Directory.EnumerateDirectories(Path.Combine(_home.Path, "reviews", "cache", "acme", "api", "7")).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal([Sha('b'), Sha('c'), Sha('d')], kept);
    }

    [Fact]
    public async Task Folders_are_owner_only_and_the_file_is_0600()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        _ = await _cache.Write(Pr, Page(Sha('a')), Token);

        var head = Path.Combine(_home.Path, "reviews", "cache", "acme", "api", "7", Sha('a'));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(head, "page.json")));
        foreach (var folder in new[] { head, Path.GetDirectoryName(head)!, Path.Combine(_home.Path, "reviews") })
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(folder));
        }
    }

    [Theory]
    [InlineData("..", "api", 1)]
    [InlineData("acme", "..", 1)]
    [InlineData("a/b", "api", 1)]
    [InlineData("acme", "api", 0)]
    public async Task A_name_that_fails_GitHubs_rules_never_builds_a_path(string owner, string repo, int number)
    {
        var bad = new ReviewPullRef(owner, repo, number);

        Assert.Equal(SindriErrors.InvalidInput.Code, (await _cache.Newest(bad, Token)).Error.Code);
        Assert.Equal(SindriErrors.InvalidInput.Code, (await _cache.Write(bad, Page(Sha('a')), Token)).Error.Code);
        Assert.False(Directory.Exists(Path.Combine(_home.Path, "reviews")));
    }

    [Fact]
    public async Task A_damaged_file_reads_as_a_miss()
    {
        _ = await _cache.Write(Pr, Page(Sha('a')), Token);
        await File.WriteAllTextAsync(Path.Combine(_home.Path, "reviews", "cache", "acme", "api", "7", Sha('a'), "page.json"), "{ not json", Token);

        Assert.Null((await _cache.Newest(Pr, Token)).Value);
    }
}
