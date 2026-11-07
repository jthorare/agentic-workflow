using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Launch;

/// <summary>A skill trial's Step works in a fresh worktree of the repo's default branch and the worktree goes with the launch, but never with the agent's changes (skill flows spec §7).</summary>
[Lane(TestLane.Integration)]
public sealed class TrialWorktreeTests : IAsyncLifetime
{
    private TempGitRepo _repo = null!;
    private string _mainSha = string.Empty;
    private string _otherSha = string.Empty;
    private readonly List<string> _warnings = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _repo = await TempGitRepo.Create();
        _mainSha = await _repo.Git("rev-parse", "HEAD");
        _ = await _repo.Git("checkout", "-q", "-b", "other");
        _ = await _repo.Git("-c", "user.email=t@example.invalid", "-c", "user.name=t", "commit", "-q", "--allow-empty", "-m", "work in progress");
        _otherSha = await _repo.Git("rev-parse", "HEAD");
    }

    public ValueTask DisposeAsync()
    {
        _repo.Dispose();
        return ValueTask.CompletedTask;
    }

    private LaunchRequest Request(string recipe, string name) =>
        A.LaunchRequest().Unattended(new StepArgs(Model: null, Policy: null, new StepRef("run", "agent")))
            .OnInstance(InstanceAddress.From($"ITM-00000000000000000000000000000001/{recipe}/1"), RunToken.For(1, 1)).Build() with
        {
            RepoPath = _repo.Path,
            Worktree = Path.Combine(_repo.Path, ".claude", "worktrees", name),
        };

    [Fact]
    public async Task A_trial_starts_from_the_default_branch_not_from_head()
    {
        var request = Request("skill-trial-review", "trial");

        Assert.Null(await LaunchWorktree.Ensure(request, Token));

        Assert.Equal(_mainSha, await TempGitRepo.GitOut(request.Worktree!, "rev-parse", "HEAD"));
    }

    [Fact]
    public async Task Any_other_Step_starts_from_head_as_before()
    {
        var request = Request("step", "plain");

        Assert.Null(await LaunchWorktree.Ensure(request, Token));

        Assert.Equal(_otherSha, await TempGitRepo.GitOut(request.Worktree!, "rev-parse", "HEAD"));
    }

    [Fact]
    public async Task A_clean_trial_worktree_is_removed_with_no_warning()
    {
        var request = Request("skill-trial-review", "trial");
        _ = await LaunchWorktree.Ensure(request, Token);

        await LaunchWorktree.Remove(_repo.Path, request.Worktree!, _warnings.Add);

        Assert.False(Directory.Exists(request.Worktree));
        Assert.Empty(_warnings);
    }

    [Fact]
    public async Task A_trial_worktree_with_changes_is_kept_and_named_in_one_warning()
    {
        var request = Request("skill-trial-review", "trial");
        _ = await LaunchWorktree.Ensure(request, Token);
        await File.WriteAllTextAsync(Path.Combine(request.Worktree!, "agent-work.txt"), "unsaved", Token);

        await LaunchWorktree.Remove(_repo.Path, request.Worktree!, _warnings.Add);

        Assert.True(File.Exists(Path.Combine(request.Worktree!, "agent-work.txt")));
        Assert.Contains(request.Worktree!, Assert.Single(_warnings), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_trial_worktree_with_a_commit_on_no_branch_is_kept_and_the_warning_counts_the_commits()
    {
        var request = Request("skill-trial-review", "trial");
        _ = await LaunchWorktree.Ensure(request, Token);
        _ = await TempGitRepo.GitOut(request.Worktree!, "-c", "user.email=t@example.invalid", "-c", "user.name=t", "commit", "-q", "--allow-empty", "-m", "agent work");

        await LaunchWorktree.Remove(_repo.Path, request.Worktree!, _warnings.Add);

        Assert.True(Directory.Exists(request.Worktree));
        var warning = Assert.Single(_warnings);
        Assert.Contains(request.Worktree!, warning, StringComparison.Ordinal);
        Assert.Contains("1 commit", warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_commit_check_that_fails_keeps_the_tree_and_says_why()
    {
        // why: a git error must not drop the commit guard: a directory git can't read is kept, never removed
        var notARepo = Directory.CreateTempSubdirectory("trial-not-a-repo-").FullName;
        try
        {
            await LaunchWorktree.Remove(_repo.Path, notARepo, _warnings.Add);

            Assert.True(Directory.Exists(notARepo));
            var warning = Assert.Single(_warnings);
            Assert.Contains(notARepo, warning, StringComparison.Ordinal);
            Assert.Contains("could not check", warning, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(notARepo, recursive: true);
        }
    }
}
