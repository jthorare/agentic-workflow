using Aw.Sindri.Core.Workflow.Engine.Effects;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>What a workspace is arranged to do when it is rebased.</summary>
public enum RebaseScenario
{
    /// <summary>The base has a commit the branch lacks, and nothing conflicts.</summary>
    Moves = 1,

    /// <summary>The branch is already on the base.</summary>
    UpToDate = 2,

    /// <summary>The branch and the base changed 'a.txt' differently.</summary>
    Conflicts = 3,
}

/// <summary>
/// The contract every <see cref="IWorkspaceRebase"/> honours (PB spec §4.6): a branch behind its base moves and reports its new tip, a branch on its base
/// reports nothing, a conflict names its paths, and a worktree that isn't there is an error.
/// </summary>
public abstract class WorkspaceRebaseContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The adapter, a workspace arranged for <paramref name="scenario"/>, and the base to rebase it onto.</summary>
    /// <param name="scenario">What the workspace does.</param>
    /// <returns>The adapter, the workspace's path and the base.</returns>
    protected abstract ValueTask<(IWorkspaceRebase Rebase, string Workspace, string Onto)> Arrange(RebaseScenario scenario);

    /// <summary>A branch behind its base moves.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_branch_behind_its_base_moves_and_reports_its_new_tip()
    {
        var (rebase, workspace, onto) = await Arrange(RebaseScenario.Moves);

        var outcome = await rebase.Rebase(workspace, onto, oldBase: null, Token);

        Expect.True(outcome.IsSuccess && outcome.Value.Status == RebaseStatus.Rebased && !string.IsNullOrEmpty(outcome.Value.NewHead) && outcome.Value.Conflicts.Count == 0);
    }

    /// <summary>A branch already on its base does not move.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_branch_already_on_its_base_reports_nothing()
    {
        var (rebase, workspace, onto) = await Arrange(RebaseScenario.UpToDate);

        var outcome = await rebase.Rebase(workspace, onto, oldBase: null, Token);

        Expect.True(outcome.IsSuccess && outcome.Value.Status == RebaseStatus.Nothing && outcome.Value.NewHead is null);
    }

    /// <summary>A conflict names the paths and is not an error.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_conflict_names_its_paths()
    {
        var (rebase, workspace, onto) = await Arrange(RebaseScenario.Conflicts);

        var outcome = await rebase.Rebase(workspace, onto, oldBase: null, Token);

        Expect.True(outcome.IsSuccess && outcome.Value.Status == RebaseStatus.Conflict && outcome.Value.NewHead is null && outcome.Value.Conflicts.SequenceEqual(["a.txt"], StringComparer.Ordinal));
    }

    /// <summary>A worktree that isn't there is an error, never "nothing".</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_missing_worktree_is_an_error()
    {
        var (rebase, workspace, onto) = await Arrange(RebaseScenario.UpToDate);

        var outcome = await rebase.Rebase(Path.Combine(workspace, "no-such-dir"), onto, oldBase: null, Token);

        Expect.True(outcome.IsError);
    }
}
