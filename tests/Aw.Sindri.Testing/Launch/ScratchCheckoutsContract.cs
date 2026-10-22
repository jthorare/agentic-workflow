using Aw.Sindri.Core.Launch;
using Xunit;

namespace Aw.Sindri.Testing.Launch;

/// <summary>
/// The contract every <see cref="IScratchCheckouts"/> honours, run against the fake and the git adapter: a checkout is made at the commit and found again,
/// a relaunch keeps it, a stale one at another commit is replaced, removing is idempotent, and a path or a commit that isn't usable is refused.
/// </summary>
public abstract class ScratchCheckoutsContract
{
    /// <summary>The checkouts under test, and the repo's commits to check out: older first.</summary>
    /// <param name="repoPath">The repo the checkouts come from.</param>
    /// <param name="commits">The repo's first and second commit.</param>
    /// <returns>The port.</returns>
    protected abstract IScratchCheckouts Create(out string repoPath, out (string First, string Second) commits);

    /// <summary>The commit checked out at <paramref name="path"/>, or null when there is no checkout.</summary>
    /// <param name="path">The checkout's path.</param>
    /// <returns>The commit.</returns>
    protected abstract string? HeadAt(string path);

    /// <summary>The path a checkout may be made at.</summary>
    /// <param name="name">A name for it.</param>
    /// <returns>An absolute path that doesn't exist yet.</returns>
    protected abstract string PathFor(string name);

    /// <summary>A checkout is made at the commit asked for.</summary>
    [Fact]
    public async Task Create_checks_out_the_commit()
    {
        var checkouts = Create(out var repo, out var commits);
        var path = PathFor("eval-1");

        var made = await checkouts.Create(repo, path, commits.First, TestContext.Current.CancellationToken);

        Expect.True(made.IsSuccess);
        Expect.Equal(path, made.Value);
        Expect.Equal(commits.First, HeadAt(path));
    }

    /// <summary>A checkout already there at the commit is kept, and one at another commit is replaced.</summary>
    [Fact]
    public async Task Create_keeps_a_checkout_at_the_commit_and_replaces_one_at_another()
    {
        var checkouts = Create(out var repo, out var commits);
        var path = PathFor("eval-2");
        _ = await checkouts.Create(repo, path, commits.First, TestContext.Current.CancellationToken);

        var again = await checkouts.Create(repo, path, commits.First, TestContext.Current.CancellationToken);
        var moved = await checkouts.Create(repo, path, commits.Second, TestContext.Current.CancellationToken);

        Expect.True(again.IsSuccess);
        Expect.True(moved.IsSuccess);
        Expect.Equal(commits.Second, HeadAt(path));
    }

    /// <summary>Remove leaves nothing at the path, and removing again, or a path that never existed, is fine.</summary>
    [Fact]
    public async Task Remove_is_idempotent()
    {
        var checkouts = Create(out var repo, out var commits);
        var path = PathFor("eval-3");
        _ = await checkouts.Create(repo, path, commits.First, TestContext.Current.CancellationToken);

        var first = await checkouts.Remove(path, TestContext.Current.CancellationToken);
        var second = await checkouts.Remove(path, TestContext.Current.CancellationToken);
        var never = await checkouts.Remove(PathFor("never"), TestContext.Current.CancellationToken);

        Expect.True(first.IsSuccess && second.IsSuccess && never.IsSuccess);
        Expect.True(HeadAt(path) is null);
    }

    /// <summary>A relative path and a commit that isn't hex are SND-REQ-001, with nothing made.</summary>
    [Fact]
    public async Task Create_refuses_a_relative_path_and_a_commit_that_is_not_hex()
    {
        var checkouts = Create(out var repo, out var commits);
        var path = PathFor("eval-4");

        var relative = await checkouts.Create(repo, "eval-4", commits.First, TestContext.Current.CancellationToken);
        var option = await checkouts.Create(repo, path, "--orphan", TestContext.Current.CancellationToken);

        Expect.Equal("SND-REQ-001", relative.Error.Code);
        Expect.Equal("SND-REQ-001", option.Error.Code);
        Expect.True(HeadAt(path) is null);
    }
}
