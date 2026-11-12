using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Infrastructure.Snapshot;
using Aw.CodeIndex.Testing;
using Aw.Kernel.Testing;

namespace Aw.CodeIndex.Tests.Snapshot;

[Trait("Lane", "integration")]
public sealed class GitRepoHeadsTests
{
    private readonly GitRepoHeads _heads = new();

    [Fact]
    public async Task A_checkout_on_main_reads_its_head_and_is_on_the_default_branch()
    {
        using var repo = new GitRepoBuilder().WithFile("a.cs", "class A {}").Build();

        var heads = await _heads.Heads([repo.AsRepo()], TestContext.Current.CancellationToken);

        var head = Assert.Single(heads.Value);
        Assert.Equal((repo.Head, true), (head.Head, head.OnDefaultBranch));
    }

    [Fact]
    public async Task A_commit_only_on_a_feature_branch_is_not_on_the_default_branch()
    {
        using var repo = new GitRepoBuilder().WithFile("a.cs", "class A {}").Build();
        var onMain = repo.Head;
        _ = repo.Git("checkout", "-q", "-b", "feature");
        var onFeature = repo.Commit("feature work");

        var heads = await _heads.Heads([repo.AsRepo()], TestContext.Current.CancellationToken);
        var mainCheck = await _heads.OnDefaultBranch(repo.AsRepo(), onMain, TestContext.Current.CancellationToken);
        var featureCheck = await _heads.OnDefaultBranch(repo.AsRepo(), onFeature, TestContext.Current.CancellationToken);

        Assert.Equal((onFeature, false), (Assert.Single(heads.Value).Head, heads.Value[0].OnDefaultBranch));
        Assert.True(mainCheck.Value.Contains);
        Assert.False(featureCheck.Value.Contains);
    }

    [Fact]
    public async Task A_checkout_off_main_reports_local_mains_tip_as_the_default_tip_gh3181()
    {
        using var repo = new GitRepoBuilder().WithFile("a.cs", "class A {}").Build();
        var onMain = repo.Head;
        _ = repo.Git("checkout", "-q", "-b", "feature");
        _ = repo.Commit("feature work");

        var heads = await _heads.Heads([repo.AsRepo()], TestContext.Current.CancellationToken);

        Assert.Equal(onMain, Assert.Single(heads.Value).DefaultTip);
    }

    [Fact]
    public async Task A_checkout_with_no_local_main_reports_origin_mains_tip_as_the_default_tip_gh3181()
    {
        using var repo = new GitRepoBuilder().WithFile("a.cs", "class A {}").Build();
        var onMain = repo.Head;
        _ = repo.Git("checkout", "-q", "-b", "feature");
        _ = repo.Commit("feature work");
        _ = repo.Git("update-ref", "refs/remotes/origin/main", onMain.Value);
        _ = repo.Git("branch", "-q", "-D", "main");

        var heads = await _heads.Heads([repo.AsRepo()], TestContext.Current.CancellationToken);

        Assert.Equal(onMain, Assert.Single(heads.Value).DefaultTip);
    }

    [Fact]
    public async Task A_checkout_with_no_main_and_no_origin_main_has_no_default_tip_gh3181()
    {
        using var repo = new GitRepoBuilder().WithFile("a.cs", "class A {}").Build();
        _ = repo.Git("checkout", "-q", "-b", "feature");
        _ = repo.Commit("feature work");
        _ = repo.Git("branch", "-q", "-D", "main");

        var heads = await _heads.Heads([repo.AsRepo()], TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(heads.Value).DefaultTip);
    }

    [Fact]
    public async Task A_path_git_cannot_read_has_no_head_and_is_not_on_the_default_branch()
    {
        var missing = new CodeIndexRepo(RepoName.From("gone"), Path.Combine(Path.GetTempPath(), "aw-no-repo-" + RealTime.NewGuid().ToString("N")), CodeIndexSettings.Default);

        var heads = await _heads.Heads([missing], TestContext.Current.CancellationToken);

        var head = Assert.Single(heads.Value);
        Assert.Equal((null, false), (head.Head, head.OnDefaultBranch));
    }

    [Fact]
    public async Task A_commit_the_repo_does_not_hold_is_SND_CODEIDX_002()
    {
        using var repo = new GitRepoBuilder().WithFile("a.cs", "class A {}").Build();

        var check = await _heads.OnDefaultBranch(repo.AsRepo(), CommitSha.From("2222222222222222222222222222222222222222"), TestContext.Current.CancellationToken);

        Assert.Equal(CodeIndexErrors.RepoNotFound.Code, check.Error.Code);
    }
}
