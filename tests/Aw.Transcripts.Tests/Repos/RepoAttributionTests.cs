using Aw.Kernel.Testing;
using Aw.Transcripts.Core;
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

        Assert.Equal(repo, new RepoResolver([]).Resolve(deep));
    }

    [Fact]
    public void A_worktree_cwd_belongs_to_its_main_checkout()
    {
        var main = Directory.CreateDirectory(Path.Combine(_roots.Root, "repos", "alpha")).FullName;
        var gitDir = Directory.CreateDirectory(Path.Combine(main, ".git", "worktrees", "feature")).FullName;
        File.WriteAllText(Path.Combine(gitDir, "commondir"), "../..\n");
        var worktree = Directory.CreateDirectory(Path.Combine(_roots.Root, "wt", "feature")).FullName;
        File.WriteAllText(Path.Combine(worktree, ".git"), $"gitdir: {gitDir}\n");

        Assert.Equal(main, new RepoResolver([]).Resolve(Path.Combine(worktree)));
    }

    [Fact]
    public void A_cwd_that_is_gone_or_in_no_repo_is_kept()
    {
        var plain = Directory.CreateDirectory(Path.Combine(_roots.Root, "plain")).FullName;
        var resolver = new RepoResolver([]);

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

    [Fact]
    public async Task A_cwd_with_a_nul_byte_lists_the_session_without_a_repo()
    {
        var at = new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
        _ = _roots.Write("claude/projects/-bad/s.jsonl", at.AddMinutes(5), ClaudeLines.User("go", at, cwd: "/work/a\0b"));

        Assert.Null(new RepoResolver([]).Resolve("/work/a\0b"));
        var listed = await _roots.Corpus().ListSessions(new SessionQuery(at), CancellationToken.None);

        var session = Assert.Single(listed.Value);
        Assert.Null(session.Repo);
        Assert.Equal(1, session.HumanTurns);
    }

    [Fact]
    public void An_empty_worktree_root_list_resolves_exactly_as_before()
    {
        var repo = Directory.CreateDirectory(Path.Combine(_roots.Root, "repos", "alpha")).FullName;
        _ = Directory.CreateDirectory(Path.Combine(repo, ".git"));
        var plain = Directory.CreateDirectory(Path.Combine(_roots.Root, "plain")).FullName;
        var resolver = new RepoResolver([]);

        Assert.Equal(repo, resolver.Resolve(Path.Combine(repo, "src")));
        Assert.Equal(plain, resolver.Resolve(plain));
        Assert.Equal("/no/such/worktree", resolver.Resolve("/no/such/worktree"));
        Assert.Equal("relative/dir", resolver.Resolve("relative/dir"));
        Assert.Null(resolver.Resolve("/work/a\0b"));
    }

    [Fact]
    public void A_gone_cwd_under_a_worktree_root_belongs_to_its_main_checkout()
    {
        var resolver = new RepoResolver([new WorktreeRoot("/gone/aw-wt/alpha", "/repos/alpha")]);

        Assert.Equal("/repos/alpha", resolver.Resolve("/gone/aw-wt/alpha/fix-login"));
        Assert.Equal("/repos/alpha", resolver.Resolve("/gone/aw-wt/alpha/fix-login/src/deep"));
        Assert.Equal("/repos/alpha", resolver.Resolve("/gone/aw-wt/alpha"));
    }

    [Fact]
    public void A_gone_cwd_outside_every_root_is_kept()
    {
        var resolver = new RepoResolver([new WorktreeRoot("/gone/aw-wt/alpha", "/repos/alpha")]);

        Assert.Equal("/gone/elsewhere/tree", resolver.Resolve("/gone/elsewhere/tree"));
    }

    [Fact]
    public void A_sibling_with_a_longer_name_is_not_under_the_root()
    {
        var resolver = new RepoResolver([new WorktreeRoot("/gone/aw-wt/alpha/", "/repos/alpha")]);

        Assert.Equal("/gone/aw-wt/alpha-2/tree", resolver.Resolve("/gone/aw-wt/alpha-2/tree"));
    }

    [Fact]
    public void The_longest_matching_root_wins()
    {
        var resolver = new RepoResolver([new WorktreeRoot("/gone/aw-wt", "/repos/catch-all"), new WorktreeRoot("/gone/aw-wt/beta", "/repos/beta")]);

        Assert.Equal("/repos/beta", resolver.Resolve("/gone/aw-wt/beta/fix"));
        Assert.Equal("/repos/catch-all", resolver.Resolve("/gone/aw-wt/gamma/fix"));
    }

    [Fact]
    public void An_existing_cwd_still_resolves_through_git_whatever_the_roots_say()
    {
        var repo = Directory.CreateDirectory(Path.Combine(_roots.Root, "repos", "alpha")).FullName;
        _ = Directory.CreateDirectory(Path.Combine(repo, ".git"));
        var resolver = new RepoResolver([new WorktreeRoot(_roots.Root, "/repos/wrong")]);

        Assert.Equal(repo, resolver.Resolve(Path.Combine(repo)));
    }

    [Fact]
    public async Task A_swept_worktree_session_is_listed_under_its_main_repo()
    {
        var at = new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);
        _ = _roots.Write("claude/projects/-gone-aw-wt-alpha-fix/s.jsonl", at.AddMinutes(5), ClaudeLines.User("go", at, cwd: "/gone/aw-wt/alpha/fix"));
        var corpus = _roots.Corpus([new WorktreeRoot("/gone/aw-wt/alpha", "/repos/alpha")]);

        var listed = (await corpus.ListSessions(new SessionQuery(at) { Repo = "/repos/alpha" }, CancellationToken.None)).Value;

        Assert.Equal("/repos/alpha", Assert.Single(listed).Repo);
    }

    public void Dispose() => _roots.Dispose();
}
