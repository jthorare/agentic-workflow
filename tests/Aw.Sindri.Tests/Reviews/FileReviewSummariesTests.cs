using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Infrastructure.Reviews;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using static Aw.Sindri.Tests.Reviews.ReviewPulls;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>The summary cache on disk (PR review page spec §6.2 "Cache"): per-head files, owner-only, and path segments checked before they build a path.</summary>
[Lane(TestLane.Integration)]
public sealed class FileReviewSummariesTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly FileReviewSummaries _cache;

    public FileReviewSummariesTests()
    {
        _cache = new FileReviewSummaries(new FakeSindriHome(_home.Path));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ReviewPullRef Pr => new("acme", "api", 7);

    public void Dispose() => _home.Dispose();

    [Fact]
    public async Task A_written_summary_reads_back_from_its_head_folder()
    {
        var summary = new ReviewSummary(Head, ReviewCi.Fail, 2, KnownInstants.Epoch);

        _ = (await _cache.Write(Pr, summary, Token)).Value;

        Assert.Equal(summary, (await _cache.Read(Pr, Head, Token)).Value);
        Assert.True(File.Exists(Path.Combine(_home.Path, "reviews", "cache", "acme", "api", "7", Head, "summary.json")));
        Assert.Null((await _cache.Read(Pr, Old, Token)).Value);
    }

    [Fact]
    public async Task The_newest_summary_is_the_one_with_the_latest_time_whatever_its_head()
    {
        _ = await _cache.Write(Pr, new ReviewSummary(Head, ReviewCi.Pass, 0, KnownInstants.Epoch.AddHours(-2)), Token);
        _ = await _cache.Write(Pr, new ReviewSummary(Old, ReviewCi.Running, 1, KnownInstants.Epoch.AddHours(-1)), Token);

        var newest = (await _cache.Newest(Pr, Token)).Value;

        Assert.Equal(Old, newest?.Sha);
        Assert.Null((await _cache.Newest(new ReviewPullRef("acme", "api", 8), Token)).Value);
    }

    [Fact]
    public async Task Folders_are_owner_only_and_the_file_is_0600()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        _ = await _cache.Write(Pr, new ReviewSummary(Head, ReviewCi.Pass, 0, KnownInstants.Epoch), Token);

        var head = Path.Combine(_home.Path, "reviews", "cache", "acme", "api", "7", Head);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(head, "summary.json")));
        foreach (var folder in new[] { head, Path.GetDirectoryName(head)!, Path.Combine(_home.Path, "reviews") })
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(folder));
        }
    }

    [Theory]
    [InlineData("..", "api", 1)]
    [InlineData("acme", "..", 1)]
    [InlineData("a/b", "api", 1)]
    [InlineData("-acme", "api", 1)]
    [InlineData("acme", "api", 0)]
    [InlineData("acme", "a\\b", 1)]
    public async Task A_name_that_fails_GitHubs_rules_never_builds_a_path(string owner, string repo, int number)
    {
        var bad = new ReviewPullRef(owner, repo, number);

        Assert.Equal(SindriErrors.InvalidInput.Code, (await _cache.Read(bad, Head, Token)).Error.Code);
        Assert.Equal(SindriErrors.InvalidInput.Code, (await _cache.Newest(bad, Token)).Error.Code);
        Assert.Equal(SindriErrors.InvalidInput.Code, (await _cache.Write(bad, new ReviewSummary(Head, ReviewCi.None, RequiredOpen: null, KnownInstants.Epoch), Token)).Error.Code);
        Assert.False(Directory.Exists(Path.Combine(_home.Path, "reviews")));
    }

    [Theory]
    [InlineData("../../etc")]
    [InlineData("abc")]
    [InlineData("zzzzzzz")]
    public async Task A_head_that_is_not_a_commit_id_is_refused(string sha)
    {
        Assert.Equal(SindriErrors.InvalidInput.Code, (await _cache.Read(Pr, sha, Token)).Error.Code);
        Assert.Equal(SindriErrors.InvalidInput.Code, (await _cache.Write(Pr, new ReviewSummary(sha, ReviewCi.None, RequiredOpen: null, KnownInstants.Epoch), Token)).Error.Code);
    }

    [Fact]
    public async Task A_damaged_file_reads_as_a_miss()
    {
        _ = await _cache.Write(Pr, new ReviewSummary(Head, ReviewCi.Pass, 0, KnownInstants.Epoch), Token);
        await File.WriteAllTextAsync(Path.Combine(_home.Path, "reviews", "cache", "acme", "api", "7", Head, "summary.json"), "{ not json", Token);

        Assert.Null((await _cache.Read(Pr, Head, Token)).Value);
        Assert.Null((await _cache.Newest(Pr, Token)).Value);
    }
}
