using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>The git adapter of the workspace rebase against a real origin repo and a second repo, its workspace, that fetches from it.</summary>
[Lane(TestLane.Integration)]
public sealed class GitWorkspaceRebaseContractTests : WorkspaceRebaseContract, IDisposable
{
    private readonly List<TempGitRepo> _repos = [];

    public void Dispose()
    {
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }
    }

    [Fact]
    public async Task Only_the_commits_after_the_old_base_move_onto_the_new_one()
    {
        var (origin, workspace) = await Pair();
        _ = await origin.Git("checkout", "-q", "-b", "parent");
        await Commit(origin, "p.txt", "parent");
        var oldBase = await origin.Git("rev-parse", "HEAD");
        _ = await origin.Git("checkout", "-q", "main");
        await Commit(origin, "p.txt", "parent");   // the squash-merged parent: the same change as a new commit on main
        _ = await workspace.Git("fetch", "-q", "origin", "parent");
        _ = await workspace.Git("checkout", "-q", "-b", "feature", oldBase);
        await Commit(workspace, "c.txt", "child");

        var outcome = await new GitWorkspaceRebase().Rebase(workspace.Path, "main", oldBase, TestContext.Current.CancellationToken);

        Assert.Equal(RebaseStatus.Rebased, outcome.Value.Status);
        Assert.Equal(await origin.Git("rev-parse", "main"), await workspace.Git("rev-parse", "HEAD~1"));
        Assert.Equal("child", await workspace.Git("log", "-1", "--format=%s"));
    }

    [Fact]
    public async Task A_conflict_leaves_the_worktree_as_it_was()
    {
        var (rebase, workspace, onto) = await Arrange(RebaseScenario.Conflicts);
        var repo = _repos.Single(r => string.Equals(r.Path, workspace, StringComparison.Ordinal));
        var before = await repo.Git("rev-parse", "HEAD");

        _ = await rebase.Rebase(workspace, onto, oldBase: null, TestContext.Current.CancellationToken);

        Assert.Equal(before, await repo.Git("rev-parse", "HEAD"));
        Assert.Empty(await repo.Git("status", "--porcelain"));
        Assert.False(Directory.Exists(Path.Combine(workspace, ".git", "rebase-merge")));
    }

    protected override async ValueTask<(IWorkspaceRebase Rebase, string Workspace, string Onto)> Arrange(RebaseScenario scenario)
    {
        var (origin, workspace) = await Pair();
        await Commit(origin, "a.txt", "base");
        _ = await workspace.Git("fetch", "-q", "origin", "main");
        _ = await workspace.Git("checkout", "-q", "-b", "feature", "origin/main");
        switch (scenario)
        {
            case RebaseScenario.Moves:
                await Commit(workspace, "b.txt", "feature");
                await Commit(origin, "c.txt", "newer");
                break;
            case RebaseScenario.Conflicts:
                await Commit(workspace, "a.txt", "feature");
                await Commit(origin, "a.txt", "newer");
                break;
            case RebaseScenario.UpToDate:
                await Commit(workspace, "b.txt", "feature");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        return (new GitWorkspaceRebase(), workspace.Path, "main");
    }

    private async Task<(TempGitRepo Origin, TempGitRepo Workspace)> Pair()
    {
        var origin = await TempGitRepo.Create();
        var workspace = await TempGitRepo.Create(commits: 0);
        _repos.Add(origin);
        _repos.Add(workspace);
        _ = await workspace.Git("remote", "add", "origin", origin.Path);
        _ = await workspace.Git("config", "user.email", "test@example.invalid");
        _ = await workspace.Git("config", "user.name", "test");
        return (origin, workspace);
    }

    private static async Task Commit(TempGitRepo repo, string file, string text)
    {
        await File.WriteAllTextAsync(Path.Combine(repo.Path, file), text, TestContext.Current.CancellationToken);
        _ = await repo.Git("add", file);
        _ = await repo.Git("-c", "user.email=test@example.invalid", "-c", "user.name=test", "commit", "-q", "-m", text);
    }
}
