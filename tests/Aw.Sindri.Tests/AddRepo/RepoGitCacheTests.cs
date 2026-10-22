using Aw.Kernel.Testing;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.AddRepo;

/// <summary>The git facts of the profile's repos (setup-once spec §5.1 "Git facts"): kept 30 seconds, forgotten on a repo event, so reading the setup list runs no git per request.</summary>
[Lane(TestLane.Unit)]
public sealed class RepoGitCacheTests
{
    private readonly FakeRepoInspector _git = new FakeRepoInspector().Has("/work/demo", git: new RepoGit("main", "abc1234"));
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reads_within_30_seconds_ask_git_once()
    {
        var cache = new RepoGitCache(_git, _clock);

        var first = await cache.Of("/work/demo", Token);
        _clock.Advance(TimeSpan.FromSeconds(29));
        var second = await cache.Of("/work/demo", Token);

        Assert.Equal(("main", "abc1234"), (first!.DefaultBranch, first.Head));
        Assert.Same(first, second);
        Assert.Equal(1, _git.Asked);
    }

    [Fact]
    public async Task After_30_seconds_git_is_asked_again()
    {
        var cache = new RepoGitCache(_git, _clock);
        _ = await cache.Of("/work/demo", Token);

        _clock.Advance(TimeSpan.FromSeconds(30));
        _ = await cache.Of("/work/demo", Token);

        Assert.Equal(2, _git.Asked);
    }

    [Fact]
    public async Task A_repo_event_makes_the_next_read_ask_git()
    {
        var cache = new RepoGitCache(_git, _clock);
        _ = await cache.Of("/work/demo", Token);

        cache.Invalidate();
        _ = await cache.Of("/work/demo", Token);

        Assert.Equal(2, _git.Asked);
    }

    [Fact]
    public async Task A_folder_git_cannot_read_has_no_facts_and_is_not_asked_again_for_30_seconds()
    {
        var cache = new RepoGitCache(_git, _clock);

        Assert.Null(await cache.Of("/work/gone", Token));
        Assert.Null(await cache.Of("/work/gone", Token));
        Assert.Equal(1, _git.Asked);
    }

    [Fact]
    public async Task Each_repo_has_its_own_entry()
    {
        _ = _git.Has("/work/other", git: new RepoGit("dev", "def5678"));
        var cache = new RepoGitCache(_git, _clock);

        Assert.Equal("main", (await cache.Of("/work/demo", Token))!.DefaultBranch);
        Assert.Equal("dev", (await cache.Of("/work/other", Token))!.DefaultBranch);
    }
}
