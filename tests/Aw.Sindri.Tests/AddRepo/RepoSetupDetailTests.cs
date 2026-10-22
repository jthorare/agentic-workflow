using Aw.Kernel.Testing;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Tests.SetupSources;

namespace Aw.Sindri.Tests.AddRepo;

/// <summary>The setup list's repo lines carry what git said (setup-once spec §5.1), from the cache: 'GET /setup' runs no git per request.</summary>
[Lane(TestLane.Unit)]
public sealed class RepoSetupDetailTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_repo_line_says_the_branch_and_head()
    {
        await using var rig = new SetupFactsRig();
        _ = rig.Inspector.Has(SetupFactsRig.Checkout, git: new RepoGit("main", "abc1234"));
        await rig.Approve(SetupFactsRig.ProfileWithRepos());

        var repos = (await rig.Facts.Repos(Token)).Value;

        Assert.Equal([("demo", SetupState.Done, "On main at abc1234")], repos.Items.Lines.Select(static i => (i.Repo, i.State, i.Detail)));
    }

    [Fact]
    public async Task Reading_the_list_again_runs_no_git()
    {
        await using var rig = new SetupFactsRig();
        _ = rig.Inspector.Has(SetupFactsRig.Checkout);
        await rig.Approve(SetupFactsRig.ProfileWithRepos());

        _ = await rig.Facts.Repos(Token);
        _ = await rig.Facts.Repos(Token);
        _ = await rig.Facts.Repos(Token);

        Assert.Equal(1, rig.Inspector.Asked);
    }

    [Fact]
    public async Task A_folder_git_cannot_read_still_counts_and_has_no_detail()
    {
        await using var rig = new SetupFactsRig();
        await rig.Approve(SetupFactsRig.ProfileWithRepos());

        var repos = (await rig.Facts.Repos(Token)).Value;

        Assert.Equal((1, null), (repos.Count, repos.Items.Lines.Single().Detail));
    }

    [Theory]
    [InlineData("main", null, "On main, no commit yet")]
    [InlineData(null, "abc1234", "At abc1234")]
    [InlineData(null, null, null)]
    public async Task The_line_is_made_of_what_git_knows(string? branch, string? head, string? line)
    {
        await using var rig = new SetupFactsRig();
        _ = rig.Inspector.Has(SetupFactsRig.Checkout, git: new RepoGit(branch, head));
        await rig.Approve(SetupFactsRig.ProfileWithRepos());

        var repos = (await rig.Facts.Repos(Token)).Value;

        Assert.Equal(line, repos.Items.Lines.Single().Detail);
    }
}
