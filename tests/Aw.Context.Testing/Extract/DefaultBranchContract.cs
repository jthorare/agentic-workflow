using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Testing.Map;
using Xunit;

namespace Aw.Context.Testing.Extract;

/// <summary>The default-branch port's contract (F1): the fake and the git adapter pass the same tests; neither fetches, and a checkout that can't be read is SND-CONTEXT-048.</summary>
public abstract class DefaultBranchContract
{
    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>Where a repo's commit stands against the default branch.</summary>
    protected enum Standing
    {
        /// <summary>HEAD is the tip of the default branch.</summary>
        AtTip,

        /// <summary>The commit is an ancestor of the default branch's tip.</summary>
        BehindTip,

        /// <summary>The commit sits on a feature branch the default branch doesn't contain.</summary>
        OffDefault,

        /// <summary>The repo has no origin/HEAD, main or master: there is no default ref.</summary>
        NoDefaultRef,

        /// <summary>The checkout can't be read.</summary>
        Unreadable,
    }

    /// <summary>Builds the port over a repo in the given standing.</summary>
    /// <param name="standing">Where the commit stands.</param>
    /// <param name="repo">The repo the port is asked about.</param>
    /// <param name="commit">The full sha to ask about.</param>
    /// <returns>The port.</returns>
    protected abstract IDefaultBranch Create(Standing standing, out ContextRepo repo, out string commit);

    /// <summary>The tip of the default branch is on it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_tip_of_the_default_branch_is_on_it()
    {
        const string test = nameof(The_tip_of_the_default_branch_is_on_it);
        var branch = Create(Standing.AtTip, out var repo, out var commit);

        Expect.That(Expect.Ok(await branch.Reaches(repo, commit, None), test), test, "the tip wasn't on the default branch");
    }

    /// <summary>A commit the default branch has moved past is still on it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_commit_behind_the_tip_is_on_the_default_branch()
    {
        const string test = nameof(A_commit_behind_the_tip_is_on_the_default_branch);
        var branch = Create(Standing.BehindTip, out var repo, out var commit);

        Expect.That(Expect.Ok(await branch.Reaches(repo, commit, None), test), test, "an ancestor wasn't on the default branch");
    }

    /// <summary>A feature branch's commit is not.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_feature_branch_commit_is_not_on_the_default_branch()
    {
        const string test = nameof(A_feature_branch_commit_is_not_on_the_default_branch);
        var branch = Create(Standing.OffDefault, out var repo, out var commit);

        Expect.That(!Expect.Ok(await branch.Reaches(repo, commit, None), test), test, "a feature commit was on the default branch");
    }

    /// <summary>With no default ref at all the answer is false, not an error.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_repo_with_no_default_ref_answers_false()
    {
        const string test = nameof(A_repo_with_no_default_ref_answers_false);
        var branch = Create(Standing.NoDefaultRef, out var repo, out var commit);

        Expect.That(!Expect.Ok(await branch.Reaches(repo, commit, None), test), test, "a repo with no default ref reached it");
    }

    /// <summary>A checkout that can't be read is SND-CONTEXT-048.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unreadable_checkout_is_unreadable()
    {
        const string test = nameof(An_unreadable_checkout_is_unreadable);
        var branch = Create(Standing.Unreadable, out var repo, out var commit);

        var reached = await branch.Reaches(repo, commit, None);

        Expect.That(reached.IsError && reached.Error.Code == "SND-CONTEXT-048", test, reached.IsError ? reached.Error.Code : "it answered");
    }
}
