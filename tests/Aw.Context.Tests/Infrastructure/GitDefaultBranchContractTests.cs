using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Extract;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Testing.Extract;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "integration")]
public sealed class GitDefaultBranchContractTests : DefaultBranchContract, IDisposable
{
    private readonly List<TempGitRepo> _repos = [];

    public void Dispose() => _repos.ForEach(r => r.Dispose());

    protected override IDefaultBranch Create(Standing standing, out ContextRepo repo, out string commit)
    {
        var temp = new TempGitRepo(RepoReaderContract.Files);
        _repos.Add(temp);
        repo = temp.Repo;
        commit = temp.Head;
        switch (standing)
        {
            case Standing.BehindTip:
                Advance(temp, "second");
                break;
            case Standing.OffDefault:
                _ = temp.Run("checkout", "-q", "-b", "feature");
                Advance(temp, "feature work");
                commit = temp.Run("rev-parse", "HEAD");
                break;
            case Standing.NoDefaultRef:
                _ = temp.Run("branch", "-m", "trunk");
                break;
            case Standing.Unreadable:
                repo = new ContextRepo(repo.Name, Path.Combine(Path.GetTempPath(), "aw-no-such-checkout"));
                break;
            case Standing.AtTip:
            default:
                break;
        }

        return new GitDefaultBranch(new GitCli());
    }

    private static void Advance(TempGitRepo temp, string message)
    {
        File.WriteAllText(Path.Combine(temp.Path, "NOTES.md"), message + "\n");
        _ = temp.Run("add", "-A");
        _ = temp.Run("commit", "-q", "-m", message);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Origin_head_is_preferred_over_main()
    {
        using var temp = new TempGitRepo(RepoReaderContract.Files);
        var onMain = temp.Head;
        Advance(temp, "ahead on main");   // main moves on; origin/HEAD stays at the first commit
        _ = temp.Run("update-ref", "refs/remotes/origin/trunk", onMain);
        _ = temp.Run("symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/trunk");
        var mainTip = temp.Run("rev-parse", "main");
        var branch = new GitDefaultBranch(new GitCli());

        var first = await branch.Reaches(temp.Repo, onMain, TestContext.Current.CancellationToken);
        var tip = await branch.Reaches(temp.Repo, mainTip, TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess && first.Value);
        Assert.True(tip.IsSuccess && !tip.Value);   // origin/HEAD says the default branch ends at the first commit
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Master_is_the_default_when_there_is_no_main()
    {
        using var temp = new TempGitRepo(RepoReaderContract.Files);
        _ = temp.Run("branch", "-m", "master");
        var branch = new GitDefaultBranch(new GitCli());

        var reached = await branch.Reaches(temp.Repo, temp.Head, TestContext.Current.CancellationToken);

        Assert.True(reached.IsSuccess && reached.Value);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_commit_that_is_not_a_full_sha_is_refused_without_reading_refs()
    {
        using var temp = new TempGitRepo(RepoReaderContract.Files);
        var branch = new GitDefaultBranch(new GitCli());

        var reached = await branch.Reaches(temp.Repo, "--output=/tmp/x", TestContext.Current.CancellationToken);

        Assert.Equal("SND-CONTEXT-048", reached.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Asking_never_changes_the_working_tree_or_the_refs()
    {
        using var temp = new TempGitRepo(RepoReaderContract.Files);
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "dirty.txt"), "uncommitted\n", TestContext.Current.CancellationToken);
        var before = temp.Run("for-each-ref");
        var branch = new GitDefaultBranch(new GitCli());

        _ = await branch.Reaches(temp.Repo, temp.Head, TestContext.Current.CancellationToken);

        Assert.Equal(before, temp.Run("for-each-ref"));
        Assert.Equal("?? dirty.txt", temp.Run("status", "--porcelain"));
    }
}
