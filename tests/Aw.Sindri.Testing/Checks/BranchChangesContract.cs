using Aw.Sindri.Core.Checks;
using Xunit;

namespace Aw.Sindri.Testing.Checks;

/// <summary>
/// The contract every <see cref="IBranchChanges"/> honours. The scenario is three commits on a base that holds src/a.cs ("one") and tests/t.test.ts: "feat: add b" adds src/b.cs
/// ("x1", "x2"), then "fix: tweak a" adds the line "two" to src/a.cs and deletes tests/t.test.ts. A second scenario merges a side branch.
/// </summary>
public abstract class BranchChangesContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The port and the range of the three-commit scenario.</summary>
    /// <returns>The port, the worktree, the base and the head.</returns>
    protected abstract ValueTask<(IBranchChanges Port, string Worktree, string Base, string Head)> Linear();

    /// <summary>The port and a range that ends in a merge commit (the side branch's commit "feat: side" is merged by "Merge side").</summary>
    /// <returns>The port, the worktree, the base and the head.</returns>
    protected abstract ValueTask<(IBranchChanges Port, string Worktree, string Base, string Head)> WithMerge();

    /// <summary>A range with a commit the repository does not have.</summary>
    /// <returns>The port, the worktree, the base and the head.</returns>
    protected abstract ValueTask<(IBranchChanges Port, string Worktree, string Base, string Head)> Unknown();

    /// <summary>The paths come back with what happened to each.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_changed_paths_come_back_with_their_kind()
    {
        var (port, worktree, baseSha, head) = await Linear();

        var changes = await port.Between(worktree, baseSha, head, Token);

        Expect.Sequence(
            [new ChangedFile("src/a.cs", ChangeKind.Modified), new ChangedFile("src/b.cs", ChangeKind.Added), new ChangedFile("tests/t.test.ts", ChangeKind.Deleted)],
            changes.Value.Files);
    }

    /// <summary>The added lines carry their file and line number.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_added_lines_come_back_with_their_path_and_line_number()
    {
        var (port, worktree, baseSha, head) = await Linear();

        var changes = await port.Between(worktree, baseSha, head, Token);

        Expect.Sequence([new AddedLine("src/a.cs", 2, "two"), new AddedLine("src/b.cs", 1, "x1"), new AddedLine("src/b.cs", 2, "x2")], changes.Value.AddedLines);
    }

    /// <summary>The commits come back oldest first with their subjects.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_commits_come_back_oldest_first()
    {
        var (port, worktree, baseSha, head) = await Linear();

        var changes = await port.Between(worktree, baseSha, head, Token);

        Expect.Sequence(["feat: add b", "fix: tweak a"], changes.Value.Commits.Select(static c => c.Subject));
        Expect.True(changes.Value.Commits.All(static c => !c.IsMerge));
    }

    /// <summary>A merge commit is marked.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_merge_commit_is_marked_as_one()
    {
        var (port, worktree, baseSha, head) = await WithMerge();

        var changes = await port.Between(worktree, baseSha, head, Token);

        Expect.True(changes.Value.Commits.Any(static c => c.IsMerge && c.Subject == "Merge side"));
    }

    /// <summary>A commit the repository does not have is an error.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unknown_commit_is_an_error()
    {
        var (port, worktree, baseSha, head) = await Unknown();

        var changes = await port.Between(worktree, baseSha, head, Token);

        Expect.True(changes.IsError);
    }
}
