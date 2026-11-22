using Aw.Kernel.Testing;
using Aw.Transcripts.Core.Sessions;
using Aw.Transcripts.Infrastructure.Repos;
using Aw.Transcripts.Tests.Fixtures;

namespace Aw.Transcripts.Tests.Repos;

[Lane(TestLane.Integration)]
public sealed class RepoAttributionTests : IDisposable
{
    private readonly TempRoots _roots = new();

    [Fact]
    public void A_cwd_inside_a_checkout_belongs_to_that_checkout()
    {
        var repo = Directory.CreateDirectory(Path.Combine(_roots.Root, "repos", "alpha")).FullName;
        _ = Directory.CreateDirectory(Path.Combine(repo, ".git"));
        var deep = Directory.CreateDirectory(Path.Combine(repo, "src", "lib")).FullName;

        Assert.Equal(repo, new RepoResolver().Resolve(deep));
    }

    [Fact]
    public void A_worktree_cwd_belongs_to_its_main_checkout()
    {
        var main = Directory.CreateDirectory(Path.Combine(_roots.Root, "repos", "alpha")).FullName;
        var gitDir = Directory.CreateDirectory(Path.Combine(main, ".git", "worktrees", "feature")).FullName;
        File.WriteAllText(Path.Combine(gitDir, "commondir"), "../..\n");
        var worktree = Directory.CreateDirectory(Path.Combine(_roots.Root, "wt", "feature")).FullName;
        File.WriteAllText(Path.Combine(worktree, ".git"), $"gitdir: {gitDir}\n");

        Assert.Equal(main, new RepoResolver().Resolve(Path.Combine(worktree)));
    }

    [Fact]
    public void A_cwd_that_is_gone_or_in_no_repo_is_kept()
    {
        var plain = Directory.CreateDirectory(Path.Combine(_roots.Root, "plain")).FullName;
        var resolver = new RepoResolver();

        Assert.Equal("/no/such/worktree", resolver.Resolve("/no/such/worktree"));
        Assert.Equal("relative/dir", resolver.Resolve("relative/dir"));
        Assert.Equal(plain, resolver.Resolve(plain));
    }

    [Fact]
    public async Task A_session_in_a_worktree_is_listed_under_the_main_repo()
    {
        var main = Directory.CreateDirectory(Path.Combine(_roots.Root, "repos", "alpha")).FullName;
        var gitDir = Directory.CreateDirectory(Path.Combine(main, ".git", "worktrees", "feature")).FullName;
        await File.WriteAllTextAsync(Path.Combine(gitDir, "commondir"), "../..\n", TestContext.Current.CancellationToken);
        var worktree = Directory.CreateDirectory(Path.Combine(_roots.Root, "wt", "feature")).FullName;
        await File.WriteAllTextAsync(Path.Combine(worktree, ".git"), $"gitdir: {gitDir}\n", TestContext.Current.CancellationToken);
        var at = new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
        _ = _roots.Write("claude/projects/-wt-feature/s.jsonl", at.AddMinutes(5), ClaudeLines.User("go", at, cwd: worktree));

        var listed = (await _roots.Corpus().ListSessions(new SessionQuery(at) { Repo = main }, CancellationToken.None)).Value;

        Assert.Equal(main, Assert.Single(listed).Repo);
    }

    public void Dispose() => _roots.Dispose();
}
