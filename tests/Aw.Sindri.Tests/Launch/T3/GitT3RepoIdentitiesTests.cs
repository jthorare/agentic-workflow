using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.T3;

/// <summary>What real git says about a repo, a linked worktree and a folder that isn't a repo.</summary>
[Lane(TestLane.Integration)]
public sealed class GitT3RepoIdentitiesTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aw-t3-git-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static void Git(string dir, params string[] args)
    {
        var start = new ProcessStartInfo("git") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in (string[])["-C", dir, "-c", "user.name=t", "-c", "user.email=t@example.com", "-c", "commit.gpgsign=false", .. args])
        {
            start.ArgumentList.Add(arg);
        }

        using var git = Process.Start(start)!;   // why: git is on every box the integration lane runs on
        git.WaitForExit();
        Expect.Equal(0, git.ExitCode);
    }

    [Fact]
    public async Task A_repo_with_upstream_and_origin_is_known_by_upstream()
    {
        var repo = Path.Combine(_root, "repo");
        Git(_root, "init", "-q", repo);
        Git(repo, "remote", "add", "origin", "git@github.com:me/fork.git");
        Git(repo, "remote", "add", "upstream", "https://github.com/them/repo.git");

        var identity = await new GitT3RepoIdentities().Of(repo, Token);

        Expect.Equal("github.com/them/repo", identity.RepositoryKey);
        Expect.True(identity.MainCheckout is { } main && T3Projects.Same(main, repo));
    }

    [Fact]
    public async Task A_linked_worktrees_main_checkout_is_its_repo()
    {
        var repo = Path.Combine(_root, "repo");
        var worktree = Path.Combine(_root, "wt");
        Git(_root, "init", "-q", repo);
        Git(repo, "commit", "-q", "--allow-empty", "-m", "init");
        Git(repo, "worktree", "add", "-q", worktree);

        var identity = await new GitT3RepoIdentities().Of(worktree, Token);

        Expect.True(identity.MainCheckout is { } main && T3Projects.Same(main, repo));
        Expect.True(identity.RepositoryKey is null);
    }

    [Fact]
    public async Task A_folder_that_is_not_a_repo_says_nothing()
    {
        var identity = await new GitT3RepoIdentities().Of(_root, Token);

        Expect.Equal(new T3RepoIdentity(MainCheckout: null, RepositoryKey: null), identity);
    }
}
