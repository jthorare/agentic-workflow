using Aw.Kernel;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Infrastructure.Worktrees;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Worktrees;

/// <summary>
/// One-off worktrees (agents spec §15.4, #3472): two starts with the same name at the same time get two checkouts, never one shared tree.
/// The checkout maker below holds each Create until a second one arrives (or 1 s passes), so the two calls overlap every run instead of by luck.
/// </summary>
public sealed class OneOffWorktreesTests : IDisposable
{
    private readonly TempHome _home = new();
    private TempGitRepo? _repo;

    public void Dispose()
    {
        _repo?.Dispose();
        _home.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task Two_starts_with_the_same_name_at_once_get_two_checkouts()
    {
        var (sut, repo) = await Rig();

        var made = await Task.WhenAll(sut.Make(repo, "fix-login", Token).AsTask(), sut.Make(repo, "fix-login", Token).AsTask());

        Assert.All(made, m => Assert.True(m.IsSuccess, m.IsError ? m.Error.ToString() : null));
        Assert.NotEqual(made[0].Value.Path, made[1].Value.Path, StringComparer.Ordinal);
        Assert.NotEqual(made[0].Value.Branch, made[1].Value.Branch, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_same_repo_through_a_symlink_still_gets_two_checkouts()
    {
        var (sut, repo) = await Rig();
        var link = Path.Combine(_home.Path, "repo-link");
        _ = Directory.CreateSymbolicLink(link, repo);

        var made = await Task.WhenAll(sut.Make(repo, "fix-login", Token).AsTask(), sut.Make(link, "fix-login", Token).AsTask());

        Assert.All(made, m => Assert.True(m.IsSuccess, m.IsError ? m.Error.ToString() : null));
        Assert.NotEqual(made[0].Value.Branch, made[1].Value.Branch, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_start_in_a_linked_worktree_and_one_in_its_main_checkout_get_two_checkouts()
    {
        var (sut, repo) = await Rig();
        var linked = Path.Combine(_home.Path, "linked");
        _ = await _repo!.Git("worktree", "add", "-q", "-b", "side", linked);   // why: one set of branches, two toplevels; the lock is keyed by the common dir

        var made = await Task.WhenAll(sut.Make(repo, "fix-login", Token).AsTask(), sut.Make(linked, "fix-login", Token).AsTask());

        Assert.All(made, m => Assert.True(m.IsSuccess, m.IsError ? m.Error.ToString() : null));
        Assert.NotEqual(made[0].Value.Path, made[1].Value.Path, StringComparer.Ordinal);
        Assert.NotEqual(made[0].Value.Branch, made[1].Value.Branch, StringComparer.Ordinal);
    }

    private async Task<(OneOffWorktrees Sut, string Repo)> Rig()
    {
        _repo = await TempGitRepo.Create();
        var toolkit = Path.Combine(_home.Path, "toolkit");
        var shared = Path.Combine(toolkit, "skills", "_shared");
        _ = Directory.CreateDirectory(shared);
        var root = Path.Combine(_home.Path, "wt");
        await File.WriteAllTextAsync(Path.Combine(shared, "worktree-path.sh"), $"#!/usr/bin/env bash\nprintf '%s\\n' \"{root}/$1\"\n", Token);
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_TOOLKIT"] = toolkit, ["HOME"] = _home.Path };
        return (new OneOffWorktrees(new OverlappingCreates(new GitWorktrees()), name => environment.GetValueOrDefault(name)), _repo.Path);
    }

    /// <summary>Holds each Create until a second caller arrives or 1 s passes, then hands it to the real git checkout maker.</summary>
    private sealed class OverlappingCreates(IWorktrees inner) : IWorktrees
    {
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public async ValueTask<Result<WorktreeCheckout>> Create(WorktreeRequest request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrived) >= 2)
            {
                _ = _both.TrySetResult();
            }

            _ = await Task.WhenAny(_both.Task, Task.Delay(TimeSpan.FromSeconds(1), cancellationToken));
            return await inner.Create(request, cancellationToken);
        }

        public ValueTask<Result<string>> Resolve(string repoRoot, string branch, CancellationToken cancellationToken) => inner.Resolve(repoRoot, branch, cancellationToken);

        public ValueTask<Result<Unit>> Remove(string path, CancellationToken cancellationToken) => inner.Remove(path, cancellationToken);
    }
}
